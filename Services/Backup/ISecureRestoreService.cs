namespace DriverLedger.Services.Backup
{
    /// <summary>
    /// Atomic, validated restore pipeline for .dlbk encrypted backup files.
    ///
    /// Safety contract — the live database is NEVER touched until ALL of these pass:
    ///   1. Magic bytes valid (.dlbk header)
    ///   2. AES-256-GCM decryption succeeds (authentication tag verified)
    ///   3. SHA-256 integrity hash matches the manifest
    ///   4. PRAGMA integrity_check passes on the decrypted SQLite file
    ///   5. Schema version in backup ≤ current schema version
    ///   6. Settlement count in backup matches the manifest
    ///
    /// Only after all 6 checks pass is the live DB replaced.
    /// On any failure: temp file is deleted, live DB is untouched.
    /// </summary>
    public interface ISecureRestoreService
    {
        /// <summary>
        /// Validates and atomically restores a .dlbk backup file.
        /// </summary>
        /// <param name="backupPath">Full path to the .dlbk file.</param>
        /// <returns>
        /// A <see cref="RestoreResult"/> describing success or the exact failure stage.
        /// Never throws.
        /// </returns>
        Task<RestoreResult> RestoreAsync(string backupPath);
    }
}
