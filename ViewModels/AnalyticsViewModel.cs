using System.Windows.Input;
using DriverLedger.Helpers;
using DriverLedger.Models;
using DriverLedger.Repositories;
using DriverLedger.Services;

namespace DriverLedger.ViewModels
{
    public class AnalyticsViewModel : BaseViewModel
    {
        private readonly ISettlementRepository    _settlementRepo;

        // ── Chart Drawables ────────────────────────────────────────────────
        public BarChartDrawable EarningsChart  { get; } = new BarChartDrawable
        {
            Title    = "Operator Bills (Last 7 Days)",
            BarColor = Color.FromArgb("#2979FF")
        };

        public BarChartDrawable FuelChart  { get; } = new BarChartDrawable
        {
            // BUG-G fix: OwnerCngShare is only the owner's CNG contribution, not total fleet fuel.
            // Label corrected to avoid misleading the user into thinking this is total CNG spent.
            Title    = "Owner CNG Share (Last 7 Days)",
            BarColor = Color.FromArgb("#F44336")
        };

        public BarChartDrawable OwnerChart { get; } = new BarChartDrawable
        {
            // BUG-10: was "Owner Pays Driver (Last 7 Days)" with green color — misleading
            // because green implies gain but the data was cash-out-to-driver (a cost).
            // Now shows Owner Net Profit, which is the meaningful financial metric here.
            Title    = "Owner Net Profit (Last 7 Days)",
            BarColor = Color.FromArgb("#2979FF")   // blue — neutral/informational
        };

        // ── Summary Properties ─────────────────────────────────────────────
        private decimal _weekEarnings;
        private decimal _weekFuel;
        private decimal _weekOwner;
        private decimal _weekDriver;
        private decimal _avgDaily;
        private int     _totalSettlements;

        public decimal WeekEarnings      { get => _weekEarnings;      set => SetProperty(ref _weekEarnings, value); }
        public decimal WeekFuel          { get => _weekFuel;          set => SetProperty(ref _weekFuel, value); }
        public decimal WeekOwner         { get => _weekOwner;         set => SetProperty(ref _weekOwner, value); }
        public decimal WeekDriver        { get => _weekDriver;        set => SetProperty(ref _weekDriver, value); }
        public decimal AvgDaily          { get => _avgDaily;          set => SetProperty(ref _avgDaily, value); }
        public int     TotalSettlements  { get => _totalSettlements;  set => SetProperty(ref _totalSettlements, value); }

        public ICommand RefreshCommand { get; }

        public AnalyticsViewModel(ISettlementRepository settlementRepo)
        {
            _settlementRepo = settlementRepo;
            Title           = "Analytics";
            RefreshCommand  = new Command(async () => await LoadAsync());
        }

        public async Task LoadAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                // BUG-03: fetch ALL settlements ONCE; filter in-memory for each day.
                // Previously called GetSettlementsByDateAsync 7 times (each was a full table scan)
                // + GetAllSettlementsAsync once = 8 total full scans. Now it's 1 scan.
                var allSettlements = await _settlementRepo.GetAllSettlementsAsync();

                var today = DateTime.Today;
                var days  = Enumerable.Range(0, 7).Select(i => today.AddDays(-6 + i)).ToList();

                var earnings = new List<float>();
                var fuel     = new List<float>();
                var owner    = new List<float>();
                var labels   = days.Select(d => d.ToString("dd/MM")).ToList();

                foreach (var day in days)
                {
                    // M1 fix: s.Date is stored as a plain local date (Kind=Unspecified).
                    // Calling .ToLocalTime() on Unspecified treats it as UTC and applies the
                    // local offset — in negative-UTC timezones this shifts the date to the
                    // previous day. Compare .Date directly instead.
                    var items = allSettlements
                        .Where(s => s.Date.Date == day.Date)
                        .ToList();
                    // BUG-FIX: TotalOperatorBill → TotalIncome (normalized model)
                    earnings.Add((float)items.Sum(s => s.TotalIncome));
                    // BUG-FIX: TotalCNG not on model — CNG cost is split and stored as OwnerCngShare
                    fuel.Add((float)items.Sum(s => s.OwnerCngShare));
                    // BUG-FIX: Old profitCalc call used 7 removed fields.
                    // OwnerNetProfit = TotalIncome - DriverShare - OwnerCngShare - TotalOwnerExpenses
                    // D6-4: use canonical helper — eliminates the duplicated inline formula.
                    owner.Add((float)items.Sum(s => CalcOwnerProfit(s)));
                }

                // Assign to charts
                EarningsChart.Values = earnings;
                EarningsChart.Labels = labels;
                FuelChart.Values     = fuel;
                FuelChart.Labels     = labels;
                OwnerChart.Values    = owner;
                OwnerChart.Labels    = labels;

                // Summary totals — reuse the already-fetched allSettlements
                // M1 fix: same direct .Date comparison (see chart loop comment above).
                var weekItems = allSettlements
                    .Where(s => s.Date.Date >= today.AddDays(-6).Date).ToList();

                // BUG-FIX: map to current model properties
                WeekEarnings     = weekItems.Sum(s => s.TotalIncome);
                WeekFuel         = weekItems.Sum(s => s.OwnerCngShare);
                WeekOwner        = weekItems.Sum(s => CalcOwnerProfit(s));
                // BUG-FIX: DriverNetHaq → NetDriverPayable (stored field, positive = driver earns)
                WeekDriver       = weekItems.Sum(s => Math.Abs(s.NetDriverPayable));
                TotalSettlements = weekItems.Count;

                // BUG-12: divide by actual operating days (days with ≥1 settlement),
                // not the hardcoded 7, so zero-settlement days don't dilute the average.
                int operatingDays = weekItems
                    .Select(s => s.Date.ToLocalTime().Date)
                    .Distinct()
                    .Count();
                AvgDaily = operatingDays > 0 ? Math.Round(WeekEarnings / operatingDays, 0) : 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AnalyticsViewModel] Load error: {ex.Message}");
            }
            finally { IsBusy = false; }
        }

    /// <summary>
    /// D6-4: Single canonical owner-profit formula.
    /// OwnerNetProfit = TotalIncome − DriverShare − OwnerCngShare − TotalOwnerExpenses.
    /// A future formula change only needs to be made in one place.
    /// </summary>
    private static decimal CalcOwnerProfit(Settlement s)
        => s.TotalIncome - s.DriverShare - s.OwnerCngShare - s.TotalOwnerExpenses;
    }
}
