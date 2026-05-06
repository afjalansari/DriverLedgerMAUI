using Microsoft.Maui.Graphics;

namespace DriverLedger.DTOs
{
    /// <summary>
    /// Display-ready driver ledger balance row.
    /// Moved from DriverLedgerListViewModel to DTOs/ in Step 4 of the architecture refactor.
    /// </summary>
    public class DriverBalanceSummary
    {
        public int     DriverId     { get; set; }
        public string  DriverName   { get; set; } = string.Empty;
        public string  MobileNumber { get; set; } = string.Empty;
        public decimal Balance      { get; set; }

        // ── Display helpers ───────────────────────────────────────────────

        public string BalanceDisplay    => $"₹{Math.Abs(Balance):N0}";
        public string AbsBalanceDisplay => $"₹{Math.Abs(Balance):N0}";

        // ── Driver-Friendly BILINGUAL Status ────────────────────────────

        /// <summary>Hindi status: "Driver ko dena hai" / "Driver se lena hai" / "Sab clear hai"</summary>
        public string StatusText => Balance > 0
            ? "Driver ko dena hai"
            : Balance < 0
                ? "Driver se lena hai"
                : "Sab clear hai ✅";

        /// <summary>English status: "You owe driver" / "Driver owes you" / "All settled"</summary>
        public string StatusTextEnglish => Balance > 0
            ? "You owe driver"
            : Balance < 0
                ? "Driver owes you"
                : "All settled";

        public string StatusLine => Balance > 0
            ? $"Driver ko ₹{Balance:N0} dena hai"
            : Balance < 0
                ? $"Driver se ₹{Math.Abs(Balance):N0} lena hai"
                : "✅ Clear — koi pending nahi";

        public string ClearButtonLabel => Balance > 0
            ? $"Pay ₹{Balance:N0}"
            : Balance < 0
                ? $"Collect ₹{Math.Abs(Balance):N0}"
                : "✅ Sab Clear Hai";

        // ── Visibility flags ──────────────────────────────────────────────
        public bool HasPending    => Balance != 0;
        public bool IsDriverOwing => Balance < 0;  // driver owes owner (negative balance)
        public bool IsOwnerOwing  => Balance > 0;   // owner owes driver (positive balance)
        public bool IsSettled     => Balance == 0;

        // ── Colors ────────────────────────────────────────────────────────

        /// <summary>Green = Owner owes driver, Red = Driver owes owner, Gray = settled</summary>
        public Color BalanceColor => Balance > 0 ? Color.FromArgb("#EF5350") :
                                     Balance < 0 ? Color.FromArgb("#4CAF50") :
                                                   Color.FromArgb("#78909C");

        /// <summary>Status dot color for the driver list card</summary>
        public Color StatusDotColor => Balance > 0 ? Color.FromArgb("#EF5350") :
                                       Balance < 0 ? Color.FromArgb("#4CAF50") :
                                                     Color.FromArgb("#78909C");

        public Color StatusBadgeColor => Balance > 0 ? Color.FromArgb("#2A0808") :
                                         Balance < 0 ? Color.FromArgb("#0A2A0A") :
                                                       Color.FromArgb("#162032");

        public Color StatusBadgeTextColor => Balance > 0 ? Color.FromArgb("#EF5350") :
                                             Balance < 0 ? Color.FromArgb("#4CAF50") :
                                                           Color.FromArgb("#78909C");
    }
}
