using SQLite;

namespace DriverLedger.Models
{
    /// <summary>
    /// Immutable record of a single create / update / delete event on a
    /// financial entity (Settlement or DriverLedgerEntry).
    ///
    /// DESIGN:
    ///   • Written once — never updated or deleted by application code.
    ///   • <see cref="SnapshotJson"/> holds the full JSON of the entity state at the
    ///     time of the event so disputes can be resolved from history alone.
    ///   • <see cref="ChangeSummary"/> is a compact human-readable diff (e.g.
    ///     "TotalIncome ₹12,000→₹14,500; NetDriverPayable ₹1,200→₹2,500") for
    ///     fast display in the activity feed without deserialising JSON.
    ///   • Indexes on DriverId+Timestamp and EntityType+EntityId are added in
    ///     Migration_006 for fast per-driver and per-settlement queries.
    /// </summary>
    [Table("AuditLog")]
    public class AuditLog
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        /// <summary>UTC timestamp of the event.</summary>
        // L4 fix: [Indexed] removed — AuditLog is created by Migration_006 via raw DDL
        // (not CreateTable<T>()), so sqlite-net never reads [Indexed] attributes here.
        // Actual index: idx_audit_timestamp (Timestamp DESC) — see Migration_006.
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>"Settlement" | "LedgerEntry"</summary>
        // Actual index: idx_audit_entity (EntityType, EntityId, Timestamp DESC) — see Migration_006.
        [MaxLength(30)]
        public string EntityType { get; set; } = string.Empty;

        /// <summary>PK of the affected row in its own table.</summary>
        // Covered by idx_audit_entity composite index.
        public int EntityId { get; set; }

        /// <summary>"Create" | "Update" | "Delete"</summary>
        [MaxLength(10)]
        public string Action { get; set; } = string.Empty;

        /// <summary>FK to Drivers.Id — allows per-driver audit queries.</summary>
        // Actual index: idx_audit_driver_time (DriverId, Timestamp DESC) — see Migration_006.
        public int DriverId { get; set; }

        /// <summary>Driver name snapshot at the time of the event.</summary>
        [MaxLength(100)]
        public string DriverName { get; set; } = string.Empty;

        /// <summary>
        /// Compact human-readable description of what changed.
        /// For Create: "Settlement created ₹{TotalIncome:N0} bill, ₹{NetDriverPayable:N0} net".
        /// For Update: "TotalIncome ₹12,000→₹14,500; NetDriverPayable ₹1,200→₹2,500".
        /// For Delete: "Settlement deleted — ₹{TotalIncome:N0} bill on {Date:dd MMM}".
        /// </summary>
        [MaxLength(500)]
        public string ChangeSummary { get; set; } = string.Empty;

        /// <summary>
        /// Full JSON snapshot of the entity at the time of the event.
        /// For Update, this is the state AFTER the change (pre-state is derivable
        /// from the previous AuditLog row for the same EntityId).
        /// Empty string for Delete events (data already gone from main table).
        /// </summary>
        public string SnapshotJson { get; set; } = string.Empty;
    }

    /// <summary>Constants for <see cref="AuditLog.Action"/> values.</summary>
    public static class AuditActions
    {
        public const string Create = "Create";
        public const string Update = "Update";
        public const string Delete = "Delete";
    }

    /// <summary>Constants for <see cref="AuditLog.EntityType"/> values.</summary>
    public static class AuditEntities
    {
        public const string Settlement  = "Settlement";
        public const string LedgerEntry = "LedgerEntry";
    }
}
