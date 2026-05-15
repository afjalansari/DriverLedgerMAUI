using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DriverLedger.Database;
using DriverLedger.Helpers;
using DriverLedger.Models;
using DriverLedger.Repositories;

namespace DriverLedger.Services.Backup
{
    /// <summary>
    /// AES-256-GCM encrypted backup service.
    ///
    /// .dlbk file format (all multi-byte integers are big-endian):
    ///   [4]  Magic          = 0x444C424B ("DLBK")
    ///   [4]  Format version = 1
    ///   [32] PBKDF2 salt    (random per backup)
    ///   [12] GCM IV         (random per backup)
    ///   [4]  Manifest JSON length (N)
    ///   [N]  Manifest JSON  (plaintext UTF-8 — for listing without decryption)
    ///   [4]  Payload length (M)
    ///   [M]  AES-256-GCM encrypted SQLite bytes
    ///   [16] GCM authentication tag
    ///
    /// Key derivation:
    ///   PBKDF2-HMAC-SHA256 (100 000 iterations, 32-byte key)
    ///   Password = "{companyName}:{appInstallId}"
    ///   Salt     = 32 random bytes (stored in file header)
    ///
    /// Why AES-GCM over AES-CBC:
    ///   GCM provides authenticated encryption — corrupt or tampered files
    ///   are rejected before any bytes touch the live database.
    /// </summary>
    public sealed class SecureBackupService : ISecureBackupService
    {
        // ── File format constants ─────────────────────────────────────────────
        private static readonly byte[] Magic = { 0x44, 0x4C, 0x42, 0x4B }; // "DLBK"
        private const int FormatVersion  = 1;
        private const int SaltSize       = 32;
        private const int IvSize         = 12;   // GCM standard
        private const int TagSize        = 16;   // GCM standard
        private const int Pbkdf2Iters    = 100_000;

        private const string BackupExt      = ".dlbk";
        private const string BackupPrefix   = "DriverLedger_secure_";
        private const string BackupFolder   = "DriverLedger";
        private const string LastBackupKey  = "SecureLastBackupTimestamp";
        private static TimeSpan AutoBackupInterval => AppConstants.AutoBackupInterval;

        // ── Dependencies ──────────────────────────────────────────────────────
        private readonly DatabaseService        _db;
        private readonly ICompanyRepository     _companyRepo;
        private readonly ISettlementRepository  _settlementRepo;
        private readonly IDriverRepository      _driverRepo;
        private readonly IVehicleRepository     _vehicleRepo;

        public SecureBackupService(
            DatabaseService       db,
            ICompanyRepository    companyRepo,
            ISettlementRepository settlementRepo,
            IDriverRepository     driverRepo,
            IVehicleRepository    vehicleRepo)
        {
            _db             = db             ?? throw new ArgumentNullException(nameof(db));
            _companyRepo    = companyRepo    ?? throw new ArgumentNullException(nameof(companyRepo));
            _settlementRepo = settlementRepo ?? throw new ArgumentNullException(nameof(settlementRepo));
            _driverRepo     = driverRepo     ?? throw new ArgumentNullException(nameof(driverRepo));
            _vehicleRepo    = vehicleRepo    ?? throw new ArgumentNullException(nameof(vehicleRepo));
        }

        // ── ISecureBackupService ──────────────────────────────────────────────

        public async Task<BackupResult> BackupAsync()
        {
            try
            {
                string sourceDb = DatabaseService.DatabasePath;
                if (!File.Exists(sourceDb))
                    return BackupResult.Fail("Database file not found.");

                // Step 1: Checkpoint WAL and close connection
                var conn = await _db.GetRawConnectionAsync().ConfigureAwait(false);
                await Task.Run(() =>
                {
                    try { conn.Execute("PRAGMA wal_checkpoint(TRUNCATE);"); }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[SecureBackupService] WAL checkpoint warning: {ex.Message}");
                    }
                }).ConfigureAwait(false);
                await _db.CloseAsync().ConfigureAwait(false);

                // Step 2: Read raw DB bytes + compute SHA-256 integrity hash
                byte[] dbBytes = await File.ReadAllBytesAsync(sourceDb).ConfigureAwait(false);
                string integrityHash = ComputeSha256Hex(dbBytes);

                // Step 3: Build manifest
                var manifest = await BuildManifestAsync(integrityHash, dbBytes.Length,
                                                         string.Empty).ConfigureAwait(false);

                // Step 4: Derive encryption key
                byte[] salt    = RandomNumberGenerator.GetBytes(SaltSize);
                byte[] key     = DeriveKey(await GetPasswordAsync().ConfigureAwait(false), salt);
                byte[] iv      = RandomNumberGenerator.GetBytes(IvSize);

                // Step 5: Encrypt
                byte[] cipherText;
                byte[] tag = new byte[TagSize];
                using (var aes = new AesGcm(key, TagSize))
                {
                    cipherText = new byte[dbBytes.Length];
                    aes.Encrypt(iv, dbBytes, cipherText, tag);
                }

                // Step 6: Write .dlbk file
                string backupDir  = ResolveBackupDirectory();
                Directory.CreateDirectory(backupDir);
                string timestamp  = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string backupPath = Path.Combine(backupDir, $"{BackupPrefix}{timestamp}{BackupExt}");
                manifest.BackupPath = backupPath;

                byte[] manifestJson = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifest));

