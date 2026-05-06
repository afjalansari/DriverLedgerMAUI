using DriverLedger.Models;
using Microsoft.Maui.Graphics;

namespace DriverLedger.DTOs
{
    /// <summary>
    /// Flat display record combining Settlement data with resolved vehicle/driver names.
    /// All display-ready properties live here so XAML stays clean.
    /// Moved from SettlementListViewModel to DTOs/ in Step 4 of the architecture refactor.
    /// </summary>
    public class SettlementRecord
    {
        public Settlement Settlement { get; set; } = new();
        public string VehicleNumber { get; set; } = "—";
        public string DriverName    { get; set; } = "—";

        // ── Raw shortcuts (current normalized model) ─────────────────────────
        public int      Id                 => Settlement.Id;
        public DateTime Date               => Settlement.Date.ToLocalTime();
        public string   ShiftType          => Settlement.ShiftType;
        // BUG-FIX: TotalOperatorBill → TotalIncome
        public decimal  TotalOperatorBill  => Settlement.TotalIncome;
        public decimal  TotalCashCollected => Settlement.TotalCashCollected;
        // BUG-H fix: renamed TotalCNG → OwnerCngShare — this field is only the owner's
        // share of fuel, NOT the total fleet CNG (which is not stored on the Settlement model).
        public decimal  OwnerCngShare      => Settlement.OwnerCngShare;
        // BUG-FIX: DriverNetHaq/DriverPaysOwner/OwnerPaysDriver → derive from NetDriverPayable
        public decimal  DriverNetHaq       => Math.Abs(Settlement.NetDriverPayable);
        public decimal  DriverPaysOwner    => Settlement.NetDriverPayable < 0 ? Math.Abs(Settlement.NetDriverPayable) : 0m;
        public decimal  OwnerPaysDriver    => Settlement.NetDriverPayable > 0 ? Settlement.NetDriverPayable : 0m;

        // ── Display helpers ───────────────────────────────────────────────

        /// <summary>e.g. "24 Mar 2025"</summary>
        public string DateDisplay => Date.ToString("dd MMM yyyy");

        /// <summary>e.g. "Mon, 24 Mar"</summary>
        public string DateShort => Date.ToString("ddd, dd MMM");

        /// <summary>Shift badge colour — Day = amber, Night = indigo.</summary>
        public Color ShiftBadgeColor => ShiftType == ShiftTypes.Night
            ? Color.FromArgb("#3949AB")
            : Color.FromArgb("#F9A825");

        /// <summary>One-line result label shown on list card.</summary>
        public string SettlementDirectionLabel
        {
            get
            {
                var net = Settlement.NetDriverPayable;
                if (net < 0) return $"Driver pays ₹{Math.Abs(net):N0}";
                if (net > 0) return $"Owner pays ₹{net:N0}";
                return "Hisaab Barabar ✅";
            }
        }

        /// <summary>Colour for the settlement direction label.</summary>
        public Color SettlementDirectionColor
        {
            get
            {
                var net = Settlement.NetDriverPayable;
                if (net < 0) return Color.FromArgb("#FFA726"); // driver pays — orange
                if (net > 0) return Color.FromArgb("#66BB6A"); // owner pays  — green
                return Color.FromArgb("#4CAF50");               // even         — green
            }
        }

        /// <summary>IsVisible binding — driver needs to pay owner.</summary>
        public bool IsDriverPaying => Settlement.NetDriverPayable < 0;

        /// <summary>IsVisible binding — owner needs to pay driver.</summary>
        public bool IsOwnerPaying  => Settlement.NetDriverPayable > 0;
    }
}
