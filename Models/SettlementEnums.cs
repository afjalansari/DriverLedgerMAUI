namespace DriverLedger.Models
{
    public enum DriverType
    {
        Hired,
        SelfDriven
    }

    public enum ExpenseType
    {
        CNG,
        Toll,
        Parking,
        Other,
        /// <summary>
        /// Traffic challan chargeable to the owner (not the driver's fault).
        /// Added in Phase 6 as a first-class type replacing the
        /// <c>Other + Name != "DriverChallan"</c> disambiguation hack.
        /// Existing rows stored as <see cref="Other"/> continue to be
        /// captured by the owner-expense filter in SettlementCalculator.
        /// </summary>
        OwnerChallan
    }
}
