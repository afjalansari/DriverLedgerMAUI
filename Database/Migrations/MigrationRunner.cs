using DriverLedger.Models;
using SQLite;

namespace DriverLedger.Database.Migrations
{
    /// <summary>
    /// Central migration engine for DriverLedger SQLite schema management.
    ///
    /// Execution model:
    ///   1. Ensures the SchemaVersion table exists (idempotent CREATE IF NOT EXISTS).
    ///   2. Loads all version numbers already recorded as successfully applied.
    ///   3. Iterates all registered migrations in ascending <see cref="IMigration.Version"/> order.
    ///   4. Skips any migration whose version is already in the applied set.
    ///   5. For each pending migration:
    ///        a. Opens a SQLite transaction.
    ///        b. Calls <see cref="IMigration.Up"/>.
    ///        c. Inserts a <see cref="SchemaVersion"/> record marking it as applied.
    ///        d. Commits. On failure, rolls back and re-throws — subsequent migrations are NOT run.
    ///
    /// Existing database safety:
    ///   Migration 001 uses sqlite-net-pcl's CreateTable&lt;T&gt; which is itself idempotent
    ///   (CREATE TABLE IF NOT EXISTS semantics). Running it on a database that already has
    ///   the tables is a no-op — no existing rows are touched.
    /// </summary>
    public sealed class MigrationRunner
    {
        private readonly IReadOnlyList<IMigration> _migrations;

        /// <summary>
        /// Constructs the runner with a fixed, ordered list of all registered migrations.
        /// Migrations MUST be registered in ascending Version order.
        /// </summary>
        public MigrationRunner(IEnumerable<IMigration> migrations)
        {
            _migrations = migrations
                .OrderBy(m => m.Version)
                .ToList();
        }

        /// <summary>
        /// Runs all pending migrations against the provided synchronous connection.
        /// Must be called BEFORE any application code accesses the database tables.
        /// </summary>
        /// <param name="conn">
        /// Open <see cref="SQLiteConnection"/> — typically obtained from
        /// <c>SQLiteAsyncConnection.GetConnection()</c> during startup.
        /// Must NOT be inside an existing transaction.
        /// </param>
        /// <returns>Number of migrations that were newly applied.</returns>
        public int RunPendingMigrations(SQLiteConnection conn)
        {
            // Step 1: Ensure the SchemaVersion tracking table exists.
            // This is the one table we always create directly — it is not managed by Migration 001.
            EnsureSchemaVersionTable(conn);

            // Step 2: Load all already-applied version numbers.
            var applied = conn
                .Table<SchemaVersion>()
                .Where(sv => sv.Success)
                .ToList()
                .Select(sv => sv.Version)
                .ToHashSet();

            System.Diagnostics.Debug.WriteLine(
                $"[MigrationRunner] {applied.Count} migration(s) already applied. " +
                $"{_migrations.Count} total registered.");

            // Step 3: Run pending migrations in order.
            int newlyApplied = 0;
            foreach (var migration in _migrations)
            {
                if (applied.Contains(migration.Version))
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[MigrationRunner] Skipping v{migration.Version} — {migration.Name} (already applied).");
                    continue;
                }

                System.Diagnostics.Debug.WriteLine(
                    $"[MigrationRunner] Applying v{migration.Version} — {migration.Name} …");

                // Step 4: Run the migration inside its own transaction.
                // If it throws, the transaction rolls back and we re-throw to halt startup.
                // M1 fix: wrap the re-throw so the exception message includes the migration
                // name and version — makes "[DatabaseService] Init error" log lines
                // immediately actionable without guessing which migration failed.
                try
                {
                    conn.RunInTransaction(() =>
                    {
                        migration.Up(conn);

                        conn.Insert(new SchemaVersion
                        {
                            Version       = migration.Version,
                            MigrationName = migration.Name,
                            AppliedAt     = DateTime.UtcNow,
                            Success       = true
                        });
                    });
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Migration v{migration.Version} ({migration.Name}) failed and was rolled back: {ex.Message}",
                        ex);
                }

                applied.Add(migration.Version);
                newlyApplied++;
                System.Diagnostics.Debug.WriteLine(
                    $"[MigrationRunner] ✅ Applied v{migration.Version} — {migration.Name}.");
            }

            System.Diagnostics.Debug.WriteLine(
                $"[MigrationRunner] Complete. {newlyApplied} new migration(s) applied.");

            return newlyApplied;
        }

        /// <summary>Returns all SchemaVersion records — useful for a diagnostics page.</summary>
        public List<SchemaVersion> GetAppliedMigrations(SQLiteConnection conn)
        {
            EnsureSchemaVersionTable(conn);
            return conn.Table<SchemaVersion>()
                .OrderBy(sv => sv.Version)
                .ToList();
        }

        /// <summary>Returns the highest version number currently applied (0 if none).</summary>
        public int GetCurrentSchemaVersion(SQLiteConnection conn)
        {
            EnsureSchemaVersionTable(conn);
            var max = conn.Table<SchemaVersion>()
                .Where(sv => sv.Success)
                .ToList()
                .Select(sv => sv.Version)
                .DefaultIfEmpty(0)
                .Max();
            return max;
        }

        // ── Private Helpers ───────────────────────────────────────────────────

        private static void EnsureSchemaVersionTable(SQLiteConnection conn)
        {
            // CreateTable<T>() is idempotent in sqlite-net-pcl (uses CREATE TABLE IF NOT EXISTS).
            conn.CreateTable<SchemaVersion>();
        }
    }
}
