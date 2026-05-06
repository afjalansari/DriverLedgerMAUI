using System.Collections.ObjectModel;
using System.Windows.Input;
using DriverLedger.DTOs;
using DriverLedger.Models;    // DashboardAlert
using DriverLedger.Services;
using DriverLedger.Views;

namespace DriverLedger.ViewModels
{
    public class DashboardViewModel : BaseViewModel
    {
        // ═══════════════════════════════════════════════════════════════════════
        //  Dependencies
        // ═══════════════════════════════════════════════════════════════════════

        private readonly IDashboardSummaryService _summaryService;
        private readonly INavigationService       _nav;
        private readonly IDialogService          _dialog;
        private readonly IExportService          _export;

        // ═══════════════════════════════════════════════════════════════════════
        //  Backing Fields
        // ═══════════════════════════════════════════════════════════════════════

        // Company
        private string _companyName = "—";
        private string _ownerName   = "—";
        private string _dateLabel   = DateTime.Now.ToString("dddd, dd MMM yyyy");

        // Fleet Overview
        private int _totalVehicles;
        private int _totalDrivers;
        private int _activeVehiclesToday;
        private int _activeDriversToday;
        private int _onDutyCount;

        // Today Performance
        private decimal _todayOperatorBill;
        private int     _todayTripCount;
        private decimal _todayCashCollected;
        private decimal _todayOnlineAmount;

        // Today Expense
        private decimal _todayCNG;
        private decimal _todayDriverCNGShare;
        private decimal _todayOwnerCNGShare;

        // Today Settlement
        private decimal _todayDriverNetHaq;
        private decimal _todayCashWithDrivers;
        private decimal _todayDriverPaysOwner;
        private decimal _todayOwnerPaysDriver;

        // Owner Profit
        private decimal _todayOwnerIncomeShare;
        private decimal _todayOwnerExpenses;
        private decimal _todayOwnerNetProfit;

        // Driver Ledger Summary
        private decimal _totalOutstandingBalance;
        private int     _driversOweOwnerCount;
        private decimal _driversOweOwnerAmount;
        private int     _driversReceiveCount;
        private decimal _driversReceiveAmount;

        // Vehicle Performance
        private string  _topEarningVehicle        = "—";
        private decimal _topEarningVehicleAmount;
        private string  _lowestEarningVehicle     = "—";
        private decimal _lowestEarningVehicleAmount;
        private int     _totalVehiclesRunningToday;

        // Driver Performance
        private string  _topDriverName      = "—";
        private decimal _topDriverEarnings;
        private string  _mostTripsDriverName = "—";
        private int     _mostTripsDriverCount;

        // Monthly Summary
        private string  _monthLabel          = DateTime.Now.ToString("MMMM yyyy");
        private decimal _monthOperatorBill;
        private decimal _monthCNG;
        private decimal _monthOwnerProfit;
        private decimal _monthDriverEarnings;

        // KPI Cards
        private decimal _todayIncome;
        private decimal _todayProfit;
        private int     _settlementCount;
        private int     _pendingSettlementsCount;
        private decimal _cashInHand;

        // Driver Ledger Card
        private decimal _ownerToGet;
        private decimal _ownerOwes;

        // Top Performance (V2 XAML aliases)
        private string  _topVehicleNumber = "—";
        private decimal _topVehicleEarning;
        private int     _mostTripsCount;

        // V2 — Notification badge
        private int  _notificationCount;
        private bool _hasNotifications;

        // V2 — RefreshView (separate from IsBusy)
        private bool _isRefreshing;

        // ═══════════════════════════════════════════════════════════════════════
        //  Public Properties
        // ═══════════════════════════════════════════════════════════════════════

        // Company
        public string CompanyName { get => _companyName; set => SetProperty(ref _companyName, value); }
        public string OwnerName   { get => _ownerName;   set => SetProperty(ref _ownerName, value); }
        public string DateLabel   { get => _dateLabel;   set => SetProperty(ref _dateLabel, value); }

        // Fleet Overview
        public int TotalVehicles      { get => _totalVehicles;      set => SetProperty(ref _totalVehicles, value); }
        public int TotalDrivers       { get => _totalDrivers;       set => SetProperty(ref _totalDrivers, value); }
        public int ActiveVehiclesToday { get => _activeVehiclesToday; set => SetProperty(ref _activeVehiclesToday, value); }
        public int ActiveDriversToday  { get => _activeDriversToday;  set => SetProperty(ref _activeDriversToday, value); }
        public int OnDutyCount         { get => _onDutyCount;         set => SetProperty(ref _onDutyCount, value); }

