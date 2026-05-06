namespace DriverLedger.Models
{
    /// <summary>
    /// Represents a single aggregator bill entry (e.g. Ola → 2000).
    /// Used as a bindable item in the settlement entry form.
    /// This is a UI-only POCO — it is serialised to JSON before being stored in Settlement.AggregatorBillsJson.
    /// </summary>
    public class AggregatorBillItem
    {
        /// <summary>Aggregator name — e.g. "Ola", "Uber", "Rapido", "OfflineTrips", "CorporateTrips".</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Bill amount for this aggregator (decimal, always non-negative).</summary>
        public decimal Amount { get; set; }
    }
}

