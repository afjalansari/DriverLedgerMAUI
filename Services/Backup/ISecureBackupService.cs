namespace DriverLedger.Services.Backup
{
    /// <summary>
    /// Result of a secure backup or restore operation.
    /// Returned instead of throwing so callers can handle failures gracefully.
    /// </summary>
    public sealed class BackupResult
    {
        public bool   Success       { get; init; }
        public string FilePath      { get; init; } = string.Empty;
        public string FailureReason { get; init; } = string.Empty;
        public long   SizeBytes     { get; init; }

        public static BackupResult Ok(string path, long bytes) =>
            new() { Success = true,  FilePath = path, SizeBytes = bytes };

        public static BackupResult Fail(string reason) =>
            new() { Success = false, FailureReason = reason };
    }

    /// <summary>
    /// Result of a secure restore operation, with additional validation details.
    /// </summary>
    public sealed class RestoreResult
    {
        public bool   Success         { get; init; }
        public string FailureReason   { get; init; } = string.Empty;
        public string FailureStage    { get; init; } = string.Empty;  // which validation step failed
        public int    SchemaVersion   { get; init; }
        public int    SettlementCount { get; init; }

        public static RestoreResult Ok(int schemaVer, int settlements) =>
            new() { Success = true, SchemaVersion = schemaVer, SettlementCount = settlements };

        public static RestoreResult Fail(string stage, string reason) =>
            new() { Success = false, FailureStage = stage, FailureReason = reason };
    }

    /// <summary>Encrypts and writes a .dlbk backup archive.</summary>
    public interface ISecureBackupService
    {
        /// <summary>
        /// Creates an AES-256-GCM encrypted backup of the live SQLite database.
        /// WAL is checkpointed and connection closed before copying.
        /// </summary>
        Task<BackupResult> BackupAsync();

        /// <summary>Lists all .dlbk backup files, newest first. Never throws.</summary>
        Task<IReadOnlyList<string>> ListBackupsAsync();

        /// <summary>
        /// Reads the plaintext <see cref="Models.BackupManifest"/> from a .dlbk file
        /// without decrypting the payload. Used for the backup list screen.
        /// </summary>
        Task<Models.BackupManifest?> ReadManifestAsync(string backupPath);

        /// <summary>Runs auto-backup if the interval has elapsed. Never throws.</summary>
        Task AutoBackupIfNeededAsync();
    }
}