                await Task.Run(() =>
                {
                    using var fs = new FileStream(backupPath, FileMode.Create, FileAccess.Write,
                                                   FileShare.None, 65536, useAsync: false);
                    using var bw = new BinaryWriter(fs, Encoding.UTF8, leaveOpen: true);

                    bw.Write(Magic);                          // [4]  magic
                    bw.Write(ToBigEndian(FormatVersion));     // [4]  version
                    bw.Write(salt);                           // [32] salt
                    bw.Write(iv);                             // [12] IV
                    bw.Write(ToBigEndian(manifestJson.Length));// [4]  manifest length
                    bw.Write(manifestJson);                   // [N]  manifest
                    bw.Write(ToBigEndian(cipherText.Length)); // [4]  payload length
                    bw.Write(cipherText);                     // [M]  encrypted payload
                    bw.Write(tag);                            // [16] GCM tag
                }).ConfigureAwait(false);

                // Step 7: Log to BackupLog table
                await LogBackupAsync(manifest).ConfigureAwait(false);

                Preferences.Set(LastBackupKey, DateTime.UtcNow.ToString("O"));
                long size = new FileInfo(backupPath).Length;

                System.Diagnostics.Debug.WriteLine(
                    $"[SecureBackupService] Backup created: {backupPath} ({size / 1024} KB)");

