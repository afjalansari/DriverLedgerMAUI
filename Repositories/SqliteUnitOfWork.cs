using DriverLedger.Database;
using DriverLedger.Models;

namespace DriverLedger.Repositories
{
    /// <summary>
    /// SQLite-backed implementation of <see cref="IUnitOfWork"/>.
    /// All multi-row mutations are wrapped in <c>RunInTransaction</c> for atomicity.
    /// </summary>
    public class SqliteUnitOfWork : IUnitOfWork
    {
        private readonly DatabaseService _db;

        public SqliteUnitOfWork(DatabaseService db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
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
                    ledgerEntry.CreatedAt = DateTime.UtcNow;
                    conn.Insert(ledgerEntry);

                    // 3️⃣ Rebalance Ledger (ensures all balances from this point forward are correct)
                    RebalanceInTransaction(conn, ledgerEntry.DriverId);
                });
            });

            return newSettlementId;
        }

        public async Task UpdateSettlementWithLedgerAsync(
            Settlement        settlement,
            DriverLedgerEntry ledgerEntry,
            int               driverId)
        {
            await _db.InitializeAsync();
            var conn = await _db.GetRawConnectionAsync();

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
        }

        public async Task DeleteSettlementWithLedgerAsync(
            Settlement         settlement,
            DriverLedgerEntry? ledgerEntry,
            int                driverId)
        {
            await _db.InitializeAsync();
            var conn = await _db.GetRawConnectionAsync();

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
        }

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
                running += e.Debit - e.Credit;
                e.Balance = Math.Round(running, 2);
                conn.Update(e);
            }
        }
    }
}
