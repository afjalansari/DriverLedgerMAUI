using System.Reflection;
using System.Text;

namespace DriverLedger.Services.Diagnostics
{
    /// <summary>
    /// File-based offline crash logging service.
    ///
    /// Thread safety: all writes are serialised through <see cref="_writeLock"/>.
    ///
    /// Storage layout:
    ///   {AppDataDirectory}/Logs/crash_yyyyMMdd_HHmmss_fff.log
    ///
    /// Log file format (plain UTF-8 text):
    ///   ════════════════════════════════════
    ///   DRIVERLEDGER CRASH REPORT
    ///   ════════════════════════════════════
    ///   Timestamp    : 2026-05-15T07:52:00Z
    ///   Context      : TaskScheduler.UnobservedTaskException
    ///   App Version  : 1.2.1 (build 11)
    ///   Android API  : 33
    ///   Manufacturer : Xiaomi
    ///   Model        : Redmi Note 11
    ///   ────────────────────────────────────
    ///   Exception    : System.InvalidOperationException
    ///   Message      : Migration v7 (AddBackupLog) failed...
    ///   ────────────────────────────────────
    ///   Inner (1)    : SQLiteException
    ///   Message      : table already exists
    ///   ────────────────────────────────────
    ///   Stack Trace:
    ///      at DriverLedger.Database.Migrations.MigrationRunner...
    ///   ════════════════════════════════════
    ///
    /// Rotation policy (enforced once per app session via <see cref="_rotated"/>):
    ///   • Delete files older than 30 days.
    ///   • Keep at most 50 files (delete oldest beyond cap).
    ///
    /// NEVER THROWS — all exceptions are caught internally.
    /// </summary>
    public sealed class CrashLogService : ICrashLogService
    {
        // ── Singleton ────────────────────────────────────────────────────────────
        // Static instance used by MainApplication (which is created before DI).
        // MauiProgram also registers this instance in DI as a singleton so the
        // same object is used by ViewModels via injection.
        public static readonly CrashLogService Instance = new();

        // ── Configuration ────────────────────────────────────────────────────────
        private const string LogFolder    = "Logs";
        private const string LogPrefix    = "crash_";
        private const string LogExtension = ".log";
        private const int    MaxAgeDays   = 30;
        private const int    MaxFileCount = 50;
        private static readonly string Separator72 = new('═', 72);
        private static readonly string Separator72d = new('─', 72);

        // ── State ────────────────────────────────────────────────────────────────
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private bool _rotated;

        // Resolved once; FileSystem.AppDataDirectory is safe from any thread.
        private string LogDirectory => Path.Combine(
            FileSystem.AppDataDirectory, LogFolder);

        // ── ICrashLogService ─────────────────────────────────────────────────────

        public async Task LogCrashAsync(Exception? ex, string context = "")
        {
            try
            {
                await _writeLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    // Rotate once per session before the first write
                    if (!_rotated)
                    {
                        await RotateInternalAsync().ConfigureAwait(false);
                        _rotated = true;
                    }

                    Directory.CreateDirectory(LogDirectory);

                    string timestamp  = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
                    string fileName   = $"{LogPrefix}{timestamp}{LogExtension}";
                    string filePath   = Path.Combine(LogDirectory, fileName);
                    string content    = BuildReport(ex, context);

                    await File.WriteAllTextAsync(filePath, content, Encoding.UTF8)
                              .ConfigureAwait(false);

                    System.Diagnostics.Debug.WriteLine(
                        $"[CrashLogService] Crash logged → {fileName}");
                }
                finally
                {
                    _writeLock.Release();
                }
            }
            catch (Exception inner)
            {
                // Last-resort: do NOT re-throw from a crash logger
                System.Diagnostics.Debug.WriteLine(
                    $"[CrashLogService] FAILED to write crash log: {inner.Message}");
            }
        }

        public Task<IReadOnlyList<string>> GetLogFilesAsync()
        {
            try
            {
                if (!Directory.Exists(LogDirectory))
                    return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

                var files = Directory
                    .GetFiles(LogDirectory, $"{LogPrefix}*{LogExtension}")
                    .OrderByDescending(f => f)   // filename timestamp → lexicographic = chronological
                    .ToList();

                return Task.FromResult<IReadOnlyList<string>>(files);
            }
            catch
            {
                return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
            }
        }

