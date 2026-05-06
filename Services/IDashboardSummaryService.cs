using DriverLedger.DTOs;

namespace DriverLedger.Services
{
    /// <summary>
    /// Produces dashboard-ready aggregations from the persistence layer.
    /// Keeping all LINQ / math here means DashboardViewModel becomes a
    /// pure property-setter and the logic is independently unit-testable.
    /// </summary>
    public interface IDashboardSummaryService
    {
        /// <summary>Aggregates all numbers for today's settlements.</summary>
        Task<DailyFleetSummary> GetDailySummaryAsync(DateTime date);

        /// <summary>Aggregates the month-level summary.</summary>
        Task<MonthlySummary> GetMonthlySummaryAsync(int year, int month);

        /// <summary>Aggregates driver-ledger balance breakdown.</summary>
        Task<LedgerBalanceSummary> GetLedgerBalanceSummaryAsync();

        /// <summary>Company display name (first company in the store).</summary>
        Task<(string CompanyName, string OwnerName)> GetCompanyInfoAsync();
    }
}
