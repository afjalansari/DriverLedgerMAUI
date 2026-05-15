using DriverLedger.Database;
using DriverLedger.Models;
using DriverLedger.Services;
using System.Text.Json;

namespace DriverLedger.Repositories
{
    /// <summary>
    /// SQLite-backed implementation of <see cref="IUnitOfWork"/>.
    /// All multi-row mutations are wrapped in <c>RunInTransaction</c> for atomicity.
    /// Phase 7: every successful write produces an <see cref="AuditLog"/> row via
    /// <see cref="IAuditService"/>. Audit failures are non-fatal (swallowed by AuditService).
    /// </summary>
    public class SqliteUnitOfWork : IUnitOfWork
    {
        private readonly DatabaseService _db;
        private readonly IAuditService   _audit;

        public SqliteUnitOfWork(DatabaseService db, IAuditService audit)
        {
            _db    = db    ?? throw new ArgumentNullException(nameof(db));
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        }

        /// <inheritdoc />
        public async Task<int> SaveSettlementWithLedgerAsync(
            Settlement        settlement,
            DriverLedgerEntry ledgerEntry)
        {
            await _db.InitializeAsync();
            var conn = await _db.GetRawConnectionAsync();

            int newSettlementId = 0;

            await Task.Run(() =>
            {
                conn.RunInTransaction(() =>
                {
                    // 1️⃣ Insert Settlement
                    conn.Insert(settlement);
                    newSettlementId = settlement.Id;

                    // 1.b️⃣ Save Child Records
                    foreach (var pi in settlement.PlatformIncomes)
                    {
                        pi.SettlementId = newSettlementId;
                        conn.Insert(pi);
                    }
                    foreach (var se in settlement.ExpenseItems)
                    {
                        se.SettlementId = newSettlementId;
                        conn.Insert(se);
                    }

                    // 2️⃣ Link and Insert Ledger Entry
                    ledgerEntry.SettlementId = newSettlementId;
                    ledgerEntry.CreatedAt    = DateTime.UtcNow;
                    conn.Insert(ledgerEntry);

                    // 3️⃣ Rebalance Ledger
                    RebalanceInTransaction(conn, ledgerEntry.DriverId);
                });
            });

            // Phase 7 — Audit (after commit, non-fatal)
            _ = _audit.LogAsync(
                entityType:    AuditEntities.Settlement,
                entityId:      newSettlementId,
                action:        AuditActions.Create,
                driverId:      settlement.DriverId,
                driverName:    settlement.DriverNameSnapshot,
                changeSummary: BuildCreateSummary(settlement),
                snapshotJson:  SerializeSettlement(settlement));

            return newSettlementId;
        }

        public async Task UpdateSettlementWithLedgerAsync(
            Settlement        settlement,
            DriverLedgerEntry ledgerEntry,
            int               driverId)
        {
            await _db.InitializeAsync();
            var conn = await _db.GetRawConnectionAsync();

            // Stamp the edit time before persisting
            settlement.UpdatedAt = DateTime.UtcNow;

            await Task.Run(() =>
            {
                conn.RunInTransaction(() =>
                {
                    // 1️⃣ Update Settlement
                    conn.Update(settlement);

                    // 1.b️⃣ Refresh Child Records
                    conn.Execute("DELETE FROM PlatformIncomes WHERE SettlementId = ?", settlement.Id);
                    conn.Execute("DELETE FROM SettlementExpenses WHERE SettlementId = ?", settlement.Id);

                    foreach (var pi in settlement.PlatformIncomes)
                    {
                        pi.SettlementId = settlement.Id;
                        conn.Insert(pi);
                    }
                    foreach (var se in settlement.ExpenseItems)
                    {
                        se.SettlementId = settlement.Id;
                        conn.Insert(se);
                    }

                    // 2️⃣ Update Ledger Entry
                    conn.Update(ledgerEntry);

                    // 3️⃣ Rebalance
                    RebalanceInTransaction(conn, driverId);
                });
            });

            // Phase 7 — Audit
            _ = _audit.LogAsync(
                entityType:    AuditEntities.Settlement,
                entityId:      settlement.Id,
                action:        AuditActions.Update,
                driverId:      settlement.DriverId,
                driverName:    settlement.DriverNameSnapshot,
                changeSummary: BuildUpdateSummary(settlement),
                snapshotJson:  SerializeSettlement(settlement));
        }

