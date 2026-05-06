using Microsoft.Maui.Storage;

namespace DriverLedger.Helpers
{
    /// <summary>
    /// Persists and applies the user's preferred app theme (Dark / Light / System).
    /// </summary>
    public class ThemeService
    {
        private const string ThemeKey = "app_theme";

        // 0 = System, 1 = Light, 2 = Dark
        public AppTheme GetSavedTheme()
        {
            var saved = Preferences.Get(ThemeKey, 2); // default: Dark
            return saved switch
            {
                1 => AppTheme.Light,
                2 => AppTheme.Dark,
                _ => AppTheme.Unspecified
            };
        }

        public void ApplyTheme(AppTheme theme)
        {
            if (Application.Current is null) return;
            Application.Current.UserAppTheme = theme;

            var key = theme switch
            {
                AppTheme.Light => 1,
                AppTheme.Dark  => 2,
                _              => 0
            };
            Preferences.Set(ThemeKey, key);
        }

        public bool IsDarkMode =>
            Application.Current?.UserAppTheme == AppTheme.Dark ||
            (Application.Current?.UserAppTheme == AppTheme.Unspecified &&
             Application.Current?.RequestedTheme == AppTheme.Dark);

        public void ApplySavedTheme() => ApplyTheme(GetSavedTheme());
    }
}

