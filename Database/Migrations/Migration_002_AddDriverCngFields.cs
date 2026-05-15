using SQLite;

namespace DriverLedger.Database.Migrations
{
    /// <summary>
    /// Migration 002 — Add DriverCngShare and DriverChallanTotal to Settlements.
    ///
    /// These two columns were added to the Settlement model as part of the BUG-2 fix
    /// (sign-convention correction for driver-fault challans and CNG split tracking).
    ///
    /// SAFETY:
    ///   • Uses <see cref="MigrationBase.AddColumnIfNotExists"/> — completely idempotent.
    ///   • Existing rows automatically receive DEFAULT value 0, preserving historical data.
    ///   • Historical settlement calculations are NOT affected — these are audit-only fields
    ///     that new entries will populate going forward.
    ///
    /// Column definitions:
    ///   DriverCngShare     REAL DEFAULT 0  — driver's share of total CNG cost
    ///   DriverChallanTotal REAL DEFAULT 0  — sum of driver-fault challan deductions
    /// </summary>
    public sealed class Migration_002_AddDriverCngFields : MigrationBase
    {
        public override int    Version => 2;
        public override string Name    => "AddDriverCngFields";

        // SQLite table name from [Table("Settlements")] on the Settlement model.
        private const string Table = "Settlements";

        public override void Up(SQLiteConnection conn)
        {
            // Driver's share of today's CNG cost (used for UI display in result card).
            AddColumnIfNotExists(conn, Table, "DriverCngShare",     "REAL DEFAULT 0 NOT NULL");

            // Total driver-fault challan deducted from this settlement's net haq.
            AddColumnIfNotExists(conn, Table, "DriverChallanTotal", "REAL DEFAULT 0 NOT NULL");

            System.Diagnostics.Debug.WriteLine(
                $"[Migration 002] DriverCngShare + DriverChallanTotal ensured on '{Table}'.");
        }
    }
}
