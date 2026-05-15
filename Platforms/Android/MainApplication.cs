using Android.App;
using Android.Runtime;
using DriverLedger.Services.Diagnostics;

namespace DriverLedger
{
    [Application]
    public class MainApplication : MauiApplication
    {
        public MainApplication(IntPtr handle, JniHandleOwnership ownership)
            : base(handle, ownership)
        {
            // Phase 4: Global unhandled exception hooks wired to CrashLogService.
            // CrashLogService.Instance is a static singleton initialised before DI
            // is built, so it is safe to call here. It NEVER throws internally.
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                _ = CrashLogService.Instance.LogCrashAsync(ex, "AppDomain.UnhandledException");
                System.Diagnostics.Debug.WriteLine($"[DriverLedger] FATAL: {ex?.Message}");
            };

            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                _ = CrashLogService.Instance.LogCrashAsync(args.Exception, "TaskScheduler.UnobservedTaskException");
                System.Diagnostics.Debug.WriteLine($"[DriverLedger] UnobservedTask: {args.Exception?.Message}");
                args.SetObserved();
            };
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }
}