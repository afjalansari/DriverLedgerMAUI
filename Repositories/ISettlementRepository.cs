using DriverLedger.Models;

namespace DriverLedger.Repositories
{
    public interface ISettlementRepository
    {
        Task<List<Settlement>> GetAllSettlementsAsync();
        Task<Settlement?> GetSettlementByIdAsync(int id);
        Task<List<Settlement>> GetSettlementsByDateAsync(DateTime date);
        Task<List<Settlement>> GetSettlementsByMonthAsync(int year, int month);
        Task<int> SaveSettlementAsync(Settlement settlement);
        Task<int> DeleteSettlementAsync(Settlement settlement);

        /// <summary>
        /// Returns the most recent <paramref name="count"/> settlements ordered by Date descending.
        /// Avoids a full table scan when only the latest records are needed. (BUG-7)
        /// </summary>
        Task<List<Settlement>> GetRecentSettlementsAsync(int count);
    }
}

