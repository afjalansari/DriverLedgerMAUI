using DriverLedger.Models;

namespace DriverLedger.Repositories
{
    public interface ICompanyRepository
    {
        Task<List<Company>> GetAllCompaniesAsync();
        Task<Company?> GetCompanyByIdAsync(int id);
        Task<Company?> GetCompanyByMobileAsync(string mobileNumber);
        Task<Company?> GetCompanyByPhoneAsync(string phoneNumber); // legacy
        Task<int> SaveCompanyAsync(Company company);
        Task<int> DeleteCompanyAsync(Company company);
    }
}


