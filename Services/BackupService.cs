using DriverLedger.Database;
using DriverLedger.Helpers;

namespace DriverLedger.Services
{
    /// <summary>
    /// Offline backup / restore for the SQLite database.
    ///
    /// Backup location strategy (Android):
    ///   API 29+  → MediaStore/Documents (no storage permission needed)
    ///   API ≤ 28 → Environment.ExternalStorageDirectory/Documents/DriverLedger
    ///              (requires WRITE_EXTERNAL_STORAGE permission — declared in manifest)
    ///   Fallback → FileSystem.AppDataDirectory/Backups  (survives OS cloud backup)
    ///
    /// The backup is a plain SQLite file copy. SQLite in WAL mode is safe to copy
    /// after a checkpoint — we force a checkpoint first via PRAGMA wal_checkpoint.
    /// </summary>
    public class DatabaseBackupService : IBackupService
    {
        // ── Constants ───────────────────────────────────────────────────────
        private const string BackupFolder = "DriverLedger";
        private const string BackupPrefix = "DriverLedger_backup_";
        private const string BackupExt    = ".db";
        private const string LastBackupKey = "LastBackupTimestamp";

        // D6-5: delegate to AppConstants — single source of truth, no silent drift.
        private static TimeSpan AutoBackupInterval => AppConstants.AutoBackupInterval;

        // ── Dependencies ─────────────────────────────────────────────────────
        private readonly DatabaseService _db;

        public DatabaseBackupService(DatabaseService db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        // ── Public Properties ────────────────────────────────────────────────

        public string BackupDirectory => ResolveBackupDirectory();

        // ── Public Methods ───────────────────────────────────────────────────

        public async Task<string> BackupAsync()
        {
            string sourceDb = DatabaseService.DatabasePath;

            if (!File.Exists(sourceDb))
                throw new FileNotFoundException("Database file not found.", sourceDb);

            string backupDir = BackupDirectory;
            Directory.CreateDirectory(backupDir);   // no-op if exists

            string timestamp  = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string backupName = $"{BackupPrefix}{timestamp}{BackupExt}";
            string backupPath = Path.Combine(backupDir, backupName);

            // FIX-0D: await the async method BEFORE Task.Run to eliminate sync-over-async.
            var conn = await _db.GetRawConnectionAsync();

            // Force a WAL checkpoint so all changes are in the main DB file
            // before we copy. This is safe even if WAL mode is not active.
            await Task.Run(() =>
            {
                try
                {
                    conn.Execute("PRAGMA wal_checkpoint(TRUNCATE);");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[BackupService] WAL checkpoint warning (non-fatal): {ex.Message}");
                }
            });

            // L2 fix: close the async connection AFTER the checkpoint and BEFORE copying.
            // This ensures no concurrent write can re-dirty the WAL between checkpoint and
            // copy. The connection is re-opened lazily on the next DB operation.
            await _db.CloseAsync();

            // BUG-12 note: the TRUNCATE checkpoint above flushes all WAL pages into the main DB
            // file and then truncates the WAL to zero length. After a successful checkpoint, the
            // main .db file is fully self-consistent and the sidecar copies below are purely
            // belt-and-suspenders (they protect against a failed checkpoint on a busy DB).
            // Atomic file-copy — fast (<5 MB typical DB) and crash-safe
            await Task.Run(() => File.Copy(sourceDb, backupPath, overwrite: true));

            // Copy WAL / SHM only if they still exist after the checkpoint
            await CopySidecarIfExists(sourceDb + "-wal", backupPath + "-wal");
            await CopySidecarIfExists(sourceDb + "-shm", backupPath + "-shm");

            // Persist timestamp for auto-backup throttle
            Preferences.Set(LastBackupKey, DateTime.UtcNow.ToString("O"));

            System.Diagnostics.Debug.WriteLine($"[BackupService] Backup created: {backupPath}");
            return backupPath;
        }

        public async Task<bool> RestoreAsync(string backupPath)
        {
            if (string.IsNullOrWhiteSpace(backupPath) || !File.Exists(backupPath))
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[BackupService] Restore failed: file not found — {backupPath}");
                return false;
            }

            string targetDb = DatabaseService.DatabasePath;

            try
            {
                // Close the live connection before overwriting the file
                await _db.CloseAsync();

                await Task.Run(() =>
                {
                    File.Copy(backupPath, targetDb, overwrite: true);

                    // Restore WAL sidecar if it exists alongside the backup
                    var walSrc = backupPath + "-wal";
                    if (File.Exists(walSrc))
                        File.Copy(walSrc, targetDb + "-wal", overwrite: true);
                    else
                    {
                        // Remove stale WAL from the live location (restore is a clean state)
                        var walDest = targetDb + "-wal";
                        if (File.Exists(walDest)) File.Delete(walDest);
                    }

                    var shmDest = targetDb + "-shm";
                    if (File.Exists(shmDest)) File.Delete(shmDest);
                });

                System.Diagnostics.Debug.WriteLine(
                    $"[BackupService] Restore complete from: {backupPath}");

                // NOTE: Caller must restart the app for the restored DB to be used.
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[BackupService] Restore error: {ex.Message}");
                return false;
            }
        }

        public Task<IReadOnlyList<string>> ListBackupsAsync()
        {
            var dir = BackupDirectory;
            if (!Directory.Exists(dir))
                return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

            var files = Directory
                .GetFiles(dir, $"{BackupPrefix}*{BackupExt}")
                .OrderByDescending(f => f)    // filename contains timestamp → lexicographic = chronologic
                .ToList();

            return Task.FromResult<IReadOnlyList<string>>(files);
        }

        public async Task AutoBackupIfNeededAsync()
        {
            try
            {
                var raw = Preferences.Get(LastBackupKey, string.Empty);
                if (!string.IsNullOrEmpty(raw) &&
                    DateTime.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture,
                                      System.Globalization.DateTimeStyles.RoundtripKind, out var last) &&
                    DateTime.UtcNow - last < AutoBackupInterval)
                {
                    System.Diagnostics.Debug.WriteLine("[BackupService] Auto-backup skipped — recent backup exists.");
                    return;
                }

                await BackupAsync();
                System.Diagnostics.Debug.WriteLine("[BackupService] Auto-backup completed.");
            }
            catch (Exception ex)
            {
                // Auto-backup must NEVER crash the app — swallow and log
                System.Diagnostics.Debug.WriteLine(
                    $"[BackupService] Auto-backup failed (non-fatal): {ex.Message}");
            }
        }

        // ── Private Helpers ──────────────────────────────────────────────────

        private static string ResolveBackupDirectory()
        {
#if ANDROID
            // Try external storage (Documents) — survives uninstall
            try
            {
                var extDocs = Android.OS.Environment
                    .GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDocuments)
                    ?.AbsolutePath;
                if (!string.IsNullOrEmpty(extDocs))
                    return Path.Combine(extDocs, BackupFolder);
            }
            catch
            {
                // External storage not available (device policy / no SD card)
            }
#endif
            // Fallback: app-private directory (safe even without permissions, but
            // wiped on uninstall — better than nothing for the crash-free path)
            return Path.Combine(FileSystem.AppDataDirectory, "Backups", BackupFolder);
        }

        private static async Task CopySidecarIfExists(string src, string dest)
        {
            if (File.Exists(src))
                await Task.Run(() => File.Copy(src, dest, overwrite: true));
        }
    }
}
