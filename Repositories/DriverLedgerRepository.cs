using DriverLedger.Database;
using DriverLedger.Models;
using DriverLedger.Services;

namespace DriverLedger.Repositories
{
    public class DriverLedgerRepository : IDriverLedgerRepository
    {
        private readonly DatabaseService _db;
        private readonly IAuditService   _audit;

        public DriverLedgerRepository(DatabaseService db, IAuditService audit)
        {
            _db    = db    ?? throw new ArgumentNullException(nameof(db));
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        }

        /// <summary>
        /// Adds a new ledger entry, computing the running balance automatically.
        /// Debit = money owed to driver (increases balance).
        /// Credit = money received from driver (decreases balance).
        /// Phase 7: writes an AuditLog row after successful insert.
        /// </summary>
        public async Task<int> AddLedgerEntryAsync(DriverLedgerEntry entry)
        {
            // Compute running balance from the last entry for this driver
            var currentBalance = await GetDriverBalanceAsync(entry.DriverId);
            entry.Balance   = currentBalance + entry.Debit - entry.Credit;
            entry.CreatedAt = DateTime.UtcNow;

            var result = await _db.InsertAsync(entry);

            // Phase 7 — Audit (non-fatal)
            _ = _audit.LogAsync(
                entityType:    AuditEntities.LedgerEntry,
                entityId:      entry.Id,
                action:        AuditActions.Create,
                driverId:      entry.DriverId,
                driverName:    string.Empty,   // driver name resolved by caller context
                changeSummary: BuildLedgerSummary(entry, AuditActions.Create));

            return result;
        }

        /// <summary>All ledger entries for a driver, ordered by date then CreatedAt ascending.</summary>
        public async Task<List<DriverLedgerEntry>> GetDriverLedgerAsync(int driverId)
        {
            // P5-1 fix: query through the raw connection so the idx_ledger_driver_date
            // composite index (DriverId, Date) created in Migration_003 is used.
            var conn = await _db.GetRawConnectionAsync();
            return await Task.Run(() =>
                conn.Table<DriverLedgerEntry>()
                    .Where(e => e.DriverId == driverId)
                    .ToList()
                    .OrderBy(e => e.Date)
                    .ThenBy(e => e.CreatedAt)
                    .ToList());
        }

        /// <summary>
        /// Current balance = Σ Debit − Σ Credit for a driver.
        /// Computed from raw debit/credit columns rather than the stored Balance
        /// snapshot so that stale snapshots (e.g. after a deletion) never produce
        /// a wrong base for the next AddLedgerEntryAsync call. (BUG-1)
        /// </summary>
        public async Task<decimal> GetDriverBalanceAsync(int driverId)
        {
            var entries = await GetDriverLedgerAsync(driverId);
            if (!entries.Any()) return 0m;
            return entries.Sum(e => e.Debit) - entries.Sum(e => e.Credit);
        }

        /// <summary>
        /// Returns a dictionary of DriverId → CurrentBalance for all drivers that have ledger entries.
        /// BUG-B fix: computes balance from Σ Debit − Σ Credit instead of reading the stored
        /// Balance snapshot, which can be stale after deletions or missed rebalances.
        /// </summary>
        public async Task<Dictionary<int, decimal>> GetAllDriverBalancesAsync()
        {
            // P5-3 fix: use raw connection — DriverId index is hit during the sequential read.
            var conn = await _db.GetRawConnectionAsync();
            var all  = await Task.Run(() => conn.Table<DriverLedgerEntry>().ToList());
            return all
                .GroupBy(e => e.DriverId)
                .ToDictionary(
                    g => g.Key,
                    g => Math.Round(g.Sum(e => e.Debit) - g.Sum(e => e.Credit), 2)
                );
        }

        /// <summary>Ledger history between two dates (inclusive) for a driver.</summary>
        public async Task<List<DriverLedgerEntry>> GetLedgerHistoryAsync(int driverId, DateTime from, DateTime to)
        {
            var all      = await GetDriverLedgerAsync(driverId);
            var fromDate = from.Date;
            var toDate   = to.Date;
            return all
                // BUG-11 fix: dates are stored as UTC. Dates with Kind=Unspecified would be
                // double-converted by ToLocalTime(). Force Utc kind first to be safe.
                .Where(e => DateTime.SpecifyKind(e.Date, DateTimeKind.Utc).ToLocalTime().Date >= fromDate
                         && DateTime.SpecifyKind(e.Date, DateTimeKind.Utc).ToLocalTime().Date <= toDate)
                .ToList();
        }

        public async Task<int> UpdateLedgerEntryAsync(DriverLedgerEntry entry)
        {
            var result = await _db.UpdateAsync(entry);

            // Phase 7 — Audit
            _ = _audit.LogAsync(
                entityType:    AuditEntities.LedgerEntry,
                entityId:      entry.Id,
                action:        AuditActions.Update,
                driverId:      entry.DriverId,
                driverName:    string.Empty,
                changeSummary: BuildLedgerSummary(entry, AuditActions.Update));

            return result;
        }

        public async Task<int> DeleteLedgerEntryAsync(DriverLedgerEntry entry)
        {
            var summary = BuildLedgerSummary(entry, AuditActions.Delete);
            var result  = await _db.DeleteAsync(entry);

            // Phase 7 — Audit (capture summary before delete)
            _ = _audit.LogAsync(
                entityType:    AuditEntities.LedgerEntry,
                entityId:      entry.Id,
                action:        AuditActions.Delete,
                driverId:      entry.DriverId,
                driverName:    string.Empty,
                changeSummary: summary);

            return result;
        }

        /// <inheritdoc />
        public async Task RebalanceDriverLedgerAsync(int driverId)
        {
            // P5-4 fix: wrap all N Update calls in a single RunInTransaction so the
            // rebalance is atomic and commits in one fsync instead of N separate ones.
            var entries = await GetDriverLedgerAsync(driverId); // already sorted ASC
            var conn    = await _db.GetRawConnectionAsync();
            await Task.Run(() =>
            {
                conn.RunInTransaction(() =>
                {
                    decimal running = 0m;
                    foreach (var e in entries)
                    {
                        running   += e.Debit - e.Credit;
                        e.Balance  = Math.Round(running, 2);
                        conn.Update(e);
                    }
                });
            });
        }

        // ── Audit Summary Builder ────────────────────────────────────────────

        private static string BuildLedgerSummary(DriverLedgerEntry e, string action)
        {
            var direction = e.Debit > 0
                ? $"Debit ₹{e.Debit:N0}"
                : $"Credit ₹{e.Credit:N0}";
            return $"{e.TransactionType} {action.ToLower()}d — {direction} " +
                   $"({e.Date.ToLocalTime():dd MMM yyyy}) — {e.Description}";
        }
    }
}
