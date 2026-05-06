using DriverLedger.Views;

namespace DriverLedger
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // Auth routes
            Routing.RegisterRoute(nameof(ForgotPasswordPage), typeof(ForgotPasswordPage));

            // Phase 2 — Driver module
            Routing.RegisterRoute(nameof(DriverListPage),   typeof(DriverListPage));
            Routing.RegisterRoute(nameof(AddDriverPage),    typeof(AddDriverPage));

            // Phase 2 — Vehicle module
            Routing.RegisterRoute(nameof(VehicleListPage),  typeof(VehicleListPage));
            Routing.RegisterRoute(nameof(AddVehiclePage),   typeof(AddVehiclePage));
            Routing.RegisterRoute(nameof(AssignDriverPage), typeof(AssignDriverPage));

            // Phase 3 — Settlement module
            Routing.RegisterRoute(nameof(SettlementListPage),   typeof(SettlementListPage));
            Routing.RegisterRoute(nameof(SettlementEntryPage),  typeof(SettlementEntryPage));
            Routing.RegisterRoute(nameof(SettlementDetailPage), typeof(SettlementDetailPage));

            // Phase 4 — Driver Ledger module
            Routing.RegisterRoute(nameof(DriverLedgerListPage),   typeof(DriverLedgerListPage));
            Routing.RegisterRoute(nameof(DriverLedgerDetailPage), typeof(DriverLedgerDetailPage));
            Routing.RegisterRoute(nameof(AddAdvancePage),         typeof(AddAdvancePage));
            Routing.RegisterRoute(nameof(ReceivePaymentPage),     typeof(ReceivePaymentPage));

            // Phase 6 — Analytics & Settings
            Routing.RegisterRoute(nameof(AnalyticsPage), typeof(AnalyticsPage));
            Routing.RegisterRoute(nameof(SettingsPage),  typeof(SettingsPage));
        }
    }
}


