namespace DriverLedger.Services
{
    /// <summary>
    /// Calculates expense splits and driver net earning (DriverNetHaq).
    ///
    /// Verified formula (2026-04-09):
    ///
    ///   Driver Share        = (TotalOperatorBill - TotalCNG) / 2
    ///                       = TotalOperatorBill × DriverIncomePercent%  − TotalCNG × DriverCngPercent%
    ///                         (equivalent when both splits are 50/50)
    ///
    ///   DriverNetHaq        = DriverShare + OwnerExpenses − DriverFaultChallan
    ///
    ///   CashWithDriver      = TotalCashCollected − TotalCNG     [computed in SettlementEngine]
    ///
    ///   NetHaq              = DriverNetHaq − CashWithDriver
    ///     + → Owner pays Driver
    ///     − → Driver pays Owner
    ///
    /// ⚠️  CNG must NOT be added to DriverNetHaq.
    ///     It is already deducted once from TotalCashCollected (CashWithDriver = Cash - CNG).
    ///     Adding it to income would count it twice, over-crediting the driver by TotalCNG.
    ///
    /// Rules:
    ///   - DriverFaultChallan (signal jump, overspeeding)  → reduces DriverNetHaq.
    ///   - OwnerExpenses (Parking/Toll/Repair/Misc/OwnerChallan) → ADDED to DriverNetHaq
    ///     because when the owner bears a cost it increases what the owner owes the driver
    ///     at settlement (reduces what driver needs to hand back).
    /// </summary>
    public class ExpenseEngine
    {
        /// <summary>
        /// Computes the driver's share of CNG cost.
        /// DriverCngShare = TotalCNG × DriverCngPercent / 100
        /// </summary>
        public decimal ComputeDriverCngShare(decimal totalCng, decimal driverCngPercent)
        {
            var pct = Math.Clamp(driverCngPercent, 0m, 100m);
            return Math.Round(totalCng * pct / 100m, 2);
        }

        /// <summary>
        /// Computes the owner's share of CNG cost.
        /// OwnerCngShare = TotalCNG × OwnerCngPercent / 100
        /// </summary>
        public decimal ComputeOwnerCngShare(decimal totalCng, decimal ownerCngPercent)
        {
            var pct = Math.Clamp(ownerCngPercent, 0m, 100m);
            return Math.Round(totalCng * pct / 100m, 2);
        }

        /// <summary>
        /// Computes the driver's net haq (settlement entitlement).
        ///
        ///   DriverNetHaq = DriverIncomeShare − DriverCngShare + OwnerExpenses − DriverFaultChallan
        ///
        /// Owner expenses (Parking/Toll/Repair/Misc/OwnerChallan) are ADDED here because
        /// they represent costs the owner absorbs — at settlement they reduce how much
        /// the driver owes back, effectively transferring those costs onto the owner's side.
        ///
        /// ⚠️  Do NOT add TotalCNG here. CNG is already handled by
        ///     SettlementEngine.ComputeCashAfterFuel (CashWithDriver = Cash − CNG).
        ///     Adding it here would double-count CNG in the driver's favour.
        ///
        /// BUG-D fix: No longer floors at 0. A negative value means the driver's
        /// expenses exceeded their income share — the settlement engine handles
        /// this correctly by increasing what the driver owes the owner.
        /// </summary>
        /// <param name="driverIncomeShare">Driver's income portion (Bill × DriverIncomePercent%).</param>
        /// <param name="driverCngShare">Driver's CNG portion (TotalCNG × DriverCngPercent%).</param>
        /// <param name="ownerExpenses">Sum of all owner-side expenses: OwnerFaultChallan + Parking + Toll + Repair + Misc.</param>
        /// <param name="driverFaultChallan">Challan chargeable to driver.</param>
        /// <returns>Driver net haq — can be negative if expenses exceed income share.</returns>
        public decimal ComputeDriverNetHaq(
            decimal driverIncomeShare,
            decimal driverCngShare,
            decimal ownerExpenses,
            decimal driverFaultChallan)
        {
            var haq = driverIncomeShare
                    - driverCngShare
                    + Math.Max(0m, ownerExpenses)        // owner costs benefit driver at settlement
                    - Math.Max(0m, driverFaultChallan);
            return Math.Round(haq, 2);
        }
    }
}

