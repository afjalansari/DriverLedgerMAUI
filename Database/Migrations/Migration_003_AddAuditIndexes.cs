using SQLite;

namespace DriverLedger.Database.Migrations
{
    /// <summary>
    /// Migration 003 — Add Performance Indexes.
    ///
    /// Creates composite and single-column indexes that eliminate table scans on the
    /// hot-path queries (settlement list, ledger list, date-range filters).
    ///
    /// SAFETY:
    ///   • All statements use CREATE INDEX IF NOT EXISTS — completely idempotent.
    ///   • Indexes NEVER affect data — only query performance.
    ///   • Safe to run on databases of any size; SQLite builds indexes transactionally.
    ///
    /// Indexes created:
    ///
    ///   idx_settlements_date       — Settlements(Date)
    ///                                Speeds up daily/monthly summary queries.
    ///
    ///   idx_settlements_driver     — Settlements(DriverId)
    ///                                Speeds up per-driver settlement lookup.
    ///
    ///   idx_settlements_vehicle    — Settlements(VehicleId)
    ///                                Speeds up per-vehicle earning reports.
    ///
    ///   idx_settlements_date_drv_veh — Settlements(Date, DriverId, VehicleId, ShiftType)
    ///                                Composite for duplicate-check query in SaveNewAsync.
    ///
    ///   idx_ledger_driver_date     — DriverLedger(DriverId, Date)
    ///                                Speeds up per-driver ledger fetch + rebalance sort.
    ///
    ///   idx_platform_settlement    — PlatformIncomes(SettlementId)
    ///                                Speeds up LoadCollectionsAsync child-row fetch.
    ///
    ///   idx_expenses_settlement    — SettlementExpenses(SettlementId)
    ///                                Speeds up LoadCollectionsAsync expense-row fetch.
    /// </summary>
    public sealed class Migration_003_AddAuditIndexes : MigrationBase
    {
        public override int    Version => 3;
        public override string Name    => "AddPerformanceIndexes";

        public override void Up(SQLiteConnection conn)
        {
            // ── Settlements ──────────────────────────────────────────────────
            CreateIndexIfNotExists(conn,
                indexName : "idx_settlements_date",
                table     : "Settlements",
                columns   : "Date");

            CreateIndexIfNotExists(conn,
                indexName : "idx_settlements_driver",
                table     : "Settlements",
                columns   : "DriverId");

            CreateIndexIfNotExists(conn,
                indexName : "idx_settlements_vehicle",
                table     : "Settlements",
                columns   : "VehicleId");

            // Composite index used by the duplicate-prevention check
            // (Date + DriverId + VehicleId + ShiftType all in one scan).
            CreateIndexIfNotExists(conn,
                indexName : "idx_settlements_duplicate_check",
                table     : "Settlements",
                columns   : "Date, DriverId, VehicleId, ShiftType");

            // ── DriverLedger ─────────────────────────────────────────────────
            CreateIndexIfNotExists(conn,
                indexName : "idx_ledger_driver_date",
                table     : "DriverLedger",
                columns   : "DriverId, Date");

            // ── Child Record Tables ──────────────────────────────────────────
            CreateIndexIfNotExists(conn,
                indexName : "idx_platform_settlement",
                table     : "PlatformIncomes",
                columns   : "SettlementId");

            CreateIndexIfNotExists(conn,
                indexName : "idx_expenses_settlement",
                table     : "SettlementExpenses",
                columns   : "SettlementId");

            System.Diagnostics.Debug.WriteLine(
                "[Migration 003] 7 performance indexes ensured.");
        }
    }
}
