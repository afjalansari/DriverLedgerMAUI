using DriverLedger.Database;
using DriverLedger.Models;

namespace DriverLedger.Repositories
{
    public class CompanyRepository : ICompanyRepository
    {
        private readonly DatabaseService _db;

        public CompanyRepository(DatabaseService db)
        {
            _db = db;
        }

        public Task<List<Company>> GetAllCompaniesAsync()
            => _db.GetAllAsync<Company>();

        public Task<Company?> GetCompanyByIdAsync(int id)
            => _db.GetByIdAsync<Company>(id);

        /// <summary>
        /// Finds a company whose MobileNumber matches (primary lookup).
        /// Falls back to PhoneNumber for legacy records.
        /// </summary>
        public async Task<Company?> GetCompanyByMobileAsync(string mobileNumber)
        {
            try
            {
                var normalised = mobileNumber.Trim();
                var query = await _db.QueryAsync<Company>();

                // Try primary MobileNumber first
                var result = await query.Where(c => c.MobileNumber == normalised).FirstOrDefaultAsync();

                // Fallback to legacy PhoneNumber if not found
                if (result == null)
                {
                    query = await _db.QueryAsync<Company>();
                    result = await query.Where(c => c.PhoneNumber == normalised).FirstOrDefaultAsync();
                }

                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[CompanyRepository] GetCompanyByMobileAsync error: {ex.Message}");
                return null;
            }
        }

        /// <summary>Legacy overload — delegates to GetCompanyByMobileAsync.</summary>
        public Task<Company?> GetCompanyByPhoneAsync(string phoneNumber)
            => GetCompanyByMobileAsync(phoneNumber);

        public async Task<int> SaveCompanyAsync(Company company)
        {
            if (company.Id == 0)
            {
                company.CreatedAt = DateTime.UtcNow;
                return await _db.InsertAsync(company);
            }
            return await _db.UpdateAsync(company);
        }

        public Task<int> DeleteCompanyAsync(Company company)
            => _db.DeleteAsync(company);
    }
}


