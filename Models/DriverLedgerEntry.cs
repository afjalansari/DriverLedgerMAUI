using SQLite;

namespace DriverLedger.Models
{
    [Table("DriverLedger")]
    public class DriverLedgerEntry
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int DriverId { get; set; }

        [Indexed]
        public DateTime Date { get; set; } = DateTime.UtcNow;

        public int VehicleId { get; set; }

        /// <summary>Day / Night — from settlement; empty for Advance/Payment</summary>
        [MaxLength(10)]
        public string ShiftType { get; set; } = string.Empty;

        /// <summary>FK to Settlements.Id when TransactionType = Settlement; 0 otherwise</summary>
        public int SettlementId { get; set; }

        /// <summary>Settlement / Advance / Payment</summary>
        [MaxLength(20)]
        public string TransactionType { get; set; } = TransactionTypes.Settlement;

        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        /// <summary>Money given TO driver (owner owes driver)</summary>
        public decimal Debit { get; set; }

        /// <summary>Money received FROM driver (driver owes owner)</summary>
        public decimal Credit { get; set; }

        /// <summary>Running balance — positive means driver is owed money; negative means driver owes owner</summary>
        public decimal Balance { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public static class TransactionTypes
    {
        public const string Settlement = "Settlement";
        public const string Advance    = "Advance";
        public const string Payment    = "Payment";
        /// <summary>
        /// Used when an outstanding balance is cleared manually — either:
        ///   owner collects from driver (Credit entry), or
        ///   owner pays driver what's owed (Debit entry).
        /// Distinct from Advance (which is a pre-settlement advance) and
        /// Payment (which is a regular cash receipt from the driver).
        /// </summary>
        public const string Clearance  = "Clearance";

        public static readonly List<string> All = new() { Settlement, Advance, Payment, Clearance };
    }
}

