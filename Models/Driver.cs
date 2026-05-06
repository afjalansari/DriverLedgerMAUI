using SQLite;

namespace DriverLedger.Models
{
    [Table("Drivers")]
    public class Driver
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [NotNull, MaxLength(200)]
        public string DriverName { get; set; } = string.Empty;

        [NotNull, MaxLength(20)]
        public string MobileNumber { get; set; } = string.Empty;

        [MaxLength(50)]
        public string LicenseNumber { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Address { get; set; } = string.Empty;

        public DateTime JoinDate { get; set; } = DateTime.UtcNow;

        /// <summary>Active or Inactive</summary>
        [MaxLength(20)]
        public string Status { get; set; } = DriverStatus.Active;

        // ── Contract Settings ────────────────────────────────────────────
        /// <summary>Driver's percentage share of total operator bill income (0–100).</summary>
        public decimal DriverIncomePercent { get; set; } = 50m;

        /// <summary>Owner's percentage share of total operator bill income (0–100). Must equal 100 − DriverIncomePercent.</summary>
        public decimal OwnerIncomePercent { get; set; } = 50m;

        /// <summary>Driver's percentage share of CNG / fuel cost (0–100).</summary>
        public decimal DriverCngPercent { get; set; } = 50m;

        /// <summary>Owner's percentage share of CNG / fuel cost (0–100). Must equal 100 − DriverCngPercent.</summary>
        public decimal OwnerCngPercent { get; set; } = 50m;
    }

    public static class DriverStatus
    {
        public const string Active = "Active";
        public const string Inactive = "Inactive";

        public static readonly List<string> All = new() { Active, Inactive };
    }
}