        // Today Performance
        public decimal TodayOperatorBill  { get => _todayOperatorBill;  set => SetProperty(ref _todayOperatorBill, value); }
        public int     TodayTripCount     { get => _todayTripCount;     set => SetProperty(ref _todayTripCount, value); }
        public decimal TodayCashCollected { get => _todayCashCollected; set => SetProperty(ref _todayCashCollected, value); }
        public decimal TodayOnlineAmount  { get => _todayOnlineAmount;  set => SetProperty(ref _todayOnlineAmount, value); }

        // Today Expense
        public decimal TodayCNG           { get => _todayCNG;           set => SetProperty(ref _todayCNG, value); }
        public decimal TodayDriverCNGShare { get => _todayDriverCNGShare; set => SetProperty(ref _todayDriverCNGShare, value); }
        public decimal TodayOwnerCNGShare  { get => _todayOwnerCNGShare;  set => SetProperty(ref _todayOwnerCNGShare, value); }

        // Today Settlement
        public decimal TodayDriverNetHaq    { get => _todayDriverNetHaq;    set => SetProperty(ref _todayDriverNetHaq, value); }
        public decimal TodayCashWithDrivers { get => _todayCashWithDrivers; set => SetProperty(ref _todayCashWithDrivers, value); }
        public decimal TodayDriverPaysOwner { get => _todayDriverPaysOwner; set => SetProperty(ref _todayDriverPaysOwner, value); }
        public decimal TodayOwnerPaysDriver { get => _todayOwnerPaysDriver; set => SetProperty(ref _todayOwnerPaysDriver, value); }

        // Owner Profit
        public decimal TodayOwnerIncomeShare { get => _todayOwnerIncomeShare; set => SetProperty(ref _todayOwnerIncomeShare, value); }
        public decimal TodayOwnerExpenses    { get => _todayOwnerExpenses;    set => SetProperty(ref _todayOwnerExpenses, value); }
        public decimal TodayOwnerNetProfit   { get => _todayOwnerNetProfit;   set => SetProperty(ref _todayOwnerNetProfit, value); }

        // Driver Ledger Summary
        public decimal TotalOutstandingBalance { get => _totalOutstandingBalance; set => SetProperty(ref _totalOutstandingBalance, value); }
        public int     DriversOweOwnerCount    { get => _driversOweOwnerCount;    set => SetProperty(ref _driversOweOwnerCount, value); }
        public decimal DriversOweOwnerAmount   { get => _driversOweOwnerAmount;   set => SetProperty(ref _driversOweOwnerAmount, value); }
        public int     DriversReceiveCount     { get => _driversReceiveCount;     set => SetProperty(ref _driversReceiveCount, value); }
        public decimal DriversReceiveAmount    { get => _driversReceiveAmount;    set => SetProperty(ref _driversReceiveAmount, value); }

        // Vehicle Performance
        public string  TopEarningVehicle       { get => _topEarningVehicle;       set => SetProperty(ref _topEarningVehicle, value); }
        public decimal TopEarningVehicleAmount { get => _topEarningVehicleAmount; set => SetProperty(ref _topEarningVehicleAmount, value); }
        public string  LowestEarningVehicle    { get => _lowestEarningVehicle;    set => SetProperty(ref _lowestEarningVehicle, value); }
        public decimal LowestEarningVehicleAmount { get => _lowestEarningVehicleAmount; set => SetProperty(ref _lowestEarningVehicleAmount, value); }
        public int     TotalVehiclesRunningToday { get => _totalVehiclesRunningToday; set => SetProperty(ref _totalVehiclesRunningToday, value); }

        // Driver Performance
        public string  TopDriverName          { get => _topDriverName;          set => SetProperty(ref _topDriverName, value); }
        public decimal TopDriverEarnings      { get => _topDriverEarnings;      set => SetProperty(ref _topDriverEarnings, value); }
        public string  MostTripsDriverName    { get => _mostTripsDriverName;    set => SetProperty(ref _mostTripsDriverName, value); }
        public int     MostTripsDriverCount   { get => _mostTripsDriverCount;   set => SetProperty(ref _mostTripsDriverCount, value); }

        // Monthly Summary
        public string  MonthLabel          { get => _monthLabel;          set => SetProperty(ref _monthLabel, value); }
        public decimal MonthOperatorBill   { get => _monthOperatorBill;   set => SetProperty(ref _monthOperatorBill, value); }
        public decimal MonthCNG            { get => _monthCNG;            set => SetProperty(ref _monthCNG, value); }
        public decimal MonthOwnerProfit    { get => _monthOwnerProfit;    set => SetProperty(ref _monthOwnerProfit, value); }
        public decimal MonthDriverEarnings { get => _monthDriverEarnings; set => SetProperty(ref _monthDriverEarnings, value); }

