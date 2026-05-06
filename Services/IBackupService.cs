namespace DriverLedger.Services
{
    /// <summary>
    /// Manages offline backup and restore of the SQLite database.
    /// All backups go to the device's public Documents/DriverLedger/ folder so
    /// they survive an app uninstall.
    /// </summary>
    public interface IBackupService
    {
        /// <summary>
        /// Copies the live SQLite database to Documents/DriverLedger/.
        /// Returns the full path of the created backup file.
        /// </summary>
        Task<string> BackupAsync();

        /// <summary>
        /// Overwrites the app's live database with the file at <paramref name="backupPath"/>.
        /// The app MUST be restarted after calling this.
        /// </summary>
        Task<bool> RestoreAsync(string backupPath);

        /// <summary>
        /// Returns all backup files found in the backup directory, newest first.
        /// </summary>
        Task<IReadOnlyList<string>> ListBackupsAsync();

        /// <summary>
        /// Runs a backup automatically if no backup has been taken in the last 24 hours.
        /// Safe to call on every app start.
        /// </summary>
        Task AutoBackupIfNeededAsync();

        /// <summary>
        /// Full path of the directory where backups are stored.
        /// </summary>
        string BackupDirectory { get; }
    }
}
