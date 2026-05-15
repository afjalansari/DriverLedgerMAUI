using SQLite;

namespace DriverLedger.Database.Migrations
{
    /// <summary>
    /// Abstract base class for all migrations.
    /// Provides helper methods that make SQL changes idempotent (safe to run on existing databases).
    /// </summary>
    public abstract class MigrationBase : IMigration
    {
        /// <inheritdoc />
        public abstract int Version { get; }

        /// <inheritdoc />
        public abstract string Name { get; }

        /// <inheritdoc />
        public abstract void Up(SQLiteConnection conn);

        // ── Idempotency Helpers ───────────────────────────────────────────────

        /// <summary>
        /// Adds a column to an existing table ONLY if it does not already exist.
        /// Safe to call on both new and existing databases.
        /// </summary>
        /// <param name="conn">Open synchronous SQLite connection (inside a transaction).</param>
        /// <param name="table">Exact table name as stored in SQLite (case-sensitive).</param>
        /// <param name="column">Column name to add.</param>
        /// <param name="definition">
        /// SQLite type definition, e.g. "REAL DEFAULT 0" or "TEXT DEFAULT ''".
        /// </param>
        protected static void AddColumnIfNotExists(
            SQLiteConnection conn,
            string table,
            string column,
            string definition)
        {
            // pragma_table_info returns one row per column — COUNT > 0 means column exists.
            // L1 fix: parameterize the column name to eliminate the SQL injection risk.
            // The table name cannot be parameterized in SQLite PRAGMA syntax, but it is
            // always a compile-time constant at migration call sites.
            var exists = conn.ExecuteScalar<int>(
                $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = ?", column);

            if (exists == 0)
            {
                conn.Execute($"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {definition}");
                System.Diagnostics.Debug.WriteLine(
                    $"[Migration] Added column '{column}' to '{table}'.");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Migration] Column '{column}' already exists in '{table}' — skipped.");
            }
        }

        /// <summary>
        /// Creates a SQLite index ONLY if it does not already exist.
        /// Equivalent to CREATE INDEX IF NOT EXISTS.
        /// </summary>
        protected static void CreateIndexIfNotExists(
            SQLiteConnection conn,
            string indexName,
            string table,
            string columns,
            bool unique = false)
        {
            var uniqueKw = unique ? "UNIQUE " : string.Empty;
            conn.Execute(
                $"CREATE {uniqueKw}INDEX IF NOT EXISTS \"{indexName}\" ON \"{table}\" ({columns})");
            System.Diagnostics.Debug.WriteLine(
                $"[Migration] Index '{indexName}' ensured on '{table}' ({columns}).");
        }

        /// <summary>
        /// Returns true if the given table exists in the database.
        /// Uses the SQLite master schema — works regardless of sqlite-net-pcl version.
        /// </summary>
        protected static bool TableExists(SQLiteConnection conn, string tableName)
        {
            var count = conn.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=?",
                tableName);
            return count > 0;
        }
    }
}
