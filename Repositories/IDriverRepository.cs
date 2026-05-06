using DriverLedger.Models;

namespace DriverLedger.Repositories
{
    public interface IDriverRepository
    {
        Task<List<Driver>> GetAllDriversAsync();
        Task<List<Driver>> GetActiveDriversAsync();
        Task<Driver?> GetDriverByIdAsync(int id);
        Task<int> SaveDriverAsync(Driver driver);
        Task<int> DeleteDriverAsync(Driver driver);
    }
}