                return BackupResult.Ok(backupPath, size);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[SecureBackupService] BackupAsync failed: {ex.Message}");
                return BackupResult.Fail(ex.Message);
            }
        }

        public Task<IReadOnlyList<string>> ListBackupsAsync()
        {
            try
            {
                var dir = ResolveBackupDirectory();
                if (!Directory.Exists(dir))
                    return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

                var files = Directory
                    .GetFiles(dir, $"{BackupPrefix}*{BackupExt}")
                    .OrderByDescending(f => f)
                    .ToList();

                return Task.FromResult<IReadOnlyList<string>>(files);
            }
            catch
            {
                return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
            }
        }

        public async Task<BackupManifest?> ReadManifestAsync(string backupPath)
        {
            try
            {
                if (!File.Exists(backupPath)) return null;

                return await Task.Run(() =>
                {
                    using var fs = new FileStream(backupPath, FileMode.Open, FileAccess.Read,
                                                   FileShare.Read, 4096, useAsync: false);
                    using var br = new BinaryReader(fs, Encoding.UTF8, leaveOpen: true);

                    // Validate magic
                    var magic = br.ReadBytes(4);
                    if (!magic.SequenceEqual(Magic)) return null;

                    br.ReadBytes(4 + SaltSize + IvSize); // skip version + salt + IV

                    int manifestLen  = FromBigEndian(br.ReadBytes(4));
                    byte[] jsonBytes = br.ReadBytes(manifestLen);
                    string json      = Encoding.UTF8.GetString(jsonBytes);

                    return JsonSerializer.Deserialize<BackupManifest>(json);
                }).ConfigureAwait(false);
            }
            catch
            {
                return null;
            }
        }

        public async Task AutoBackupIfNeededAsync()
        {
            try
            {
                var raw = Preferences.Get(LastBackupKey, string.Empty);
                if (!string.IsNullOrEmpty(raw) &&
                    DateTime.TryParse(raw,
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.RoundtripKind, out var last) &&
                    DateTime.UtcNow - last < AutoBackupInterval)
                {
                    System.Diagnostics.Debug.WriteLine(
                        "[SecureBackupService] Auto-backup skipped — recent backup exists.");
                    return;
                }

                var result = await BackupAsync().ConfigureAwait(false);
                System.Diagnostics.Debug.WriteLine(result.Success
                    ? $"[SecureBackupService] Auto-backup complete: {result.FilePath}"
                    : $"[SecureBackupService] Auto-backup failed (non-fatal): {result.FailureReason}");
            }
            catch (Exception ex)
            {
                // Auto-backup NEVER crashes the app
                System.Diagnostics.Debug.WriteLine(
                    $"[SecureBackupService] AutoBackup swallowed exception: {ex.Message}");
            }
        }

        // ── Internal helpers ──────────────────────────────────────────────────

        private async Task<BackupManifest> BuildManifestAsync(
            string integrityHash, long originalSizeBytes, string backupPath)
        {
            var company     = (await _companyRepo.GetAllCompaniesAsync().ConfigureAwait(false)).FirstOrDefault();
            var settlements = await _settlementRepo.GetAllSettlementsAsync().ConfigureAwait(false);
            var drivers     = await _driverRepo.GetAllDriversAsync().ConfigureAwait(false);
            var vehicles    = await _vehicleRepo.GetAllVehiclesAsync().ConfigureAwait(false);
            var conn        = await _db.GetRawConnectionAsync().ConfigureAwait(false);
            int schemaVer   = await Task.Run(() =>
                conn.ExecuteScalar<int>("SELECT MAX(Version) FROM SchemaVersion;"))
                .ConfigureAwait(false);

            string appVer = Assembly.GetExecutingAssembly()
                                    .GetName().Version?.ToString() ?? "1.0.0";

            return new BackupManifest
            {
                FormatVersion       = FormatVersion,
                AppVersion          = appVer,
                SchemaVersion       = schemaVer,
                CompanyName         = company?.CompanyName ?? string.Empty,
                SettlementCount     = settlements.Count,
                DriverCount         = drivers.Count,
                VehicleCount        = vehicles.Count,
                BackupTimestampUtc  = DateTime.UtcNow.ToString("O"),
                IntegrityHash       = integrityHash,
                OriginalSizeBytes   = originalSizeBytes,
                BackupPath          = backupPath,
                Success             = true
            };
        }

        private async Task<string> GetPasswordAsync()
        {
            // Key material: company name (user-memorable) + install ID (device-bound)
            // Using both prevents cross-device restore with just the company name,
            // AND makes the key unique per installation.
            string companyName = (await _companyRepo.GetAllCompaniesAsync().ConfigureAwait(false)).FirstOrDefault()?.CompanyName ?? "DriverLedger";

            // Install ID: stable per-install, survives app updates
            string installId = Preferences.Get("InstallId", string.Empty);
            if (string.IsNullOrEmpty(installId))
            {
                installId = Guid.NewGuid().ToString("N");
                Preferences.Set("InstallId", installId);
            }

            return $"{companyName}:{installId}";
        }

        private async Task LogBackupAsync(BackupManifest manifest)
        {
            try
            {
                var conn = await _db.GetRawConnectionAsync().ConfigureAwait(false);
                await Task.Run(() => conn.Insert(manifest)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[SecureBackupService] BackupLog insert failed (non-fatal): {ex.Message}");
            }
        }

        // ── Crypto helpers ────────────────────────────────────────────────────

        private static byte[] DeriveKey(string password, byte[] salt)
        {
            // SYSLIB0060 fix: use static Pbkdf2() — identical output, non-obsolete API.
            return Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                Pbkdf2Iters,
                HashAlgorithmName.SHA256,
                32);  // 256-bit AES key
        }

        internal static string ComputeSha256Hex(byte[] data)
        {
            byte[] hash = SHA256.HashData(data);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        /// <summary>Reads the PBKDF2 salt from a .dlbk file. Used by SecureRestoreService.</summary>
        internal static (byte[] salt, byte[] iv, BackupManifest? manifest, long payloadOffset)
            ReadHeader(BinaryReader br)
        {
            var magic = br.ReadBytes(4);
            if (!magic.SequenceEqual(Magic))
                throw new InvalidDataException("Not a valid .dlbk file (bad magic bytes).");

            int version = FromBigEndian(br.ReadBytes(4));
            if (version != FormatVersion)
                throw new InvalidDataException($"Unsupported .dlbk format version: {version}");

            byte[] salt        = br.ReadBytes(SaltSize);
            byte[] iv          = br.ReadBytes(IvSize);
            int    manifestLen = FromBigEndian(br.ReadBytes(4));
            byte[] jsonBytes   = br.ReadBytes(manifestLen);
            string json        = Encoding.UTF8.GetString(jsonBytes);
            var    manifest    = JsonSerializer.Deserialize<BackupManifest>(json);

            // payloadOffset = position after manifest (pointing at payload length int)
            long offset = 4 + 4 + SaltSize + IvSize + 4 + manifestLen;
            return (salt, iv, manifest, offset);
        }

        private static byte[] ToBigEndian(int value)
        {
            byte[] b = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian) Array.Reverse(b);
            return b;
        }

        private static int FromBigEndian(byte[] b)
        {
            if (BitConverter.IsLittleEndian) Array.Reverse(b);
            return BitConverter.ToInt32(b, 0);
        }

        private static string ResolveBackupDirectory()
        {
#if ANDROID
            try
            {
                var extDocs = Android.OS.Environment
                    .GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDocuments)
                    ?.AbsolutePath;
                if (!string.IsNullOrEmpty(extDocs))
                    return Path.Combine(extDocs, BackupFolder);
            }
            catch { /* no external storage */ }
#endif
            return Path.Combine(FileSystem.AppDataDirectory, "SecureBackups", BackupFolder);
        }
    }
}
