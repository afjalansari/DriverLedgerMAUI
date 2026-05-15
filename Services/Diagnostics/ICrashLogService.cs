namespace DriverLedger.Services.Diagnostics
{
    /// <summary>
    /// Offline-only, file-based crash logging service.
    ///
    /// Contract rules:
    ///   • NEVER throws — all internal failures are swallowed silently.
    ///   • All methods are safe to call from any thread (including unhandled-exception hooks).
    ///   • Log files are stored in <see cref="FileSystem.AppDataDirectory"/>/Logs/.
    ///   • Rotation policy: files older than 30 days are deleted on startup.
    ///   • Cap: maximum 50 log files are kept regardless of age.
    /// </summary>
    public interface ICrashLogService
    {
        /// <summary>
        /// Writes a structured crash report for <paramref name="ex"/> to a timestamped log file.
        /// </summary>
        /// <param name="ex">The exception to log. Null is accepted but produces a minimal record.</param>
        /// <param name="context">
        /// Free-text context string — e.g. the page name, command name, or hook source
        /// ("AppDomain.UnhandledException", "TaskScheduler.UnobservedTaskException", "LoginViewModel.SaveAsync").
        /// </param>
        Task LogCrashAsync(Exception? ex, string context = "");

        /// <summary>Returns all crash log file paths, newest-first.</summary>
        Task<IReadOnlyList<string>> GetLogFilesAsync();

        /// <summary>Reads the raw text content of a single log file.</summary>
        Task<string> ReadLogAsync(string filePath);

        /// <summary>Deletes ALL crash log files. Used from the Diagnostics screen.</summary>
        Task PurgeLogsAsync();

        /// <summary>
        /// Runs the rotation policy: deletes files older than 30 days AND any files
        /// beyond the 50-file cap (oldest deleted first). Called automatically on first
        /// log write of each app session.
        /// </summary>
        Task RotateLogsAsync();
    }
}
