using SQLite;
using System.Collections.Generic;

namespace DriverLedger.Models
{
    [Table("Settlements")]
    public class Settlement
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public DateTime Date { get; set; }

        // ── Driver Snapshot ──────────────────────────────────────────────
        public int DriverId { get; set; }
        public string DriverNameSnapshot { get; set; } = string.Empty;
        public DriverType DriverTypeSnapshot { get; set; }

        // ── Vehicle Snapshot ─────────────────────────────────────────────
        public int VehicleId { get; set; }
        public string VehicleNumberSnapshot { get; set; } = string.Empty;

        // ── Navigation Properties (Ignored by SQLite, handled by Repo) ───
        [Ignore]
        public List<PlatformIncome> PlatformIncomes { get; set; } = new();

        [Ignore]
        public List<SettlementExpense> ExpenseItems { get; set; } = new();

        // ── Financial Totals (Audit Source of Truth) ─────────────────────
        public decimal TotalIncome { get; set; }
        public decimal TotalCashCollected { get; set; }
        
        /// <summary>Driver's portion of the income based on percentage split</summary>
        public decimal DriverShare { get; set; }
        
        /// <summary>Owner's portion of CNG (Liability to the owner)</summary>
        public decimal OwnerCngShare { get; set; }
        
        /// <summary>Sum of Toll + Parking + OtherExpenses</summary>
        public decimal TotalOwnerExpenses { get; set; }

        /// <summary>
        /// Final settlement result:
        /// Positive (+) → Owner pays driver
        /// Negative (-) → Driver pays owner
        /// Formula: NetDriverPayable = DriverShare - TotalCashCollected + OwnerCngShare + TotalOwnerExpenses
        /// </summary>
        public decimal NetDriverPayable { get; set; }

        // ── Metadata ─────────────────────────────────────────────────────
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }
        
        /// <summary>Shift metadata (Day/Night)</summary>
        public string ShiftType { get; set; } = "Day";
    }
}
