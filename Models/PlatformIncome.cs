using SQLite;

namespace DriverLedger.Models
{
    [Table("PlatformIncomes")]
    public class PlatformIncome
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int SettlementId { get; set; }

        public string PlatformName { get; set; } = string.Empty;

        public decimal OperatorBill { get; set; }

        public decimal CashCollected { get; set; }
    }
}
