using Microsoft.Maui.Storage;

namespace DriverLedger.Helpers
{
    /// <summary>
    /// Handles SQLite database backup (export via Share) and restore (import via FilePicker).
    /// Fully offline — uses Android's built-in share sheet and file picker.
    /// </summary>
    public class BackupService
    {
        private string DbPath =>
            Path.Combine(FileSystem.AppDataDirectory, AppConstants.DatabaseName);

        /// <summary>
        /// Helper to get the current page for displaying alerts without using the obsolete MainPage property.
        /// Uses the Window.Page approach recommended for .NET MAUI 9+.
        /// </summary>
        private static Page? CurrentPage =>
            Application.Current?.Windows.FirstOrDefault()?.Page;

        // ── Backup ────────────────────────────────────────────────────────────
        public async Task<bool> BackupAsync()
        {
            try
            {
                if (!File.Exists(DbPath))
                {
                    if (CurrentPage is Page page)
                        await page.DisplayAlertAsync("Backup", "No database file found.", "OK");
                    return false;
                }

                // Copy DB to a shareable cache location with a timestamped name
                var timestamp  = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var backupName = $"DriverLedger_Backup_{timestamp}.db";
                var cachePath  = Path.Combine(FileSystem.CacheDirectory, backupName);

                File.Copy(DbPath, cachePath, overwrite: true);

                // Let Android/user pick where to save it
                await Share.RequestAsync(new ShareFileRequest
                {
                    Title = "Save DriverLedger Backup",
                    File  = new ShareFile(cachePath)
                });

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BackupService] Backup error: {ex.Message}");
                if (CurrentPage is Page page)
                    await page.DisplayAlertAsync("Backup Failed",
                        "Could not create backup. Please try again.", "OK");
                return false;
            }
        }

        // ── Restore ───────────────────────────────────────────────────────────
        public async Task<bool> RestoreAsync()
        {
            try
            {
                var options = new PickOptions
                {
                    PickerTitle = "Select DriverLedger backup (.db)"
                };

                var result = await FilePicker.PickAsync(options);
                if (result is null) return false;

                var ext = Path.GetExtension(result.FileName).ToLower();
                if (ext != ".db")
                {
                    if (CurrentPage is Page page)
                        await page.DisplayAlertAsync("Restore",
                            "Please select a valid .db backup file.", "OK");
                    return false;
                }

                // Confirm overwrite
                if (CurrentPage is Page confirmPage)
                {
                    bool proceed = await confirmPage.DisplayAlertAsync(
                        "⚠️ Restore Database",
                        "This will replace all current data with the backup file. Continue?",
                        "Yes, Restore", "Cancel");

                    if (!proceed) return false;
                }

                // Copy the picked file over the current DB
                using var src = await result.OpenReadAsync();
                using var dst = File.Create(DbPath);
                await src.CopyToAsync(dst);

                if (CurrentPage is Page donePage)
                    await donePage.DisplayAlertAsync("✅ Restore Complete",
                        "Database restored successfully.\nPlease restart the app for changes to take effect.", "OK");

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BackupService] Restore error: {ex.Message}");
                if (CurrentPage is Page page)
                    await page.DisplayAlertAsync("Restore Failed",
                        "Could not restore backup. Please try again.", "OK");
                return false;
            }
        }
    }
}
