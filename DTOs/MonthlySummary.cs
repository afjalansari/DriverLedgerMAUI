namespace DriverLedger.DTOs
{
    /// <summary>Month-level aggregation: revenue, fuel, profit, and driver earnings.</summary>
    public class MonthlySummary
    {
        public string  Label          { get; init; } = string.Empty;   // "March 2026"
        public decimal OperatorBill   { get; init; }
        public decimal TotalCNG       { get; init; }
        public decimal OwnerProfit    { get; init; }
        public decimal DriverEarnings { get; init; }
    }
}
