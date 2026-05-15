using DriverLedger.DTOs;
using DriverLedger.Models;
using DriverLedger.Repositories;

namespace DriverLedger.Services
{
    /// <summary>
    /// SQLite-backed implementation of IDashboardSummaryService.
    /// All repository queries and LINQ aggregation live here so that
    /// DashboardViewModel is reduced to a thin property-setting coordinator.
    /// </summary>
    public class DashboardSummaryService : IDashboardSummaryService
    {
        private readonly ICompanyRepository        _companyRepo;
        private readonly ISettlementRepository     _settlementRepo;
        private readonly IDriverLedgerRepository   _ledgerRepo;
        private readonly IDriverRepository         _driverRepo;
        private readonly IVehicleRepository        _vehicleRepo;

        public DashboardSummaryService(
            ICompanyRepository        companyRepo,
            ISettlementRepository     settlementRepo,
            IDriverLedgerRepository   ledgerRepo,
            IDriverRepository         driverRepo,
            IVehicleRepository        vehicleRepo)
        {
            _companyRepo    = companyRepo;
            _settlementRepo = settlementRepo;
            _ledgerRepo     = ledgerRepo;
            _driverRepo     = driverRepo;
            _vehicleRepo    = vehicleRepo;
        }

        // ── Company ───────────────────────────────────────────────────────────

        public async Task<(string CompanyName, string OwnerName)> GetCompanyInfoAsync()
        {
            var companies = await _companyRepo.GetAllCompaniesAsync();
            if (companies.Count == 0) return ("—", "—");
            return (companies[0].CompanyName, companies[0].OwnerName);
        }

        // ── Daily Summary ─────────────────────────────────────────────────────

        public async Task<DailyFleetSummary> GetDailySummaryAsync(DateTime date)
        {
            var todayTask   = _settlementRepo.GetSettlementsByDateAsync(date);
            var driversTask = _driverRepo.GetAllDriversAsync();
            var vehiclesTask = _vehicleRepo.GetAllVehiclesAsync();
            var recentTask  = _settlementRepo.GetRecentSettlementsAsync(5);

            await Task.WhenAll(todayTask, driversTask, vehiclesTask, recentTask);

            var today    = todayTask.Result;
            var drivers  = driversTask.Result;
            var vehicles = vehiclesTask.Result;
            var recent5  = recentTask.Result;

            // ── Fleet ─────────────────────────────────────────────────────────
            var activeVehicleIds = today.Select(s => s.VehicleId).Distinct().ToHashSet();
            var activeDriverIds  = today.Select(s => s.DriverId).Distinct().ToHashSet();

            // ── Revenue ───────────────────────────────────────────────────────
            var operatorBill  = today.Sum(s => s.TotalIncome);
            var cashCollected = today.Sum(s => s.TotalCashCollected);
            var online        = Math.Max(0m, operatorBill - cashCollected);

            // ── Fuel ──────────────────────────────────────────────────────────
            // BUG-8 fix: sum actual CNG expense items, not just the owner's portion.
            // ExpenseItems are pre-loaded by GetSettlementsByDateAsync → LoadCollectionsAsync.
            var totalFleetCng = today.Sum(s => s.ExpenseItems
                .Where(e => e.Type == ExpenseType.CNG)
                .Sum(e => e.Amount));
            var ownerCng      = today.Sum(s => s.OwnerCngShare);

            // ── Settlement ────────────────────────────────────────────────────
            var driverShare     = today.Sum(s => s.DriverShare);
            var pendingCount    = today.Count(s => s.NetDriverPayable != 0);

            // ── Owner Profit ──────────────────────────────────────────────────
            var ownerExpenses = today.Sum(s => s.TotalOwnerExpenses);
            // D6-4: use canonical helper — same formula as GetMonthlySummaryAsync.
            var ownerProfit   = today.Sum(s => CalcOwnerProfit(s));

            // ── Vehicle Performance ───────────────────────────────────────────
            string topVehicle = "—", bottomVehicle = "—";
            decimal topVehicleAmt = 0, bottomVehicleAmt = 0;

            var vehicleEarnings = today
                .GroupBy(s => s.VehicleId)
                .Select(g => new { VehicleId = g.Key, Total = g.Sum(s => s.TotalIncome) })
                .OrderByDescending(v => v.Total)
                .ToList();

            if (vehicleEarnings.Count > 0)
            {
                var tv  = vehicles.FirstOrDefault(v => v.Id == vehicleEarnings[0].VehicleId);
                var bv  = vehicles.FirstOrDefault(v => v.Id == vehicleEarnings[vehicleEarnings.Count - 1].VehicleId);
                topVehicle       = tv?.VehicleNumber ?? "—";
                topVehicleAmt    = vehicleEarnings[0].Total;
                bottomVehicle    = bv?.VehicleNumber ?? "—";
                bottomVehicleAmt = vehicleEarnings[vehicleEarnings.Count - 1].Total;
            }

            // ── Driver Performance ────────────────────────────────────────────
            string topDriver = "—", mostTripsDriver = "—";
            decimal topDriverEarnings = 0;
            int mostTrips = 0;

            var driverStats = today
                .GroupBy(s => s.DriverId)
                .Select(g => new
                {
                    DriverId   = g.Key,
                    Earnings   = g.Sum(s => s.DriverShare),
                    TripCount  = g.Count()
                })
                .ToList();

            if (driverStats.Count > 0)
            {
                var td = driverStats.OrderByDescending(d => d.Earnings).First();
                var mt = driverStats.OrderByDescending(d => d.TripCount).First();

                topDriver         = drivers.FirstOrDefault(d => d.Id == td.DriverId)?.DriverName ?? "—";
                topDriverEarnings = td.Earnings;
                mostTripsDriver   = drivers.FirstOrDefault(d => d.Id == mt.DriverId)?.DriverName ?? "—";
                mostTrips         = mt.TripCount;
            }

            // ── Recent Settlements (last 5) ───────────────────────────────────
            // FIX-0E: Project directly to RecentSettlementItem (the richer DTO used by the
            // ViewModel/XAML). Removes the intermediate RecentSettlementRow class entirely.
            var recent = recent5
                .Select(s => new RecentSettlementItem
                {
                    Id                 = s.Id,
                    DateDisplay        = s.Date.ToLocalTime().ToString("dd MMM"),
                    VehicleNumber      = vehicles.FirstOrDefault(v => v.Id == s.VehicleId)?.VehicleNumber ?? "—",
                    DriverName         = drivers.FirstOrDefault(d => d.Id == s.DriverId)?.DriverName ?? "—",
                    TotalIncome        = s.TotalIncome,
                    DriverShare        = s.DriverShare,
                    TotalCashCollected = s.TotalCashCollected,
                    TotalOwnerExpenses = s.TotalOwnerExpenses,
                    NetDriverPayable   = s.NetDriverPayable
                })
                .ToList();

            return new DailyFleetSummary
            {
                ActiveVehiclesCount      = activeVehicleIds.Count,
                ActiveDriversCount       = activeDriverIds.Count,
                TripCount                = today.Count,
                OperatorBill             = operatorBill,
                CashCollected            = cashCollected,
                OnlineAmount             = online,
                TotalCNG                 = totalFleetCng,           // BUG-8 fix: actual fleet CNG total
                DriverCngShare           = 0, // no longer flat tracked in summary
                OwnerCngShare            = ownerCng,
                DriverNetHaq             = driverShare,
                CashWithDrivers          = cashCollected,
                DriverPaysOwner          = today.Where(s => s.NetDriverPayable < 0).Sum(s => Math.Abs(s.NetDriverPayable)),
                OwnerPaysDriver          = today.Where(s => s.NetDriverPayable > 0).Sum(s => s.NetDriverPayable),
                PendingCount             = pendingCount,
                OwnerIncomeShare         = today.Sum(s => s.TotalIncome - s.DriverShare),
                OwnerExpenses            = ownerExpenses,
                OwnerNetProfit           = ownerProfit,
                TopEarningVehicle        = topVehicle,
                TopEarningVehicleAmount  = topVehicleAmt,
                LowestEarningVehicle     = bottomVehicle,
                LowestEarningVehicleAmount = bottomVehicleAmt,
                TopDriverName            = topDriver,
                TopDriverEarnings        = topDriverEarnings,
                MostTripsDriver          = mostTripsDriver,
                MostTripsCount           = mostTrips,
                RecentSettlements        = recent
            };
        }

