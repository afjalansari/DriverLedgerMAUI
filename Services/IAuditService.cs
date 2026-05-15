using DriverLedger.Models;

namespace DriverLedger.Services
{
    /// <summary>
    /// Writes and reads the <see cref="AuditLog"/> table.
    ///
    /// CONTRACT:
    ///   • LogAsync must NEVER throw — audit failures must not crash the calling operation.
    ///   • All writes go through the raw SQLite connection for performance (no async proxy).
    ///   • Callers (SqliteUnitOfWork, DriverLedgerRepository) invoke LogAsync AFTER the
    ///     primary operation completes so that audit rows are never written for failed ops.
    /// </summary>
    public interface IAuditService
    {
        /// <summary>
        /// Persists a single audit event.
        /// Safe to fire-and-forget from UoW (exceptions are swallowed internally).
        /// </summary>
        Task LogAsync(
            string entityType,
            int    entityId,
            string action,
            int    driverId,
            string driverName,
            string changeSummary,
            string snapshotJson = "");

        /// <summary>
        /// Returns the <paramref name="count"/> most recent audit events across all entities,
        /// ordered by Timestamp descending. Used for the fleet-wide activity feed.
        /// </summary>
        Task<List<AuditLog>> GetRecentAsync(int count = 50);

        /// <summary>
        /// Returns all audit events for a specific driver, newest first.
        /// Used in the per-driver ledger detail view.
        /// </summary>
        Task<List<AuditLog>> GetForDriverAsync(int driverId, int count = 100);

        /// <summary>
        /// Returns all audit events for a specific entity (e.g. one Settlement),
        /// ordered by Timestamp descending. Used for the per-settlement changelog drawer.
        /// </summary>
        Task<List<AuditLog>> GetForEntityAsync(string entityType, int entityId);

        /// <summary>
        /// Returns the total number of rows in the AuditLog table via a cheap COUNT(*) query.
        /// Use this instead of loading all rows and calling .Count when you only need the count.
        /// </summary>
        Task<int> GetTotalCountAsync();
    }
}
