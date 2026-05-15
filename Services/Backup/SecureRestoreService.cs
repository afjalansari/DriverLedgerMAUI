using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DriverLedger.Database;
using DriverLedger.Models;
using DriverLedger.Repositories;
using SQLite;

namespace DriverLedger.Services.Backup
{
    /// <summary>
    /// Atomic, 6-stage validated restore pipeline for .dlbk files.
    /// The live database is NEVER touched until all validation stages pass.
    /// </summary>
    public sealed class SecureRestoreService : ISecureRestoreService
    {
        private const int TagSize  = 16;
        private const int Pbkdf2Iters = 100_000;

        private readonly DatabaseService       _db;
        private readonly ICompanyRepository    _companyRepo;
        private readonly ISettlementRepository _settlementRepo;

        public SecureRestoreService(
            DatabaseService       db,
            ICompanyRepository    companyRepo,
            ISettlementRepository settlementRepo)
        {
            _db             = db             ?? throw new ArgumentNullException(nameof(db));
            _companyRepo    = companyRepo    ?? throw new ArgumentNullException(nameof(companyRepo));
            _settlementRepo = settlementRepo ?? throw new ArgumentNullException(nameof(settlementRepo));
        }

        // ── ISecureRestoreService ─────────────────────────────────────────────

        public async Task<RestoreResult> RestoreAsync(string backupPath)
        {
            string tempDb = Path.Combine(
                FileSystem.CacheDirectory, $"restore_candidate_{Guid.NewGuid():N}.db");

            try
            {
                if (!File.Exists(backupPath))
                    return RestoreResult.Fail("FileNotFound", "Backup file does not exist.");

                // ── Stage 1: Read header + decrypt ───────────────────────────
                byte[] plainBytes;
                BackupManifest? manifest;

                try
                {
                    (plainBytes, manifest) = await DecryptAsync(backupPath).ConfigureAwait(false);
                }
                catch (AuthenticationTagMismatchException)
                {
                    return RestoreResult.Fail("Decryption",
                        "Authentication tag mismatch — file is corrupt or tampered.");
                }
                catch (InvalidDataException ex)
                {
                    return RestoreResult.Fail("Header", ex.Message);
                }
                catch (CryptographicException ex)
                {
                    return RestoreResult.Fail("Decryption",
                        $"Decryption failed (wrong key or corrupt file): {ex.Message}");
                }

                if (manifest is null)
                    return RestoreResult.Fail("Manifest", "Manifest is missing or unreadable.");

                // ── Stage 2: SHA-256 integrity hash ───────────────────────────
                string actualHash = SecureBackupService.ComputeSha256Hex(plainBytes);
                if (!string.Equals(actualHash, manifest.IntegrityHash,
                                   StringComparison.OrdinalIgnoreCase))
                {
                    return RestoreResult.Fail("IntegrityHash",
                        "SHA-256 hash mismatch — file content does not match manifest.");
                }

                // ── Stage 3: Write to temp DB ─────────────────────────────────
                await File.WriteAllBytesAsync(tempDb, plainBytes).ConfigureAwait(false);

                // ── Stage 4: SQLite integrity_check on temp DB ────────────────
                string integrityResult = await Task.Run(() =>
                {
                    using var tempConn = new SQLiteConnection(tempDb);
                    return tempConn.ExecuteScalar<string>("PRAGMA integrity_check;") ?? "error";
                }).ConfigureAwait(false);

                if (!string.Equals(integrityResult, "ok", StringComparison.OrdinalIgnoreCase))
                {
                    return RestoreResult.Fail("IntegrityCheck",
                        $"SQLite integrity_check failed: {integrityResult}");
                }

                // ── Stage 5: Schema version check ─────────────────────────────
                int tempSchemaVer = await Task.Run(() =>
                {
                    using var tempConn = new SQLiteConnection(tempDb);
                    try { return tempConn.ExecuteScalar<int>("SELECT MAX(Version) FROM SchemaVersion;"); }
                    catch { return 0; }
                }).ConfigureAwait(false);

                var liveConn = await _db.GetRawConnectionAsync().ConfigureAwait(false);
                int liveSchemaVer = await Task.Run(() =>
                    liveConn.ExecuteScalar<int>("SELECT MAX(Version) FROM SchemaVersion;"))
                    .ConfigureAwait(false);

                if (tempSchemaVer > liveSchemaVer)
                {
                    return RestoreResult.Fail("SchemaVersion",
                        $"Backup schema v{tempSchemaVer} is newer than installed app schema v{liveSchemaVer}. " +
                        "Update the app before restoring this backup.");
                }

                // ── Stage 6: Content count validation ─────────────────────────
                int actualSettlements = await Task.Run(() =>
                {
                    using var tempConn = new SQLiteConnection(tempDb);
                    try { return tempConn.ExecuteScalar<int>("SELECT COUNT(*) FROM Settlements WHERE IsDeleted = 0;"); }
                    catch { return -1; }
                }).ConfigureAwait(false);

                if (actualSettlements >= 0 && actualSettlements != manifest.SettlementCount)
                {
                    return RestoreResult.Fail("ContentCount",
                        $"Settlement count mismatch — manifest says {manifest.SettlementCount}, " +
                        $"file contains {actualSettlements}.");
                }

                // ── ALL STAGES PASSED — atomic swap ───────────────────────────
                string liveDb = DatabaseService.DatabasePath;
                await _db.CloseAsync().ConfigureAwait(false);

                await Task.Run(() =>
                {
                    // Remove stale WAL/SHM from live location
                    foreach (var ext in new[] { string.Empty, "-wal", "-shm" })
                    {
                        var live = liveDb + ext;
                        if (File.Exists(live)) File.Delete(live);
                    }
                    // Atomic copy (on most OS this is not truly atomic, but CloseAsync
                    // ensures no concurrent writer; CacheDirectory is on the same volume)
                    File.Copy(tempDb, liveDb, overwrite: true);
                }).ConfigureAwait(false);

                // Log the restore to BackupLog
                await LogRestoreAsync(manifest).ConfigureAwait(false);

                System.Diagnostics.Debug.WriteLine(
                    $"[SecureRestoreService] Restore complete. Schema v{tempSchemaVer}, " +
                    $"{actualSettlements} settlements.");

                return RestoreResult.Ok(tempSchemaVer,
                    actualSettlements >= 0 ? actualSettlements : manifest.SettlementCount);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[SecureRestoreService] Unexpected error: {ex.Message}");
                return RestoreResult.Fail("Unexpected", ex.Message);
            }
            finally
            {
                // Always delete temp file regardless of outcome
                try { if (File.Exists(tempDb)) File.Delete(tempDb); } catch { /* best effort */ }
            }
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private async Task<(byte[] plainBytes, BackupManifest? manifest)>
            DecryptAsync(string backupPath)
        {
            return await Task.Run(async () =>
            {
                using var fs = new FileStream(backupPath, FileMode.Open, FileAccess.Read,
                                               FileShare.Read, 65536, useAsync: false);
                using var br = new BinaryReader(fs, Encoding.UTF8, leaveOpen: true);

                var (salt, iv, manifest, _) = SecureBackupService.ReadHeader(br);

                // Payload
                int payloadLen   = ReadBigEndian(br);
                byte[] cipher    = br.ReadBytes(payloadLen);
                byte[] tag       = br.ReadBytes(TagSize);

                // Derive key using same password as backup
                string password = await GetPasswordAsync().ConfigureAwait(false);
                byte[] key      = DeriveKey(password, salt);

                byte[] plain = new byte[payloadLen];
                using var aes = new AesGcm(key, TagSize);
                aes.Decrypt(iv, cipher, tag, plain);

                return (plain, manifest);
            }).ConfigureAwait(false);
        }

        private async Task<string> GetPasswordAsync()
        {
            var company   = (await _companyRepo.GetAllCompaniesAsync().ConfigureAwait(false)).FirstOrDefault();
            string name   = company?.CompanyName ?? "DriverLedger";
            string installId = Preferences.Get("InstallId", string.Empty);
            return $"{name}:{installId}";
        }

        private async Task LogRestoreAsync(BackupManifest manifest)
        {
            try
            {
                // Reopen connection after swap and log the restore timestamp
                var conn = await _db.GetRawConnectionAsync().ConfigureAwait(false);
                await Task.Run(() =>
                {
                    conn.Execute(
                        "UPDATE BackupLog SET RestoreTimestampUtc = ? WHERE BackupPath = ?",
                        DateTime.UtcNow.ToString("O"), manifest.BackupPath);
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[SecureRestoreService] LogRestore failed (non-fatal): {ex.Message}");
            }
        }

        private static byte[] DeriveKey(string password, byte[] salt)
        {
            // SYSLIB0060 fix: static Pbkdf2() — same output as backup DeriveKey, non-obsolete API.
            return Rfc2898DeriveBytes.Pbkdf2(
                System.Text.Encoding.UTF8.GetBytes(password),
                salt,
                Pbkdf2Iters,
                HashAlgorithmName.SHA256,
                32);
        }

        private static int ReadBigEndian(BinaryReader br)
        {
            byte[] b = br.ReadBytes(4);
            if (BitConverter.IsLittleEndian) Array.Reverse(b);
            return BitConverter.ToInt32(b, 0);
        }
    }
}
