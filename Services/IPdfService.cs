using DriverLedger.Models;

namespace DriverLedger.Services
{
    public interface IPdfService
    {
        /// <summary>
        /// Generates a professional PDF receipt for a specific settlement.
        /// </summary>
        /// <returns>The file path to the generated PDF.</returns>
        Task<string> GenerateSettlementReceiptAsync(Settlement settlement);

        /// <summary>
        /// Generates a monthly summary report for the fleet.
        /// </summary>
        Task<string> GenerateMonthlyReportAsync(int year, int month);
    }
}
