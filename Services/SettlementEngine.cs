namespace DriverLedger.Services
{
    /// <summary>
    /// Determines the final cash settlement direction between driver and owner.
    ///
    /// BUSINESS MODEL:
    ///   The driver pays the FULL CNG/fuel cost in cash during the day.
    ///   Cash held by driver at settlement time = TotalCashCollected − TotalCNG.
    ///
    /// SETTLEMENT FORMULA:
    ///   CashAfterFuel = TotalCashCollected − TotalCNG
    ///   (This is the real physical cash in the driver's hand after fuelling.)
    ///
    ///   Compare CashAfterFuel vs DriverNetHaq:
    ///
    ///   CASE 1 - Driver Pays Owner:
    ///     CashAfterFuel &gt; DriverNetHaq  -&gt;  DriverPaysOwner = CashAfterFuel - DriverNetHaq
    ///
    ///   CASE 2 - Owner Pays Driver:
    ///     CashAfterFuel &lt; DriverNetHaq  -&gt;  OwnerPaysDriver = DriverNetHaq - CashAfterFuel
    ///
    ///   CASE 3 - Exact:
    ///     CashAfterFuel == DriverNetHaq -&gt;  Both amounts are 0 (settled exactly).
    ///
    /// NOTE: CNG must NOT be subtracted twice.
    ///   - DriverCngShare already reduces DriverNetHaq through income split.
    ///   - TotalCNG reduces cash position (driver paid it physically).
    ///   - These are two separate effects on different sides of the settlement.
    /// </summary>
    public class SettlementEngine
    {
        /// <summary>
        /// Computes the real cash physically remaining with the driver after paying FULL CNG.
        ///
        ///   CashAfterFuel = TotalCashCollected − TotalCNG
        ///
        /// BUG-E fix: Can be negative — indicates the driver advanced their own money
        /// for fuel (CNG exceeded cash collected). The settlement engine handles this
        /// correctly by increasing what the owner owes the driver.
        ///
        /// Stored as Settlement.CashWithDriver in the database for compatibility.
        /// </summary>
        /// <param name="totalCashCollected">Total cash collected from passengers today.</param>
        /// <param name="totalCng">Full CNG/fuel paid by driver from that cash today.</param>
        public decimal ComputeCashAfterFuel(decimal totalCashCollected, decimal totalCng)
            => Math.Round(totalCashCollected - totalCng, 2);

        /// <summary>
        /// Resolves the settlement direction.
        /// </summary>
        /// <param name="cashAfterFuel">From ComputeCashAfterFuel — physical cash with driver after fuel.</param>
        /// <param name="driverNetHaq">From ExpenseEngine.ComputeDriverNetHaq — driver's net earned amount.</param>
        /// <param name="driverPaysOwner">Amount driver must hand over to owner (0 if owner pays).</param>
        /// <param name="ownerPaysDriver">Amount owner must hand over to driver (0 if driver pays).</param>
        public void Resolve(
            decimal cashAfterFuel,
            decimal driverNetHaq,
            out decimal driverPaysOwner,
            out decimal ownerPaysDriver)
        {
            var diff = Math.Round(cashAfterFuel - driverNetHaq, 2);

            if (diff > 0m)
            {
                // Driver has more cash than they earned → hand difference to owner
                driverPaysOwner = diff;
                ownerPaysDriver = 0m;
            }
            else if (diff < 0m)
            {
                // Driver earned more than cash in hand → owner tops up driver
                driverPaysOwner = 0m;
                ownerPaysDriver = Math.Abs(diff);
            }
            else
            {
                driverPaysOwner = 0m;
                ownerPaysDriver = 0m;
            }
        }
    }
}

