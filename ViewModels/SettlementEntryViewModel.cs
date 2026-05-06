using System.Collections.ObjectModel;
using System.Windows.Input;
using DriverLedger.Models;
using DriverLedger.Repositories;
using DriverLedger.Services;

namespace DriverLedger.ViewModels
{
    // Edit mode: navigate with ?editId=X to pre-populate all fields from an existing settlement
    [QueryProperty(nameof(EditSettlementId), "editId")]
    public class SettlementEntryViewModel : BaseViewModel
    {
        // ── Dependencies ──────────────────────────────────────────────────
        private readonly IUnitOfWork             _uow;
        private readonly IVehicleRepository      _vehicleRepo;
        private readonly IDriverRepository       _driverRepo;
        private readonly IVehicleDriverRepository _vdRepo;
        private readonly ISettlementRepository   _settlementRepo;
        private readonly IDriverLedgerRepository _ledgerRepo;
        private readonly SettlementCalculator    _calculator;
        private readonly INavigationService      _nav;
        private readonly IDialogService          _dialog;

        // ── Edit-mode state ───────────────────────────────────────────────
        private int        _editSettlementId;      // 0 = new, >0 = editing existing
        private Settlement? _editingSettlement;     // original record for UPDATE
        private bool       _isLoadingForEdit;       // BUG-G: suppresses LookupDriverAsync during edit pre-population

        // ── Basic info backing fields ─────────────────────────────────────
        private DateTime _date = DateTime.Today;
        private Vehicle? _selectedVehicle;
        private string _selectedShift = ShiftTypes.Day;
        private Driver? _assignedDriver;
        private string _assignedDriverName = "— select vehicle & shift —";

        // ── Contract % (loaded from driver record) ────────────────────────
        private decimal _driverIncomePercent = 50m;
        private decimal _ownerIncomePercent = 50m;
        private decimal _driverCngPercent = 50m;
        private decimal _ownerCngPercent = 50m;

        // ── Raw expense decimal values (source of truth) ──────────────────
        private decimal _totalCng;
        private decimal _parking;
        private decimal _toll;
        private decimal _repair;
        private decimal _miscellaneous;

        // ── String proxies for Entry bindings ─────────────────────────────
        private string _cngText = string.Empty;
        private string _driverChallanText = string.Empty;
        private string _ownerChallanText = string.Empty;
        private string _parkingText = string.Empty;
        private string _tollText = string.Empty;
        private string _repairText = string.Empty;
        private string _miscText = string.Empty;

        // ── Calculated result backing fields ──────────────────────────────
        private decimal _totalOperatorBill;
        private decimal _totalCashCollected;
        private decimal _netDriverPayable;
        private string _settlementLabel = "—";

        // ── UI state ──────────────────────────────────────────────────────
        private bool   _isExpensesExpanded = false;
        private const  string DefaultSplit  = "50% / 50%";
        private string _contractSummary  = DefaultSplit;
        private string _fuelSplitSummary = DefaultSplit;

        // ── Collections ───────────────────────────────────────────────────
        public ObservableCollection<Vehicle> Vehicles { get; } = new();
        public ObservableCollection<AggregatorRowViewModel> OperatorRows { get; } = new();
        public static List<string> ShiftOptions => ShiftTypes.All;

        public int EditSettlementId
        {
            get => _editSettlementId;
            set
            {
                _editSettlementId = value;
                if (value > 0) _ = LoadForEditAsync(value);
            }
        }

        public bool IsEditMode => _editSettlementId > 0;

        public DateTime Date { get => _date; set => SetProperty(ref _date, value); }
        public Vehicle? SelectedVehicle { get => _selectedVehicle; set { SetProperty(ref _selectedVehicle, value); if (!_isLoadingForEdit) _ = LookupDriverAsync(); } }
        public string SelectedShift { get => _selectedShift; set { SetProperty(ref _selectedShift, value); if (!_isLoadingForEdit) _ = LookupDriverAsync(); } }
        public string AssignedDriverName { get => _assignedDriverName; set => SetProperty(ref _assignedDriverName, value); }

        public decimal DriverIncomePercent => _driverIncomePercent;
        public decimal OwnerIncomePercent => _ownerIncomePercent;
        public decimal DriverCngPercent => _driverCngPercent;
        public decimal OwnerCngPercent => _ownerCngPercent;

        public string ContractSummary { get => _contractSummary; private set => SetProperty(ref _contractSummary, value); }
        public string FuelSplitSummary { get => _fuelSplitSummary; private set => SetProperty(ref _fuelSplitSummary, value); }

