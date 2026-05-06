namespace DriverLedger.Services
{
    /// <summary>
    /// Provides stateless password hashing and verification using BCrypt (work factor 12).
    /// Session management is handled by <see cref="ISessionService"/>.
    /// </summary>
    public class AuthService
    {
        // ── Hashing ───────────────────────────────────────────────────────────

        /// <summary>
        /// Returns a BCrypt hash of <paramref name="plainPassword"/> with work factor 12.
        /// Passwords are NEVER stored in plain text.
        /// </summary>
        public string HashPassword(string plainPassword)
        {
            if (string.IsNullOrEmpty(plainPassword))
                return string.Empty;

            return BCrypt.Net.BCrypt.HashPassword(plainPassword, workFactor: 12);
        }

        /// <summary>Verifies a plain-text password against a stored BCrypt hash.</summary>
        public bool VerifyPassword(string plainPassword, string storedHash)
        {
            if (string.IsNullOrEmpty(plainPassword) || string.IsNullOrEmpty(storedHash))
                return false;

            try
            {
                // BCrypt.Verify handles both BCrypt hashes natively
                return BCrypt.Net.BCrypt.Verify(plainPassword, storedHash);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[AuthService] VerifyPassword error: {ex.Message}");
                return false;
            }
        }
    }
}


