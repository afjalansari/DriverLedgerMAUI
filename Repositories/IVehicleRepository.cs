using DriverLedger.Models;

namespace DriverLedger.Repositories
{
    public interface IVehicleRepository
    {
        Task<List<Vehicle>> GetAllVehiclesAsync();
        Task<List<Vehicle>> GetActiveVehiclesAsync();
        Task<Vehicle?> GetVehicleByIdAsync(int id);
        Task<int> SaveVehicleAsync(Vehicle vehicle);
        Task<int> DeleteVehicleAsync(Vehicle vehicle);
    }
}

