using System.Text.RegularExpressions;
using System.Windows.Input;
using DriverLedger.Models;
using DriverLedger.Repositories;
using DriverLedger.Services;

namespace DriverLedger.ViewModels
{
    public class CompanyCreationViewModel : BaseViewModel
    {
        private readonly ICompanyRepository  _companyRepo;
        private readonly AuthService         _authService;
        private readonly INavigationService  _nav;
        private readonly IDialogService      _dialog;

        // ── Focus-advance callbacks (wired in code-behind) ──────────────────
        public Action? RequestFocusOwnerName  { get; set; }
        public Action? RequestFocusMobile     { get; set; }
        public Action? RequestFocusCity       { get; set; }
        public Action? RequestFocusPassword   { get; set; }
        public Action? RequestFocusConfirmPass{ get; set; }

        // ── Form fields ──────────────────────────────────────────────────────

        private string _companyName = string.Empty;
        public string CompanyName
        {
            get => _companyName;
            set { SetProperty(ref _companyName, value); CompanyNameError = string.Empty; }
        }

        private string _ownerName = string.Empty;
        public string OwnerName
        {
            get => _ownerName;
            set { SetProperty(ref _ownerName, value); OwnerNameError = string.Empty; }
        }

        private string _mobileNumber = string.Empty;
        public string MobileNumber
        {
            get => _mobileNumber;
            set { SetProperty(ref _mobileNumber, value); MobileNumberError = string.Empty; }
        }

        private string _city = string.Empty;
        public string City
        {
            get => _city;
            set { SetProperty(ref _city, value); CityError = string.Empty; }
        }

        private string _password = string.Empty;
        public string Password
        {
            get => _password;
            set { SetProperty(ref _password, value); PasswordError = string.Empty; }
        }

        private string _confirmPassword = string.Empty;
        public string ConfirmPassword
        {
            get => _confirmPassword;
            set { SetProperty(ref _confirmPassword, value); ConfirmPasswordError = string.Empty; }
        }

        // ── Inline error properties ──────────────────────────────────────────

        private string _companyNameError = string.Empty;
        public string CompanyNameError
        {
            get => _companyNameError;
            set { SetProperty(ref _companyNameError, value); OnPropertyChanged(nameof(HasCompanyNameError)); }
        }
        public bool HasCompanyNameError => !string.IsNullOrEmpty(CompanyNameError);

        private string _ownerNameError = string.Empty;
        public string OwnerNameError
        {
            get => _ownerNameError;
            set { SetProperty(ref _ownerNameError, value); OnPropertyChanged(nameof(HasOwnerNameError)); }
        }
        public bool HasOwnerNameError => !string.IsNullOrEmpty(OwnerNameError);

        private string _mobileNumberError = string.Empty;
        public string MobileNumberError
        {
            get => _mobileNumberError;
            set { SetProperty(ref _mobileNumberError, value); OnPropertyChanged(nameof(HasMobileNumberError)); }
        }
        public bool HasMobileNumberError => !string.IsNullOrEmpty(MobileNumberError);

        private string _cityError = string.Empty;
        public string CityError
        {
            get => _cityError;
            set { SetProperty(ref _cityError, value); OnPropertyChanged(nameof(HasCityError)); }
        }
        public bool HasCityError => !string.IsNullOrEmpty(CityError);

        private string _passwordError = string.Empty;
        public string PasswordError
        {
            get => _passwordError;
            set { SetProperty(ref _passwordError, value); OnPropertyChanged(nameof(HasPasswordError)); }
        }
        public bool HasPasswordError => !string.IsNullOrEmpty(PasswordError);

        private string _confirmPasswordError = string.Empty;
        public string ConfirmPasswordError
        {
            get => _confirmPasswordError;
            set { SetProperty(ref _confirmPasswordError, value); OnPropertyChanged(nameof(HasConfirmPasswordError)); }
        }
        public bool HasConfirmPasswordError => !string.IsNullOrEmpty(ConfirmPasswordError);

        // ── Commands ──────────────────────────────────────────────────────────

