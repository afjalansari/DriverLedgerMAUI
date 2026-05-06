using System.Text.RegularExpressions;
using System.Windows.Input;
using DriverLedger.Repositories;
using DriverLedger.Services;

namespace DriverLedger.ViewModels
{
    public class ForgotPasswordViewModel : BaseViewModel
    {
        private readonly ICompanyRepository _companyRepo;
        private readonly AuthService        _authService;
        private readonly INavigationService _nav;

        // ── Focus-advance callbacks ──────────────────────────────────────────
        public Action? RequestFocusNewPassword     { get; set; }
        public Action? RequestFocusConfirmPassword { get; set; }

        // ── Form fields ──────────────────────────────────────────────────────

        private string _mobileNumber = string.Empty;
        public string MobileNumber
        {
            get => _mobileNumber;
            set { SetProperty(ref _mobileNumber, value); MobileNumberError = string.Empty; }
        }

        private string _newPassword = string.Empty;
        public string NewPassword
        {
            get => _newPassword;
            set { SetProperty(ref _newPassword, value); NewPasswordError = string.Empty; }
        }

        private string _confirmPassword = string.Empty;
        public string ConfirmPassword
        {
            get => _confirmPassword;
            set { SetProperty(ref _confirmPassword, value); ConfirmPasswordError = string.Empty; }
        }

        // ── Inline error properties ──────────────────────────────────────────

        private string _mobileNumberError = string.Empty;
        public string MobileNumberError
        {
            get => _mobileNumberError;
            set { SetProperty(ref _mobileNumberError, value); OnPropertyChanged(nameof(HasMobileError)); }
        }
        public bool HasMobileError => !string.IsNullOrEmpty(MobileNumberError);

        private string _newPasswordError = string.Empty;
        public string NewPasswordError
        {
            get => _newPasswordError;
            set { SetProperty(ref _newPasswordError, value); OnPropertyChanged(nameof(HasNewPasswordError)); }
        }
        public bool HasNewPasswordError => !string.IsNullOrEmpty(NewPasswordError);

        private string _confirmPasswordError = string.Empty;
        public string ConfirmPasswordError
        {
            get => _confirmPasswordError;
            set { SetProperty(ref _confirmPasswordError, value); OnPropertyChanged(nameof(HasConfirmPasswordError)); }
        }
        public bool HasConfirmPasswordError => !string.IsNullOrEmpty(ConfirmPasswordError);

        // ── Commands ──────────────────────────────────────────────────────────

        public ICommand ResetCommand               { get; }
        public ICommand BackToLoginCommand         { get; }
        public ICommand FocusNewPasswordCommand    { get; }
        public ICommand FocusConfirmPasswordCommand{ get; }

        public ForgotPasswordViewModel(
            ICompanyRepository companyRepo,
            AuthService        authService,
            INavigationService nav)
        {
            _companyRepo = companyRepo;
            _authService = authService;
            _nav         = nav;
            Title        = "Reset Password";

            ResetCommand       = new Command(async () => await OnResetAsync(), () => !IsBusy);
            BackToLoginCommand = new Command(async () => await _nav.GoToAsync("//LoginPage"));

            FocusNewPasswordCommand     = new Command(() => RequestFocusNewPassword?.Invoke());
            FocusConfirmPasswordCommand = new Command(() => RequestFocusConfirmPassword?.Invoke());
        }

        private bool Validate()
        {
            bool valid = true;

            var mobile = MobileNumber.Trim();
            if (string.IsNullOrWhiteSpace(mobile))
            { MobileNumberError = "Mobile number is required."; valid = false; }

            var pwd = NewPassword;
            if (string.IsNullOrEmpty(pwd) || pwd.Length < 8 ||
                !Regex.IsMatch(pwd, @"[A-Z]") || !Regex.IsMatch(pwd, @"\d"))
            { NewPasswordError = "Min 8 characters, at least 1 uppercase letter and 1 digit."; valid = false; }

            if (ConfirmPassword != NewPassword)
            { ConfirmPasswordError = "Passwords do not match."; valid = false; }

            return valid;
        }

        private async Task OnResetAsync()
        {
            if (IsBusy) return;
            if (!Validate()) return;

            IsBusy = true;
            try
            {
                var mobile  = MobileNumber.Trim();
                var company = await _companyRepo.GetCompanyByMobileAsync(mobile);

                if (company == null)
                {
                    MobileNumberError = "No account found with this mobile number.";
                    return;
                }

                company.PasswordHash = _authService.HashPassword(NewPassword);
                await _companyRepo.SaveCompanyAsync(company);

                // Clear form
                MobileNumber    = string.Empty;
                NewPassword     = string.Empty;
                ConfirmPassword = string.Empty;

                // Success — shown inline via inline error; now navigate back
                await _nav.GoToAsync("//LoginPage");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[ForgotPasswordViewModel] OnResetAsync error: {ex.Message}");
                MobileNumberError = "An unexpected error occurred. Please try again.";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
