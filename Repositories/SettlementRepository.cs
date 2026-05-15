using DriverLedger.Database;
using DriverLedger.Models;

namespace DriverLedger.Repositories
{
    public class SettlementRepository : ISettlementRepository
    {
        private readonly DatabaseService _db;

        public SettlementRepository(DatabaseService db) => _db = db;

        public async Task<List<Settlement>> GetAllSettlementsAsync()
        {
            var list = await _db.GetAllAsync<Settlement>();
            foreach (var s in list) await LoadCollectionsAsync(s);
            return list;
        }

        public async Task<Settlement?> GetSettlementByIdAsync(int id)
        {
            var s = await _db.GetByIdAsync<Settlement>(id);
            if (s != null) await LoadCollectionsAsync(s);
            return s;
        }

        public async Task<List<Settlement>> GetSettlementsByDateAsync(DateTime date)
        {
            // BUG-10 fix: filter at the SQLite table level — avoids full table scan.
            var start = date.Date;
            var end   = start.AddDays(1);

            var conn = await _db.GetRawConnectionAsync();
            var filtered = await Task.Run(() =>
                conn.Table<Settlement>()
                    .Where(s => s.Date >= start && s.Date < end)
                    .ToList());

            foreach (var s in filtered) await LoadCollectionsAsync(s);
            return filtered;
        }

        public async Task<List<Settlement>> GetSettlementsByMonthAsync(int year, int month)
        {
            // BUG-10 fix: filter at the SQLite table level.
            var start = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Unspecified);
            var end   = start.AddMonths(1);

            var conn = await _db.GetRawConnectionAsync();
            var filtered = await Task.Run(() =>
                conn.Table<Settlement>()
                    .Where(s => s.Date >= start && s.Date < end)
                    .ToList());

            foreach (var s in filtered) await LoadCollectionsAsync(s);
            return filtered;
        }

        public async Task<int> SaveSettlementAsync(Settlement settlement)
        {
            // Rule 9: Unique constraint: Date + Driver + Vehicle
            var start = settlement.Date.Date;
            var end = start.AddDays(1);

            var query = await _db.QueryAsync<Settlement>();
            var duplicate = await query.FirstOrDefaultAsync(s =>
                s.Date >= start && s.Date < end &&
                s.DriverId  == settlement.DriverId  &&
                s.VehicleId == settlement.VehicleId &&
                // H3 fix: include ShiftType so Day+Night on the same date are both valid.
                s.ShiftType == settlement.ShiftType &&
                s.Id        != settlement.Id);

            if (duplicate != null)
                throw new InvalidOperationException($"A settlement already exists for this Driver and Vehicle on {settlement.Date:yyyy-MM-dd} ({settlement.ShiftType} shift)");

            var conn = await _db.GetRawConnectionAsync();
            int result = 0;

            // BUG-4 fix: wrap synchronous RunInTransaction in Task.Run to
            // avoid blocking the calling async thread / thread-pool thread.
            await Task.Run(() =>
            {
                conn.RunInTransaction(() =>
                {
                    if (settlement.Id == 0)
                    {
                        settlement.CreatedAt = DateTime.UtcNow;
                        conn.Insert(settlement);
                        result = settlement.Id;
                    }
                    else
                    {
                        // Phase 3 — increment revision + re-stamp hash on every edit
                        new Services.SettlementIntegrityService()
                            .StampRevision(settlement, settlement.CalculatorVersion);

                        conn.Update(settlement);
                        result = settlement.Id;

                        conn.Execute("DELETE FROM PlatformIncomes WHERE SettlementId = ?", settlement.Id);
                        conn.Execute("DELETE FROM SettlementExpenses WHERE SettlementId = ?", settlement.Id);
                    }

                    foreach (var income in settlement.PlatformIncomes)
                    {
                        income.SettlementId = settlement.Id;
                        conn.Insert(income);
                    }

                    foreach (var expense in settlement.ExpenseItems)
                    {
                        expense.SettlementId = settlement.Id;
                        conn.Insert(expense);
                    }
                });
            });

            return result;
        }

        public async Task<int> DeleteSettlementAsync(Settlement settlement)
        {
            // P5-5 fix: wrap RunInTransaction in Task.Run — avoids blocking the
            // thread-pool thread (same BUG-4 pattern already fixed in SaveSettlementAsync).
            var conn = await _db.GetRawConnectionAsync();
            await Task.Run(() =>
            {
                conn.RunInTransaction(() =>
                {
                    conn.Execute("DELETE FROM PlatformIncomes WHERE SettlementId = ?", settlement.Id);
                    conn.Execute("DELETE FROM SettlementExpenses WHERE SettlementId = ?", settlement.Id);
                    conn.Delete(settlement);
                });
            });
            return 1;
        }

        public async Task<List<Settlement>> GetRecentSettlementsAsync(int count)
        {
            var query = await _db.QueryAsync<Settlement>();
            var list = await query.OrderByDescending(s => s.Date).Take(count).ToListAsync();
            foreach (var s in list) await LoadCollectionsAsync(s);
            return list;
        }

        private async Task LoadCollectionsAsync(Settlement s)
        {
            // Optimized: use direct connection table query to avoid full table scans via QueryAsync() proxy
            var conn = await _db.GetRawConnectionAsync();
            
            s.PlatformIncomes = conn.Table<PlatformIncome>()
                .Where(p => p.SettlementId == s.Id)
                .ToList();

            s.ExpenseItems = conn.Table<SettlementExpense>()
                .Where(e => e.SettlementId == s.Id)
                .ToList();
        }
    }
}