        public string CngText { get => _cngText; set { SetProperty(ref _cngText, value); _totalCng = Parse(value); Recalculate(); } }
        public string DriverChallanText { get => _driverChallanText; set { SetProperty(ref _driverChallanText, value); Recalculate(); } }
        public string OwnerChallanText  { get => _ownerChallanText;  set { SetProperty(ref _ownerChallanText,  value); Recalculate(); } }
        public string ParkingText { get => _parkingText; set { SetProperty(ref _parkingText, value); _parking = Parse(value); Recalculate(); } }
        public string TollText { get => _tollText; set { SetProperty(ref _tollText, value); _toll = Parse(value); Recalculate(); } }
        public string RepairText { get => _repairText; set { SetProperty(ref _repairText, value); _repair = Parse(value); Recalculate(); } }
        public string MiscText { get => _miscText; set { SetProperty(ref _miscText, value); _miscellaneous = Parse(value); Recalculate(); } }

        public decimal TotalOperatorBill { get => _totalOperatorBill; private set => SetProperty(ref _totalOperatorBill, value); }
        public decimal TotalCashCollected { get => _totalCashCollected; private set => SetProperty(ref _totalCashCollected, value); }
        public decimal NetDriverPayable { get => _netDriverPayable; private set => SetProperty(ref _netDriverPayable, value); }
        public string SettlementLabel { get => _settlementLabel; private set => SetProperty(ref _settlementLabel, value); }

        public bool IsExpensesExpanded { get => _isExpensesExpanded; set => SetProperty(ref _isExpensesExpanded, value); }

        public ICommand AddOperatorRowCommand { get; }
        public ICommand ToggleExpensesCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        /// <summary>
        /// Groups the data-layer dependencies to keep the constructor under the 7-param limit.
        /// </summary>
        public sealed class Dependencies
        {
            public IUnitOfWork              Uow            { get; init; } = null!;
            public ISettlementRepository    SettlementRepo  { get; init; } = null!;
            public IDriverLedgerRepository  LedgerRepo      { get; init; } = null!;
            public IVehicleRepository       VehicleRepo     { get; init; } = null!;
            public IDriverRepository        DriverRepo      { get; init; } = null!;
            public IVehicleDriverRepository VdRepo          { get; init; } = null!;
            public SettlementCalculator     Calculator      { get; init; } = null!;
        }

        public SettlementEntryViewModel(
            Dependencies        deps,
            INavigationService  nav,
            IDialogService      dialog)
        {
            _uow            = deps.Uow;
            _settlementRepo = deps.SettlementRepo;
            _ledgerRepo     = deps.LedgerRepo;
            _vehicleRepo    = deps.VehicleRepo;
            _driverRepo     = deps.DriverRepo;
            _vdRepo         = deps.VdRepo;
            _calculator     = deps.Calculator;
            _nav            = nav;
            _dialog         = dialog;
            Title = "New Settlement";

            AddRow("Ola");

            AddOperatorRowCommand = new Command(OnAddRow);
            ToggleExpensesCommand = new Command(() => IsExpensesExpanded = !IsExpensesExpanded);
            SaveCommand           = new Command(async () => await OnSaveAsync(), () => !IsBusy);
            CancelCommand         = new Command(async () => await _nav.GoBackAsync());
        }

        private void OnAddRow()
        {
            var used = OperatorRows.Select(r => r.OperatorName).ToHashSet();
            var next = AggregatorRowViewModel.PresetOperators.FirstOrDefault(p => !used.Contains(p)) ?? "Other";
            AddRow(next);
        }

        private void AddRow(string operatorName)
        {
            var row = new AggregatorRowViewModel(Recalculate, RemoveRow, operatorName);
            OperatorRows.Add(row);
        }

        private void RemoveRow(AggregatorRowViewModel row)
        {
            if (OperatorRows.Count <= 1)
            {
                row.BillText = string.Empty;
                row.CashText = string.Empty;
                return;
            }
            OperatorRows.Remove(row);
            Recalculate();
        }

