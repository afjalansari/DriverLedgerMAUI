namespace DriverLedger.Services
{
    /// <summary>
    /// Calculates owner net profit from a settlement's financial fields.
    ///
    /// Formula:
    ///   OwnerNetProfit = OwnerIncomeShare
    ///                  − OwnerCngShare
    ///                  − OwnerFaultChallan
    ///                  − Parking
    ///                  − Toll
    ///                  − Repair
    ///                  − Miscellaneous
    ///
    /// Centralising this prevents the formula from drifting between
    /// DashboardSummaryService, AnalyticsViewModel, and any future consumers.
    /// </summary>
    public interface IProfitCalculationService
    {
        /// <summary>
        /// Computes owner net profit for a single settlement row.
        /// All input values must already be non-negative (as stored in the DB).
        /// BUG-M fix: result can be negative — indicates owner had a loss on this settlement
        /// (expenses exceeded income share).
        /// </summary>
        decimal ComputeOwnerNetProfit(
            decimal ownerIncomeShare,
            decimal ownerCngShare,
            decimal ownerFaultChallan,
            decimal parking,
            decimal toll,
            decimal repair,
            decimal miscellaneous);

        /// <summary>
        /// Sums owner-side expenses for a single settlement row (no income share subtracted).
        /// Used to surface the raw expense total on the Dashboard expense card.
        /// </summary>
        decimal ComputeOwnerExpenses(
            decimal ownerFaultChallan,
            decimal parking,
            decimal toll,
            decimal repair,
            decimal miscellaneous);
    }
}
