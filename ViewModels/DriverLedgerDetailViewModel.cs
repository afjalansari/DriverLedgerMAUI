using System.Collections.ObjectModel;
using System.Windows.Input;
using DriverLedger.Models;
using DriverLedger.Repositories;
using DriverLedger.Services;

namespace DriverLedger.ViewModels
{
    /// <summary>
    /// Full wallet-style driver ledger detail.
    /// </summary>
    [QueryProperty(nameof(DriverId), "DriverId")]
    public class DriverLedgerDetailViewModel : BaseViewModel
    {
        private readonly IDriverRepository       _driverRepo;
        private readonly IDriverLedgerRepository _ledgerRepo;
        private readonly INavigationService      _nav;
        private readonly IDialogService          _dialog;
        private readonly IExportService          _export;

        // ── Driver Info ───────────────────────────────────────────────────
        private int     _driverId;
        private string  _driverName   = string.Empty;
        private string  _mobileNumber = string.Empty;

        // ── Wallet Totals ─────────────────────────────────────────────────
        private decimal _totalDebit;
        private decimal _totalCredit;
        private decimal _currentBalance;

        // ── Filter ────────────────────────────────────────────────────────
        private string _selectedFilter = FilterAll;
        private const string FilterAll   = "Sab";
        private const string FilterWeek  = "Is Hafte";
        private const string FilterMonth = "Is Mahine";

        // ── Collections ───────────────────────────────────────────────────
        private List<LedgerEntryDisplay> _allEntries = new();
        public ObservableCollection<LedgerEntryDisplay> Entries { get; } = new();

        public int DriverId
        {
            get => _driverId;
            set
            {
                SetProperty(ref _driverId, value);
                // BUG-FIX: must trigger LoadAsync when query param is set
                if (value > 0) _ = LoadAsync();
            }
        }

        public string  DriverName     { get => _driverName;   set => SetProperty(ref _driverName, value); }
        public string  MobileNumber   { get => _mobileNumber; set => SetProperty(ref _mobileNumber, value); }
        public decimal TotalDebit     { get => _totalDebit;   set => SetProperty(ref _totalDebit, value); }
        public decimal TotalCredit    { get => _totalCredit;  set => SetProperty(ref _totalCredit, value); }
        public decimal CurrentBalance
        {
            get => _currentBalance;
            set
            {
                SetProperty(ref _currentBalance, value);
                OnPropertyChanged(nameof(BalanceLabel));
                OnPropertyChanged(nameof(BalanceInterpretation));
                OnPropertyChanged(nameof(BalanceInterpretationEnglish));
                OnPropertyChanged(nameof(BalanceSubLabel));
                OnPropertyChanged(nameof(BalanceColor));
                OnPropertyChanged(nameof(HasPendingBalance));
                OnPropertyChanged(nameof(ClearButtonLabel));
                OnPropertyChanged(nameof(ClearButtonColor));
                OnPropertyChanged(nameof(NetSettlementDisplay));
            }
        }

        public string BalanceLabel => "NET SETTLEMENT";

        public string BalanceInterpretation => CurrentBalance > 0
            ? $"Driver ko ₹{CurrentBalance:N0} milega"
            : CurrentBalance < 0
                ? $"Driver ko ₹{Math.Abs(CurrentBalance):N0} dena hai"
                : "✅ Sab clear — koi pending nahi";

        public string BalanceInterpretationEnglish => CurrentBalance > 0
            ? "Owner pays driver"
            : CurrentBalance < 0
                ? "Driver pays owner"
                : "All settled";

        public string BalanceSubLabel => CurrentBalance > 0
            ? $"{DriverName} ko ₹{CurrentBalance:N0} milega"
            : CurrentBalance < 0
                ? $"{DriverName} ko ₹{Math.Abs(CurrentBalance):N0} dena hai"
                : "Koi pending amount nahi hai";

        public string NetSettlementDisplay => $"₹{Math.Abs(CurrentBalance):N0}";

