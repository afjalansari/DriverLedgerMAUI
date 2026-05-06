using DriverLedger.Helpers;
using DriverLedger.Services;

namespace DriverLedger
{
    public partial class App : Application
    {
        private readonly AppShell      _appShell;
        private readonly ThemeService  _themeService;
        private readonly IBackupService _backup;

        public App(AppShell appShell, ThemeService themeService, IBackupService backup)
        {
            InitializeComponent();
            _appShell     = appShell;
            _themeService = themeService;
            _backup       = backup;

            // Global crash protection for Android Release builds
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                System.Diagnostics.Debug.WriteLine($"[GlobalError] Unhandled: {ex?.Message}\n{ex?.StackTrace}");
            };

            TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                System.Diagnostics.Debug.WriteLine($"[GlobalError] Unobserved task: {e.Exception.Message}");
                e.SetObserved(); // prevent crash
            };
        }

        protected override void OnStart()
        {
            base.OnStart();
            _themeService.ApplySavedTheme();

            // Auto-backup on startup if > 24 h since last backup.
            // Fire-and-forget on a background thread — never blocks UI.
            Task.Run(async () =>
            {
                try   { await _backup.AutoBackupIfNeededAsync(); }
                catch { /* already logged inside BackupService */ }
            });
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            try
            {
                // Return window with _appShell. AppShell defaults to SplashPage,
                // and SplashPage now calls StartupService when rendered.
                return new Window(_appShell);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[App] CreateWindow error: {ex.Message}");
                return new Window(new ContentPage
                {
                    Content = new Label
                    {
                        Text = "App failed to start. Please restart.",
                        TextColor = Colors.White,
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions   = LayoutOptions.Center
                    },
                    BackgroundColor = Color.FromArgb("#0D1B2A")
                });
            }
        }
    }
}
