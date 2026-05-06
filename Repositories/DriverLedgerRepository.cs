using DriverLedger.Database;
using DriverLedger.Models;

namespace DriverLedger.Repositories
{
    public class DriverLedgerRepository : IDriverLedgerRepository
    {
        private readonly DatabaseService _db;

        public DriverLedgerRepository(DatabaseService db) => _db = db;

        /// <summary>
        /// Adds a new ledger entry, computing the running balance automatically.
        /// Debit = money owed to driver (increases balance).
        /// Credit = money received from driver (decreases balance).
        /// </summary>
        public async Task<int> AddLedgerEntryAsync(DriverLedgerEntry entry)
        {
            // Compute running balance from the last entry for this driver
            var currentBalance = await GetDriverBalanceAsync(entry.DriverId);
            entry.Balance  = currentBalance + entry.Debit - entry.Credit;
            entry.CreatedAt = DateTime.UtcNow;

            return await _db.InsertAsync(entry);
        }

        /// <summary>All ledger entries for a driver, ordered by date then CreatedAt ascending.</summary>
        public async Task<List<DriverLedgerEntry>> GetDriverLedgerAsync(int driverId)
        {
            var all = await _db.GetAllAsync<DriverLedgerEntry>();
            return all
                .Where(e => e.DriverId == driverId)
                .OrderBy(e => e.Date)
                .ThenBy(e => e.CreatedAt)
                .ToList();
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
            var all = await _db.GetAllAsync<DriverLedgerEntry>();
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
            var all = await GetDriverLedgerAsync(driverId);
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
            return await _db.UpdateAsync(entry);
        }

        public async Task<int> DeleteLedgerEntryAsync(DriverLedgerEntry entry)
        {
            return await _db.DeleteAsync(entry);
        }

        /// <inheritdoc />
        public async Task RebalanceDriverLedgerAsync(int driverId)
        {
            // Load all remaining entries for this driver in chronological order
            var entries = await GetDriverLedgerAsync(driverId); // already sorted ASC

            decimal running = 0m;
            foreach (var e in entries)
            {
                running += e.Debit - e.Credit;
                e.Balance = Math.Round(running, 2);
                await _db.UpdateAsync(e);
            }
        }
    }
}