        public Color BalanceColor => CurrentBalance > 0 ? Color.FromArgb("#4CAF50") : 
                                     CurrentBalance < 0 ? Color.FromArgb("#EF5350") : 
                                                          Color.FromArgb("#78909C");

        public bool HasPendingBalance => CurrentBalance != 0;

        public string ClearButtonLabel => CurrentBalance > 0
            ? $"💸 Pay ₹{CurrentBalance:N0} (Driver ko diya)"
            : CurrentBalance < 0
                ? $"💰 Collect ₹{Math.Abs(CurrentBalance):N0} (Driver se liya)"
                : "✅ Balance Clear Hai";

        public Color ClearButtonColor => CurrentBalance > 0
            ? Color.FromArgb("#2E7D32") 
            : CurrentBalance < 0
                ? Color.FromArgb("#C62828") 
                : Color.FromArgb("#37474F");

        public List<string> FilterOptions { get; } = new() { FilterAll, FilterWeek, FilterMonth };

        public string SelectedFilter
        {
            get => _selectedFilter;
            set { SetProperty(ref _selectedFilter, value); ApplyFilter(); }
        }

        public int    EntryCount      => Entries.Count;
        public string EntryCountLabel => $"{EntryCount} transaction{(EntryCount == 1 ? "" : "s")}";

        public ICommand ShareLedgerCommand   { get; }
        public ICommand ExportCsvCommand     { get; }
        public ICommand BackCommand          { get; }
        public ICommand ClearBalanceCommand  { get; }
        public ICommand AddAdvanceCommand    { get; }
        public ICommand ReceivePaymentCommand{ get; }
        public ICommand RefreshCommand       { get; }

        public DriverLedgerDetailViewModel(
            IDriverRepository       driverRepo,
            IDriverLedgerRepository ledgerRepo,
            INavigationService      nav,
            IDialogService          dialog,
            IExportService          export)
        {
            _driverRepo = driverRepo;
            _ledgerRepo = ledgerRepo;
            _nav        = nav;
            _dialog     = dialog;
            _export     = export;
            Title       = "Ledger";

            ShareLedgerCommand  = new Command(async () => await OnShareAsync());
            ExportCsvCommand    = new Command(async () => await OnExportCsvAsync());
            BackCommand         = new Command(async () => await _nav.GoBackAsync());
            ClearBalanceCommand = new Command(async () => await OnClearBalanceAsync(), () => HasPendingBalance);
            RefreshCommand      = new Command(async () => await LoadAsync());

            AddAdvanceCommand = new Command(async () =>
            {
                if (_driverId == 0) return;
                await _nav.GoToAsync($"{nameof(Views.AddAdvancePage)}?driverId={_driverId}");
            });

            ReceivePaymentCommand = new Command(async () =>
            {
                if (_driverId == 0) return;
                await _nav.GoToAsync($"{nameof(Views.ReceivePaymentPage)}?driverId={_driverId}");
            });
        }

        public async Task LoadAsync()
        {
            if (_driverId == 0 || IsBusy) return;
            IsBusy = true;
            try
            {
                var driver = await _driverRepo.GetDriverByIdAsync(_driverId);
                if (driver is not null)
                {
                    DriverName   = driver.DriverName;
                    MobileNumber = driver.MobileNumber;
                    Title        = driver.DriverName;
                }

                var rawEntries = await _ledgerRepo.GetDriverLedgerAsync(_driverId);

                // L4 fix: the stored Balance field is computed by RebalanceInTransaction
                // (sorted by Date+CreatedAt). Do NOT recompute here with a different sort
                // (Date only) — that produces divergent balances when entries share a date.
                // Trust the DB-stored Balance directly.
                _allEntries = rawEntries
                    .OrderByDescending(e => e.Date)
                    .ThenByDescending(e => e.CreatedAt)
                    .Select(e => new LedgerEntryDisplay(e))
                    .ToList();

                TotalDebit     = rawEntries.Sum(e => e.Debit);
                TotalCredit    = rawEntries.Sum(e => e.Credit);
                CurrentBalance = Math.Round(TotalDebit - TotalCredit, 2);

                ((Command)ClearBalanceCommand).ChangeCanExecute();
                ApplyFilter();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DriverLedgerDetailViewModel] Load error: {ex.Message}");
            }
            finally { IsBusy = false; }
        }

