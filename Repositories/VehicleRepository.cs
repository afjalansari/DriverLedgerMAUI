using DriverLedger.Database;
using DriverLedger.Models;

namespace DriverLedger.Repositories
{
    public class VehicleRepository : IVehicleRepository
    {
        private readonly DatabaseService _db;

        public VehicleRepository(DatabaseService db) => _db = db;

        public Task<List<Vehicle>> GetAllVehiclesAsync()
            => _db.GetAllAsync<Vehicle>();

        public async Task<List<Vehicle>> GetActiveVehiclesAsync()
        {
            var query = await _db.QueryAsync<Vehicle>();
            return await query.Where(v => v.Status == VehicleStatus.Active).ToListAsync();
        }

        public Task<Vehicle?> GetVehicleByIdAsync(int id)
            => _db.GetByIdAsync<Vehicle>(id);

        public async Task<int> SaveVehicleAsync(Vehicle vehicle)
        {
            if (vehicle.Id == 0)
                return await _db.InsertAsync(vehicle);
            return await _db.UpdateAsync(vehicle);
        }

        public Task<int> DeleteVehicleAsync(Vehicle vehicle)
            => _db.DeleteAsync(vehicle);
    }
}

