using SQLite;

namespace DriverLedger.Models
{
    /// <summary>
    /// Plaintext metadata header embedded in every .dlbk backup file.
    /// Stored as JSON before the encrypted payload so backups can be
    /// listed and identified without decryption.
    ///
    /// Also persisted to the BackupLog table (Migration_007) for audit.
    /// </summary>
    [Table("BackupLog")]
    public class BackupManifest
    {
        // ── BackupLog PK (ignored when used as JSON header) ───────────────
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        // ── Format ────────────────────────────────────────────────────────
        /// <summary>
        /// Binary format version of the .dlbk file.
        /// Increment when the file structure changes to maintain backward compat.
        /// </summary>
        public int FormatVersion { get; set; } = 1;

        // ── App metadata ─────────────────────────────────────────────────
        public string AppVersion     { get; set; } = string.Empty;
        public int    SchemaVersion  { get; set; }
        public string CompanyName    { get; set; } = string.Empty;

        // ── Content counts (used in restore validation) ───────────────────
        public int SettlementCount { get; set; }
        public int DriverCount     { get; set; }
        public int VehicleCount    { get; set; }

        // ── Timing ───────────────────────────────────────────────────────
        /// <summary>ISO-8601 UTC timestamp of when the backup was created.</summary>
        public string BackupTimestampUtc { get; set; } = string.Empty;

        /// <summary>ISO-8601 UTC timestamp of when this backup was last restored. Null if never.</summary>
        public string? RestoreTimestampUtc { get; set; }

        // ── Integrity ────────────────────────────────────────────────────
        /// <summary>
        /// SHA-256 hex digest of the raw (pre-encryption) SQLite file bytes.
        /// Verified after decryption during restore to detect corruption.
        /// </summary>
        public string IntegrityHash { get; set; } = string.Empty;

        /// <summary>Size of the original SQLite file in bytes (pre-encryption).</summary>
        public long OriginalSizeBytes { get; set; }

        // ── BackupLog audit fields ────────────────────────────────────────
        /// <summary>Full path to the .dlbk file on disk.</summary>
        public string BackupPath { get; set; } = string.Empty;

        /// <summary>True = backup/restore succeeded. False = operation failed.</summary>
        public bool Success { get; set; } = true;

        /// <summary>Human-readable failure reason (null on success).</summary>
        public string? FailureReason { get; set; }
    }
}
