using SQLite;

namespace DriverLedger.Models
{
    [Table("SettlementExpenses")]
    public class SettlementExpense
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int SettlementId { get; set; }

        public ExpenseType Type { get; set; }

        /// <summary>Specific name for 'Other' expenses</summary>
        public string Name { get; set; } = string.Empty;

        public decimal Amount { get; set; }
    }
}
