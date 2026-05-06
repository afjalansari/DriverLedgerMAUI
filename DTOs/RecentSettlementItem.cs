namespace DriverLedger.DTOs
{
    /// <summary>
    /// Flat DTO for the "Recent Settlements" CollectionView on the dashboard.
    /// Moved from the inline class in DashboardViewModel to DTOs/ in Step 4.
    /// </summary>
    public class RecentSettlementItem
    {
        // Raw Data (set by DashboardViewModel)
        public int     Id              { get; set; }
        public string  DateDisplay     { get; set; } = string.Empty;
        public string  VehicleNumber   { get; set; } = string.Empty;
        public string  DriverName      { get; set; } = string.Empty;
        public decimal TotalIncome         { get; set; }
        public decimal DriverShare          { get; set; }
        public decimal TotalCashCollected   { get; set; }
        public decimal TotalOwnerExpenses   { get; set; }
        public decimal NetDriverPayable     { get; set; }

        // Settlement Direction
        public bool IsOwnerPaying  => NetDriverPayable > 0;
        public bool IsDriverPaying => NetDriverPayable < 0;

        // Status
        public string Status    => (NetDriverPayable != 0) ? "Pending" : "Settled";
        public bool   IsPending => NetDriverPayable != 0;
        public bool   IsSettled => !IsPending;

        public string StatusBadgeColor => IsSettled ? "#0D2A14" : "#2A1400";
        public string StatusTextColor  => IsSettled ? "#4CAF50" : "#FFA726";

        // Settlement Amount (Absolute)
        public decimal SettlementAmount => Math.Abs(NetDriverPayable);

        /// <summary>Bilingual settlement result: Hindi + English context</summary>
        public string SettlementResult
        {
            get
            {
                if (NetDriverPayable > 0) return $"₹{NetDriverPayable:N0} (Driver ko milega)";
                if (NetDriverPayable < 0) return $"₹{Math.Abs(NetDriverPayable):N0} (Driver ko dena hai)";
                return "✅ Hisaab Barabar";
            }
        }

        /// <summary>Color: Green = Driver gets paid, Red = Driver pays owner</summary>
        public string ResultColor =>
            NetDriverPayable > 0 ? "#4CAF50" :
            NetDriverPayable < 0 ? "#EF5350" : "#78909C";

        // Net Haq Display (Alias for DriverShare)
        public string NetHaqDisplay => $"{DriverShare:N0}";
    }
}