        public async Task<MonthlySummary> GetMonthlySummaryAsync(int year, int month)
        {
            var items = await _settlementRepo.GetSettlementsByMonthAsync(year, month);

            return new MonthlySummary
            {
                Label          = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Unspecified).ToString("MMMM yyyy"),
                OperatorBill   = items.Sum(s => s.TotalIncome),
                // D6-2 fix: was summing OwnerCngShare (only the owner's portion of CNG).
                // GetSettlementsByMonthAsync → LoadCollectionsAsync always populates ExpenseItems,
                // so we can sum the real fleet CNG cost here.
                TotalCNG       = items.Sum(s => s.ExpenseItems
                                     .Where(e => e.Type == ExpenseType.CNG)
                                     .Sum(e => e.Amount)),
                OwnerProfit    = items.Sum(s => CalcOwnerProfit(s)),
                DriverEarnings = items.Sum(s => s.DriverShare)
            };
        }

        public async Task<LedgerBalanceSummary> GetLedgerBalanceSummaryAsync()
        {
            var balances = await _ledgerRepo.GetAllDriverBalancesAsync();
            var oweOwner   = balances.Where(kv => kv.Value < 0).ToList();
            var ownerOwes  = balances.Where(kv => kv.Value > 0).ToList();

            return new LedgerBalanceSummary
            {
                TotalOutstandingBalance = balances.Values.Sum(),
                DriversOweOwnerCount    = oweOwner.Count,
                // C3 fix: oweOwner values are all < 0 — sum is negative; take Abs for display.
                DriversOweOwnerAmount   = Math.Abs(oweOwner.Sum(kv => kv.Value)),
                OwnerOwesDriverCount    = ownerOwes.Count,
                OwnerOwesDriverAmount   = Math.Abs(ownerOwes.Sum(kv => kv.Value))
            };
        }

    // ── Private Helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// D6-4: Single canonical owner-profit formula shared across GetDailySummaryAsync
    /// and GetMonthlySummaryAsync.
    /// OwnerNetProfit = TotalIncome − DriverShare − OwnerCngShare − TotalOwnerExpenses.
    /// </summary>
    private static decimal CalcOwnerProfit(Settlement s)
        => s.TotalIncome - s.DriverShare - s.OwnerCngShare - s.TotalOwnerExpenses;
    }
}
