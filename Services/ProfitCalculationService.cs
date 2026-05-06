namespace DriverLedger.Services
{
    /// <summary>
    /// SQLite-agnostic implementation of <see cref="IProfitCalculationService"/>.
    /// Pure math — no repository dependencies.
    /// Register as Singleton in DI; it holds no mutable state.
    /// </summary>
    public class ProfitCalculationService : IProfitCalculationService
    {
        /// <inheritdoc />
        public decimal ComputeOwnerNetProfit(
            decimal ownerIncomeShare,
            decimal ownerCngShare,
            decimal ownerFaultChallan,
            decimal parking,
            decimal toll,
            decimal repair,
            decimal miscellaneous)
        {
            var expenses = ComputeOwnerExpenses(ownerFaultChallan, parking, toll, repair, miscellaneous);
            var profit   = ownerIncomeShare - ownerCngShare - expenses;
            // BUG-M fix: allow negative owner profit so losses are visible on dashboard
            // and monthly summaries aggregate correctly.
            return Math.Round(profit, 2);
        }

        /// <inheritdoc />
        public decimal ComputeOwnerExpenses(
            decimal ownerFaultChallan,
            decimal parking,
            decimal toll,
            decimal repair,
            decimal miscellaneous)
        {
            return Math.Round(
                Math.Max(0m, ownerFaultChallan) +
                Math.Max(0m, parking)           +
                Math.Max(0m, toll)              +
                Math.Max(0m, repair)            +
                Math.Max(0m, miscellaneous),
                2);
        }
    }
}
