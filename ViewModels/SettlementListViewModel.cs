using System.Collections.ObjectModel;
using System.Windows.Input;
using DriverLedger.DTOs;
using DriverLedger.Models;
using DriverLedger.Repositories;
using DriverLedger.Services;
using DriverLedger.Views;

namespace DriverLedger.ViewModels
{
    public class SettlementListViewModel : BaseViewModel
    {
        private readonly ISettlementRepository   _settlementRepo;
        private readonly IVehicleRepository      _vehicleRepo;
        private readonly IDriverRepository       _driverRepo;
        private readonly IDriverLedgerRepository _ledgerRepo;
        private readonly IUnitOfWork             _uow;
        private readonly INavigationService      _nav;
        private readonly IDialogService          _dialog;

        public ObservableCollection<SettlementRecord> Settlements { get; } = new();

        private List<SettlementRecord> _allRecords = new();
        private string _searchText = string.Empty;

        // ── Summary totals (shown at top of list page) ────────────────────
        private decimal _totalBillSum;
        private decimal _totalCashSum;
        private decimal _totalCNGSum;
        private decimal _totalDriverHaqSum;

        public decimal TotalBillSum        { get => _totalBillSum;        private set => SetProperty(ref _totalBillSum, value); }
        public decimal TotalCashSum        { get => _totalCashSum;        private set => SetProperty(ref _totalCashSum, value); }
        // BUG-H cascade: renamed from TotalCNGSum — this is OwnerCngShare only, not total fleet CNG
        public decimal TotalOwnerCngSum    { get => _totalCNGSum;         private set => SetProperty(ref _totalCNGSum, value); }
        public decimal TotalDriverHaqSum   { get => _totalDriverHaqSum;   private set => SetProperty(ref _totalDriverHaqSum, value); }

        // ── Search ────────────────────────────────────────────────────────
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                    ApplyFilter();
            }
        }

        public int    SettlementCount      => Settlements.Count;
        public string SettlementCountLabel => $"{SettlementCount} record{(SettlementCount == 1 ? "" : "s")}";

        // ── Commands ──────────────────────────────────────────────────────
        public ICommand AddSettlementCommand    { get; }
        public ICommand ViewDetailCommand       { get; }
        public ICommand EditSettlementCommand   { get; }
        public ICommand DeleteSettlementCommand { get; }
        public ICommand RefreshCommand          { get; }

        public SettlementListViewModel(
            ISettlementRepository   settlementRepo,
            IVehicleRepository      vehicleRepo,
            IDriverRepository       driverRepo,
            IDriverLedgerRepository ledgerRepo,
            IUnitOfWork             uow,
            INavigationService      nav,
            IDialogService          dialog)
        {
            _settlementRepo = settlementRepo;
            _vehicleRepo    = vehicleRepo;
            _driverRepo     = driverRepo;
            _ledgerRepo     = ledgerRepo;
            _uow            = uow;
            _nav            = nav;
            _dialog         = dialog;
            Title           = "Settlement History";

            AddSettlementCommand = new Command(async () =>
                await _nav.GoToAsync(nameof(SettlementEntryPage)));

            ViewDetailCommand = new Command<SettlementRecord>(async rec =>
            {
                if (rec is null) return;
                await _nav.GoToAsync($"{nameof(SettlementDetailPage)}?settlementId={rec.Id}");
            });

            EditSettlementCommand = new Command<SettlementRecord>(async rec =>
            {
                if (rec is null) return;
                await _nav.GoToAsync($"{nameof(SettlementEntryPage)}?editId={rec.Id}");
            });

            DeleteSettlementCommand = new Command<SettlementRecord>(async rec =>
            {
                if (rec is null) return;
                bool ok = await _dialog.ShowConfirmAsync(
                    "Delete Settlement",
                    $"Delete settlement for {rec.DriverName} on {rec.DateDisplay}?\n\nLinked ledger entry will also be removed.",
                    "Delete", "Cancel");
                if (!ok) return;

                // BUG-F fix: guard against double-tap sending two concurrent deletes
                if (IsBusy) return;
                IsBusy = true;
                try
                {
                    var entries = await _ledgerRepo.GetDriverLedgerAsync(rec.Settlement.DriverId);
                    var linked  = entries.FirstOrDefault(e => e.SettlementId == rec.Id);
                    await _uow.DeleteSettlementWithLedgerAsync(rec.Settlement, linked, rec.Settlement.DriverId);

                    _allRecords.Remove(rec);
                    ApplyFilter();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SettlementListViewModel] Delete error: {ex.Message}");
                    await _dialog.ShowAlertAsync("Error", "Failed to delete. Please try again.");
                }
                finally { IsBusy = false; }
            });

            RefreshCommand = new Command(async () => await LoadAsync());
        }

        // ══════════════════════════════════════════════════════════════════
        //  Load
        // ══════════════════════════════════════════════════════════════════

        public async Task LoadAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                var settlements = await _settlementRepo.GetAllSettlementsAsync();
                var vehicles    = await _vehicleRepo.GetAllVehiclesAsync();
                var drivers     = await _driverRepo.GetAllDriversAsync();

                var vDict = vehicles.ToDictionary(v => v.Id, v => v.VehicleNumber);
                var dDict = drivers.ToDictionary(d => d.Id, d => d.DriverName);

                _allRecords = settlements
                    .OrderByDescending(s => s.Date)
                    .Select(s => new SettlementRecord
                    {
                        Settlement    = s,
                        VehicleNumber = vDict.TryGetValue(s.VehicleId, out var vn) ? vn : "—",
                        DriverName    = dDict.TryGetValue(s.DriverId, out var dn) ? dn : "—"
                    }).ToList();

                ApplyFilter(); // totals are computed inside ApplyFilter (BUG-11 fix)

            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[SettlementListViewModel] Load error: {ex.Message}");
            }
            finally { IsBusy = false; }
        }

        // ══════════════════════════════════════════════════════════════════
        //  Filter
        // ══════════════════════════════════════════════════════════════════

        private void ApplyFilter()
        {
            var term = _searchText.Trim().ToLowerInvariant();
            var filtered = string.IsNullOrEmpty(term)
                ? _allRecords
                : _allRecords.Where(r =>
                    r.VehicleNumber.ToLowerInvariant().Contains(term) ||
                    r.DriverName.ToLowerInvariant().Contains(term) ||
                    r.DateDisplay.ToLowerInvariant().Contains(term) ||
                    r.ShiftType.ToLowerInvariant().Contains(term)).ToList();

            Settlements.Clear();
            foreach (var r in filtered) Settlements.Add(r);

            // BUG-11: update summary totals to match the current filtered view
            TotalBillSum      = filtered.Sum(r => r.TotalOperatorBill);
            TotalCashSum      = filtered.Sum(r => r.TotalCashCollected);
            TotalOwnerCngSum  = filtered.Sum(r => r.OwnerCngShare);   // BUG-H cascade: was r.TotalCNG
            TotalDriverHaqSum = filtered.Sum(r => r.DriverNetHaq);

            OnPropertyChanged(nameof(SettlementCount));
            OnPropertyChanged(nameof(SettlementCountLabel));
        }
    }
}
