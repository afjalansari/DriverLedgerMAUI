using System.Text.Json;
using System.Windows.Input;
using DriverLedger.Models;
using DriverLedger.Repositories;
using DriverLedger.Services;

namespace DriverLedger.ViewModels
{
    [QueryProperty(nameof(SettlementId), "settlementId")]
    public class SettlementDetailViewModel : BaseViewModel
    {
        private readonly ISettlementRepository   _settlementRepo;
        private readonly IVehicleRepository      _vehicleRepo;
        private readonly IDriverRepository       _driverRepo;
        private readonly IDriverLedgerRepository _ledgerRepo;
        private readonly IUnitOfWork             _uow;
        private readonly INavigationService      _nav;
        private readonly IDialogService          _dialog;
        private readonly IPdfService             _pdf;

        private int     _settlementId;
        private string  _vehicleNumber = "—";
        private string  _driverName    = "—";
        private string  _shiftType     = "—";
        private string  _dateText      = "—";

        private decimal _totalIncome;
        private decimal _totalCashCollected;
        private decimal _driverShare;
        private decimal _ownerCngShare;
        private decimal _totalOwnerExpenses;
        private decimal _netDriverPayable;
        private decimal _driverIncomePercent;
        private decimal _totalCng;
        private decimal _driverFaultChallan;

        private string      _settlementLabel  = "—";
        private string      _aggregatorSummary = "—";
        private Settlement? _settlement;

        // ── Public Properties ─────────────────────────────────────────────

        public int     SettlementId    { get => _settlementId;   set { SetProperty(ref _settlementId, value);   if (value > 0) _ = LoadAsync(value); } }
        public string  VehicleNumber   { get => _vehicleNumber;  set => SetProperty(ref _vehicleNumber, value); }
        public string  DriverName      { get => _driverName;     set => SetProperty(ref _driverName, value); }
        public string  ShiftType       { get => _shiftType;      set => SetProperty(ref _shiftType, value); }
        public string  DateText        { get => _dateText;       set => SetProperty(ref _dateText, value); }

        public decimal TotalIncome        { get => _totalIncome;         set => SetProperty(ref _totalIncome, value); }
        public decimal TotalCashCollected { get => _totalCashCollected;  set => SetProperty(ref _totalCashCollected, value); }
        public decimal DriverShare        { get => _driverShare;         set => SetProperty(ref _driverShare, value); }
        public decimal OwnerCngShare      { get => _ownerCngShare;       set => SetProperty(ref _ownerCngShare, value); }
        public decimal TotalOwnerExpenses { get => _totalOwnerExpenses;  set => SetProperty(ref _totalOwnerExpenses, value); }
        public decimal NetDriverPayable   { get => _netDriverPayable;    set => SetProperty(ref _netDriverPayable, value); }

        public decimal DriverIncomePercent { get => _driverIncomePercent; set => SetProperty(ref _driverIncomePercent, value); }
        public decimal TotalCNG            { get => _totalCng;            set => SetProperty(ref _totalCng, value); }

        public bool    HasOwnerExpenses   => _totalOwnerExpenses > 0;
        public bool    HasDriverChallan   => _driverFaultChallan > 0;

        public string SettlementLabel   { get => _settlementLabel;   set => SetProperty(ref _settlementLabel, value); }
        public string AggregatorSummary { get => _aggregatorSummary; set => SetProperty(ref _aggregatorSummary, value); }

        // ── Commands ───────────────────────────────────────────────
        public ICommand ShareCommand       { get; }
        public ICommand DownloadPdfCommand { get; }
        public ICommand EditCommand        { get; }
        public ICommand DeleteCommand      { get; }
        public ICommand BackCommand        { get; }

        public SettlementDetailViewModel(
            ISettlementRepository   settlementRepo,
            IVehicleRepository      vehicleRepo,
            IDriverRepository       driverRepo,
            IDriverLedgerRepository ledgerRepo,
            IUnitOfWork             uow,
            INavigationService      nav,
            IDialogService          dialog,
            IPdfService             pdf)
        {
            _settlementRepo = settlementRepo ?? throw new ArgumentNullException(nameof(settlementRepo));
            _vehicleRepo    = vehicleRepo    ?? throw new ArgumentNullException(nameof(vehicleRepo));
            _driverRepo     = driverRepo     ?? throw new ArgumentNullException(nameof(driverRepo));
            _ledgerRepo     = ledgerRepo     ?? throw new ArgumentNullException(nameof(ledgerRepo));
            _uow            = uow            ?? throw new ArgumentNullException(nameof(uow));
            _nav            = nav            ?? throw new ArgumentNullException(nameof(nav));
            _dialog         = dialog         ?? throw new ArgumentNullException(nameof(dialog));
            _pdf            = pdf            ?? throw new ArgumentNullException(nameof(pdf));
            Title           = "Settlement";

            ShareCommand       = new Command(async () => await OnShareAsync());
            DownloadPdfCommand = new Command(async () => await OnDownloadPdfAsync(), () => !IsBusy);
            EditCommand        = new Command(async () =>
            {
                if (_settlementId > 0)
                    await _nav.GoToAsync($"{nameof(Views.SettlementEntryPage)}?editId={_settlementId}");
            }, () => !IsBusy);
            DeleteCommand = new Command(async () => await OnDeleteAsync(), () => !IsBusy);
            BackCommand   = new Command(async () => await _nav.GoBackAsync());
        }