        public ICommand CreateCommand           { get; }
        public ICommand GoToLoginCommand        { get; }
        public ICommand FocusOwnerNameCommand   { get; }
        public ICommand FocusMobileCommand      { get; }
        public ICommand FocusCityCommand        { get; }
        public ICommand FocusPasswordCommand    { get; }
        public ICommand FocusConfirmPasswordCommand { get; }

        public CompanyCreationViewModel(
            ICompanyRepository companyRepo,
            AuthService        authService,
            INavigationService nav,
            IDialogService     dialog)
        {
            _companyRepo = companyRepo;
            _authService = authService;
            _nav         = nav;
            _dialog      = dialog;
            Title        = "Create Company";

            CreateCommand    = new Command(async () => await OnCreateAsync(), () => !IsBusy);
            GoToLoginCommand = new Command(async () => await _nav.GoToAsync("//LoginPage"));

            FocusOwnerNameCommand       = new Command(() => RequestFocusOwnerName?.Invoke());
            FocusMobileCommand          = new Command(() => RequestFocusMobile?.Invoke());
            FocusCityCommand            = new Command(() => RequestFocusCity?.Invoke());
            FocusPasswordCommand        = new Command(() => RequestFocusPassword?.Invoke());
            FocusConfirmPasswordCommand = new Command(() => RequestFocusConfirmPass?.Invoke());
        }

        // ── Validation ────────────────────────────────────────────────────────

        private bool Validate()
        {
            bool valid = true;

            var name = CompanyName.Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Length < 2 || name.Length > 100)
            { CompanyNameError = "Company name must be 2–100 characters."; valid = false; }

            var owner = OwnerName.Trim();
            if (string.IsNullOrWhiteSpace(owner) || owner.Length < 2 || owner.Length > 80)
            { OwnerNameError = "Owner name must be 2–80 characters."; valid = false; }

            var mobile = MobileNumber.Trim();
            if (string.IsNullOrWhiteSpace(mobile) || !Regex.IsMatch(mobile, @"^[+\d][\d\s\-]{7,19}$"))
            { MobileNumberError = "Enter a valid mobile number."; valid = false; }

            var city = City.Trim();
            if (string.IsNullOrWhiteSpace(city))
            { CityError = "City is required."; valid = false; }

            var pwd = Password;
            if (string.IsNullOrEmpty(pwd) || pwd.Length < 8 ||
                !Regex.IsMatch(pwd, @"[A-Z]") || !Regex.IsMatch(pwd, @"\d"))
            { PasswordError = "Min 8 characters, at least 1 uppercase letter and 1 digit."; valid = false; }

            if (ConfirmPassword != Password)
            { ConfirmPasswordError = "Passwords do not match."; valid = false; }

            return valid;
        }

        private async Task OnCreateAsync()
        {
            if (IsBusy) return;
            if (!Validate()) return;

            IsBusy = true;
            try
            {
                var mobile = MobileNumber.Trim();

                // Check if mobile already registered
                var existing = await _companyRepo.GetCompanyByMobileAsync(mobile);
                if (existing != null)
                {
                    MobileNumberError = "This mobile number is already registered.";
                    return;
                }

                var company = new Company
                {
                    CompanyName  = CompanyName.Trim(),
                    OwnerName    = OwnerName.Trim(),
                    MobileNumber = mobile,
                    PhoneNumber  = mobile,   // keep legacy column in sync
                    City         = City.Trim(),
                    PasswordHash = _authService.HashPassword(Password),
                    CreatedAt    = DateTime.UtcNow
                };

                await _companyRepo.SaveCompanyAsync(company);

                // Clear form
                CompanyName     = string.Empty;
                OwnerName       = string.Empty;
                MobileNumber    = string.Empty;
                City            = string.Empty;
                Password        = string.Empty;
                ConfirmPassword = string.Empty;

                await _dialog.ShowAlertAsync(
                    "✅ Company Created",
                    "Your fleet account is ready. Please log in.");

                await _nav.GoToAsync("//LoginPage");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[CompanyCreationViewModel] OnCreateAsync error: {ex.Message}");
                CompanyNameError = "An unexpected error occurred. Please try again.";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}