        public async Task<string> ReadLogAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return "[File not found]";
                return await File.ReadAllTextAsync(filePath, Encoding.UTF8)
                                 .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return $"[Error reading log: {ex.Message}]";
            }
        }

        public async Task PurgeLogsAsync()
        {
            try
            {
                if (!Directory.Exists(LogDirectory)) return;

                var files = Directory.GetFiles(LogDirectory, $"{LogPrefix}*{LogExtension}");
                foreach (var f in files)
                {
                    try { File.Delete(f); }
                    catch { /* skip locked files */ }
                }

                await Task.CompletedTask.ConfigureAwait(false);
                System.Diagnostics.Debug.WriteLine(
                    $"[CrashLogService] Purged {files.Length} log file(s).");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[CrashLogService] PurgeLogs failed: {ex.Message}");
            }
        }

        public Task RotateLogsAsync()
        {
            _ = RotateInternalAsync();    // fire-and-forget; errors are swallowed inside
            return Task.CompletedTask;
        }

        // ── Internal Rotation ────────────────────────────────────────────────────

        private Task RotateInternalAsync()
        {
            try
            {
                if (!Directory.Exists(LogDirectory)) return Task.CompletedTask;

                var files = Directory
                    .GetFiles(LogDirectory, $"{LogPrefix}*{LogExtension}")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(fi => fi.LastWriteTimeUtc)
                    .ToList();

                var cutoff  = DateTime.UtcNow.AddDays(-MaxAgeDays);
                int deleted = 0;

                // Delete old files first
                foreach (var fi in files.Where(fi => fi.LastWriteTimeUtc < cutoff))
                {
                    try { fi.Delete(); deleted++; }
                    catch { /* skip */ }
                }

                // Re-evaluate after age purge, then cap at MaxFileCount
                var remaining = files
                    .Where(fi => fi.Exists && fi.LastWriteTimeUtc >= cutoff)
                    .OrderByDescending(fi => fi.LastWriteTimeUtc)
                    .ToList();

                if (remaining.Count > MaxFileCount)
                {
                    foreach (var fi in remaining.Skip(MaxFileCount))
                    {
                        try { fi.Delete(); deleted++; }
                        catch { /* skip */ }
                    }
                }

                if (deleted > 0)
                    System.Diagnostics.Debug.WriteLine(
                        $"[CrashLogService] Rotation: deleted {deleted} old log file(s).");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[CrashLogService] Rotation failed (non-fatal): {ex.Message}");
            }

            return Task.CompletedTask;
        }

        // ── Report Builder ───────────────────────────────────────────────────────

        private static string BuildReport(Exception? ex, string context)
        {
            var sb = new StringBuilder(2048);

            sb.AppendLine(Separator72);
            sb.AppendLine("DRIVERLEDGER CRASH REPORT");
            sb.AppendLine(Separator72);
            sb.AppendLine($"Timestamp    : {DateTime.UtcNow:O}");
            sb.AppendLine($"Context      : {(string.IsNullOrEmpty(context) ? "(none)" : context)}");
            sb.AppendLine(AppVersionLine());
            sb.AppendLine(DeviceInfoLine());
            sb.AppendLine(Separator72d);

            if (ex is null)
            {
                sb.AppendLine("Exception    : (null — no exception provided)");
            }
            else
            {
                AppendExceptionChain(sb, ex);
            }

            sb.AppendLine(Separator72);
            return sb.ToString();
        }

        private static void AppendExceptionChain(StringBuilder sb, Exception ex)
        {
            int depth = 0;
            Exception? current = ex;

            while (current is not null)
            {
                string label = depth == 0 ? "Exception" : $"Inner ({depth})";
                sb.AppendLine($"{label,-13}: {current.GetType().FullName}");
                sb.AppendLine($"{"Message",-13}: {current.Message}");

                if (depth == 0 && ex.StackTrace is not null)
                {
                    sb.AppendLine(Separator72d);
                    sb.AppendLine("Stack Trace:");
                    sb.AppendLine(ex.StackTrace);
                }

                current = current.InnerException;
                depth++;

                if (current is not null)
                    sb.AppendLine(Separator72d);
            }
        }

        private static string AppVersionLine()
        {
            try
            {
                var asm     = Assembly.GetExecutingAssembly();
                var version = asm.GetName().Version?.ToString() ?? "unknown";
                return $"App Version  : {version}";
            }
            catch { return "App Version  : (unavailable)"; }
        }

        private static string DeviceInfoLine()
        {
            var sb = new StringBuilder();
            try
            {
#if ANDROID
                sb.AppendLine($"Android API  : {(int)Android.OS.Build.VERSION.SdkInt}");
                sb.AppendLine($"Manufacturer : {Android.OS.Build.Manufacturer}");
                sb.AppendLine($"Model        : {Android.OS.Build.Model}");
                sb.AppendLine($"Device       : {Android.OS.Build.Device}");
                sb.AppendLine($"Brand        : {Android.OS.Build.Brand}");
                sb.Append(    $"Build Tags   : {Android.OS.Build.Tags}");
#else
                sb.Append("Platform     : Non-Android");
#endif
            }
            catch
            {
                sb.Append("Device Info  : (unavailable)");
            }
            return sb.ToString();
        }
    }
}