        // KPI Cards
        public decimal TodayIncome      { get => _todayIncome;      set => SetProperty(ref _todayIncome, value); }
        public decimal TodayProfit      { get => _todayProfit;      set => SetProperty(ref _todayProfit, value); }
        public int     SettlementCount { get => _settlementCount;  set => SetProperty(ref _settlementCount, value); }
        public int     PendingSettlementsCount { get => _pendingSettlementsCount; set => SetProperty(ref _pendingSettlementsCount, value); }
        public decimal CashInHand       { get => _cashInHand;       set => SetProperty(ref _cashInHand, value); }

        // Driver Ledger Card
        public decimal OwnerToGet { get => _ownerToGet; set => SetProperty(ref _ownerToGet, value); }
        public decimal OwnerOwes  { get => _ownerOwes;  set => SetProperty(ref _ownerOwes, value); }

        // V2 — Notification badge
        public int  NotificationCount { get => _notificationCount; set { if (SetProperty(ref _notificationCount, value)) HasNotifications = value > 0; } }
        public bool HasNotifications  { get => _hasNotifications;  set => SetProperty(ref _hasNotifications, value); }

        // V2 — RefreshView
        public bool IsRefreshing      { get => _isRefreshing;      set => SetProperty(ref _isRefreshing, value); }

        // V2 — Top Performance XAML aliases (backing fields declared above)
        public string  TopVehicleNumber { get => _topVehicleNumber; set => SetProperty(ref _topVehicleNumber, value); }
        public decimal TopVehicleEarning { get => _topVehicleEarning; set => SetProperty(ref _topVehicleEarning, value); }
        public int     MostTripsCount   { get => _mostTripsCount;   set => SetProperty(ref _mostTripsCount, value); }

        // Collections
        public ObservableCollection<RecentSettlementItem> RecentSettlements { get; } = new();
        public ObservableCollection<DashboardAlert>       Alerts            { get; } = new();

        // Computed — true when there are any alerts to show in the Alerts section
        public bool HasAlerts => Alerts.Count > 0;

        // ═══════════════════════════════════════════════════════════════════════
        //  Commands
        // ═══════════════════════════════════════════════════════════════════════

        public ICommand RefreshCommand       { get; }
        public ICommand DriversCommand       { get; }
        public ICommand VehiclesCommand      { get; }
        public ICommand SettlementsCommand   { get; }
        public ICommand DriverLedgerCommand  { get; }
        public ICommand AnalyticsCommand     { get; }
        public ICommand SettingsCommand      { get; }
        public ICommand ExportSummaryCommand { get; }

        // ═══════════════════════════════════════════════════════════════════════
        //  Constructor
        // ═══════════════════════════════════════════════════════════════════════

