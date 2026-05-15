using SQLite;

namespace DriverLedger.Database.Migrations
{
    /// <summary>
    /// Contract for a single, idempotent database migration step.
    ///
    /// Rules for implementors:
    ///   • <see cref="Up"/> MUST be idempotent (safe to call twice without data loss).
    ///   • Use CREATE TABLE IF NOT EXISTS, CREATE INDEX IF NOT EXISTS,
    ///     and the <see cref="MigrationBase.AddColumnIfNotExists"/> helper for ALTER TABLE.
    ///   • NEVER DROP or RENAME columns — this destroys existing user data.
    ///   • <see cref="Version"/> MUST be unique across all registered migrations.
    ///   • <see cref="Up"/> runs inside a SQLite transaction managed by <see cref="MigrationRunner"/>.
    /// </summary>
    public interface IMigration
    {
        /// <summary>Monotonically increasing integer (1, 2, 3 …). Must be unique.</summary>
        int Version { get; }

        /// <summary>Human-readable description logged to SchemaVersion table.</summary>
        string Name { get; }

        /// <summary>
        /// Applies the migration. Called inside an open <see cref="SQLiteConnection"/>
        /// transaction by <see cref="MigrationRunner"/>. Must be idempotent.
        /// </summary>
        void Up(SQLiteConnection conn);
    }
}
