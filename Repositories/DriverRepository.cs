using DriverLedger.Database;
using DriverLedger.Models;

namespace DriverLedger.Repositories
{
    public class DriverRepository : IDriverRepository
    {
        private readonly DatabaseService _db;

        public DriverRepository(DatabaseService db) => _db = db;

        public Task<List<Driver>> GetAllDriversAsync()
            => _db.GetAllAsync<Driver>();

        public async Task<List<Driver>> GetActiveDriversAsync()
        {
            var query = await _db.QueryAsync<Driver>();
            return await query.Where(d => d.Status == DriverStatus.Active).ToListAsync();
        }

        public Task<Driver?> GetDriverByIdAsync(int id)
            => _db.GetByIdAsync<Driver>(id);

        public async Task<int> SaveDriverAsync(Driver driver)
        {
            if (driver.Id == 0)
            {
                driver.JoinDate = driver.JoinDate == default ? DateTime.UtcNow : driver.JoinDate;
                return await _db.InsertAsync(driver);
            }
            return await _db.UpdateAsync(driver);
        }

        public Task<int> DeleteDriverAsync(Driver driver)
            => _db.DeleteAsync(driver);
    }
}

