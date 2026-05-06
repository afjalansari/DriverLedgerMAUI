using System.Windows.Input;
using DriverLedger.Models;
using DriverLedger.Repositories;
using DriverLedger.Services;

namespace DriverLedger.ViewModels
{
    [QueryProperty(nameof(DriverId), "driverId")]
    public class AddDriverViewModel : BaseViewModel
    {
        private readonly IDriverRepository    _driverRepo;
        private readonly INavigationService   _nav;
        private readonly IDialogService       _dialog;

        private int      _driverId;
        private string   _driverName    = string.Empty;
        private string   _mobileNumber  = string.Empty;
        private string   _licenseNumber = string.Empty;
        private string   _address       = string.Empty;
        private DateTime _joinDate      = DateTime.Today;
        private string   _status        = DriverStatus.Active;
        private bool     _isEditMode;

        // ── Contract % Fields ────────────────────────────────────────────
        private decimal _driverIncomePercent = 50m;
        private decimal _ownerIncomePercent  = 50m;
        private decimal _driverCngPercent    = 50m;
        private decimal _ownerCngPercent     = 50m;

        public int DriverId
        {
            get => _driverId;
            set { SetProperty(ref _driverId, value); if (value > 0) _ = LoadDriverAsync(value); }
        }

        public string   DriverName    { get => _driverName;    set => SetProperty(ref _driverName, value); }
        public string   MobileNumber  { get => _mobileNumber;  set => SetProperty(ref _mobileNumber, value); }
        public string   LicenseNumber { get => _licenseNumber; set => SetProperty(ref _licenseNumber, value); }
        public string   Address       { get => _address;       set => SetProperty(ref _address, value); }
        public DateTime JoinDate      { get => _joinDate;      set => SetProperty(ref _joinDate, value); }
        public string   Status        { get => _status;        set => SetProperty(ref _status, value); }
        public bool     IsEditMode    { get => _isEditMode;    set => SetProperty(ref _isEditMode, value); }

        public decimal DriverIncomePercent
        {
            get => _driverIncomePercent;
            set { SetProperty(ref _driverIncomePercent, value); OwnerIncomePercent = 100m - value; }
        }

        public decimal OwnerIncomePercent
        {
            get => _ownerIncomePercent;
            set => SetProperty(ref _ownerIncomePercent, value);
        }

        public decimal DriverCngPercent
        {
            get => _driverCngPercent;
            set { SetProperty(ref _driverCngPercent, value); OwnerCngPercent = 100m - value; }
        }

        public decimal OwnerCngPercent
        {
            get => _ownerCngPercent;
            set => SetProperty(ref _ownerCngPercent, value);
        }

        public List<string> StatusOptions => DriverStatus.All;

        public ICommand SaveCommand   { get; }
        public ICommand CancelCommand { get; }

        public AddDriverViewModel(
            IDriverRepository  driverRepo,
            INavigationService nav,
            IDialogService     dialog)
        {
            _driverRepo   = driverRepo;
            _nav          = nav;
            _dialog       = dialog;
            Title         = "Add Driver";
            SaveCommand   = new Command(async () => await OnSaveAsync(), () => !IsBusy);
            CancelCommand = new Command(async () => await _nav.GoBackAsync());
        }

        private async Task LoadDriverAsync(int id)
        {
            IsBusy = true;
            try
            {
                var driver = await _driverRepo.GetDriverByIdAsync(id);
                if (driver is null) return;
                DriverName          = driver.DriverName;
                MobileNumber        = driver.MobileNumber;
                LicenseNumber       = driver.LicenseNumber;
                Address             = driver.Address;
                JoinDate            = driver.JoinDate.ToLocalTime();
                Status              = driver.Status;
                // Load contract % (if stored as 0, revert to 50/50 default)
                DriverIncomePercent = driver.DriverIncomePercent > 0 ? driver.DriverIncomePercent : 50m;
                DriverCngPercent    = driver.DriverCngPercent    > 0 ? driver.DriverCngPercent    : 50m;
                IsEditMode          = true;
                Title               = "Edit Driver";
            }
            finally { IsBusy = false; }
        }

        private async Task OnSaveAsync()
        {
            if (IsBusy) return;

            if (string.IsNullOrWhiteSpace(DriverName))
            {
                await _dialog.ShowAlertAsync("Validation", "Driver Name is required.");
                return;
            }
            if (string.IsNullOrWhiteSpace(MobileNumber))
            {
                await _dialog.ShowAlertAsync("Validation", "Mobile Number is required.");
                return;
            }
            var mobile = MobileNumber.Trim();
            if (!mobile.All(char.IsDigit) || mobile.Length < 10)
            {
                await _dialog.ShowAlertAsync("Validation",
                    "Mobile Number must be at least 10 digits (numbers only).");
                return;
            }

            // Validate contract % pairs
            var incomePairOk = Math.Round(DriverIncomePercent + OwnerIncomePercent, 1) == 100m;
            var cngPairOk    = Math.Round(DriverCngPercent    + OwnerCngPercent,    1) == 100m;
            if (!incomePairOk || !cngPairOk)
            {
                await _dialog.ShowAlertAsync("Validation",
                    "Income % and CNG % must each add up to 100.");
                return;
            }

            IsBusy = true;
            try
            {
                var driver = new Driver
                {
                    Id                  = _driverId,
                    DriverName          = DriverName.Trim(),
                    MobileNumber        = mobile,
                    LicenseNumber       = LicenseNumber.Trim(),
                    Address             = Address.Trim(),
                    JoinDate            = JoinDate.ToUniversalTime(),
                    Status              = Status,
                    DriverIncomePercent = DriverIncomePercent,
                    OwnerIncomePercent  = OwnerIncomePercent,
                    DriverCngPercent    = DriverCngPercent,
                    OwnerCngPercent     = OwnerCngPercent
                };
                await _driverRepo.SaveDriverAsync(driver);
                var msg = IsEditMode ? "Driver updated successfully." : "Driver added successfully.";
                await _dialog.ShowAlertAsync("✅ Saved", msg);
                await _nav.GoBackAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AddDriverViewModel] Save error: {ex.Message}");
                await _dialog.ShowAlertAsync("Error", "Failed to save driver. Please try again.");
            }
            finally { IsBusy = false; }
        }
    }
}

