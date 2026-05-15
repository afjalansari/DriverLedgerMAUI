using System.Windows.Input;
using DriverLedger.Helpers;
using DriverLedger.Repositories;
using DriverLedger.Services;

namespace DriverLedger.ViewModels
{
    public class LoginViewModel : BaseViewModel, IDisposable
    {
        private readonly ICompanyRepository  _companyRepo;
        private readonly AuthService         _authService;
        private readonly ISessionService     _sessionService;
        private readonly INavigationService  _nav;

        // ── Focus callback ────────────────────────────────────────────────────
        public Action? RequestFocusPassword { get; set; }

        // ── Form fields ───────────────────────────────────────────────────────

        private string _mobileNumber = string.Empty;
        public string MobileNumber
        {
            get => _mobileNumber;
            set { SetProperty(ref _mobileNumber, value); MobileError = string.Empty; }
        }

        private string _password = string.Empty;
        public string Password
        {
            get => _password;
            set { SetProperty(ref _password, value); PasswordError = string.Empty; }
        }

        // ── Password visibility ───────────────────────────────────────────────

        private bool _isPasswordHidden = true;
        public bool IsPasswordHidden
        {
            get => _isPasswordHidden;
            set { SetProperty(ref _isPasswordHidden, value); OnPropertyChanged(nameof(PasswordToggleIcon)); }
        }
        public string PasswordToggleIcon => IsPasswordHidden ? "👁" : "🙈";

        // ── Inline errors ─────────────────────────────────────────────────────

        private string _mobileError = string.Empty;
        public string MobileError
        {
            get => _mobileError;
            set { SetProperty(ref _mobileError, value); OnPropertyChanged(nameof(HasMobileError)); }
        }
        public bool HasMobileError => !string.IsNullOrEmpty(MobileError);

        private string _passwordError = string.Empty;
        public string PasswordError
        {
            get => _passwordError;
            set { SetProperty(ref _passwordError, value); OnPropertyChanged(nameof(HasPasswordError)); }
        }
        public bool HasPasswordError => !string.IsNullOrEmpty(PasswordError);

        private string _loginError = string.Empty;
        public string LoginError
        {
            get => _loginError;
            set { SetProperty(ref _loginError, value); OnPropertyChanged(nameof(HasLoginError)); }
        }
        public bool HasLoginError => !string.IsNullOrEmpty(LoginError);

        // ── Brute-force lockout (constants sourced from AppConstants) ─────────────

        private int  _failedAttempts    = 0;
        private const int  MaxAttempts  = AppConstants.MaxLoginAttempts;
        private const int  LockoutSecs  = AppConstants.LoginLockoutSeconds;
        private DateTime   _lockoutUntil = DateTime.MinValue;
        private IDispatcherTimer? _lockoutTimer;

        private bool _isLockedOut = false;
        public bool IsLockedOut
        {
            get => _isLockedOut;
            set { SetProperty(ref _isLockedOut, value); OnPropertyChanged(nameof(CanLogin)); }
        }

        public bool CanLogin => !IsBusy && !IsLockedOut;

        private string _lockoutMessage = string.Empty;
        public string LockoutMessage
        {
            get => _lockoutMessage;
            set => SetProperty(ref _lockoutMessage, value);
        }

        // ── App version footer ────────────────────────────────────────────────
        public string AppVersion =>
            $"v{AppInfo.VersionString} (build {AppInfo.BuildString})";

        // ── Commands ──────────────────────────────────────────────────────────

        public ICommand LoginCommand         { get; }
        public ICommand ForgotPasswordCommand{ get; }
        public ICommand CreateCompanyCommand { get; }
        public ICommand TogglePasswordCommand{ get; }
        public ICommand FocusPasswordCommand { get; }

        public LoginViewModel(
            ICompanyRepository  companyRepo,
            AuthService         authService,
            ISessionService     sessionService,
            INavigationService  nav)
        {
            _companyRepo    = companyRepo;
            _authService    = authService;
            _sessionService = sessionService;
            _nav            = nav;
            Title           = "DriverLedger";

            LoginCommand = new Command(
                async () => await OnLoginAsync(),
                () => !IsBusy && !IsLockedOut);
            ForgotPasswordCommand = new Command(async () =>
                await _nav.GoToAsync(nameof(Views.ForgotPasswordPage)));
            CreateCompanyCommand  = new Command(async () =>
                await _nav.GoToAsync("//CompanyCreationPage"));
            TogglePasswordCommand = new Command(() => IsPasswordHidden = !IsPasswordHidden);
            FocusPasswordCommand  = new Command(() => RequestFocusPassword?.Invoke());
        }

