using DriverLedger.Helpers;
using DriverLedger.Services;

namespace DriverLedger
{
    public partial class App : Application
    {
        private readonly AppShell       _appShell;
        private readonly ThemeService   _themeService;
        private readonly IBackupService _backup;

        public App(AppShell appShell, ThemeService themeService, IBackupService backup)
        {
            InitializeComponent();
            _appShell     = appShell;
            _themeService = themeService;
            _backup       = backup;

            // ── Phase 8-C: Global crash protection ───────────────────────────
            // Writes full stack traces to crash_log.txt in AppDataDirectory so
            // support sessions can read them without a debugger attached.
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException      += OnUnobservedTaskException;
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

        // ── Crash handlers ───────────────────────────────────────────────────

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            var msg = $"[GlobalError] Unhandled: {ex?.Message}\n{ex?.StackTrace}";
            System.Diagnostics.Debug.WriteLine(msg);
            WriteCrashLog(msg);
        }

        private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            var msg = $"[GlobalError] Unobserved task: {e.Exception.Message}\n{e.Exception.StackTrace}";
            System.Diagnostics.Debug.WriteLine(msg);
            WriteCrashLog(msg);
            e.SetObserved(); // prevent crash on Android
        }

        /// <summary>
        /// Appends a timestamped crash entry to crash_log.txt in AppDataDirectory.
        /// Trims the file to the last 50 KB to prevent unbounded growth.
        /// Never throws — crash logging must be unconditionally safe.
        /// </summary>
        private static void WriteCrashLog(string message)
        {
            try
            {
                const long maxBytes = 50 * 1024; // 50 KB cap
                var path = Path.Combine(FileSystem.AppDataDirectory, "crash_log.txt");

                // Append new entry
                var entry = $"\n=== {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n{message}\n";
                File.AppendAllText(path, entry);

                // Trim to last 50 KB if oversized
                var info = new FileInfo(path);
                if (info.Length > maxBytes)
                {
                    var content = File.ReadAllText(path);
                    var trimmed = content.Length > (int)maxBytes
                        ? content[^(int)maxBytes..]
                        : content;
                    File.WriteAllText(path, trimmed);
                }
            }
            catch
            {
                // WriteCrashLog must NEVER throw — swallow all errors silently
            }
        }
    }
}
