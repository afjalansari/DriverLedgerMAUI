using SQLite;

namespace DriverLedger.Database.Migrations
{
    /// <summary>
    /// Phase 2 — Encrypted Backup System.
    ///
    /// Creates the BackupLog table used by SecureBackupService and SecureRestoreService
    /// to maintain an immutable audit trail of all backup and restore operations.
    ///
    /// Safe on:
    ///   • Fresh install  — CREATE TABLE IF NOT EXISTS is a no-op on second run.
    ///   • Existing DB    — table is new; no existing tables modified.
    ///   • Re-run         — fully idempotent.
    /// </summary>
    public class Migration_007_AddBackupLog : MigrationBase
    {
        public override int    Version => 7;
        public override string Name    => "AddBackupLog";

        public override void Up(SQLiteConnection conn)
        {
            // BackupManifest model maps to this table via [Table("BackupLog")].
            // Raw DDL is used (not CreateTable<T>) to ensure exact column types
            // and to avoid sqlite-net's automatic column-name casing.
            conn.Execute(@"
                CREATE TABLE IF NOT EXISTS BackupLog (
                    Id                  INTEGER PRIMARY KEY AUTOINCREMENT,
                    FormatVersion       INTEGER NOT NULL DEFAULT 1,
                    AppVersion          TEXT    NOT NULL DEFAULT '',
                    SchemaVersion       INTEGER NOT NULL DEFAULT 0,
                    CompanyName         TEXT    NOT NULL DEFAULT '',
                    SettlementCount     INTEGER NOT NULL DEFAULT 0,
                    DriverCount         INTEGER NOT NULL DEFAULT 0,
                    VehicleCount        INTEGER NOT NULL DEFAULT 0,
                    BackupTimestampUtc  TEXT    NOT NULL DEFAULT '',
                    RestoreTimestampUtc TEXT,
                    IntegrityHash       TEXT    NOT NULL DEFAULT '',
                    OriginalSizeBytes   INTEGER NOT NULL DEFAULT 0,
                    BackupPath          TEXT    NOT NULL DEFAULT '',
                    Success             INTEGER NOT NULL DEFAULT 1,
                    FailureReason       TEXT
                );");

            // Index for listing most-recent backups quickly
            conn.Execute(@"
                CREATE INDEX IF NOT EXISTS idx_backup_log_timestamp
                    ON BackupLog (BackupTimestampUtc DESC);");

            // Index for querying restore history
            conn.Execute(@"
                CREATE INDEX IF NOT EXISTS idx_backup_log_restore
                    ON BackupLog (RestoreTimestampUtc DESC)
                    WHERE RestoreTimestampUtc IS NOT NULL;");
        }
    }
}
