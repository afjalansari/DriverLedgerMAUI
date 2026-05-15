using DriverLedger.Models;
using SQLite;

namespace DriverLedger.Database.Migrations
{
    /// <summary>
    /// Migration 001 — Initial Schema.
    ///
    /// Creates all 8 core application tables using sqlite-net-pcl's
    /// <c>CreateTable&lt;T&gt;()</c> which maps to CREATE TABLE IF NOT EXISTS.
    ///
    /// SAFETY: Completely idempotent. If all tables already exist (e.g., the user is
    /// upgrading from an older version that used raw CreateTableAsync calls), this
    /// migration is a no-op — no existing rows are touched, no columns are dropped.
    ///
    /// Tables created:
    ///   Companies, Drivers, Vehicles, VehicleDrivers,
    ///   Settlements, PlatformIncomes, SettlementExpenses, DriverLedger
    /// </summary>
    public sealed class Migration_001_InitialSchema : MigrationBase
    {
        public override int    Version => 1;
        public override string Name    => "InitialSchema";

        public override void Up(SQLiteConnection conn)
        {
            // Each CreateTable<T> call is idempotent:
            //   • Creates the table if it doesn't exist.
            //   • Adds any new columns defined on the model if the table already exists
            //     but the column is missing (sqlite-net-pcl migration mode).
            //   • NEVER drops existing columns or rows.

            conn.CreateTable<Company>();
            conn.CreateTable<Driver>();
            conn.CreateTable<Vehicle>();
            conn.CreateTable<VehicleDriver>();
            conn.CreateTable<Settlement>();
            conn.CreateTable<PlatformIncome>();
            conn.CreateTable<SettlementExpense>();
            conn.CreateTable<DriverLedgerEntry>();

            System.Diagnostics.Debug.WriteLine(
                "[Migration 001] All 8 core tables ensured.");
        }
    }
}
