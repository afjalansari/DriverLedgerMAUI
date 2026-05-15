using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;

namespace DriverLedger
{
    [Activity(
        Theme = "@style/Maui.SplashTheme",
        MainLauncher = true,
        LaunchMode = LaunchMode.SingleTop,
        ConfigurationChanges =
            ConfigChanges.ScreenSize | ConfigChanges.Orientation |
            ConfigChanges.UiMode    | ConfigChanges.ScreenLayout |
            ConfigChanges.SmallestScreenSize | ConfigChanges.Density,
        Exported = true)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // Phase 7 — FLAG_SECURE: prevents screenshots, screen recording,
            // and screen casting of sensitive financial data.
            // This flag is enforced in Release builds on all Android versions.
            // NOTE: disable this only for UI testing / demo builds.
#if !DEBUG
            Window?.SetFlags(WindowManagerFlags.Secure, WindowManagerFlags.Secure);
#endif
        }
    }
}