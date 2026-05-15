namespace DriverLedger.Helpers
{
    /// <summary>
    /// Application-wide constants. All magic numbers and string literals that appear
    /// in more than one place must be declared here — never duplicated inline.
    ///
    /// OFFLINE CONTRACT: Nothing in this class makes network calls or references
    /// any cloud/telemetry service. All values are compile-time constants.
    /// </summary>
    public static class AppConstants
    {
        // ── Database ─────────────────────────────────────────────────────────────

        /// <summary>SQLite database filename in <c>FileSystem.AppDataDirectory</c>.</summary>
        public const string DatabaseName = "driverledger.db";

        /// <summary>
        /// Current database schema version — the highest migration number.
        /// Increment when a new migration is added.
        /// </summary>
        public const int SchemaVersion = 6;

        // ── Financial Engine ─────────────────────────────────────────────────────

        /// <summary>
        /// Version of the settlement calculation formula currently in use.
        /// Matches <see cref="Services.SettlementCalculator.CurrentVersion"/>.
        /// Historical settlements with a lower version retain their original values.
        /// NEVER change this without also incrementing SchemaVersion and adding a migration.
        /// </summary>
        public const int CalculatorVersion = 1;

        // ── Authentication ────────────────────────────────────────────────────────

        /// <summary>
        /// Maximum consecutive failed login attempts before the lockout kicks in.
        /// In-memory only — resets on app restart (intentional for offline app).
        /// </summary>
        public const int MaxLoginAttempts = 5;

        /// <summary>
        /// Duration (seconds) of the brute-force lockout window.
        /// After <see cref="MaxLoginAttempts"/> failures, the login form is disabled
        /// for this many seconds before accepting further attempts.
        /// </summary>
        public const int LoginLockoutSeconds = 30;

        /// <summary>Convenience <see cref="TimeSpan"/> for the lockout duration.</summary>
        public static readonly TimeSpan LoginLockDuration =
            TimeSpan.FromSeconds(LoginLockoutSeconds);

        // ── Backup ────────────────────────────────────────────────────────────────

        /// <summary>Prefix for auto-generated backup filenames.</summary>
        public const string BackupFilePrefix = "DriverLedger_backup_";

        /// <summary>File extension for SQLite backup copies.</summary>
        public const string BackupFileExt = ".db";

        /// <summary>
        /// Minimum interval between automatic backups.
        /// <see cref="Services.DatabaseBackupService"/> checks this before creating a new backup.
        /// </summary>
        public static readonly TimeSpan AutoBackupInterval = TimeSpan.FromHours(24);

        // ── Export ───────────────────────────────────────────────────────────────

        /// <summary>Sub-directory inside <c>FileSystem.AppDataDirectory</c> for PDF/CSV exports.</summary>
        public const string ExportsFolder = "Exports";
    }
}
