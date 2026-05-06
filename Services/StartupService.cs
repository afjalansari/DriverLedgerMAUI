using DriverLedger.Database;
using DriverLedger.Repositories;

namespace DriverLedger.Services
{
    /// <summary>
    /// Orchestrates app startup: waits for DB init, checks session validity,
    /// checks company existence, then routes to the appropriate Shell page.
    /// Call RunAsync() from App.xaml.cs after the window is created.
    /// </summary>
    public class StartupService
    {
        private readonly DatabaseService    _db;
        private readonly ICompanyRepository _companyRepo;
        private readonly ISessionService    _session;

        public StartupService(
            DatabaseService    db,
            ICompanyRepository companyRepo,
            ISessionService    session)
        {
            _db          = db;
            _companyRepo = companyRepo;
            _session     = session;
        }

        public async Task RunAsync()
        {
            try
            {
                // 1. Ensure DB is ready
                await _db.InitializeAsync();

                // 2. Check if an active, non-expired session exists
                bool sessionValid = await _session.IsSessionValidAsync();
                if (sessionValid)
                {
                    await Shell.Current.GoToAsync("//DashboardPage");
                    return;
                }

                // If expired, clear stale session data
                if (_session.IsLoggedIn)
                    _session.ClearSession();

                // 3. Check if a company has been registered
                var companies = await _companyRepo.GetAllCompaniesAsync();
                if (companies.Count == 0)
                {
                    await Shell.Current.GoToAsync("//CompanyCreationPage");
                }
                else
                {
                    await Shell.Current.GoToAsync("//LoginPage");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[StartupService] RunAsync error: {ex.Message}\n{ex.StackTrace}");

                // Fallback: send to login if something goes wrong
                try { await Shell.Current.GoToAsync("//LoginPage"); }
                catch (Exception navEx)
                {
                    // BUG-I fix: log the swallowed exception so it's visible in device logs.
                    // (Shell.Current may be null on catastrophic startup failure)
                    System.Diagnostics.Debug.WriteLine(
                        $"[StartupService] Fallback navigation also failed: {navEx.Message}");
                }
            }
        }
    }
}

