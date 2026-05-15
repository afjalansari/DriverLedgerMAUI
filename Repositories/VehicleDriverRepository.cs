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
            // P5-6 fix: use raw connection so idx_vehicledrivers_vehicle (Migration_005)
            // is used instead of a full async table scan through the QueryAsync proxy.
            var conn = await _db.GetRawConnectionAsync();
            return await Task.Run(() =>
                conn.Table<VehicleDriver>()
                    .Where(vd => vd.VehicleId == vehicleId)
                    .ToList());
        }

        public async Task<VehicleDriver?> GetAssignmentAsync(int vehicleId, string shiftType)
        {
            // P5-6 fix: same pattern — hits idx_vehicledrivers_shift composite index.
            // Called on every vehicle/shift change in SettlementEntryPage.
            var conn = await _db.GetRawConnectionAsync();
            return await Task.Run(() =>
                conn.Table<VehicleDriver>()
                    .Where(vd => vd.VehicleId == vehicleId && vd.ShiftType == shiftType)
                    .FirstOrDefault());
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