        // ══════════════════════════════════════════════════════════════════
        //  Load
        // ══════════════════════════════════════════════════════════════════

        public async Task LoadAsync(int id)
        {
            IsBusy = true;
            if (DeleteCommand is Command d) d.ChangeCanExecute();
            if (EditCommand is Command e) e.ChangeCanExecute();
            try
            {
                _settlement = await _settlementRepo.GetSettlementByIdAsync(id);
                // BUG-6 fix: don't silently show a blank page — tell the user and go back.
                if (_settlement is null)
                {
                    await _dialog.ShowAlertAsync("Not Found", "Settlement not found. It may have been deleted.");
                    await _nav.GoBackAsync();
                    return;
                }

                var vehicle = await _vehicleRepo.GetVehicleByIdAsync(_settlement.VehicleId);
                var driver  = await _driverRepo.GetDriverByIdAsync(_settlement.DriverId);

                var localDate = _settlement.Date == default
                    ? DateTime.Now
                    : _settlement.Date.ToLocalTime();
                DateText = localDate.ToString("dd MMM yyyy");

                VehicleNumber = vehicle?.VehicleNumber ?? "—";
                DriverName    = driver?.DriverName     ?? "—";
                ShiftType     = _settlement.ShiftType;
                Title         = $"Settlement — {DateText}";

                TotalIncome         = _settlement.TotalIncome;
                TotalCashCollected  = _settlement.TotalCashCollected;
                DriverShare         = _settlement.DriverShare;
                OwnerCngShare       = _settlement.OwnerCngShare;
                TotalOwnerExpenses  = _settlement.TotalOwnerExpenses;
                NetDriverPayable    = _settlement.NetDriverPayable;
                // DriverIncomePercent: not stored — derive from DriverShare/TotalIncome
                DriverIncomePercent = _settlement.TotalIncome > 0
                    ? Math.Round(_settlement.DriverShare / _settlement.TotalIncome * 100, 0)
                    : 0m;
                // BUG-9 fix: compute TotalCNG from the loaded ExpenseItems collection instead
                // of hardcoding 0. The repository always populates ExpenseItems via LoadCollectionsAsync.
                TotalCNG            = _settlement.ExpenseItems
                    .Where(e => e.Type == ExpenseType.CNG)
                    .Sum(e => e.Amount);
                // Note: DriverChallan has no dedicated ExpenseType in the current model;
                // challans are not stored in ExpenseItems, so _driverFaultChallan stays 0.
                _driverFaultChallan = 0m;

                OnPropertyChanged(nameof(HasOwnerExpenses));
                OnPropertyChanged(nameof(HasDriverChallan));

                // Build aggregator summary
                if (_settlement.PlatformIncomes != null)
                {
                    AggregatorSummary = string.Join("  •  ", _settlement.PlatformIncomes.Select(pi => $"{pi.PlatformName} ₹{pi.OperatorBill:N0}"));
                }
                else
                {
                    AggregatorSummary = "—";
                }

                // Final bilingual label logic
                if (NetDriverPayable > 0)
                    SettlementLabel = $"₹{NetDriverPayable:N0} (Driver ko milega)\nOwner pays Driver";
                else if (NetDriverPayable < 0)
                    SettlementLabel = $"₹{Math.Abs(NetDriverPayable):N0} (Driver ko dena hai)\nDriver pays Owner";
                else
                    SettlementLabel = "✅ Hisaab Barabar\nSettled Exactly";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[SettlementDetailViewModel] LoadAsync error: {ex.Message}\n{ex.StackTrace}");
                await _dialog.ShowAlertAsync("Error", "There was a problem loading the settlement. Please go back and try again.");
                await _nav.GoBackAsync();
            }
            finally 
            { 
                IsBusy = false; 
                if (DeleteCommand is Command d3) d3.ChangeCanExecute();
                if (EditCommand is Command e3) e3.ChangeCanExecute();
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  Download PDF
        // ══════════════════════════════════════════════════════════════════

        private async Task OnDownloadPdfAsync()
        {
            if (_settlement is null) return;
            if (DownloadPdfCommand is Command c) c.ChangeCanExecute();
            IsBusy = true;
            try
            {
                string path = await _pdf.GenerateSettlementReceiptAsync(_settlement);

                await Share.RequestAsync(new ShareFileRequest
                {
                    Title = $"Settlement Receipt — {DriverName} — {DateText}",
                    File  = new ShareFile(path)
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettlementDetailViewModel] PDF error: {ex.Message}");
                await _dialog.ShowAlertAsync("PDF Error", "Receipt generate nahi ho saka. Try karo.");
            }
            finally
            {
                IsBusy = false;
                if (DownloadPdfCommand is Command c2) c2.ChangeCanExecute();
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  Share (WhatsApp text)
        // ══════════════════════════════════════════════════════════════════

        private async Task OnShareAsync()
        {
            if (_settlement is null) return;
            var text = BuildShareText();

            try
            {
                await Share.RequestAsync(new ShareTextRequest
                {
                    Text  = text,
                    Title = "Share Settlement"
                });
            }
            catch
            {
                await Clipboard.SetTextAsync(text);
                await _dialog.ShowAlertAsync("✅ Copied!", "Settlement summary copied to clipboard. Paste it in WhatsApp.");
            }
        }

        private string BuildShareText()
        {
            var sb = new System.Text.StringBuilder();

            sb.AppendLine("🚗 *Daily Settlement Report*");
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine($"📅 *Date:*    {DateText}");
            sb.AppendLine($"🚙 *Vehicle:* {VehicleNumber}");
            sb.AppendLine($"👤 *Driver:*  {DriverName}");
            sb.AppendLine($"🔄 *Shift:*   {ShiftType}");
            sb.AppendLine();

            sb.AppendLine("📱 *Operator Bill (Total Kamai)*");
            sb.AppendLine("─────────────────────");
            if (!string.IsNullOrWhiteSpace(AggregatorSummary) && AggregatorSummary != "—")
            {
                foreach (var part in AggregatorSummary.Split("  •  ", StringSplitOptions.RemoveEmptyEntries))
                    sb.AppendLine($"  • {part.Trim()}");
            }
            sb.AppendLine($"  *Total Bill    : ₹{TotalIncome:N0}*");
            sb.AppendLine($"  Cash Collected : ₹{TotalCashCollected:N0}");
            sb.AppendLine();

            sb.AppendLine("💰 *Driver ka Hissa (Driver Share)*");
            sb.AppendLine("─────────────────────");
            sb.AppendLine($"  Driver Share : ₹{DriverShare:N0}");
            sb.AppendLine();

            sb.AppendLine("⛽ *Owner CNG Share (Refund)*");
            sb.AppendLine("─────────────────────");
            sb.AppendLine($"  Owner CNG Share  : ₹{OwnerCngShare:N0}");
            if (TotalOwnerExpenses > 0)
                sb.AppendLine($"  Other Expenses   : ₹{TotalOwnerExpenses:N0}");
            sb.AppendLine();

            // ── FINAL RESULT (the only thing that matters) ──
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━");
            var net = NetDriverPayable;
            if (net > 0)
                sb.AppendLine($"💸 *Owner pays Driver : ₹{net:N0}*  (Driver ko milega ✓)");
            else if (net < 0)
                sb.AppendLine($"✅ *Driver pays Owner : ₹{Math.Abs(net):N0}*  (Driver ko dena hai)");
            else
                sb.AppendLine("✅ *Hisaab Barabar — koi lena dena nahi*");

            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.Append($"_Sent via DriverLedger • {DateTime.Now:dd MMM yyyy, hh:mm tt}_");

            return sb.ToString();
        }

        // ══════════════════════════════════════════════════════════════════
        //  Delete
        // ══════════════════════════════════════════════════════════════════

        private async Task OnDeleteAsync()
        {
            if (_settlement is null) return;

            bool confirm = await _dialog.ShowConfirmAsync(
                "Delete Settlement",
                $"Delete settlement for {DriverName} on {DateText}?\n\nThis will also remove the linked ledger entry.",
                "Delete", "Cancel");
            if (!confirm) return;

            IsBusy = true;
            if (DeleteCommand is Command d2) d2.ChangeCanExecute();
            if (EditCommand is Command e2) e2.ChangeCanExecute();
            try
            {
                // BUG-C fix: atomic delete via UoW
                var ledgerEntries = await _ledgerRepo.GetDriverLedgerAsync(_settlement.DriverId);
                var linked = ledgerEntries.FirstOrDefault(e => e.SettlementId == _settlement.Id);
                await _uow.DeleteSettlementWithLedgerAsync(_settlement, linked, _settlement.DriverId);

                await _nav.GoBackAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettlementDetailViewModel] Delete error: {ex.Message}");
                await _dialog.ShowAlertAsync("Error", "Failed to delete settlement. Please try again.");
            }
            finally 
            { 
                IsBusy = false; 
                // Notify commands that IsBusy has changed
                if (DeleteCommand is Command deleteCmd) deleteCmd.ChangeCanExecute();
                if (EditCommand is Command editCmd) editCmd.ChangeCanExecute();
            }
        }
    }
}