        // ── Startup routing (kept as no-op for legacy call sites) ─────────────
        public Task CheckStartupRouteAsync() => Task.CompletedTask;

        // ── Login logic ───────────────────────────────────────────────────────

        private async Task OnLoginAsync()
        {
            if (IsBusy || IsLockedOut) return;

            var mobile = MobileNumber.Trim();
            bool valid = true;

            if (string.IsNullOrWhiteSpace(mobile))
            { MobileError = "Mobile number is required."; valid = false; }

            if (string.IsNullOrWhiteSpace(Password))
            { PasswordError = "Password is required."; valid = false; }

            if (!valid) return;

            IsBusy = true;
            LoginError = string.Empty;
            try
            {
                var company = await _companyRepo.GetCompanyByMobileAsync(mobile);

                if (company == null)
                {
                    LoginError = "Account not found. Check your mobile number.";
                    IncrementFailedAttempts();
                    return;
                }

                if (!_authService.VerifyPassword(Password, company.PasswordHash))
                {
                    Password   = string.Empty;
                    IncrementFailedAttempts();
                    return;
                }

                // ✅ Authenticated
                _failedAttempts = 0;
                _sessionService.SetLoggedIn(true);
                MobileNumber = string.Empty;
                Password     = string.Empty;
                await _nav.GoToAsync("//DashboardPage");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LoginViewModel] OnLoginAsync error: {ex.Message}");
                LoginError = "Something went wrong. Please try again.";
            }
            finally
            {
                IsBusy = false;
                ((Command)LoginCommand).ChangeCanExecute();
            }
        }

        private void IncrementFailedAttempts()
        {
            _failedAttempts++;

            if (_failedAttempts >= MaxAttempts)
            {
                _lockoutUntil   = DateTime.UtcNow.AddSeconds(LockoutSecs);
                _failedAttempts = 0;
                IsLockedOut     = true;
                LoginError      = string.Empty;
                StartLockoutCountdown();
            }
            else
            {
                int remaining = MaxAttempts - _failedAttempts;
                LoginError = $"Incorrect password. {remaining} attempt(s) remaining before lockout.";
            }
        }

        private void StartLockoutCountdown()
        {
            _lockoutTimer?.Stop();
            _lockoutTimer = Application.Current?.Dispatcher.CreateTimer();
            if (_lockoutTimer == null) return;

            _lockoutTimer.Interval    = TimeSpan.FromSeconds(1);
            _lockoutTimer.IsRepeating = true;
            _lockoutTimer.Tick += (s, e) =>
            {
                var remaining = _lockoutUntil - DateTime.UtcNow;
                if (remaining.TotalSeconds <= 0)
                {
                    IsLockedOut     = false;
                    LockoutMessage  = string.Empty;
                    LoginError      = string.Empty;
                    _lockoutTimer?.Stop();
                    ((Command)LoginCommand).ChangeCanExecute();
                }
                else
                {
                    LockoutMessage = $"Too many failed attempts. Please wait {(int)remaining.TotalSeconds}s before trying again.";
                }
            };
            LockoutMessage = $"Too many failed attempts. Please wait {LockoutSecs}s before trying again.";
            _lockoutTimer.Start();
        }
        // ── IDisposable ───────────────────────────────────────────────────
        // L1 fix: the _lockoutTimer Tick lambda captures 'this' (via _lockoutUntil,
        // IsLockedOut, LockoutMessage, LoginCommand). If the user navigates away
        // before the lockout expires, the timer holds the ViewModel alive and
        // keeps firing ticks into a dead VM. Stopping it in Dispose() releases
        // the reference immediately when LoginPage is removed from the nav stack.
        public void Dispose()
        {
            _lockoutTimer?.Stop();
            _lockoutTimer = null;
            GC.SuppressFinalize(this);
        }
    }
}
