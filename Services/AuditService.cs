using DriverLedger.Database;
using DriverLedger.Models;

namespace DriverLedger.Services
{
    /// <summary>
    /// SQLite-backed implementation of <see cref="IAuditService"/>.
    /// Register as Singleton in DI — holds no mutable per-request state.
    ///
    /// PERFORMANCE:
    ///   All writes use the raw SQLite connection (no async proxy overhead).
    ///   Reads use indexed columns (see Migration_006 for index definitions):
    ///     • GetRecentAsync     → idx_audit_timestamp  (Timestamp DESC)
    ///     • GetForDriverAsync  → idx_audit_driver_time (DriverId, Timestamp DESC)
    ///     • GetForEntityAsync  → idx_audit_entity      (EntityType, EntityId, Timestamp DESC)
    /// </summary>
    public class AuditService : IAuditService
    {
        private readonly DatabaseService _db;
        // M3 fix: AuditService is a Singleton. LogAsync is called concurrently from
        // SqliteUnitOfWork and DriverLedgerRepository on different async contexts.
        // SQLite-net's synchronous connection is NOT thread-safe — two concurrent
        // conn.Insert() calls produce SQLITE_BUSY or silent data loss.
        // SemaphoreSlim(1,1) serialises writes with zero overhead on the happy path.
        private readonly SemaphoreSlim _writeLock = new(1, 1);

        public AuditService(DatabaseService db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        /// <inheritdoc />
        public async Task LogAsync(
            string entityType,
            int    entityId,
            string action,
            int    driverId,
            string driverName,
            string changeSummary,
            string snapshotJson = "")
        {
            try
            {
                var conn = await _db.GetRawConnectionAsync();
                var row = new AuditLog
                {
                    Timestamp     = DateTime.UtcNow,
                    EntityType    = entityType,
                    EntityId      = entityId,
                    Action        = action,
                    DriverId      = driverId,
                    DriverName    = driverName,
                    ChangeSummary = changeSummary,
                    SnapshotJson  = snapshotJson
                };
                // M3 fix: acquire the write lock before Insert to prevent SQLITE_BUSY
                // when multiple callers invoke LogAsync concurrently.
                await _writeLock.WaitAsync();
                try
                {
                    await Task.Run(() => conn.Insert(row));
                }
                finally
                {
                    _writeLock.Release();
                }
            }
            catch (Exception ex)
            {
                // Audit failures must NEVER crash the primary operation.
                System.Diagnostics.Debug.WriteLine(
                    $"[AuditService] LogAsync failed (non-fatal): {ex.Message}");
            }
        }

        /// <inheritdoc />
        public async Task<List<AuditLog>> GetRecentAsync(int count = 50)
        {
            try
            {
                var conn = await _db.GetRawConnectionAsync();
                return await Task.Run(() =>
                    conn.Table<AuditLog>()
                        .OrderByDescending(a => a.Timestamp)
                        .Take(count)
                        .ToList());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AuditService] GetRecentAsync error: {ex.Message}");
                return new List<AuditLog>();
            }
        }

        /// <inheritdoc />
        public async Task<List<AuditLog>> GetForDriverAsync(int driverId, int count = 100)
        {
            try
            {
                var conn = await _db.GetRawConnectionAsync();
                return await Task.Run(() =>
                    conn.Table<AuditLog>()
                        .Where(a => a.DriverId == driverId)
                        .OrderByDescending(a => a.Timestamp)
                        .Take(count)
                        .ToList());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AuditService] GetForDriverAsync error: {ex.Message}");
                return new List<AuditLog>();
            }
        }

        /// <inheritdoc />
        public async Task<List<AuditLog>> GetForEntityAsync(string entityType, int entityId)
        {
            try
            {
                var conn = await _db.GetRawConnectionAsync();
                return await Task.Run(() =>
                    conn.Table<AuditLog>()
                        .Where(a => a.EntityType == entityType && a.EntityId == entityId)
                        .OrderByDescending(a => a.Timestamp)
                        .ToList());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AuditService] GetForEntityAsync error: {ex.Message}");
                return new List<AuditLog>();
            }
        }

        /// <inheritdoc />
        public async Task<int> GetTotalCountAsync()
        {
            try
            {
                var conn = await _db.GetRawConnectionAsync();
                return await Task.Run(() =>
                    conn.ExecuteScalar<int>("SELECT COUNT(*) FROM AuditLog"));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AuditService] GetTotalCountAsync error: {ex.Message}");
                return 0;
            }
        }
    }
}
