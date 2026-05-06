using SQLite;
using DriverLedger.Helpers;
using DriverLedger.Models;

namespace DriverLedger.Database
{
    public class DatabaseService
    {
        private SQLiteAsyncConnection? _connection;
        private readonly SemaphoreSlim _initLock = new(1, 1);
        private bool _initialized = false;

        /// <summary>Full path of the SQLite database file on this device.</summary>
        public static string DatabasePath =>
            Path.Combine(FileSystem.AppDataDirectory, AppConstants.DatabaseName);

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

                // Phase 1
                await _connection.CreateTableAsync<Company>();
                // Phase 2
                await _connection.CreateTableAsync<Driver>();
                await _connection.CreateTableAsync<Vehicle>();
                await _connection.CreateTableAsync<VehicleDriver>();
                // Phase 3
                await _connection.CreateTableAsync<Settlement>();
                await _connection.CreateTableAsync<PlatformIncome>();
                await _connection.CreateTableAsync<SettlementExpense>();
                // Phase 4
                await _connection.CreateTableAsync<DriverLedgerEntry>();

                _initialized = true;
                System.Diagnostics.Debug.WriteLine($"[DatabaseService] Ready at {DatabasePath}");
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
            if (!_initialized) await InitializeAsync();
            return _connection!;
        }

        /// <summary>
        /// Returns the underlying synchronous <see cref="SQLite.SQLiteConnection"/>
        /// so callers can use <c>RunInTransaction()</c> for multi-statement atomicity.
        /// NOTE: ensure <see cref="InitializeAsync"/> has been called first.
        /// </summary>
        public async Task<SQLiteConnection> GetRawConnectionAsync()
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
        /// Must be called by <see cref="BackupService"/> before copying the DB file.
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

