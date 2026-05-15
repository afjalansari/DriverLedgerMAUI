using DriverLedger.Models;
using DriverLedger.Repositories;

namespace DriverLedger.Services
{
    /// <summary>
    /// Orchestrates export operations: generate file → share via OS sheet.
    /// This is the ONLY class that constructs export file paths.
    /// ViewModels inject IExportOrchestrator and call a single method — they never
    /// see a file path or a stream.
    ///
    /// Root-cause fix for export crash:
    ///   • Files are written to FileSystem.AppDataDirectory (private, no FileProvider needed).
    ///   • Share.Default.RequestAsync works with private-path files on all Android versions
    ///     because MAUI wraps the path in a content:// URI internally.
    ///   • All I/O is async + wrapped in try/catch.
    /// </summary>
    public class ExportOrchestrator : IExportOrchestrator
    {
        private readonly IPdfService           _pdf;
        private readonly IExportService        _csv;
        private readonly ISettlementRepository _settlementRepo;
        private readonly IDriverLedgerRepository _ledgerRepo;
        private readonly IDialogService        _dialog;

        public ExportOrchestrator(
            IPdfService              pdf,
            IExportService           csv,
            ISettlementRepository    settlementRepo,
            IDriverLedgerRepository  ledgerRepo,
            IDialogService           dialog)
        {
            _pdf            = pdf;
            _csv            = csv;
            _settlementRepo = settlementRepo;
            _ledgerRepo     = ledgerRepo;
            _dialog         = dialog;
        }

        // ── PDF Export ───────────────────────────────────────────────────────

        public async Task ExportSettlementPdfAsync(int settlementId)
        {
            try
            {
                var settlement = await _settlementRepo.GetSettlementByIdAsync(settlementId);
                if (settlement is null)
                {
                    await _dialog.ShowAlertAsync("Not Found",
                        "Settlement record not found.");
                    return;
                }

                // Generate on background thread (CPU-heavy QuestPDF work)
                string filePath = await _pdf.GenerateSettlementReceiptAsync(settlement);

                if (!File.Exists(filePath))
                    throw new FileNotFoundException("PDF generation returned a path that does not exist.", filePath);

                await ShareFileAsync(filePath, $"Settlement Receipt #{settlementId}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[ExportOrchestrator] PDF export error: {ex.Message}\n{ex.StackTrace}");
                await _dialog.ShowAlertAsync("Export Failed",
                    "Could not generate PDF. Please try again.\n\nDetails: " + ex.Message);
            }
        }

        // ── CSV Exports ──────────────────────────────────────────────────────

        public async Task ExportAllSettlementsCsvAsync()
        {
            try
            {
                var settlements = await _settlementRepo.GetAllSettlementsAsync();
                if (!settlements.Any())
                {
                    await _dialog.ShowAlertAsync("No Data", "No settlement records to export.");
                    return;
                }

                // Project to a flat anonymous DTO — avoids exporting FK columns,
                // navigation properties, and JSON blobs that confuse CSV readers.
                var rows = settlements.Select(s => new
                {
                    s.Id,
                    Date           = s.Date.ToLocalTime().ToString("dd MMM yyyy"),
                    s.ShiftType,
                    s.TotalIncome,
                    s.TotalCashCollected,
                    s.DriverShare,
                    s.OwnerCngShare,
                    s.TotalOwnerExpenses,
                    s.NetDriverPayable,
                    s.DriverNameSnapshot,
                    s.VehicleNumberSnapshot
                });

                string filePath = await _csv.ExportToCsvAsync(rows, "DriverLedger_Settlements");
                await ShareFileAsync(filePath, "All Settlements Export");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[ExportOrchestrator] CSV settlements error: {ex.Message}");
                await _dialog.ShowAlertAsync("Export Failed",
                    "Could not export settlements. Details: " + ex.Message);
            }
        }

        public async Task ExportDriverLedgerCsvAsync(int driverId)
        {
            try
            {
                var entries = await _ledgerRepo.GetDriverLedgerAsync(driverId);
                if (!entries.Any())
                {
                    await _dialog.ShowAlertAsync("No Data",
                        "No ledger entries found for this driver.");
                    return;
                }

                var rows = entries.Select(e => new
                {
                    e.Id,
                    Date            = e.Date.ToLocalTime().ToString("dd MMM yyyy HH:mm"),
                    e.TransactionType,
                    e.Description,
                    Debit           = e.Debit,
                    Credit          = e.Credit,
                    // FIX-0B: Use the stored Balance field (maintained by RebalanceInTransaction)
                    // instead of per-row Debit-Credit, which was a single-row delta, not a running total.
                    RunningBalance  = e.Balance
                });

                string filePath = await _csv.ExportToCsvAsync(rows,
                    $"DriverLedger_Driver{driverId}");
                await ShareFileAsync(filePath, $"Driver Ledger Export");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[ExportOrchestrator] CSV ledger error: {ex.Message}");
                await _dialog.ShowAlertAsync("Export Failed",
                    "Could not export driver ledger. Details: " + ex.Message);
            }
        }

        // ── Shared Share Logic ───────────────────────────────────────────────

        private static async Task ShareFileAsync(string filePath, string title)
        {
            // MAUI Share.Default handles the FileProvider content:// URI wrapping on Android.
            // This is why we do NOT use a raw file:// URI — that causes FileUriExposedException.
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = title,
                File  = new ShareFile(filePath)
            });
        }
    }
}
