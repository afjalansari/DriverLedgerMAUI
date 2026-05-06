using System.Collections.ObjectModel;
using System.Windows.Input;
using DriverLedger.DTOs;
using DriverLedger.Models;
using DriverLedger.Repositories;
using DriverLedger.Services;
using DriverLedger.Views;

namespace DriverLedger.ViewModels
{
    /// <summary>
    /// Shows all drivers with their current ledger balance.
    /// </summary>
    public class DriverLedgerListViewModel : BaseViewModel
    {
        private readonly IDriverRepository       _driverRepo;
        private readonly IDriverLedgerRepository _ledgerRepo;
        private readonly INavigationService      _nav;
        private readonly IDialogService          _dialog;

        // ── Collections ───────────────────────────────────────────────────
        public ObservableCollection<DriverBalanceSummary> DriverSummaries { get; } = new();
        private readonly List<DriverBalanceSummary> _allSummaries = new();

        // ── Search ────────────────────────────────────────────────────────
        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set { if (SetProperty(ref _searchText, value)) ApplyFilter(); }
        }

        // ── Wallet Totals ─────────────────────────────────────────────────
        private decimal _totalOwnerToGet;
        private decimal _totalOwnerOwes;
        private int     _pendingCount;

        /// <summary>Sum of all negative balances — drivers owe the owner this much.</summary>
        public decimal TotalOwnerToGet
        {
            get => _totalOwnerToGet;
            set => SetProperty(ref _totalOwnerToGet, value);
        }

        /// <summary>Sum of all positive balances — owner owes drivers this much.</summary>
        public decimal TotalOwnerOwes
        {
            get => _totalOwnerOwes;
            set => SetProperty(ref _totalOwnerOwes, value);
        }

        /// <summary>Number of drivers with a non-zero pending balance.</summary>
        public int PendingCount
        {
            get => _pendingCount;
            set
            {
                SetProperty(ref _pendingCount, value);
                OnPropertyChanged(nameof(PendingLabel));
                OnPropertyChanged(nameof(HasPending));
            }
        }

        public string PendingLabel => PendingCount == 0
            ? "✅ Sab clear hai"
            : $"{PendingCount} driver(s) ka balance pending hai";

        public bool HasPending => PendingCount > 0;

        // ── Commands ──────────────────────────────────────────────────────
        public ICommand ViewLedgerCommand     { get; }
        public ICommand AddAdvanceCommand     { get; }
        public ICommand ReceivePaymentCommand { get; }
        public ICommand BackCommand           { get; }
        public ICommand RefreshCommand        { get; }
        public ICommand ClearBalanceCommand   { get; }
        public ICommand QuickAdvanceCommand   { get; }
        public ICommand QuickPaymentCommand   { get; }

        public DriverLedgerListViewModel(
            IDriverRepository       driverRepo,
            IDriverLedgerRepository ledgerRepo,
            INavigationService      nav,
            IDialogService          dialog)
        {
            _driverRepo = driverRepo;
            _ledgerRepo = ledgerRepo;
            _nav        = nav;
            _dialog     = dialog;
            Title       = "Driver Ledger";

            ViewLedgerCommand = new Command<DriverBalanceSummary>(async s =>
            {
                if (s is null) return;
                await _nav.GoToAsync($"{nameof(DriverLedgerDetailPage)}?DriverId={s.DriverId}");
            });

            AddAdvanceCommand     = new Command(async () => await _nav.GoToAsync(nameof(AddAdvancePage)));
            ReceivePaymentCommand = new Command(async () => await _nav.GoToAsync(nameof(ReceivePaymentPage)));
            BackCommand           = new Command(async () => await _nav.GoBackAsync());
            RefreshCommand        = new Command(async () => await LoadAsync());

            QuickAdvanceCommand = new Command<DriverBalanceSummary>(async s =>
            {
                if (s is null) return;
                await _nav.GoToAsync($"{nameof(AddAdvancePage)}?driverId={s.DriverId}");
            });

            QuickPaymentCommand = new Command<DriverBalanceSummary>(async s =>
            {
                if (s is null) return;
                await _nav.GoToAsync($"{nameof(ReceivePaymentPage)}?driverId={s.DriverId}");
            });

            ClearBalanceCommand = new Command<DriverBalanceSummary>(async s =>
                await OnClearBalanceAsync(s));
        }

