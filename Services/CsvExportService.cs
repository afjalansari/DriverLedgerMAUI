using System.Text;
using System.Reflection;

namespace DriverLedger.Services
{
    public class CsvExportService : IExportService
    {
        public async Task<string> ExportToCsvAsync<T>(IEnumerable<T> data, string fileNameWithoutExtension) where T : class
        {
            if (data == null || !data.Any())
                throw new ArgumentException("No data to export.");

            // Use the first item's type for anonymous types/objects
            var firstItem  = data.First();
            var actualType = firstItem?.GetType() ?? typeof(T);

            // BUG-13 fix: if T is object (e.g. List<object>), every element must share
            // the same runtime type, otherwise property columns from the first item's type
            // won't match later rows and the CSV will be silently misaligned.
            if (typeof(T) == typeof(object))
            {
                var mismatch = data.FirstOrDefault(item => item is not null && item.GetType() != actualType);
                if (mismatch is not null)
                    throw new InvalidOperationException(
                        $"All items must be the same type for CSV export. " +
                        $"Expected '{actualType.Name}', found '{mismatch.GetType().Name}'.");
            }

            var properties = actualType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead)
                .ToList();

            if (!properties.Any())
                throw new InvalidOperationException("Could not find any properties to export.");

            var csvBuilder = new StringBuilder();

            // Header
            csvBuilder.AppendLine(string.Join(",", properties.Select(p => EscapeCsv(p.Name))));

            // Rows
            foreach (var item in data)
            {
                var values = properties.Select(p =>
                {
                    if (item == null || EqualityComparer<T>.Default.Equals(item, default!)) return string.Empty;
                    var val = p.GetValue(item);
                    return EscapeCsv(val?.ToString() ?? string.Empty);
                });
                csvBuilder.AppendLine(string.Join(",", values));
            }

            string fileName = $"{fileNameWithoutExtension}_{DateTime.Now:yyyyMMdd_HHmm}.csv";

            // ROOT-CAUSE FIX: use AppDataDirectory (private, MAUI-shareable via content://)
            // not CacheDirectory (causes FileUriExposedException on Android 7+).
            string exportDir = Path.Combine(FileSystem.AppDataDirectory, "Exports");
            Directory.CreateDirectory(exportDir);   // no-op if exists
            string filePath = Path.Combine(exportDir, fileName);

            try
            {
                await File.WriteAllTextAsync(filePath, csvBuilder.ToString());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[CsvExportService] Write failed: {ex.Message}");
                throw;  // propagate to ExportOrchestrator which will show the dialog
            }

            return filePath;
        }

        public async Task ShareFileAsync(string filePath, string title)
        {
            if (!File.Exists(filePath)) return;

            await Share.RequestAsync(new ShareFileRequest
            {
                Title = title,
                File = new ShareFile(filePath)
            });
        }

        private static string EscapeCsv(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            if (text.Contains(",") || text.Contains("\"") || text.Contains("\n") || text.Contains("\r"))
            {
                return "\"" + text.Replace("\"", "\"\"") + "\"";
            }
            return text;
        }
    }
}
