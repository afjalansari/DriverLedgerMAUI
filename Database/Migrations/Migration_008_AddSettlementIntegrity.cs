using SQLite;

namespace DriverLedger.Database.Migrations
{
    /// <summary>
    /// Phase 3 — Immutable Accounting Safety.
    ///
    /// Adds three columns to the Settlements table to enable tamper detection
    /// and historical reproducibility validation:
    ///
    ///   CalculationHash  — SHA-256 of the canonical financial inputs at save time.
    ///                      A mismatch at read time indicates the row was tampered with
    ///                      outside the application (e.g. direct SQLite edit).
    ///
    ///   RevisionNumber   — Starts at 0 (original save). Increments by 1 on every
    ///                      legitimate edit via SqliteUnitOfWork. Provides an ordered
    ///                      revision history for dispute resolution.
    ///
    ///   FormulaVersion   — Mirrors SettlementCalculator.CurrentVersion at save time.
    ///                      Allows the UI to warn when a settlement was calculated with
    ///                      an older formula and the result may differ if recalculated.
    ///
    /// Safe on:
    ///   • Existing rows  — all three columns default to empty/0 which is a valid
    ///                      "legacy / not yet stamped" sentinel. SettlementIntegrityService
    ///                      treats empty CalculationHash as "not verified" (no false alarms).
    ///   • Re-run         — ALTER TABLE ADD COLUMN is idempotent when wrapped in the
    ///                      MigrationBase.AddColumnIfNotExists helper.
    /// </summary>
    public class Migration_008_AddSettlementIntegrity : MigrationBase
    {
        public override int    Version => 8;
        public override string Name    => "AddSettlementIntegrity";

        public override void Up(SQLiteConnection conn)
        {
            // Use AddColumnIfNotExists (MigrationBase helper) — safe to re-run.
            AddColumnIfNotExists(conn, "Settlements", "CalculationHash",  "TEXT    NOT NULL DEFAULT ''");
            AddColumnIfNotExists(conn, "Settlements", "RevisionNumber",   "INTEGER NOT NULL DEFAULT 0");
            AddColumnIfNotExists(conn, "Settlements", "FormulaVersion",   "INTEGER NOT NULL DEFAULT 0");

            // Index supporting "find all settlements with a given formula version"
            // — used by the admin Diagnostics screen to flag settlements that need review.
            conn.Execute(@"
                CREATE INDEX IF NOT EXISTS idx_settlements_formula_version
                    ON Settlements (FormulaVersion)
                    WHERE IsDeleted = 0;");
        }
    }
}
