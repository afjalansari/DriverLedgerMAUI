using SQLite;

namespace DriverLedger.Models
{
    [Table("VehicleDrivers")]
    public class VehicleDriver
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public int VehicleId { get; set; }

        public int DriverId { get; set; }

        /// <summary>Day or Night</summary>
        [MaxLength(10)]
        public string ShiftType { get; set; } = ShiftTypes.Day;

        public DateTime AssignedDate { get; set; } = DateTime.UtcNow;
    }

    public static class ShiftTypes
    {
        public const string Day = "Day";
        public const string Night = "Night";

        public static readonly List<string> All = new() { Day, Night };
    }
}

