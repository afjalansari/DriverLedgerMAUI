namespace DriverLedger.Models
{
    /// <summary>Alert card displayed in the Smart Alerts section of the dashboard.</summary>
    public class DashboardAlert
    {
        public string Icon    { get; set; } = "⚠️";
        public string Title   { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;

        /// <summary>Card background color (hex).</summary>
        public string BackgroundColor { get; set; } = "#1A2A1A";

        /// <summary>Border / accent color (hex).</summary>
        public string AccentColor { get; set; } = "#4CAF50";

        /// <summary>Text color for the title (hex).</summary>
        public string TitleColor { get; set; } = "#A5D6A7";
    }
}