        public async Task LoadAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                var drivers  = await _driverRepo.GetAllDriversAsync();
                var balances = await _ledgerRepo.GetAllDriverBalancesAsync();

                _allSummaries.Clear();
                foreach (var d in drivers.Where(d => d.Status == DriverStatus.Active))
                {
                    balances.TryGetValue(d.Id, out var bal);
                    _allSummaries.Add(new DriverBalanceSummary
                    {
                        DriverId     = d.Id,
                        DriverName   = d.DriverName,
                        MobileNumber = d.MobileNumber,
                        Balance      = bal
                    });
                }

                ApplyFilter();
                RecalculateTotals();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DriverLedgerListViewModel] Load error: {ex.Message}");
            }
            finally { IsBusy = false; }
        }

        private async Task OnClearBalanceAsync(DriverBalanceSummary? s)
        {
            if (s is null || s.Balance == 0) return;

            var direction = s.Balance > 0
                ? $"₹{s.Balance:N0} (Driver ko milega)"
                : $"₹{Math.Abs(s.Balance):N0} (Driver ko dena hai)";

            bool confirmed = await _dialog.ShowConfirmAsync(
                "Balance Clear Karo",
                $"{direction}\n\nKya aapne paisa settle kar diya hai?",
                "Haan, Clear Karo",
                "Cancel");

            if (!confirmed) return;
            if (IsBusy) return;

            IsBusy = true;
            try
            {
                var entry = new DriverLedgerEntry
                {
                    DriverId        = s.DriverId,
                    Date            = DateTime.UtcNow,
                    TransactionType = TransactionTypes.Clearance,
                    Description = s.Balance > 0
                        ? $"Balance clearance — Driver ko ₹{s.Balance:N0} diya"
                        : $"Balance clearance — Driver se ₹{Math.Abs(s.Balance):N0} liya",
                    Debit  = s.Balance < 0 ? Math.Abs(s.Balance) : 0m,
                    Credit = s.Balance > 0 ? s.Balance : 0m,
                };

                await _ledgerRepo.AddLedgerEntryAsync(entry);
                await _dialog.ShowAlertAsync("✅ Done", $"{s.DriverName} ka balance clear ho gaya!");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DriverLedgerListViewModel] ClearBalance error: {ex.Message}");
                await _dialog.ShowAlertAsync("Error", "Balance clear karne mein dikkat aayi.");
            }
            finally
            {
                // BUG-D fix: always reset IsBusy in finally, then reload (was premature reset before dialog)
                IsBusy = false;
                await LoadAsync();
            }
        }

        private void ApplyFilter()
        {
            var term = _searchText.Trim().ToLowerInvariant();
            var filtered = string.IsNullOrEmpty(term)
                ? _allSummaries
                : _allSummaries.Where(s =>
                    s.DriverName.ToLowerInvariant().Contains(term) ||
                    s.MobileNumber.Contains(term)).ToList();

            DriverSummaries.Clear();
            foreach (var s in filtered) DriverSummaries.Add(s);
        }

        private void RecalculateTotals()
        {
            // BUG-J fix: compute totals from the filtered (visible) list so wallet badges
            // match what is shown on screen when a search is active.
            var visible = DriverSummaries.ToList();
            TotalOwnerOwes  = visible.Where(s => s.Balance > 0).Sum(s => s.Balance);
            TotalOwnerToGet = visible.Where(s => s.Balance < 0).Sum(s => Math.Abs(s.Balance));
            PendingCount    = visible.Count(s => s.Balance != 0);
        }
    }
}
