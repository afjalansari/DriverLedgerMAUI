using SQLite;
using DriverLedger.Helpers;
using DriverLedger.Models;
using DriverLedger.Database.Migrations;

namespace DriverLedger.Database
{
    public class DatabaseService
    {
        private SQLiteAsyncConnection? _connection;
        private readonly SemaphoreSlim  _initLock = new(1, 1);
        private bool _initialized = false;

        private readonly MigrationRunner _migrationRunner;

        /// <summary>Full path of the SQLite database file on this device.</summary>
        public static string DatabasePath =>
            Path.Combine(FileSystem.AppDataDirectory, AppConstants.DatabaseName);

        /// <param name="migrationRunner">
        /// Injected migration engine — runs all pending schema migrations on first connect.
        /// </param>
        public DatabaseService(MigrationRunner migrationRunner)
        {
            _migrationRunner = migrationRunner
                ?? throw new ArgumentNullException(nameof(migrationRunner));
        }

        public async Task InitializeAsync()
        {
            if (_initialized) return;
            await _initLock.WaitAsync();
            try
            {
                if (_initialized) return;

                // BUG-12: SharedCache removed — it causes SQLITE_LOCKED when the async
                // connection (repositories) and the sync connection (SqliteUnitOfWork) overlap.
                _connection = new SQLiteAsyncConnection(DatabasePath,
                    SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create);

                // ── SQLite Hardening Pragmas (Phase 5A) ──────────────────────────
                // WAL mode: safe concurrent reads during writes; survives app crash mid-write.
                await _connection.ExecuteAsync("PRAGMA journal_mode=WAL;");
                // Enforce foreign key constraints at the SQLite level.
                await _connection.ExecuteAsync("PRAGMA foreign_keys=ON;");
                // NORMAL sync: safe on mobile — OS flushes before app suspend/kill.
                await _connection.ExecuteAsync("PRAGMA synchronous=NORMAL;");
                // Phase 1 OEM hardening: wait up to 3 seconds when the DB is locked
                // instead of immediately returning SQLITE_BUSY. Prevents crashes on
                // Xiaomi/Redmi ROMs that can briefly lock the DB during background kills.
                await _connection.ExecuteAsync("PRAGMA busy_timeout=3000;");

                // ── Migration Runner (Phase 1) ────────────────────────────────────
                // Replaces the old per-table CreateTableAsync calls.
                // MigrationRunner.RunPendingMigrations uses the sync connection which
                // must be obtained via GetConnection() on the async connection.
                // We run it in Task.Run to avoid blocking the UI thread, keeping the
                // async chain intact.
                var syncConn = _connection.GetConnection();
                int applied = await Task.Run(() =>
                    _migrationRunner.RunPendingMigrations(syncConn));

                _initialized = true;
                System.Diagnostics.Debug.WriteLine(
                    $"[DatabaseService] Ready at {DatabasePath}. " +
                    $"{applied} new migration(s) applied.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DatabaseService] Init error: {ex.Message}");
                throw;
            }
            finally { _initLock.Release(); }
        }


        private async Task<SQLiteAsyncConnection> GetConnectionAsync()
        {
            // P5-7 fix: delegate to InitializeAsync() which owns the double-checked
            // SemaphoreSlim lock. The old bare `if (!_initialized)` check was a race
            // window — two concurrent callers could both see false before either
            // acquired _initLock, causing duplicate PRAGMA execution.
            // InitializeAsync() returns immediately when _initialized is true (cheap path).
            await InitializeAsync();
            return _connection!;
        }

        /// <summary>
        /// Returns the underlying synchronous <see cref="SQLite.SQLiteConnection"/>
        /// so callers can use <c>RunInTransaction()</c> for multi-statement atomicity.
        /// NOTE: ensure <see cref="InitializeAsync"/> has been called first.
        /// </summary>
        public virtual async Task<SQLiteConnection> GetRawConnectionAsync()
        {
            var asyncConn = await GetConnectionAsync();
            // SQLiteAsyncConnection exposes its inner sync connection via GetConnection().
            return asyncConn.GetConnection();
        }

        public async Task<List<T>> GetAllAsync<T>() where T : new()
        {
            var db = await GetConnectionAsync();
            return await db.Table<T>().ToListAsync();
        }

        public async Task<T?> GetByIdAsync<T>(int id) where T : new()
        {
            var db = await GetConnectionAsync();
            return await db.FindAsync<T>(id);
        }

        public async Task<int> InsertAsync<T>(T item)
        {
            var db = await GetConnectionAsync();
            return await db.InsertAsync(item);
        }

        public async Task<int> UpdateAsync<T>(T item)
        {
            var db = await GetConnectionAsync();
            return await db.UpdateAsync(item);
        }

        public async Task<int> DeleteAsync<T>(T item)
        {
            var db = await GetConnectionAsync();
            return await db.DeleteAsync(item);
        }

        public async Task<AsyncTableQuery<T>> QueryAsync<T>() where T : new()
        {
            var db = await GetConnectionAsync();
            return db.Table<T>();
        }

        /// <summary>
        /// Closes the async connection gracefully.
        /// Must be called by backup services before copying the DB file.
        /// After calling this, the next DB operation will re-open the connection automatically.
        /// </summary>
        public async Task CloseAsync()
        {
            if (_connection is not null)
            {
                await _connection.CloseAsync();
                _connection  = null;
                _initialized = false;
                System.Diagnostics.Debug.WriteLine("[DatabaseService] Connection closed for backup.");
            }
        }
    }
}

