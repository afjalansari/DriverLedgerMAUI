namespace DriverLedger.Services
{
    /// <summary>
    /// Abstracts Shell navigation so ViewModels never reference Shell.Current directly.
    /// Swap this for an HTTP-redirect service when migrating to a web backend.
    /// </summary>
    public interface INavigationService
    {
        /// <summary>Navigate to an absolute or relative route, e.g. "//DashboardPage" or ".."</summary>
        Task GoToAsync(string route);

        /// <summary>Navigate to a route with query parameters.</summary>
        Task GoToAsync(string route, IDictionary<string, object> parameters);

        /// <summary>Equivalent to GoToAsync("..")</summary>
        Task GoBackAsync();
    }
}
