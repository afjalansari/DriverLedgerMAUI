namespace DriverLedger.DTOs
{
    /// <summary>
    /// All aggregated numbers for a single day's settlements.
    /// Produced by IDashboardSummaryService and consumed by DashboardViewModel.
    /// </summary>
    public class DailyFleetSummary
    {
        // ── Fleet ────────────────────────────────────────────────────────────
        public int ActiveVehiclesCount { get; init; }
        public int ActiveDriversCount  { get; init; }
        public int TripCount           { get; init; }

        // ── Revenue ──────────────────────────────────────────────────────────
        public decimal OperatorBill    { get; init; }
        public decimal CashCollected   { get; init; }
        public decimal OnlineAmount    { get; init; }   // OperatorBill - CashCollected (≥0)

        // ── Fuel / CNG ───────────────────────────────────────────────────────
        public decimal TotalCNG        { get; init; }
        public decimal DriverCngShare  { get; init; }
        public decimal OwnerCngShare   { get; init; }

        // ── Settlement ───────────────────────────────────────────────────────
        public decimal DriverNetHaq    { get; init; }
        public decimal CashWithDrivers { get; init; }
        public decimal DriverPaysOwner { get; init; }
        public decimal OwnerPaysDriver { get; init; }
        public int     PendingCount    { get; init; }   // settlements with cash exchange

        // ── Owner Profit ──────────────────────────────────────────────────────
        public decimal OwnerIncomeShare  { get; init; }
        public decimal OwnerExpenses     { get; init; }  // Parking+Toll+Repair+Misc+Challan
        public decimal OwnerNetProfit    { get; init; }  // OwnerIncomeShare - OwnerCngShare - OwnerExpenses

        // ── Vehicle Performance ───────────────────────────────────────────────
        public string  TopEarningVehicle        { get; init; } = "—";
        public decimal TopEarningVehicleAmount  { get; init; }
        public string  LowestEarningVehicle     { get; init; } = "—";
        public decimal LowestEarningVehicleAmount { get; init; }

        // ── Driver Performance ────────────────────────────────────────────────
        public string  TopDriverName      { get; init; } = "—";
        public decimal TopDriverEarnings  { get; init; }
        public string  MostTripsDriver    { get; init; } = "—";
        public int     MostTripsCount     { get; init; }

        // ── Recent Settlements ────────────────────────────────────────────────
        public IReadOnlyList<RecentSettlementRow> RecentSettlements { get; init; } = [];
    }

    /// <summary>A single row in the "Recent Settlements" list.</summary>
    public class RecentSettlementRow
    {
        public int     Id              { get; init; }
        public string  DateDisplay     { get; init; } = string.Empty;
        public string  VehicleNumber   { get; init; } = "—";
        public string  DriverName      { get; init; } = "—";
        public decimal TotalIncome         { get; init; }
        public decimal DriverShare          { get; init; }
        public decimal TotalCashCollected   { get; init; }
        public decimal TotalOwnerExpenses   { get; init; }
        public decimal NetDriverPayable     { get; init; }

        public bool IsOwnerPaying  => NetDriverPayable > 0;
        public bool IsDriverPaying => NetDriverPayable < 0;
    }
}
