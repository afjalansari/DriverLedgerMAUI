using SQLite;

namespace DriverLedger.Database.Migrations
{
    /// <summary>
    /// Migration 004 — Add CalculatorVersion to Settlements.
    ///
    /// Records which version of <c>SettlementCalculator</c> produced each settlement,
    /// enabling historical reproducibility and future formula change auditing.
    ///
    /// SAFETY:
    ///   • Uses <see cref="MigrationBase.AddColumnIfNotExists"/> — completely idempotent.
    ///   • Existing rows receive DEFAULT 0, meaning "produced before versioning was introduced".
    ///   • New settlements produced by SettlementCalculator v1 will receive CalculatorVersion = 1.
    ///   • Historical settlement math is NEVER recalculated — the stored values remain as-is.
    ///
    /// Column definition:
    ///   CalculatorVersion  INTEGER DEFAULT 0  — version of the formula that produced this row
    /// </summary>
    public sealed class Migration_004_AddCalculatorVersion : MigrationBase
    {
        public override int    Version => 4;
        public override string Name    => "AddCalculatorVersion";

        private const string Table = "Settlements";

        public override void Up(SQLiteConnection conn)
        {
            AddColumnIfNotExists(conn, Table, "CalculatorVersion", "INTEGER DEFAULT 0 NOT NULL");

            System.Diagnostics.Debug.WriteLine(
                $"[Migration 004] CalculatorVersion ensured on '{Table}'.");
        }
    }
}
