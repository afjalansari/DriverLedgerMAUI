using SQLite;

namespace DriverLedger.Models
{
    [Table("Vehicles")]
    public class Vehicle
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [NotNull, MaxLength(30)]
        public string VehicleNumber { get; set; } = string.Empty;

        [MaxLength(100)]
        public string VehicleModel { get; set; } = string.Empty;

        /// <summary>Taxi / Private / Commercial</summary>
        [MaxLength(30)]
        public string VehicleType { get; set; } = VehicleTypes.Taxi;

        [MaxLength(50)]
        public string RegistrationNumber { get; set; } = string.Empty;

        public DateTime InsuranceExpiryDate { get; set; } = DateTime.UtcNow.AddYears(1);

        public DateTime PUCExpiryDate { get; set; } = DateTime.UtcNow.AddMonths(6);

        public int OwnerCompanyId { get; set; }

        /// <summary>Active or Inactive</summary>
        [MaxLength(20)]
        public string Status { get; set; } = VehicleStatus.Active;
    }

    public static class VehicleTypes
    {
        public const string Taxi = "Taxi";
        public const string Private = "Private";
        public const string Commercial = "Commercial";

        public static readonly List<string> All = new() { Taxi, Private, Commercial };
    }

    public static class VehicleStatus
    {
        public const string Active = "Active";
        public const string Inactive = "Inactive";

        public static readonly List<string> All = new() { Active, Inactive };
    }
}