        private void ApplyFilter()
        {
            var today = DateTime.Today;
            IEnumerable<LedgerEntryDisplay> filtered = _selectedFilter switch
            {
                FilterWeek  => _allEntries.Where(e => e.Date.Date >= today.AddDays(-6)),
                FilterMonth => _allEntries.Where(e =>
                    e.Date.Year  == today.Year &&
                    e.Date.Month == today.Month),
                _           => _allEntries
            };

            Entries.Clear();
            foreach (var e in filtered) Entries.Add(e);
            OnPropertyChanged(nameof(EntryCount));
            OnPropertyChanged(nameof(EntryCountLabel));
        }

        private async Task OnClearBalanceAsync()
        {
            if (CurrentBalance == 0) return;

            var direction = CurrentBalance > 0
                ? $"₹{CurrentBalance:N0} (Driver ko milega)"
                : $"₹{Math.Abs(CurrentBalance):N0} (Driver ko dena hai)";

            bool confirmed = await _dialog.ShowConfirmAsync(
                "Balance Clear Karo",
                $"{direction}\n\nKya yeh amount settle ho gayi?",
                "Haan, Clear Karo",
                "Ruko");

            if (!confirmed) return;
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                var entry = new DriverLedgerEntry
                {
                    DriverId        = _driverId,
                    Date            = DateTime.UtcNow,
                    TransactionType = TransactionTypes.Clearance,
                    Description = CurrentBalance > 0
                        ? $"Balance clearance — Driver ko ₹{CurrentBalance:N0} diya"
                        : $"Balance clearance — Driver se ₹{Math.Abs(CurrentBalance):N0} liya",
                    Debit  = CurrentBalance < 0 ? Math.Abs(CurrentBalance) : 0m,
                    Credit = CurrentBalance > 0 ? CurrentBalance : 0m,
                };

                await _ledgerRepo.AddLedgerEntryAsync(entry);
                await _dialog.ShowAlertAsync("✅ Clear Ho Gaya", $"{DriverName} ka balance ab zero hai!");
                // H1 fix: release IsBusy BEFORE calling LoadAsync so its own guard doesn't
                // bail out. The finally block below guarantees the reset even on exceptions.
                IsBusy = false;
                await LoadAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DriverLedgerDetailViewModel] ClearBalance error: {ex.Message}");
                await _dialog.ShowAlertAsync("Error", "Galti aayi. Try karo.");
            }
            finally
            {
                // H1 fix: always reset IsBusy — protects against any exception before the
                // manual IsBusy = false in the try block.
                IsBusy = false;
            }
        }

        private async Task OnExportCsvAsync()
        {
            if (!Entries.Any()) return;
            IsBusy = true;
            try
            {
                // Export the filtered view as it is most relevant to the user
                var exportData = Entries.Select(e => new
                {
                    Date = e.DateDisplay,
                    Time = e.TimeDisplay,
                    Type = e.TypeLabel,
                    Description = e.Description,
                    Debit = e.Debit,
                    Credit = e.Credit,
                    RunningBalance = e.Balance,
                    Status = e.BalanceInterpretation
                });

                string path = await _export.ExportToCsvAsync(exportData, $"Ledger_{DriverName.Replace(" ", "_")}");
                await _export.ShareFileAsync(path, $"Ledger for {DriverName}");
            }
            catch (Exception ex)
            {
                await _dialog.ShowAlertAsync("Export Error", ex.Message);
            }
            finally { IsBusy = false; }
        }

        private async Task OnShareAsync()
        {
            try
            {
                var interpretation = CurrentBalance > 0
                    ? $"✅ {DriverName} ko ₹{CurrentBalance:N0} milega"
                    : CurrentBalance < 0
                        ? $"⚠️ {DriverName} ko ₹{Math.Abs(CurrentBalance):N0} dena hai"
                        : "✅ Sab settled hai — koi pending nahi";

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("🚗 *Driver Ledger — DriverLedger App*");
                sb.AppendLine($"Driver : {DriverName}");
                sb.AppendLine($"Mobile : {MobileNumber}");
                sb.AppendLine("──────────────────────────");
                sb.AppendLine($"Hisaab : {interpretation}");
                sb.AppendLine("──────────────────────────");
                sb.AppendLine();

                var recent = _allEntries.Take(5).ToList();
                if (recent.Any())
                {
                    sb.AppendLine("*Recent Transactions:*");
                    foreach (var e in recent)
                        sb.AppendLine($"  {e.DateDisplay}  {e.TypeLabel}  {e.AmountDisplay}  Bal: ₹{e.BalanceDisplay}");
                    sb.AppendLine();
                }

                sb.AppendLine($"Generated: {DateTime.Now:dd MMM yyyy, hh:mm tt}");

                await Clipboard.SetTextAsync(sb.ToString());
                await _dialog.ShowAlertAsync("Copied!", "Ledger summary copy ho gaya. WhatsApp mein paste karo.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DriverLedgerDetailViewModel] Share error: {ex.Message}");
                await _dialog.ShowAlertAsync("Error", "Copy nahi ho saka.");
            }
        }
    }

    public class LedgerEntryDisplay
    {
        private readonly DriverLedgerEntry _entry;
        public LedgerEntryDisplay(DriverLedgerEntry entry) => _entry = entry;

        public DateTime Date    => _entry.Date.ToLocalTime();
        public decimal  Debit   => _entry.Debit;
        public decimal  Credit  => _entry.Credit;
        public decimal  Balance => _entry.Balance;

        public string DateDisplay => _entry.Date.ToLocalTime().ToString("dd MMM yy");
        public string TimeDisplay => _entry.Date.ToLocalTime().ToString("hh:mm tt");
        public string Description => _entry.Description;

        public string TypeLabel => _entry.TransactionType switch
        {
            TransactionTypes.Advance    => "Advance Liya",
            TransactionTypes.Payment    => "Payment Diya",
            TransactionTypes.Settlement => "Settlement",
            TransactionTypes.Clearance  => "Clearance",
            _                           => _entry.TransactionType
        };

        public bool IsDebit  => _entry.Debit  > 0;
        public bool IsCredit => _entry.Credit > 0;

        public string AmountDisplay => (IsDebit && IsCredit)
            // BUG-15 fix: edge case — both non-zero, show net
            ? $"₹{Math.Abs(_entry.Debit - _entry.Credit):N0}"
            : IsDebit
                ? $"+₹{_entry.Debit:N0}"
                : $"−₹{_entry.Credit:N0}";

        public string BalanceDisplay => $"{_entry.Balance:N0}";

        public Color AmountColor => IsDebit
            ? Color.FromArgb("#4CAF50") 
            : Color.FromArgb("#EF5350"); 

        public string BalanceInterpretation => _entry.Balance > 0
            ? "Milega"
            : _entry.Balance < 0
                ? "Dena hai"
                : "Clear";

        public Color BalanceRowColor => _entry.Balance > 0 ? Color.FromArgb("#4CAF50") :
                                        _entry.Balance < 0 ? Color.FromArgb("#EF5350") :
                                                              Color.FromArgb("#78909C");

        public Color BadgeTextColor => _entry.TransactionType switch
        {
            TransactionTypes.Advance    => Color.FromArgb("#FFA726"),
            TransactionTypes.Payment    => Color.FromArgb("#66BB6A"),
            TransactionTypes.Settlement => Color.FromArgb("#90CAF9"),
            TransactionTypes.Clearance  => Color.FromArgb("#80DEEA"),
            _                           => Color.FromArgb("#BDBDBD")
        };

        public string TypeIcon => _entry.TransactionType switch
        {
            TransactionTypes.Advance    => "🔴",
            TransactionTypes.Payment    => "🟢",
            TransactionTypes.Settlement => "📋",
            TransactionTypes.Clearance  => "🔄",
            _                           => "❓"
        };
    }
}
