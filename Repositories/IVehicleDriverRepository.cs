using DriverLedger.Models;

namespace DriverLedger.Repositories
{
    public interface IVehicleDriverRepository
    {
        Task<List<VehicleDriver>> GetAssignmentsForVehicleAsync(int vehicleId);
        Task<VehicleDriver?> GetAssignmentAsync(int vehicleId, string shiftType);
        Task<VehicleDriver?> GetByIdAsync(int id);
        Task<int> SaveAssignmentAsync(VehicleDriver assignment);
        Task<int> DeleteAssignmentAsync(VehicleDriver assignment);
    }
}

