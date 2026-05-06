using DriverLedger.Models;

namespace DriverLedger.Repositories
{
    /// <summary>
    /// Coordinates atomic writes of Settlement + DriverLedgerEntry inside
    /// a single SQLite transaction. Either all rows are committed or none.
    /// </summary>
    public interface IUnitOfWork
    {
        /// <summary>
        /// Inserts <paramref name="settlement"/> and <paramref name="ledgerEntry"/>
        /// inside a single transaction.  On success, <c>settlement.Id</c> is
        /// populated with the newly generated primary key.
        /// </summary>
        /// <returns>The new settlement's database Id.</returns>
        Task<int> SaveSettlementWithLedgerAsync(
            Settlement        settlement,
            DriverLedgerEntry ledgerEntry);

        /// <summary>
        /// Updates an existing <paramref name="settlement"/> and its linked
        /// <paramref name="ledgerEntry"/> atomically, then rebalances the
        /// driver's running Balance column.  (BUG-A fix)
        /// </summary>
        Task UpdateSettlementWithLedgerAsync(
            Settlement        settlement,
            DriverLedgerEntry ledgerEntry,
            int               driverId);

        /// <summary>
        /// Deletes the linked <paramref name="ledgerEntry"/>, rebalances the
        /// driver's ledger, then deletes the <paramref name="settlement"/> —
        /// all inside a single transaction.  (BUG-C fix)
        /// </summary>
        Task DeleteSettlementWithLedgerAsync(
            Settlement         settlement,
            DriverLedgerEntry? ledgerEntry,
            int                driverId);
    }
}