        public async Task DeleteSettlementWithLedgerAsync(
            Settlement         settlement,
            DriverLedgerEntry? ledgerEntry,
            int                driverId)
        {
            await _db.InitializeAsync();
            var conn = await _db.GetRawConnectionAsync();

            // Capture snapshot before deletion
            var deleteSummary = BuildDeleteSummary(settlement);
            int auditDriverId = settlement.DriverId;
            string auditDriverName = settlement.DriverNameSnapshot;
            int auditEntityId = settlement.Id;

            await Task.Run(() =>
            {
                conn.RunInTransaction(() =>
                {
                    // 1️⃣ Delete Child Records
                    conn.Execute("DELETE FROM PlatformIncomes WHERE SettlementId = ?", settlement.Id);
                    conn.Execute("DELETE FROM SettlementExpenses WHERE SettlementId = ?", settlement.Id);

                    // 2️⃣ Delete Ledger Entry
                    if (ledgerEntry is not null)
                        conn.Delete(ledgerEntry);

                    // 3️⃣ Rebalance
                    RebalanceInTransaction(conn, driverId);

                    // 4️⃣ Delete Settlement
                    conn.Delete(settlement);
                });
            });

            // Phase 7 — Audit (snapshot already captured above; entity is now gone)
            _ = _audit.LogAsync(
                entityType:    AuditEntities.Settlement,
                entityId:      auditEntityId,
                action:        AuditActions.Delete,
                driverId:      auditDriverId,
                driverName:    auditDriverName,
                changeSummary: deleteSummary,
                snapshotJson:  string.Empty);
        }

        // ── Private Helpers ──────────────────────────────────────────────────

        /// <summary>
        /// Recomputes running Balance for all entries of a driver inside an
        /// already-open synchronous transaction.
        /// </summary>
        private static void RebalanceInTransaction(SQLite.SQLiteConnection conn, int driverId)
        {
            var entries = conn
                .Table<DriverLedgerEntry>()
                .Where(e => e.DriverId == driverId)
                .ToList()
                .OrderBy(e => e.Date)
                .ThenBy(e => e.CreatedAt)
                .ToList();

            decimal running = 0m;
            foreach (var e in entries)
            {
                running  += e.Debit - e.Credit;
                e.Balance = Math.Round(running, 2);
                conn.Update(e);
            }
        }

        // ── Audit Summary Builders ───────────────────────────────────────────

        private static string BuildCreateSummary(Settlement s)
            => $"Settlement created — bill ₹{s.TotalIncome:N0}, " +
               $"driver share ₹{s.DriverShare:N0}, " +
               $"net payable ₹{s.NetDriverPayable:N0} ({s.ShiftType} shift, {s.Date.ToLocalTime():dd MMM yyyy})";

        private static string BuildUpdateSummary(Settlement s)
            => $"Settlement edited — bill ₹{s.TotalIncome:N0}, " +
               $"driver share ₹{s.DriverShare:N0}, " +
               $"net payable ₹{s.NetDriverPayable:N0} (updated {s.UpdatedAt?.ToLocalTime():dd MMM yyyy, hh:mm tt})";

        private static string BuildDeleteSummary(Settlement s)
            => $"Settlement deleted — bill ₹{s.TotalIncome:N0} on " +
               $"{s.Date.ToLocalTime():dd MMM yyyy} ({s.ShiftType} shift)";

        private static string SerializeSettlement(Settlement s)
        {
            try
            {
                return JsonSerializer.Serialize(new
                {
                    s.Id, s.Date, s.DriverId, s.DriverNameSnapshot,
                    s.VehicleId, s.VehicleNumberSnapshot, s.ShiftType,
                    s.TotalIncome, s.TotalCashCollected, s.DriverShare,
                    s.OwnerCngShare, s.DriverCngShare, s.DriverChallanTotal,
                    s.TotalOwnerExpenses, s.NetDriverPayable,
                    s.CalculatorVersion, s.CreatedAt, s.UpdatedAt
                });
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
