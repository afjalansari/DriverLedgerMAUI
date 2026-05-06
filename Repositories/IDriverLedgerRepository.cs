using DriverLedger.Models;

namespace DriverLedger.Repositories
{
    public interface IDriverLedgerRepository
    {
        Task<int> AddLedgerEntryAsync(DriverLedgerEntry entry);
        Task<List<DriverLedgerEntry>> GetDriverLedgerAsync(int driverId);
        Task<decimal> GetDriverBalanceAsync(int driverId);
        Task<Dictionary<int, decimal>> GetAllDriverBalancesAsync();
        Task<List<DriverLedgerEntry>> GetLedgerHistoryAsync(int driverId, DateTime from, DateTime to);
        Task<int> UpdateLedgerEntryAsync(DriverLedgerEntry entry);
        Task<int> DeleteLedgerEntryAsync(DriverLedgerEntry entry);
        /// <summary>
        /// Recomputes and persists the running Balance column for every ledger entry
        /// belonging to the specified driver, in chronological order.
        /// Must be called after any entry deletion to prevent stale balance snapshots.
        /// </summary>
        Task RebalanceDriverLedgerAsync(int driverId);
    }
}
