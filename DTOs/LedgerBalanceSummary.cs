namespace DriverLedger.DTOs
{
    /// <summary>
    /// Driver-ledger balance breakdown used by the dashboard ledger card.
    /// </summary>
    public class LedgerBalanceSummary
    {
        public decimal TotalOutstandingBalance { get; init; }

        /// <summary>Number of drivers who owe the owner money.</summary>
        public int     DriversOweOwnerCount  { get; init; }
        /// <summary>Sum that drivers owe the owner (positive).</summary>
        public decimal DriversOweOwnerAmount { get; init; }

        /// <summary>Number of drivers the owner owes money to.</summary>
        public int     OwnerOwesDriverCount  { get; init; }
        /// <summary>Sum the owner owes drivers (positive magnitude).</summary>
        public decimal OwnerOwesDriverAmount { get; init; }
    }
}
