using System.Collections.ObjectModel;
using System.Windows.Input;
using DriverLedger.Models;
using DriverLedger.Repositories;
using DriverLedger.Services;

namespace DriverLedger.ViewModels
{
    [QueryProperty(nameof(VehicleId), "vehicleId")]
    public class AssignDriverViewModel : BaseViewModel
    {
        private readonly IVehicleRepository       _vehicleRepo;
        private readonly IDriverRepository        _driverRepo;
        private readonly IVehicleDriverRepository _vdRepo;
        private readonly INavigationService       _nav;
        private readonly IDialogService           _dialog;

        private int _vehicleId;
        private Vehicle? _selectedVehicle;
        private Driver? _selectedDriver;
        private string _selectedShift = ShiftTypes.Day;

        public int VehicleId
        {
            get => _vehicleId;
            set
            {
                SetProperty(ref _vehicleId, value);
                if (value > 0)
                    _ = PreSelectVehicleAsync(value);
            }
        }

        public ObservableCollection<Vehicle> Vehicles { get; } = new();
        public ObservableCollection<Driver> Drivers { get; } = new();
        public List<string> ShiftOptions => ShiftTypes.All;

        public Vehicle? SelectedVehicle
        {
            get => _selectedVehicle;
            set => SetProperty(ref _selectedVehicle, value);
        }

        public Driver? SelectedDriver
        {
            get => _selectedDriver;
            set => SetProperty(ref _selectedDriver, value);
        }

        public string SelectedShift
        {
            get => _selectedShift;
            set => SetProperty(ref _selectedShift, value);
        }

        public ICommand AssignCommand { get; }
        public ICommand CancelCommand { get; }

        public AssignDriverViewModel(
            IVehicleRepository       vehicleRepo,
            IDriverRepository        driverRepo,
            IVehicleDriverRepository vdRepo,
            INavigationService       nav,
            IDialogService           dialog)
        {
            _vehicleRepo  = vehicleRepo;
            _driverRepo   = driverRepo;
            _vdRepo       = vdRepo;
            _nav          = nav;
            _dialog       = dialog;
            Title         = "Assign Driver";
            AssignCommand = new Command(async () => await OnAssignAsync(), () => !IsBusy);
            CancelCommand = new Command(async () => await _nav.GoBackAsync());
        }

        public async Task LoadDataAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                var vehicles = await _vehicleRepo.GetActiveVehiclesAsync();
                var drivers  = await _driverRepo.GetActiveDriversAsync();

                Vehicles.Clear();
                foreach (var v in vehicles) Vehicles.Add(v);

                Drivers.Clear();
                foreach (var d in drivers) Drivers.Add(d);

                // Re-apply pre-selected vehicle after data loads
                if (_vehicleId > 0)
                    SelectedVehicle = Vehicles.FirstOrDefault(v => v.Id == _vehicleId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AssignDriverViewModel] Load error: {ex.Message}");
            }
            finally { IsBusy = false; }
        }

        private async Task PreSelectVehicleAsync(int vehicleId)
        {
            // Called when vehicleId query param arrives — will re-apply in LoadDataAsync if vehicles not loaded yet
            SelectedVehicle = Vehicles.FirstOrDefault(v => v.Id == vehicleId);
            if (SelectedVehicle is null)
            {
                var v = await _vehicleRepo.GetVehicleByIdAsync(vehicleId);
                if (v != null) SelectedVehicle = v;
            }
        }

        private async Task OnAssignAsync()
        {
            if (IsBusy) return;

            if (SelectedVehicle is null)
            {
                await _dialog.ShowAlertAsync("Validation", "Please select a vehicle.");
                return;
            }
            if (SelectedDriver is null)
            {
                await _dialog.ShowAlertAsync("Validation", "Please select a driver.");
                return;
            }

            IsBusy = true;
            try
            {
                var assignment = new VehicleDriver
                {
                    VehicleId    = SelectedVehicle.Id,
                    DriverId     = SelectedDriver.Id,
                    ShiftType    = SelectedShift,
                    AssignedDate = DateTime.UtcNow
                };

                await _vdRepo.SaveAssignmentAsync(assignment);

                await _dialog.ShowAlertAsync(
                    "Assigned",
                    $"{SelectedDriver.DriverName} assigned to {SelectedVehicle.VehicleNumber} — {SelectedShift} Shift.");

                await _nav.GoBackAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AssignDriverViewModel] Assign error: {ex.Message}");
                await _dialog.ShowAlertAsync("Error", "Failed to assign driver. Please try again.");
            }
            finally { IsBusy = false; }
        }
    }
}

