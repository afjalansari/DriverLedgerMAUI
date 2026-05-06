namespace DriverLedger.Services
{
    /// <summary>
    /// Calculates income split between driver and owner based on aggregator bills.
    /// All arithmetic uses decimal to ensure financial precision.
    /// </summary>
    public class IncomeEngine
    {
        /// <summary>
        /// Computes the total operator bill by summing all aggregator amounts.
        /// </summary>
        /// <param name="aggregatorBills">Key = aggregator name, Value = bill amount.</param>
        /// <returns>Total operator bill (sum of all values).</returns>
        public decimal ComputeTotalOperatorBill(IEnumerable<KeyValuePair<string, decimal>> aggregatorBills)
        {
            decimal total = 0m;
            foreach (var bill in aggregatorBills)
                total += Math.Max(0m, bill.Value);
            return total;
        }

        /// <summary>
        /// Computes the driver's income share.
        /// DriverIncomeShare = TotalOperatorBill × DriverIncomePercent / 100
        /// </summary>
        public decimal ComputeDriverIncomeShare(decimal totalOperatorBill, decimal driverIncomePercent)
        {
            var pct = Math.Clamp(driverIncomePercent, 0m, 100m);
            return Math.Round(totalOperatorBill * pct / 100m, 2);
        }

        /// <summary>
        /// Computes the owner's income share.
        /// OwnerIncomeShare = TotalOperatorBill × OwnerIncomePercent / 100
        /// </summary>
        public decimal ComputeOwnerIncomeShare(decimal totalOperatorBill, decimal ownerIncomePercent)
        {
            var pct = Math.Clamp(ownerIncomePercent, 0m, 100m);
            return Math.Round(totalOperatorBill * pct / 100m, 2);
        }
    }
}

