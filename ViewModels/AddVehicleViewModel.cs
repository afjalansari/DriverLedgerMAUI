using System.Windows.Input;
using DriverLedger.Models;
using DriverLedger.Repositories;
using DriverLedger.Services;

namespace DriverLedger.ViewModels
{
    [QueryProperty(nameof(VehicleId), "vehicleId")]
    public class AddVehicleViewModel : BaseViewModel
    {
        private readonly IVehicleRepository   _vehicleRepo;
        private readonly INavigationService   _nav;
        private readonly IDialogService       _dialog;

        private int _vehicleId;
        private string _vehicleNumber = string.Empty;
        private string _vehicleModel = string.Empty;
        private string _vehicleType = VehicleTypes.Taxi;
        private string _registrationNumber = string.Empty;
        private DateTime _insuranceExpiry = DateTime.Today.AddYears(1);
        private DateTime _pucExpiry = DateTime.Today.AddMonths(6);
        private string _status = VehicleStatus.Active;
        private bool _isEditMode;

        public int VehicleId
        {
            get => _vehicleId;
            set
            {
                SetProperty(ref _vehicleId, value);
                if (value > 0)
                    _ = LoadVehicleAsync(value);
            }
        }

        public string VehicleNumber      { get => _vehicleNumber;      set => SetProperty(ref _vehicleNumber, value); }
        public string VehicleModel       { get => _vehicleModel;       set => SetProperty(ref _vehicleModel, value); }
        public string VehicleType        { get => _vehicleType;        set => SetProperty(ref _vehicleType, value); }
        public string RegistrationNumber { get => _registrationNumber; set => SetProperty(ref _registrationNumber, value); }
        public DateTime InsuranceExpiry  { get => _insuranceExpiry;    set => SetProperty(ref _insuranceExpiry, value); }
        public DateTime PUCExpiry        { get => _pucExpiry;          set => SetProperty(ref _pucExpiry, value); }
        public string Status             { get => _status;             set => SetProperty(ref _status, value); }
        public bool IsEditMode           { get => _isEditMode;         set => SetProperty(ref _isEditMode, value); }

        public List<string> VehicleTypeOptions => VehicleTypes.All;
        public List<string> StatusOptions => VehicleStatus.All;

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        public AddVehicleViewModel(
            IVehicleRepository vehicleRepo,
            INavigationService nav,
            IDialogService     dialog)
        {
            _vehicleRepo  = vehicleRepo;
            _nav          = nav;
            _dialog       = dialog;
            Title         = "Add Vehicle";
            SaveCommand   = new Command(async () => await OnSaveAsync(), () => !IsBusy);
            CancelCommand = new Command(async () => await _nav.GoBackAsync());
        }

        private async Task LoadVehicleAsync(int id)
        {
            IsBusy = true;
            try
            {
                var v = await _vehicleRepo.GetVehicleByIdAsync(id);
                if (v is null) return;
                VehicleNumber      = v.VehicleNumber;
                VehicleModel       = v.VehicleModel;
                VehicleType        = v.VehicleType;
                RegistrationNumber = v.RegistrationNumber;
                InsuranceExpiry    = v.InsuranceExpiryDate.ToLocalTime();
                PUCExpiry          = v.PUCExpiryDate.ToLocalTime();
                Status             = v.Status;
                IsEditMode         = true;
                Title              = "Edit Vehicle";
            }
            finally { IsBusy = false; }
        }

        private async Task OnSaveAsync()
        {
            if (IsBusy) return;

            if (string.IsNullOrWhiteSpace(VehicleNumber))
            {
                await _dialog.ShowAlertAsync("Validation", "Vehicle Number is required.");
                return;
            }

            IsBusy = true;
            try
            {
                var vehicle = new Vehicle
                {
                    Id                   = _vehicleId,
                    VehicleNumber        = VehicleNumber.Trim().ToUpper(),
                    VehicleModel         = VehicleModel.Trim(),
                    VehicleType          = VehicleType,
                    RegistrationNumber   = RegistrationNumber.Trim(),
                    InsuranceExpiryDate  = InsuranceExpiry.ToUniversalTime(),
                    PUCExpiryDate        = PUCExpiry.ToUniversalTime(),
                    Status               = Status
                };
                await _vehicleRepo.SaveVehicleAsync(vehicle);
                var msg = IsEditMode ? "Vehicle updated successfully." : "Vehicle added successfully.";
                await _dialog.ShowAlertAsync("✅ Saved", msg);
                await _nav.GoBackAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AddVehicleViewModel] Save error: {ex.Message}");
                await _dialog.ShowAlertAsync("Error", "Failed to save vehicle. Please try again.");
            }
            finally { IsBusy = false; }
        }
    }
}

