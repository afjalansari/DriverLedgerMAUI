namespace DriverLedger.Services
{
    /// <summary>
    /// Singleton implementation of <see cref="ISessionService"/>.
    /// Uses Preferences for the login flag and SecureStorage for the last login timestamp.
    /// Auto-logout enforced for sessions older than 30 days.
    /// </summary>
    public class SessionService : ISessionService
    {
        private const string SessionKey       = "IsLoggedIn";
        private const string LastLoginTimeKey = "LastLoginTime";
        private const int    SessionExpiryDays = 30;

        // ── Session flag ──────────────────────────────────────────────────────

        public bool IsLoggedIn
            => Preferences.Default.Get(SessionKey, false);

        public void SetLoggedIn(bool value)
        {
            Preferences.Default.Set(SessionKey, value);

            if (value)
            {
                // Fire-and-forget: store timestamp in SecureStorage
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await SecureStorage.Default.SetAsync(
                            LastLoginTimeKey,
                            DateTime.UtcNow.ToString("o")); // ISO 8601 round-trip
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[SessionService] SecureStorage write error: {ex.Message}");
                    }
                });
            }
        }

        public void ClearSession()
        {
            try { Preferences.Default.Remove(SessionKey); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[SessionService] Preferences.Remove error: {ex.Message}");
            }

            try { SecureStorage.Default.Remove(LastLoginTimeKey); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[SessionService] SecureStorage.Remove error: {ex.Message}");
            }
        }

        // ── 30-day expiry check ────────────────────────────────────────────────

        public async Task<bool> IsSessionValidAsync()
        {
            if (!IsLoggedIn)
                return false;

            try
            {
                var raw = await SecureStorage.Default.GetAsync(LastLoginTimeKey);
                if (string.IsNullOrEmpty(raw))
                    return false;

                if (!DateTime.TryParse(raw, null,
                        System.Globalization.DateTimeStyles.RoundtripKind,
                        out var lastLogin))
                    return false;

                return (DateTime.UtcNow - lastLogin).TotalDays <= SessionExpiryDays;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[SessionService] IsSessionValidAsync error: {ex.Message}");
                return false;
            }
        }
    }
}

