using System.Collections.ObjectModel;
using System.Windows.Input;
using DriverLedger.Database;
using DriverLedger.Database.Migrations;
using DriverLedger.Models;
using DriverLedger.Services;

namespace DriverLedger.ViewModels
{
    /// <summary>
    /// Exposes live diagnostic information about the DriverLedger database and runtime.
    ///
    /// Surfaces for the Diagnostics page:
    ///   • Schema version and full migration history grid
    ///   • SQLite DB file size on disk
    ///   • AuditLog row count and last audit timestamp
    ///   • App package version
    ///   • 🧹 Clear Audit Log (reclaim disk space)
    ///   • 📤 Share plain-text diagnostic report
    /// </summary>
    public class DiagnosticsViewModel : BaseViewModel
    {
        private readonly DatabaseService _db;
        private readonly IAuditService   _audit;
        private readonly IDialogService  _dialog;
        private readonly MigrationRunner _runner;
        private readonly INavigationService _nav;

        // ── Backing fields ────────────────────────────────────────────────────
        private int    _schemaVersion;
        private string _appVersion  = "—";
        private string _dbSizeText  = "—";
        private int    _auditRows;
        private string _lastAuditAt = "Never";
        private bool   _isRefreshing;

        // ── Public Properties ─────────────────────────────────────────────────

        public int    SchemaVersion { get => _schemaVersion; set => SetProperty(ref _schemaVersion, value); }
        public string AppVersion    { get => _appVersion;    set => SetProperty(ref _appVersion,    value); }
        public string DbSizeText    { get => _dbSizeText;    set => SetProperty(ref _dbSizeText,    value); }
        public int    AuditRows     { get => _auditRows;     set => SetProperty(ref _auditRows,     value); }
        public string LastAuditAt   { get => _lastAuditAt;   set => SetProperty(ref _lastAuditAt,   value); }
        public bool   IsRefreshing  { get => _isRefreshing;  set => SetProperty(ref _isRefreshing,  value); }

        /// <summary>Ordered list of all applied schema migrations.</summary>
        public ObservableCollection<SchemaVersionDisplay> Migrations { get; } = new();

        // ── Commands ──────────────────────────────────────────────────────────
        public ICommand RefreshCommand         { get; }
        public ICommand ClearAuditLogCommand   { get; }
        public ICommand ShareDiagnosticsCommand { get; }
        public ICommand BackCommand            { get; }

        public DiagnosticsViewModel(
            DatabaseService    db,
            IAuditService      audit,
            MigrationRunner    runner,
            IDialogService     dialog,
            INavigationService nav)
        {
            _db     = db     ?? throw new ArgumentNullException(nameof(db));
            _audit  = audit  ?? throw new ArgumentNullException(nameof(audit));
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
            _dialog = dialog ?? throw new ArgumentNullException(nameof(dialog));
            _nav    = nav    ?? throw new ArgumentNullException(nameof(nav));
            Title   = "Diagnostics";

            RefreshCommand          = new Command(async () => await OnRefreshAsync());
            ClearAuditLogCommand    = new Command(async () => await OnClearAuditLogAsync(), () => !IsBusy);
            ShareDiagnosticsCommand = new Command(async () => await OnShareDiagnosticsAsync(), () => !IsBusy);
            BackCommand             = new Command(async () => await _nav.GoBackAsync());
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        public async Task LoadAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                await RefreshDataAsync();
            }
            finally { IsBusy = false; }
        }

        // ── Handlers ──────────────────────────────────────────────────────────

        private async Task OnRefreshAsync()
        {
            IsRefreshing = true;
            try   { await RefreshDataAsync(); }
            finally { IsRefreshing = false; }
        }

        private async Task RefreshDataAsync()
        {
            // App version
            AppVersion = $"v{AppInfo.VersionString} (build {AppInfo.BuildString})";

            // DB file size
            try
            {
                var info = new FileInfo(DatabaseService.DatabasePath);
                DbSizeText = info.Exists
                    ? $"{info.Length / 1024.0:F1} KB"
                    : "File not found";
            }
            catch { DbSizeText = "Unavailable"; }

            // Schema version + migration history
            try
            {
                var conn = await _db.GetRawConnectionAsync();
                SchemaVersion = _runner.GetCurrentSchemaVersion(conn);

                var rows = _runner.GetAppliedMigrations(conn);
                Migrations.Clear();
                foreach (var r in rows)
                    Migrations.Add(new SchemaVersionDisplay(r));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DiagnosticsVM] Schema read error: {ex.Message}");
            }

            // Audit stats
            try
            {
                var recent = await _audit.GetRecentAsync(1);
                // H2 fix: use a cheap COUNT(*) query instead of loading up to 10,000 full rows.
                AuditRows   = await _audit.GetTotalCountAsync();
                LastAuditAt = recent.Count > 0
                    ? recent[0].Timestamp.ToLocalTime().ToString("dd MMM yyyy, hh:mm tt")
                    : "Never";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DiagnosticsVM] Audit stat error: {ex.Message}");
            }
        }

        private async Task OnClearAuditLogAsync()
        {
            bool confirm = await _dialog.ShowConfirmAsync(
                "Clear Audit Log",
                $"This will permanently delete all {AuditRows} audit records.\n\nThis cannot be undone.",
                "Clear", "Cancel");
            if (!confirm) return;

            IsBusy = true;
            if (ClearAuditLogCommand is Command c) c.ChangeCanExecute();
            try
            {
                var conn = await _db.GetRawConnectionAsync();
                conn.Execute("DELETE FROM AuditLog");
                await RefreshDataAsync();
                await _dialog.ShowAlertAsync("✅ Done", "Audit log cleared.");
            }
            catch (Exception ex)
            {
                await _dialog.ShowAlertAsync("Error", $"Failed to clear audit log: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
                if (ClearAuditLogCommand is Command c2) c2.ChangeCanExecute();
            }
        }

        private async Task OnShareDiagnosticsAsync()
        {
            var report = BuildReport();
            await Share.RequestAsync(new ShareTextRequest
            {
                Title   = "DriverLedger Diagnostics",
                Text    = report,
                Subject = $"DriverLedger Diagnostics — {DateTime.Now:dd MMM yyyy}"
            });
        }

        private string BuildReport()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== DriverLedger Diagnostics ===");
            sb.AppendLine($"Generated  : {DateTime.Now:dd MMM yyyy HH:mm:ss}");
            sb.AppendLine($"App Version: {AppVersion}");
            sb.AppendLine($"Schema Ver : {SchemaVersion}");
            sb.AppendLine($"DB Size    : {DbSizeText}");
            sb.AppendLine($"Audit Rows : {AuditRows}");
            sb.AppendLine($"Last Audit : {LastAuditAt}");
            sb.AppendLine();
            sb.AppendLine("--- Migration History ---");
            foreach (var m in Migrations)
                sb.AppendLine($"  v{m.Version}  {m.Name,-45}  {m.AppliedAt}");
            return sb.ToString();
        }
    }

    /// <summary>Display wrapper for a <see cref="SchemaVersion"/> row.</summary>
    public sealed class SchemaVersionDisplay
    {
        private readonly SchemaVersion _sv;
        public SchemaVersionDisplay(SchemaVersion sv) => _sv = sv;

        public int    Version   => _sv.Version;
        public string Name      => _sv.MigrationName;
        public string AppliedAt => _sv.AppliedAt.ToLocalTime().ToString("dd MMM yyyy");
        public string StatusIcon => _sv.Success ? "✅" : "❌";
    }
}
