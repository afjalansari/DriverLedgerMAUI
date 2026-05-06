namespace DriverLedger.Services
{
    /// <summary>
    /// Manages login session persistence via Preferences and SecureStorage.
    /// Register as Singleton in MauiProgram.
    /// </summary>
    public interface ISessionService
    {
        /// <summary>True if an active login session exists in Preferences.</summary>
        bool IsLoggedIn { get; }

        /// <summary>Persist or clear the login flag and update LastLoginTime in SecureStorage.</summary>
        void SetLoggedIn(bool value);

        /// <summary>Clears IsLoggedIn from Preferences and LastLoginTime from SecureStorage.</summary>
        void ClearSession();

        /// <summary>
        /// Returns true if IsLoggedIn is true AND LastLoginTime is within the last 30 days.
        /// Returns false if the session has expired or does not exist.
        /// </summary>
        Task<bool> IsSessionValidAsync();
    }
}

