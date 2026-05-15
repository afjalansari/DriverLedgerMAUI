using SQLite;

namespace DriverLedger.Models
{
    /// <summary>
    /// Tracks which database migrations have been applied.
    /// One row per successfully executed migration.
    /// </summary>
    [Table("SchemaVersion")]
    public class SchemaVersion
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        /// <summary>Monotonically increasing version number (1, 2, 3 …)</summary>
        public int Version { get; set; }

        /// <summary>Human-readable name matching the IMigration.Name property.</summary>
        [MaxLength(200)]
        public string MigrationName { get; set; } = string.Empty;

        /// <summary>UTC timestamp when this migration was applied.</summary>
        public DateTime AppliedAt { get; set; } = DateTime.UtcNow;

        /// <summary>True if the migration completed without error.</summary>
        public bool Success { get; set; } = true;
    }
}
