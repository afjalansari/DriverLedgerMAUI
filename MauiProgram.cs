using Microsoft.Extensions.Logging;
using DriverLedger.Database;
using DriverLedger.Database.Migrations;
using DriverLedger.Helpers;
using DriverLedger.Repositories;
using DriverLedger.Services;
using DriverLedger.Services.Backup;
using DriverLedger.Services.Diagnostics;
using DriverLedger.Services.Security;
using DriverLedger.ViewModels;
using DriverLedger.Views;

namespace DriverLedger
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemiBold");
                });

#if DEBUG
            builder.Logging.AddDebug();
#endif

            // ── Migration Framework ───────────────────────────────────────────
            // Register concrete migrations as the IMigration collection.
            // MigrationRunner resolves them in ascending Version order at startup.
            builder.Services.AddSingleton<IMigration, Migration_001_InitialSchema>();
            builder.Services.AddSingleton<IMigration, Migration_002_AddDriverCngFields>();
            builder.Services.AddSingleton<IMigration, Migration_003_AddAuditIndexes>();
            builder.Services.AddSingleton<IMigration, Migration_004_AddCalculatorVersion>();
            builder.Services.AddSingleton<IMigration, Migration_005_AddVehicleDriverIndex>();
            builder.Services.AddSingleton<IMigration, Migration_006_AddAuditLog>();
            builder.Services.AddSingleton<IMigration, Migration_007_AddBackupLog>();
            builder.Services.AddSingleton<IMigration, Migration_008_AddSettlementIntegrity>();
            builder.Services.AddSingleton<MigrationRunner>();

            // ── Database ─────────────────────────────────────────────────────
            // Factory lambda: resolves MigrationRunner from DI and injects it.
            builder.Services.AddSingleton<DatabaseService>(sp =>
                new DatabaseService(sp.GetRequiredService<MigrationRunner>()));

            // ── Phase 1 Repositories ─────────────────────────────────────────
            builder.Services.AddSingleton<ICompanyRepository, CompanyRepository>();

            // ── Phase 2 Repositories ─────────────────────────────────────────
            builder.Services.AddSingleton<IDriverRepository,        DriverRepository>();
            builder.Services.AddSingleton<IVehicleRepository,       VehicleRepository>();
            builder.Services.AddSingleton<IVehicleDriverRepository,  VehicleDriverRepository>();

            // ── Phase 3 Repositories ─────────────────────────────────────────
            builder.Services.AddSingleton<ISettlementRepository, SettlementRepository>();

            // ── Phase 4 Repositories ──────────────────────────────────────
            builder.Services.AddSingleton<IDriverLedgerRepository, DriverLedgerRepository>();

            // ── Unit of Work (transactional Settlement + Ledger save) ─────────
            builder.Services.AddSingleton<IUnitOfWork, SqliteUnitOfWork>();

            // ── Auth Services ─────────────────────────────────────────────────
            builder.Services.AddSingleton<AuthService>();
            builder.Services.AddSingleton<ISessionService, SessionService>();
            builder.Services.AddSingleton<StartupService>();

            // ── Navigation & Dialog Abstractions ──────────────────────────────
            builder.Services.AddSingleton<INavigationService, ShellNavigationService>();
            builder.Services.AddSingleton<IDialogService,    ShellDialogService>();

            // ── Financial Engine ─────────────────────────────────────────────
            // D6-1: IncomeEngine, ExpenseEngine, SettlementEngine removed —
            // they were superseded by SettlementCalculator and had no callers.
            // P8-D: registered behind ISettlementCalculator for testability.
            builder.Services.AddSingleton<ISettlementCalculator, SettlementCalculator>();

            // ── Phase 6 Services ─────────────────────────────────────────
            // ThemeService: used by App.xaml.cs (OnStart) and SettingsViewModel (IsDarkMode toggle).
            builder.Services.AddSingleton<ThemeService>();
            // Legacy plain-copy backup (kept for backward compat with existing ViewModel references)
            builder.Services.AddSingleton<IBackupService, DatabaseBackupService>();

            // Phase 2: Encrypted backup/restore services
            builder.Services.AddSingleton<ISecureBackupService, SecureBackupService>();
            builder.Services.AddSingleton<ISecureRestoreService, SecureRestoreService>();

            // Phase 3: Settlement integrity stamping
            builder.Services.AddSingleton<SettlementIntegrityService>();

            // Phase 4 (crash logging): Register DI alias to the static instance so
            // ViewModels can inject ICrashLogService without knowing about the static.
            builder.Services.AddSingleton<ICrashLogService>(CrashLogService.Instance);

            // Phase 7: Security service (root detection, secure delete)
            builder.Services.AddSingleton<SecurityService>();

            builder.Services.AddSingleton<IProfitCalculationService, ProfitCalculationService>();
            builder.Services.AddSingleton<IDashboardSummaryService, DashboardSummaryService>();
            builder.Services.AddSingleton<IExportService, CsvExportService>();
            builder.Services.AddSingleton<IPdfService, PdfService>();
            builder.Services.AddSingleton<IExportOrchestrator, ExportOrchestrator>();

            // ── Phase 7 Services ────────────────────────────────────────────────
            builder.Services.AddSingleton<IAuditService, AuditService>();

            // ── Auth ViewModels ───────────────────────────────────────────────
            builder.Services.AddTransient<LoginViewModel>();
            builder.Services.AddTransient<CompanyCreationViewModel>();
            builder.Services.AddTransient<ForgotPasswordViewModel>();

            // ── Phase 1 ViewModels ───────────────────────────────────────────
            builder.Services.AddTransient<DashboardViewModel>();

            // ── Phase 2 ViewModels ───────────────────────────────────────────
            builder.Services.AddTransient<DriverListViewModel>();
            builder.Services.AddTransient<AddDriverViewModel>();
            builder.Services.AddTransient<VehicleListViewModel>();
            builder.Services.AddTransient<AddVehicleViewModel>();
            builder.Services.AddTransient<AssignDriverViewModel>();

            // ── Phase 3 ViewModels ───────────────────────────────────────────
            builder.Services.AddTransient<SettlementListViewModel>();
            builder.Services.AddTransient<SettlementEntryViewModel>(sp =>
                new SettlementEntryViewModel(
                    new SettlementEntryViewModel.Dependencies
                    {
                        Uow            = sp.GetRequiredService<IUnitOfWork>(),
                        SettlementRepo = sp.GetRequiredService<ISettlementRepository>(),
                        LedgerRepo     = sp.GetRequiredService<IDriverLedgerRepository>(),
                        VehicleRepo    = sp.GetRequiredService<IVehicleRepository>(),
                        DriverRepo     = sp.GetRequiredService<IDriverRepository>(),
                        VdRepo         = sp.GetRequiredService<IVehicleDriverRepository>(),
                        Calculator     = sp.GetRequiredService<ISettlementCalculator>(),
                    },
                    sp.GetRequiredService<INavigationService>(),
                    sp.GetRequiredService<IDialogService>()));
            builder.Services.AddTransient<SettlementDetailViewModel>();

            // ── Phase 4 ViewModels ──────────────────────────────────────
            builder.Services.AddTransient<DriverLedgerListViewModel>();
            builder.Services.AddTransient<DriverLedgerDetailViewModel>();
            builder.Services.AddTransient<AddAdvanceViewModel>();
            builder.Services.AddTransient<ReceivePaymentViewModel>();

            // ── Phase 7 ViewModels ───────────────────────────────────────
            builder.Services.AddTransient<SettlementHistoryViewModel>();

            // ── Phase 8 ViewModels ───────────────────────────────────────
            builder.Services.AddTransient<DiagnosticsViewModel>();

            // ── Phase 6 ViewModels ───────────────────────────────────────
            builder.Services.AddTransient<SettingsViewModel>();
            builder.Services.AddTransient<AnalyticsViewModel>();

            // ── Auth Pages ────────────────────────────────────────────────────
            builder.Services.AddTransient<SplashPage>();
            builder.Services.AddTransient<LoginPage>();
            builder.Services.AddTransient<CompanyPage>();
            builder.Services.AddTransient<CompanyCreationPage>();
            builder.Services.AddTransient<ForgotPasswordPage>();

            // ── Phase 1 Pages ────────────────────────────────────────────────
            builder.Services.AddTransient<DashboardPage>();

            // ── Phase 2 Pages ────────────────────────────────────────────────
            builder.Services.AddTransient<DriverListPage>();
            builder.Services.AddTransient<AddDriverPage>();
            builder.Services.AddTransient<VehicleListPage>();
            builder.Services.AddTransient<AddVehiclePage>();
            builder.Services.AddTransient<AssignDriverPage>();

            // ── Phase 3 Pages ────────────────────────────────────────────────
            builder.Services.AddTransient<SettlementListPage>();
            builder.Services.AddTransient<SettlementEntryPage>();
            builder.Services.AddTransient<SettlementDetailPage>();

            // ── Phase 4 Pages ──────────────────────────────────────────
            builder.Services.AddTransient<DriverLedgerListPage>();
            builder.Services.AddTransient<DriverLedgerDetailPage>();
            builder.Services.AddTransient<AddAdvancePage>();
            builder.Services.AddTransient<ReceivePaymentPage>();

            // ── Phase 7 Pages ────────────────────────────────────────────
            builder.Services.AddTransient<SettlementHistoryPage>();

            // ── Phase 8 Pages ────────────────────────────────────────────
            builder.Services.AddTransient<DiagnosticsPage>();

            // ── Phase 6 Pages ────────────────────────────────────────────
            builder.Services.AddTransient<SettingsPage>();
            builder.Services.AddTransient<AnalyticsPage>();

            // ── Shell ────────────────────────────────────────────────────────────
            builder.Services.AddSingleton<AppShell>();

            // Database initialization is handled by StartupService.RunAsync()
            // which is called from SplashPage.OnNavigatedTo — no fire-and-forget needed.
            return builder.Build();
        }
    }
}


