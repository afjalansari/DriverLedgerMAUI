using SQLite;

namespace DriverLedger.Database.Migrations
{
    /// <summary>
    /// Migration 005 — Add VehicleDrivers Performance Indexes.
    ///
    /// Background:
    ///   Migration 003 created 7 indexes for Settlements, DriverLedger, PlatformIncomes,
    ///   and SettlementExpenses. The VehicleDrivers table was omitted — it was assumed to
    ///   be a small lookup table, but it is queried on every vehicle/shift change in
    ///   SettlementEntryPage (driver auto-lookup) and on every AssignDriverPage load.
    ///
    /// Indexes created:
    ///
    ///   idx_vehicledrivers_vehicle  — VehicleDrivers(VehicleId)
    ///     Speeds up GetAssignmentsForVehicleAsync (list all shifts for a vehicle).
    ///
    ///   idx_vehicledrivers_shift    — VehicleDrivers(VehicleId, ShiftType)
    ///     Composite index for GetAssignmentAsync — the hot-path query in
    ///     SettlementEntryViewModel.LookupDriverAsync, called on every picker change.
    ///     Covers the unique-assignment business rule lookup in SaveAssignmentAsync.
    ///
    /// SAFETY:
    ///   • Both statements use CREATE INDEX IF NOT EXISTS — completely idempotent.
    ///   • Indexes NEVER affect stored data — only query performance.
    ///   • Safe to run on databases of any size.
    /// </summary>
    public sealed class Migration_005_AddVehicleDriverIndex : MigrationBase
    {
        public override int    Version => 5;
        public override string Name    => "AddVehicleDriverIndex";

        public override void Up(SQLiteConnection conn)
        {
            // Single-column index: covers GetAssignmentsForVehicleAsync
            CreateIndexIfNotExists(conn,
                indexName : "idx_vehicledrivers_vehicle",
                table     : "VehicleDrivers",
                columns   : "VehicleId");

            // Composite index: covers GetAssignmentAsync (vehicle + shift lookup)
            // and the uniqueness check in SaveAssignmentAsync.
            CreateIndexIfNotExists(conn,
                indexName : "idx_vehicledrivers_shift",
                table     : "VehicleDrivers",
                columns   : "VehicleId, ShiftType");

            System.Diagnostics.Debug.WriteLine(
                "[Migration 005] 2 VehicleDrivers indexes ensured.");
        }
    }
}
