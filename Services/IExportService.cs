namespace DriverLedger.Services
{
    public interface IExportService
    {
        /// <summary>
        /// Exports a collection of items to a CSV file and returns the file path.
        /// </summary>
        Task<string> ExportToCsvAsync<T>(IEnumerable<T> data, string fileNameWithoutExtension) where T : class;

        /// <summary>
        /// Prompts the user to share or save a file.
        /// </summary>
        Task ShareFileAsync(string filePath, string title);
    }
}
