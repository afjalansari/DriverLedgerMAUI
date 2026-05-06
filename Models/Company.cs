using SQLite;

namespace DriverLedger.Models
{
    [Table("Companies")]
    public class Company
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [NotNull, MaxLength(200)]
        public string CompanyName { get; set; } = string.Empty;

        [NotNull, MaxLength(200)]
        public string OwnerName { get; set; } = string.Empty;

        /// <summary>Primary mobile number used for login. New canonical field.</summary>
        [MaxLength(20)]
        public string MobileNumber { get; set; } = string.Empty;

        /// <summary>Legacy column retained for SQLite backward compatibility. Use MobileNumber instead.</summary>
        [MaxLength(20)]
        public string PhoneNumber { get; set; } = string.Empty;

        [MaxLength(100)]
        public string City { get; set; } = string.Empty;

        /// <summary>BCrypt hash of the owner's password. Never stored in plain text.</summary>
        [MaxLength(200)]
        public string PasswordHash { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Legacy alias kept for compat. Use CreatedAt instead.</summary>
        [Ignore]
        public DateTime CreatedDate
        {
            get => CreatedAt;
            set => CreatedAt = value;
        }
    }
}


