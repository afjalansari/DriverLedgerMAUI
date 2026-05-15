namespace DriverLedger.Services.Security
{
    /// <summary>
    /// Runtime security checks for DriverLedger.
    ///
    /// Design decisions:
    ///   • Root detection: advisory only — we do NOT block root users because legitimate
    ///     power users root their phones. We log the detection and expose it in Diagnostics.
    ///   • FLAG_SECURE: set in MainActivity, not here. This service provides the detection
    ///     logic consumed by ViewModels / DiagnosticsViewModel.
    ///   • All checks are offline / local — zero network calls.
    /// </summary>
    public class SecurityService
    {
        private bool? _cachedRootResult;

        // ── Root Detection ───────────────────────────────────────────────────────

        /// <summary>
        /// Returns true if the device shows strong indicators of being rooted.
        /// Result is cached after first call (root status does not change at runtime).
        /// </summary>
        public bool IsDeviceRooted()
        {
            if (_cachedRootResult.HasValue) return _cachedRootResult.Value;

            _cachedRootResult = CheckRootIndicators();
            return _cachedRootResult.Value;
        }

        private static bool CheckRootIndicators()
        {
#if ANDROID
            // Indicator 1: known superuser APK paths
            var superuserPaths = new[]
            {
                "/system/app/Superuser.apk",
                "/system/app/SuperSU.apk",
                "/system/app/KingoUser.apk",
                "/system/app/360SuperUser.apk",
                "/data/app/eu.chainfire.supersu",
            };
            if (superuserPaths.Any(File.Exists)) return true;

            // Indicator 2: su binary in common locations
            var suPaths = new[]
            {
                "/system/bin/su", "/system/xbin/su", "/sbin/su",
                "/su/bin/su",     "/vendor/bin/su",  "/system/sd/xbin/su",
            };
            if (suPaths.Any(File.Exists)) return true;

            // Indicator 3: build tag is "test-keys" (ROM is not signed with OEM keys)
            var buildTags = Android.OS.Build.Tags ?? string.Empty;
            if (buildTags.Contains("test-keys", StringComparison.OrdinalIgnoreCase)) return true;

            // Indicator 4: magisk mount points
            if (Directory.Exists("/sbin/.magisk") || Directory.Exists("/data/adb/magisk")) return true;
#endif
            return false;
        }

        // ── Secure Temp File Cleanup ─────────────────────────────────────────────

        /// <summary>
        /// Overwrites a file with zeros before deletion, preventing recovery via file-carving.
        /// Used for temporary PDF export files after sharing.
        /// Falls back to plain Delete if the overwrite fails (e.g. read-only FS).
        /// </summary>
        public static async Task SecureDeleteAsync(string filePath)
        {
            if (!File.Exists(filePath)) return;

            try
            {
                // Overwrite with zeros
                long size = new FileInfo(filePath).Length;
                await Task.Run(() =>
                {
                    using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Write,
                                                  FileShare.None, 4096, useAsync: false);
                    var zeros = new byte[Math.Min(size, 65536)];
                    long written = 0;
                    while (written < size)
                    {
                        int chunk = (int)Math.Min(zeros.Length, size - written);
                        fs.Write(zeros, 0, chunk);
                        written += chunk;
                    }
                    fs.Flush();
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[SecurityService] SecureDelete overwrite failed (falling back to plain delete): {ex.Message}");
            }
            finally
            {
                try { File.Delete(filePath); }
                catch { /* last resort — best effort */ }
            }
        }
    }
}