        public async Task LoadVehiclesAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                var list = await _vehicleRepo.GetActiveVehiclesAsync();
                Vehicles.Clear();
                foreach (var v in list) Vehicles.Add(v);
            }
            finally { IsBusy = false; }
        }

        private async Task LoadForEditAsync(int id)
        {
            if (IsBusy) return;
            IsBusy = true;
            _isLoadingForEdit = true;
            try
            {
                // BUG-FIX: don't call LoadVehiclesAsync() while IsBusy=true —
                // that method also toggles IsBusy and causes a false IsBusy=false flash.
                if (!Vehicles.Any())
                {
                    var list = await _vehicleRepo.GetActiveVehiclesAsync();
                    Vehicles.Clear();
                    foreach (var v in list) Vehicles.Add(v);
                }

                var s = await _settlementRepo.GetSettlementByIdAsync(id);
                if (s is null)
                {
                    await _dialog.ShowAlertAsync("Error", "Settlement not found.");
                    return;
                }

                _editingSettlement = s;
                Title = "Edit Settlement";
                OnPropertyChanged(nameof(IsEditMode));

                Date          = s.Date.ToLocalTime();
                SelectedShift = s.ShiftType;

                var vehicle = Vehicles.FirstOrDefault(v => v.Id == s.VehicleId);
                if (vehicle is not null) { _selectedVehicle = vehicle; OnPropertyChanged(nameof(SelectedVehicle)); }

                _assignedDriver    = await _driverRepo.GetDriverByIdAsync(s.DriverId);
                AssignedDriverName = _assignedDriver?.DriverName ?? "—";

                // Derive income % from stored DriverShare / TotalIncome
                _driverIncomePercent = s.TotalIncome > 0
                    ? Math.Round(s.DriverShare / s.TotalIncome * 100, 0) : 50m;
                _ownerIncomePercent = 100m - _driverIncomePercent;
                _driverCngPercent   = 50m;
                _ownerCngPercent    = 50m;
                ContractSummary  = $"{_driverIncomePercent:0}% / {_ownerIncomePercent:0}%";
                FuelSplitSummary = $"{_driverCngPercent:0}% / {_ownerCngPercent:0}%";
                OnPropertyChanged(nameof(DriverIncomePercent));
                OnPropertyChanged(nameof(OwnerIncomePercent));
                OnPropertyChanged(nameof(DriverCngPercent));
                OnPropertyChanged(nameof(OwnerCngPercent));

                PopulateOperatorRows(s);
                PopulateExpenseFields(s);

                // Recalculate() called once in finally after _isLoadingForEdit is cleared (BUG-16 fix).
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettlementEntryViewModel] LoadForEditAsync error: {ex.Message}");
                await _dialog.ShowAlertAsync("Error", "Could not load settlement.");
            }
            finally { _isLoadingForEdit = false; Recalculate(); IsBusy = false; }
        }

        private void PopulateOperatorRows(Settlement s)
        {
            OperatorRows.Clear();
            if (s.PlatformIncomes != null && s.PlatformIncomes.Any())
            {
                foreach (var pi in s.PlatformIncomes)
                {
                    var row = new AggregatorRowViewModel(Recalculate, RemoveRow, pi.PlatformName);
                    row.BillText = pi.OperatorBill.ToString("G");
                    row.CashText = pi.CashCollected.ToString("G");
                    OperatorRows.Add(row);
                }
            }
            else { AddRow("Ola"); }
        }

        private void PopulateExpenseFields(Settlement s)
        {
            if (s.ExpenseItems is null) return;
            foreach (var exp in s.ExpenseItems)
            {
                switch (exp.Type)
                {
                    case ExpenseType.CNG:     CngText     = exp.Amount.ToString("G"); break;
                    case ExpenseType.Toll:    TollText    = exp.Amount.ToString("G"); break;
                    case ExpenseType.Parking: ParkingText = exp.Amount.ToString("G"); break;
                    case ExpenseType.Other:
                        // BUG-7 fix: distinguish Repair vs Misc by the stored Name field.
                        if (exp.Name == "Repair") RepairText = exp.Amount.ToString("G");
                        else                      MiscText   = exp.Amount.ToString("G");
                        break;
                }
            }
            IsExpensesExpanded = s.ExpenseItems.Any();
        }

        private async Task LookupDriverAsync()
        {
            _assignedDriver = null;
            if (_selectedVehicle is null) { AssignedDriverName = "— select vehicle & shift —"; ResetContractToDefault(); return; }

            try
            {
                var assignment = await _vdRepo.GetAssignmentAsync(_selectedVehicle.Id, _selectedShift);
                if (assignment is null) { AssignedDriverName = "No driver assigned"; ResetContractToDefault(); return; }

                _assignedDriver = await _driverRepo.GetDriverByIdAsync(assignment.DriverId);
                if (_assignedDriver is null) { AssignedDriverName = "Driver not found"; ResetContractToDefault(); return; }

                AssignedDriverName = _assignedDriver.DriverName;
                var dip = _assignedDriver.DriverIncomePercent > 0 ? _assignedDriver.DriverIncomePercent : 50m;
                var dcp = _assignedDriver.DriverCngPercent > 0 ? _assignedDriver.DriverCngPercent : 50m;

                _driverIncomePercent = dip;
                _ownerIncomePercent = 100m - dip;
                _driverCngPercent = dcp;
                _ownerCngPercent = 100m - dcp;

                ContractSummary = $"{_driverIncomePercent:0}% / {_ownerIncomePercent:0}%";
                FuelSplitSummary = $"{_driverCngPercent:0}% / {_ownerCngPercent:0}%";
                OnPropertyChanged(nameof(DriverIncomePercent));
                OnPropertyChanged(nameof(OwnerIncomePercent));
                OnPropertyChanged(nameof(DriverCngPercent));
                OnPropertyChanged(nameof(OwnerCngPercent));
                Recalculate();
            }
            catch { AssignedDriverName = "Error loading driver"; ResetContractToDefault(); }
        }

        private void ResetContractToDefault()
        {
            _driverIncomePercent = 50m; _ownerIncomePercent = 50m;
            _driverCngPercent = 50m; _ownerCngPercent = 50m;
            ContractSummary  = DefaultSplit;
            FuelSplitSummary = DefaultSplit;
            OnPropertyChanged(nameof(DriverIncomePercent)); OnPropertyChanged(nameof(OwnerIncomePercent));
            OnPropertyChanged(nameof(DriverCngPercent)); OnPropertyChanged(nameof(OwnerCngPercent));
            Recalculate();
        }

        public void Recalculate()
        {
            // BUG-16 fix: suppress intermediate recalculations while LoadForEditAsync
            // is setting expense fields one-by-one. A single call is made in the finally block.
            if (_isLoadingForEdit) return;

            if (_selectedVehicle == null || _assignedDriver == null) return;
            var request = BuildCalculationRequest();
            try
            {
                var s = _calculator.Calculate(request);
                NetDriverPayable = s.NetDriverPayable;
                TotalOperatorBill = s.TotalIncome;
                TotalCashCollected = s.TotalCashCollected;

                if (NetDriverPayable > 0)
                    SettlementLabel = $"₹{NetDriverPayable:N0} (Driver ko milega)";
                else if (NetDriverPayable < 0)
                    SettlementLabel = $"₹{Math.Abs(NetDriverPayable):N0} (Driver ko dena hai)";
                else
                    SettlementLabel = "✅ Hisaab Barabar";
            }
            catch { SettlementLabel = "⚠️ Calculation Error"; }

            OnPropertyChanged(nameof(NetDriverPayable));
            OnPropertyChanged(nameof(TotalOperatorBill));
            OnPropertyChanged(nameof(TotalCashCollected));
        }

        private SettlementCalculator.CalculationRequest BuildCalculationRequest()
        {
            var request = new SettlementCalculator.CalculationRequest
            {
                Date = Date, Driver = _assignedDriver!, Vehicle = _selectedVehicle!, ShiftType = SelectedShift,
                DriverIncomePercent = _driverIncomePercent, DriverCngPercent = _driverCngPercent,
                Incomes = OperatorRows.Select(r => new PlatformIncome { PlatformName = r.OperatorName, OperatorBill = r.Bill, CashCollected = r.Cash }).ToList(),
                Expenses = new List<SettlementExpense>()
            };
            if (_totalCng > 0) request.Expenses.Add(new SettlementExpense { Type = ExpenseType.CNG, Amount = _totalCng });
            if (_toll > 0) request.Expenses.Add(new SettlementExpense { Type = ExpenseType.Toll, Amount = _toll });
            if (_parking > 0) request.Expenses.Add(new SettlementExpense { Type = ExpenseType.Parking, Amount = _parking });
            if (_repair > 0) request.Expenses.Add(new SettlementExpense { Type = ExpenseType.Other, Amount = _repair, Name = "Repair" });
            if (_miscellaneous > 0) request.Expenses.Add(new SettlementExpense { Type = ExpenseType.Other, Amount = _miscellaneous, Name = "Misc" });
            return request;
        }

        private async Task OnSaveAsync()
        {
            if (!await ValidateSaveAsync()) return;
            if (IsBusy) return;

            IsBusy = true;
            if (SaveCommand is Command sc) sc.ChangeCanExecute();
            try
            {
                var request = BuildCalculationRequest();
                var s       = _calculator.Calculate(request);

                if (IsEditMode)
                {
                    s.Id        = _editingSettlement!.Id;
                    // BUG-FIX: preserve original CreatedAt for audit immutability
                    s.CreatedAt = _editingSettlement.CreatedAt;
                    await SaveEditAsync(s);
                }
                else
                {
                    await SaveNewAsync(s);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettlementEntryViewModel] Save error: {ex.Message}");
                await _dialog.ShowAlertAsync("Error", ex.Message);
            }
            finally
            {
                IsBusy = false;
                if (SaveCommand is Command sc2) sc2.ChangeCanExecute();
            }
        }

        private async Task<bool> ValidateSaveAsync()
        {
            if (_selectedVehicle is null) { await _dialog.ShowAlertAsync("Validation", "Select vehicle.");       return false; }
            if (_assignedDriver  is null) { await _dialog.ShowAlertAsync("Validation", "No driver assigned.");   return false; }
            if (TotalOperatorBill <= 0)   { await _dialog.ShowAlertAsync("Validation", "Enter operator bill.");  return false; }
            return true;
        }

        private static string BuildResultMsg(Settlement s)
        {
            if (s.NetDriverPayable > 0)  return $"₹{s.NetDriverPayable:N0} (Driver ko milega)";
            if (s.NetDriverPayable < 0)  return $"₹{Math.Abs(s.NetDriverPayable):N0} (Driver ko dena hai)";
            return "Hisaab Barabar";
        }

        private async Task SaveNewAsync(Settlement s)
        {
            var existing = await _settlementRepo.GetSettlementsByDateAsync(Date);
            if (existing.Any(e => e.DriverId == _assignedDriver!.Id && e.VehicleId == _selectedVehicle!.Id))
            {
                await _dialog.ShowAlertAsync("Duplicate", "Settlement already exists for this Driver/Vehicle on this date.");
                return;
            }

            var ledgerEntry = new DriverLedgerEntry
            {
                DriverId        = s.DriverId,
                Date            = s.Date,
                VehicleId       = s.VehicleId,
                ShiftType       = s.ShiftType,
                TransactionType = TransactionTypes.Settlement,
                Description     = $"Settlement - {s.ShiftType} | Income ₹{s.TotalIncome:N0}",
                Debit           = s.NetDriverPayable > 0 ? s.NetDriverPayable        : 0m,
                Credit          = s.NetDriverPayable < 0 ? Math.Abs(s.NetDriverPayable) : 0m
            };
            await _uow.SaveSettlementWithLedgerAsync(s, ledgerEntry);
            await _dialog.ShowAlertAsync("✅ Saved",
                $"Income: ₹{s.TotalIncome:N0}\nShare: ₹{s.DriverShare:N0}\nCash: ₹{s.TotalCashCollected:N0}\n\n{BuildResultMsg(s)}");
            await _nav.GoBackAsync();
        }

        private async Task SaveEditAsync(Settlement s)
        {
            // BUG-3 fix: check for duplicate on the (potentially changed) date,
            // excluding the record being edited.
            var sameDay = await _settlementRepo.GetSettlementsByDateAsync(Date);
            if (sameDay.Any(e => e.DriverId == _assignedDriver!.Id
                              && e.VehicleId == _selectedVehicle!.Id
                              && e.Id        != s.Id))
            {
                await _dialog.ShowAlertAsync("Duplicate", "Settlement already exists for this Driver/Vehicle on this date.");
                return;
            }

            var entries = await _ledgerRepo.GetDriverLedgerAsync(_assignedDriver!.Id);
            var linked  = entries.FirstOrDefault(e => e.SettlementId == s.Id);
            if (linked is not null)
            {
                linked.Debit       = s.NetDriverPayable > 0 ? s.NetDriverPayable        : 0m;
                linked.Credit      = s.NetDriverPayable < 0 ? Math.Abs(s.NetDriverPayable) : 0m;
                linked.Date        = s.Date;
                linked.Description = $"Settlement - {s.ShiftType} | Income ₹{s.TotalIncome:N0}";
                await _uow.UpdateSettlementWithLedgerAsync(s, linked, _assignedDriver!.Id);
            }
            else
            {
                await _settlementRepo.SaveSettlementAsync(s);
            }

            await _dialog.ShowAlertAsync("✅ Updated", BuildResultMsg(s));
            await _nav.GoBackAsync();
        }

        private static decimal Parse(string? text) => decimal.TryParse(text, out var d) ? Math.Max(0m, d) : 0m;
    }
}