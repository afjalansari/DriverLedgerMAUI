using DriverLedger.Database;
using DriverLedger.Models;

namespace DriverLedger.Repositories
{
    public class VehicleDriverRepository : IVehicleDriverRepository
    {
        private readonly DatabaseService _db;

        public VehicleDriverRepository(DatabaseService db) => _db = db;

        public async Task<List<VehicleDriver>> GetAssignmentsForVehicleAsync(int vehicleId)
        {
            var query = await _db.QueryAsync<VehicleDriver>();
            return await query.Where(vd => vd.VehicleId == vehicleId).ToListAsync();
        }

        public async Task<VehicleDriver?> GetAssignmentAsync(int vehicleId, string shiftType)
        {
            var query = await _db.QueryAsync<VehicleDriver>();
            return await query
                .Where(vd => vd.VehicleId == vehicleId && vd.ShiftType == shiftType)
                .FirstOrDefaultAsync();
        }

        public Task<VehicleDriver?> GetByIdAsync(int id)
            => _db.GetByIdAsync<VehicleDriver>(id);

        /// <summary>
        /// Enforces one-driver-per-shift rule:
        /// If an assignment already exists for this vehicle + shift, update it.
        /// Otherwise insert a new record.
        /// </summary>
        public async Task<int> SaveAssignmentAsync(VehicleDriver assignment)
        {
            var existing = await GetAssignmentAsync(assignment.VehicleId, assignment.ShiftType);
            if (existing != null)
            {
                existing.DriverId = assignment.DriverId;
                existing.AssignedDate = DateTime.UtcNow;
                return await _db.UpdateAsync(existing);
            }

            assignment.AssignedDate = DateTime.UtcNow;
            return await _db.InsertAsync(assignment);
        }

        public Task<int> DeleteAssignmentAsync(VehicleDriver assignment)
            => _db.DeleteAsync(assignment);
    }
}

