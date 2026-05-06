using Microsoft.Extensions.Logging;
using DriverLedger.Database;
using DriverLedger.Helpers;
using DriverLedger.Repositories;
using DriverLedger.Services;
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

            // ── Database ─────────────────────────────────────────────────────
            builder.Services.AddSingleton<DatabaseService>();

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

            // ── Phase 5 — Financial Engines ───────────────────────────────
            builder.Services.AddSingleton<IncomeEngine>();
            builder.Services.AddSingleton<ExpenseEngine>();
            builder.Services.AddSingleton<SettlementEngine>();
            builder.Services.AddSingleton<SettlementCalculator>();

            // ── Phase 6 Services ─────────────────────────────────────────
            builder.Services.AddSingleton<ThemeService>();
            builder.Services.AddSingleton<IBackupService, DatabaseBackupService>();
            builder.Services.AddSingleton<IProfitCalculationService, ProfitCalculationService>();
            builder.Services.AddSingleton<IDashboardSummaryService, DashboardSummaryService>();
            builder.Services.AddSingleton<IExportService, CsvExportService>();
            builder.Services.AddSingleton<IPdfService, PdfService>();
            builder.Services.AddSingleton<IExportOrchestrator, ExportOrchestrator>();

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
                        Calculator     = sp.GetRequiredService<SettlementCalculator>(),
                    },
                    sp.GetRequiredService<INavigationService>(),
                    sp.GetRequiredService<IDialogService>()));
            builder.Services.AddTransient<SettlementDetailViewModel>();

            // ── Phase 4 ViewModels ──────────────────────────────────────
            builder.Services.AddTransient<DriverLedgerListViewModel>();
            builder.Services.AddTransient<DriverLedgerDetailViewModel>();
            builder.Services.AddTransient<AddAdvanceViewModel>();
            builder.Services.AddTransient<ReceivePaymentViewModel>();

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


