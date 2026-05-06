namespace DriverLedger.Services
{
    /// <summary>
    /// Single entry-point for all export operations (PDF / CSV).
    /// ViewModels call this service only — they never construct file paths directly.
    /// </summary>
    public interface IExportOrchestrator
    {
        /// <summary>
        /// Generates a PDF receipt for the given settlement and opens the OS share sheet.
        /// </summary>
        Task ExportSettlementPdfAsync(int settlementId);

        /// <summary>
        /// Exports the full settlement history to CSV and opens the OS share sheet.
        /// </summary>
        Task ExportAllSettlementsCsvAsync();

        /// <summary>
        /// Exports the driver ledger for a specific driver to CSV.
        /// </summary>
        Task ExportDriverLedgerCsvAsync(int driverId);
    }
}
