using SQLite;

namespace DriverLedger.Database.Migrations
{
    /// <summary>
    /// Phase 7 — Audit &amp; History System.
    ///
    /// Changes:
    ///   1. Creates the <c>AuditLog</c> table (idempotent via CREATE TABLE IF NOT EXISTS).
    ///   2. Adds <c>UpdatedAt TEXT</c> column to <c>Settlements</c>.
    ///   3. Creates three indexes on <c>AuditLog</c> for fast per-driver, per-entity,
    ///      and chronological feed queries.
    ///
    /// Safe on:
    ///   • Fresh install     — all CREATE IF NOT EXISTS are no-ops on second run.
    ///   • Existing v5 DB    — AuditLog is new; UpdatedAt is added to existing rows as NULL.
    ///   • Re-run            — all DDL is fully idempotent.
    /// </summary>
    public class Migration_006_AddAuditLog : MigrationBase
    {
        public override int    Version => 6;
        public override string Name    => "Add AuditLog table and Settlements.UpdatedAt column";

        public override void Up(SQLiteConnection conn)
        {
            // ── 1. AuditLog table ─────────────────────────────────────────────
            conn.Execute(@"
                CREATE TABLE IF NOT EXISTS AuditLog (
                    Id            INTEGER PRIMARY KEY AUTOINCREMENT,
                    Timestamp     TEXT    NOT NULL DEFAULT (datetime('now','utc')),
                    EntityType    TEXT    NOT NULL DEFAULT '',
                    EntityId      INTEGER NOT NULL DEFAULT 0,
                    Action        TEXT    NOT NULL DEFAULT '',
                    DriverId      INTEGER NOT NULL DEFAULT 0,
                    DriverName    TEXT    NOT NULL DEFAULT '',
                    ChangeSummary TEXT    NOT NULL DEFAULT '',
                    SnapshotJson  TEXT    NOT NULL DEFAULT ''
                )");

            // ── 2. Indexes for hot-path queries ───────────────────────────────
            // Per-driver activity feed (DriverId → ordered by Timestamp DESC)
            CreateIndexIfNotExists(conn,
                indexName: "idx_audit_driver_time",
                table:     "AuditLog",
                columns:   "DriverId, Timestamp DESC");

            // Per-settlement/entity changelog
            CreateIndexIfNotExists(conn,
                indexName: "idx_audit_entity",
                table:     "AuditLog",
                columns:   "EntityType, EntityId, Timestamp DESC");

            // Fleet-wide chronological feed
            CreateIndexIfNotExists(conn,
                indexName: "idx_audit_timestamp",
                table:     "AuditLog",
                columns:   "Timestamp DESC");

            // ── 3. Add UpdatedAt column to Settlements ────────────────────────
            AddColumnIfNotExists(conn,
                table:      "Settlements",
                column:     "UpdatedAt",
                definition: "TEXT");
        }
    }
}
