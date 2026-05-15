using System.Windows.Input;
using DriverLedger.Helpers;
using DriverLedger.Services;

namespace DriverLedger.ViewModels
{
    public class SettingsViewModel : BaseViewModel
    {
        private const string Cancel = "Cancel";   // IDE: define constant instead of repeating literal

        private readonly ThemeService       _themeService;
        private readonly IBackupService     _backupService;
        private readonly ISessionService    _sessionService;
        private readonly INavigationService _nav;
        private readonly IDialogService     _dialog;

        private bool _isDarkMode;

        public bool IsDarkMode
        {
            get => _isDarkMode;
            set
            {
                if (SetProperty(ref _isDarkMode, value))
                    _themeService.ApplyTheme(value ? AppTheme.Dark : AppTheme.Light);
            }
        }

        // IDE: made static — reads no instance state
        public static string AppVersion =>
            $"v{AppInfo.VersionString} (build {AppInfo.BuildString})";

        public ICommand BackupCommand      { get; }
        public ICommand RestoreCommand     { get; }
        public ICommand LogoutCommand      { get; }
        public ICommand DiagnosticsCommand { get; }

        // IDE: commands extracted into private methods to reduce constructor cognitive complexity
        public SettingsViewModel(
            ThemeService        themeService,
            IBackupService      backupService,
            ISessionService     sessionService,
            INavigationService  nav,
            IDialogService      dialog)
        {
            _themeService   = themeService;
            _backupService  = backupService;
            _sessionService = sessionService;
            _nav            = nav;
            _dialog         = dialog;
            Title           = "Settings";

            _isDarkMode = themeService.IsDarkMode;

            BackupCommand      = new Command(async () => await OnBackupAsync());
            RestoreCommand     = new Command(async () => await OnRestoreAsync());
            LogoutCommand      = new Command(async () => await OnLogoutAsync());
            DiagnosticsCommand = new Command(async () => await _nav.GoToAsync(nameof(Views.DiagnosticsPage)));
        }

        // ── Command Handlers ─────────────────────────────────────────────────

        private async Task OnBackupAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                string path = await _backupService.BackupAsync();
                await _dialog.ShowAlertAsync("✅ Backup Complete",
                    $"Database backed up to:\n{Path.GetFileName(path)}");
            }
            catch (Exception ex)
            {
                await _dialog.ShowAlertAsync("Backup Failed", ex.Message);
            }
            finally { IsBusy = false; }
        }

        private async Task OnRestoreAsync()
        {
            if (IsBusy) return;

            var backups = await _backupService.ListBackupsAsync();
            if (!backups.Any())
            {
                await _dialog.ShowAlertAsync("No Backups", "No backup files found.");
                return;
            }

            var names  = backups.Select(Path.GetFileName).ToArray()!;
            string? chosen = await Application.Current!.Windows[0].Page!
                .DisplayActionSheetAsync("Select Backup to Restore", Cancel, null, names);

            if (string.IsNullOrEmpty(chosen) || chosen == Cancel) return;

            bool confirmed = await _dialog.ShowConfirmAsync(
                "Restore Backup",
                $"Restore '{chosen}'?\n\nThe app will restart after the restore.",
                "Restore", Cancel);

            if (!confirmed) return;

            IsBusy = true;
            try
            {
                string fullPath = backups.First(b => Path.GetFileName(b) == chosen);
                bool success = await _backupService.RestoreAsync(fullPath);
                if (success)
                    await _dialog.ShowAlertAsync("✅ Restored",
                        "Restore complete. Please restart the app.");
                else
                    await _dialog.ShowAlertAsync("Restore Failed",
                        "The restore operation failed. The original database is intact.");
            }
            catch (Exception ex)
            {
                await _dialog.ShowAlertAsync("Restore Failed", ex.Message);
            }
            finally { IsBusy = false; }
        }

        private async Task OnLogoutAsync()
        {
            bool confirmed = await _dialog.ShowConfirmAsync(
                "Logout",
                "Are you sure you want to logout?",
                "Logout", Cancel);

            if (confirmed)
            {
                _sessionService.ClearSession();
                await _nav.GoToAsync("//LoginPage");
            }
        }
    }
}
