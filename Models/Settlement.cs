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

        /// <summary>Driver's portion of CNG deducted from their net haq</summary>
        public decimal DriverCngShare { get; set; }

        /// <summary>Total driver-fault challan deducted from driver's net haq</summary>
        public decimal DriverChallanTotal { get; set; }

        /// <summary>Sum of Toll + Parking + OwnerChallan + Other owner expenses</summary>
        public decimal TotalOwnerExpenses { get; set; }

        /// <summary>
        /// Final settlement result:
        /// Positive (+) → Owner pays driver
        /// Negative (-) → Driver pays owner
        /// Formula: NetDriverPayable = DriverShare − DriverChallanTotal − TotalCashCollected
        ///                             + OwnerCngShare + TotalOwnerExpenses
        /// </summary>
        public decimal NetDriverPayable { get; set; }

        // ── Metadata ─────────────────────────────────────────────────────
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Set by SqliteUnitOfWork on every UpdateSettlementWithLedgerAsync call.
        /// NULL on rows that have never been edited (created directly).
        /// Added in Phase 7 — Migration_006 adds the column via ALTER TABLE.
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        public bool IsDeleted { get; set; }

        /// <summary>Shift metadata (Day/Night)</summary>
        public string ShiftType { get; set; } = "Day";

        /// <summary>
        /// Version of <see cref="Services.SettlementCalculator"/> that produced this record.
        /// Defaults to 0 for historical settlements created before the migration framework.
        /// Increment <see cref="Services.SettlementCalculator.CurrentVersion"/> whenever
        /// the calculation formula changes, then add a new database migration for this column.
        /// </summary>
        public int CalculatorVersion { get; set; } = 0;

        // ── Phase 3: Immutable Accounting Fields ─────────────────────────

        /// <summary>
        /// SHA-256 hex digest of the canonical financial inputs at save time.
        /// Computed by <see cref="Services.SettlementIntegrityService.ComputeHash"/>.
        /// Empty string = legacy row (pre-Migration_008); treated as NotStamped, NOT Tampered.
        /// </summary>
        public string CalculationHash { get; set; } = string.Empty;

        /// <summary>
        /// 0 = original save. Increments by 1 on every legitimate edit via UnitOfWork.
        /// Provides an ordered edit history for dispute resolution.
        /// </summary>
        public int RevisionNumber { get; set; } = 0;

        /// <summary>
        /// Mirrors <see cref="Services.SettlementCalculator.CurrentVersion"/> at save time.
        /// UI shows a warning when FormulaVersion &lt; current version, indicating the
        /// settlement was computed with an older formula.
        /// </summary>
        public int FormulaVersion { get; set; } = 0;
    }
}
