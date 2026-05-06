namespace DriverLedger.Services
{
    /// <summary>
    /// MAUI Shell implementation of <see cref="INavigationService"/>.
    /// Registered as a singleton in MauiProgram.
    /// </summary>
    public class ShellNavigationService : INavigationService
    {
        public Task GoToAsync(string route)
            => Shell.Current.GoToAsync(route);

        public Task GoToAsync(string route, IDictionary<string, object> parameters)
            => Shell.Current.GoToAsync(route, parameters);

        public Task GoBackAsync()
            => Shell.Current.GoToAsync("..");
    }
}