        public DashboardViewModel(
            IDashboardSummaryService summaryService,
            INavigationService       nav,
            IDialogService          dialog,
            IExportService          export)
        {
            _summaryService = summaryService;
            _nav            = nav;
            _dialog         = dialog;
            _export         = export;
            Title           = "Dashboard";

            RefreshCommand       = new Command(async () => await OnRefreshAsync());
            DriversCommand       = new Command(async () => await _nav.GoToAsync(nameof(DriverListPage)));
            VehiclesCommand      = new Command(async () => await _nav.GoToAsync(nameof(VehicleListPage)));
            SettlementsCommand   = new Command(async () => await _nav.GoToAsync(nameof(SettlementListPage)));
            DriverLedgerCommand  = new Command(async () => await _nav.GoToAsync(nameof(DriverLedgerListPage)));
            AnalyticsCommand     = new Command(async () => await _nav.GoToAsync(nameof(AnalyticsPage)));
            SettingsCommand      = new Command(async () => await _nav.GoToAsync(nameof(SettingsPage)));
            ExportSummaryCommand = new Command(async () => await OnExportSummaryAsync());
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  Handlers
        // ═══════════════════════════════════════════════════════════════════════

        private async Task OnRefreshAsync()
        {
            IsRefreshing = true;
            try   { await LoadAsync(); }
            finally { IsRefreshing = false; }
        }

        private async Task OnExportSummaryAsync()
        {
            IsBusy = true;
            try
            {
                var exportData = new List<object>
                {
                    new { Metric = "Date", Value = DateLabel },
                    new { Metric = "Total Bill", Value = TodayOperatorBill },
                    new { Metric = "Cash Collected", Value = TodayCashCollected },
                    new { Metric = "Online Amount", Value = TodayOnlineAmount },
                    new { Metric = "Owner CNG Share", Value = TodayOwnerCNGShare },
                    new { Metric = "Driver Net Haq", Value = TodayDriverNetHaq },
                    new { Metric = "Owner Net Profit", Value = TodayOwnerNetProfit },
                    new { Metric = "Pending Settlements", Value = PendingSettlementsCount }
                };

                string path = await _export.ExportToCsvAsync(exportData, $"Daily_Summary_{DateTime.Now:yyyyMMdd}");
                await _export.ShareFileAsync(path, "Daily Fleet Summary Report");
            }
            catch (Exception ex)
            {
                await _dialog.ShowAlertAsync("Export Error", ex.Message);
            }
            finally { IsBusy = false; }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  Data Loading
        // ═══════════════════════════════════════════════════════════════════════

        public async Task LoadAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                var (comp, owner) = await _summaryService.GetCompanyInfoAsync();
                CompanyName = comp;
                OwnerName   = owner;

                var summary = await _summaryService.GetDailySummaryAsync(DateTime.Today);
                UpdatePropertiesFromSummary(summary);

                var ledger = await _summaryService.GetLedgerBalanceSummaryAsync();
                UpdatePropertiesFromLedger(ledger);

                var month = await _summaryService.GetMonthlySummaryAsync(DateTime.Now.Year, DateTime.Now.Month);
                UpdatePropertiesFromMonth(month);

                RecentSettlements.Clear();
                foreach (var s in summary.RecentSettlements)
                {
                    RecentSettlements.Add(new RecentSettlementItem
                    {
                        Id = s.Id,
                        DateDisplay = s.DateDisplay,
                        VehicleNumber = s.VehicleNumber,
                        DriverName = s.DriverName,
                        TotalIncome = s.TotalIncome,
                        DriverShare = s.DriverShare,
                        TotalCashCollected = s.TotalCashCollected,
                        TotalOwnerExpenses = s.TotalOwnerExpenses,
                        NetDriverPayable = s.NetDriverPayable
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DashboardViewModel] Load error: {ex.Message}");
            }
            finally { IsBusy = false; }
        }

        private void UpdatePropertiesFromSummary(DailyFleetSummary s)
        {
            ActiveVehiclesToday = s.ActiveVehiclesCount;
            ActiveDriversToday  = s.ActiveDriversCount;
            TodayTripCount      = s.TripCount;

            TodayOperatorBill   = s.OperatorBill;
            TodayCashCollected  = s.CashCollected;
            TodayOnlineAmount   = s.OnlineAmount;

            TodayCNG            = s.TotalCNG;
            TodayOwnerCNGShare  = s.OwnerCngShare;

            TodayDriverNetHaq    = s.DriverNetHaq;
            TodayCashWithDrivers = s.CashWithDrivers;
            TodayDriverPaysOwner = s.DriverPaysOwner;
            TodayOwnerPaysDriver = s.OwnerPaysDriver;
            PendingSettlementsCount = s.PendingCount;

            TodayOwnerIncomeShare = s.OwnerIncomeShare;
            TodayOwnerExpenses    = s.OwnerExpenses;
            TodayOwnerNetProfit   = s.OwnerNetProfit;

            TopEarningVehicle        = s.TopEarningVehicle;
            TopEarningVehicleAmount  = s.TopEarningVehicleAmount;
            LowestEarningVehicle     = s.LowestEarningVehicle;
            LowestEarningVehicleAmount = s.LowestEarningVehicleAmount;

            TopDriverName          = s.TopDriverName;
            TopDriverEarnings      = s.TopDriverEarnings;
            MostTripsDriverName    = s.MostTripsDriver;
            MostTripsDriverCount   = s.MostTripsCount;

            // KPI Aliases
            TodayIncome = s.OperatorBill;
            TodayProfit = s.OwnerNetProfit;
            CashInHand  = s.CashCollected;

            // V2 XAML Performance Card Aliases
            TopVehicleNumber  = s.TopEarningVehicle;
            TopVehicleEarning = s.TopEarningVehicleAmount;
            MostTripsCount    = s.MostTripsCount;
        }

        private void UpdatePropertiesFromLedger(LedgerBalanceSummary l)
        {
            TotalOutstandingBalance = l.TotalOutstandingBalance;
            DriversOweOwnerCount    = l.DriversOweOwnerCount;
            DriversOweOwnerAmount   = l.DriversOweOwnerAmount;
            DriversReceiveCount     = l.OwnerOwesDriverCount;
            DriversReceiveAmount    = l.OwnerOwesDriverAmount;

            OwnerToGet = l.DriversOweOwnerAmount;
            OwnerOwes  = l.OwnerOwesDriverAmount;
        }

        private void UpdatePropertiesFromMonth(MonthlySummary m)
        {
            MonthLabel          = m.Label;
            MonthOperatorBill   = m.OperatorBill;
            MonthCNG            = m.TotalCNG;
            MonthOwnerProfit    = m.OwnerProfit;
            MonthDriverEarnings = m.DriverEarnings;
        }
    }
}
