using DriverLedger.Domain;
using DriverLedger.Models;

namespace DriverLedger.Services
{
    /// <summary>
    /// Pure, stateless financial calculation engine for taxi driver daily settlements.
    ///
    /// CALCULATION FORMULA (v1):
    ///
    ///   TotalIncome        = Σ(OperatorBill)          across all platform entries
    ///   TotalCashCollected = Σ(CashCollected)          across all platform entries
    ///   DriverShare        = TotalIncome × DriverIncomePercent%
    ///   OwnerCngShare      = TotalCNG    × OwnerCngPercent%
    ///   DriverCngShare     = TotalCNG    × DriverCngPercent%
    ///   DriverChallanTotal = Σ(Expenses where Name="DriverChallan")
    ///   TotalOwnerExpenses = Σ(Toll + Parking + OwnerChallan + other non-driver expenses)
    ///
    ///   NetDriverPayable   = DriverShare
    ///                       − DriverChallanTotal    (driver-fault: reduces driver haq)
    ///                       − TotalCashCollected    (already received by driver)
    ///                       + OwnerCngShare         (owner refunds fuel cost to driver)
    ///                       + TotalOwnerExpenses    (owner refunds expenses driver fronted)
    ///
    ///   (+) NetDriverPayable → Owner pays driver
    ///   (−) NetDriverPayable → Driver pays owner
    ///
    /// FINANCIAL CORRECTNESS:
    ///   All arithmetic is performed via <see cref="Money"/> (immutable struct, decimal,
    ///   MidpointRounding.AwayFromZero). Raw decimal values are used ONLY for input parsing
    ///   and final SQLite persistence. No float/double math is used anywhere.
    ///
    ///   NEVER change the formula in this class without incrementing CalculatorVersion
    ///   on the Settlement model and creating a new database migration.
    /// </summary>
    public class SettlementCalculator : ISettlementCalculator
    {
        /// <summary>
        /// Version of the calculation formula this instance implements.
        /// Stored on every new Settlement for historical reproducibility.
        /// Increment when any formula change is made.
        /// </summary>
        public const int CurrentVersion = 1;

        // ══════════════════════════════════════════════════════════════════════
        //  Request DTO
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Immutable input to <see cref="Calculate"/>.
        /// All required fields are enforced at construction via <c>required</c> keyword.
        /// Once created, no field on this record can be mutated.
        /// </summary>
        public sealed record CalculationRequest
        {
            public DateTime Date       { get; init; }
            public required Driver   Driver   { get; init; }
            public required Vehicle  Vehicle  { get; init; }
            public string ShiftType  { get; init; } = "Day";

            public IReadOnlyList<PlatformIncome>    Incomes  { get; init; } = [];
            public IReadOnlyList<SettlementExpense> Expenses { get; init; } = [];

            /// <summary>Driver's income percentage share (0–100).</summary>
            public decimal DriverIncomePercent { get; init; }

            /// <summary>Driver's CNG cost percentage share (0–100).</summary>
            public decimal DriverCngPercent    { get; init; }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Audit Trace
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Human-readable step-by-step explanation of a calculation.
        /// Attached to the result of <see cref="Calculate"/> for display in receipts
        /// and for future dispute resolution.
        ///
        /// Example steps:
        ///   "Total Income  = ₹5,000 (Ola) + ₹7,500 (Rapido) = ₹12,500"
        ///   "Driver Share  = ₹12,500 × 50% = ₹6,250"
        ///   "Owner CNG     = ₹800 × 60% = ₹480"
        ///   "Net Payable   = ₹6,250 − ₹0 − ₹10,000 + ₹480 + ₹200 = −₹3,070 (Driver pays Owner)"
        /// </summary>
        public sealed class CalculationTrace
        {
            private readonly List<string> _steps = new();

            /// <summary>Read-only ordered list of calculation steps.</summary>
            public IReadOnlyList<string> Steps => _steps;

            /// <summary>Full trace as a newline-separated string (for PDF/share receipts).</summary>
            public string AsText() => string.Join("\n", _steps);

            internal void Add(string step) => _steps.Add(step);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Calculation Result
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Combined output of a settlement calculation — the populated Settlement entity
        /// and an audit trace for receipt generation.
        /// </summary>
        public sealed class CalculationResult
        {
            /// <summary>Fully populated Settlement, ready to persist to SQLite.</summary>
            public required Settlement Settlement { get; init; }

            /// <summary>Step-by-step audit explanation of how the result was reached.</summary>
            public required CalculationTrace Trace { get; init; }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Public API
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Computes a full settlement from the supplied request.
        /// Returns both the populated <see cref="Settlement"/> and an audit trace.
        ///
        /// BACKWARD COMPATIBILITY NOTE:
        ///   This overload is the primary API. Callers that only need the Settlement
        ///   (e.g., <see cref="ViewModels.SettlementEntryViewModel"/>) can call
        ///   <see cref="Calculate(CalculationRequest)"/> which unwraps the result.
        /// </summary>
        public CalculationResult CalculateWithTrace(CalculationRequest request)
        {
            Validate(request);
            var trace = new CalculationTrace();
            var settlement = ComputeSettlement(request, trace);

            return new CalculationResult
            {
                Settlement = settlement,
                Trace      = trace
            };
        }

        /// <summary>
        /// Convenience overload that returns only the <see cref="Settlement"/>.
        /// Maintains backward compatibility with existing ViewModel call sites.
        /// </summary>
        public Settlement Calculate(CalculationRequest request)
            => CalculateWithTrace(request).Settlement;

        // ══════════════════════════════════════════════════════════════════════
        //  Core Formula  — all arithmetic via Money value object
        // ══════════════════════════════════════════════════════════════════════

        private static Settlement ComputeSettlement(CalculationRequest request, CalculationTrace trace)
        {
            var settlement = new Settlement
            {
                // BUG-5 fix: store only the date portion — UTC vs local time-of-day
                // never causes the record to fall outside a day-boundary range query.
                Date      = request.Date.Date,
                CreatedAt = DateTime.UtcNow,
                ShiftType = request.ShiftType,

                // Immutable snapshots — preserve driver/vehicle names at time of entry.
                DriverId              = request.Driver.Id,
                DriverNameSnapshot    = request.Driver.DriverName,
                DriverTypeSnapshot    = request.Driver.DriverIncomePercent >= 100m
                    ? DriverType.SelfDriven
                    : DriverType.Hired,

                VehicleId             = request.Vehicle.Id,
                VehicleNumberSnapshot = request.Vehicle.VehicleNumber,

                // Collections (stored as child rows in SQLite).
                PlatformIncomes = request.Incomes.ToList(),
                ExpenseItems    = request.Expenses.ToList(),

                // Record which formula version produced this settlement.
                CalculatorVersion = CurrentVersion
            };

            // ── Step 1: Aggregate Platform Incomes ───────────────────────────

            // Build platform summary for trace (e.g. "Ola ₹5,000 + Rapido ₹7,500")
            var incomeParts = request.Incomes
                .Select(i => $"{i.PlatformName} ₹{i.OperatorBill:N0}")
                .ToList();
            var incomeSummary = incomeParts.Count > 0
                ? string.Join(" + ", incomeParts)
                : "₹0";

            var totalIncome        = Money.Of(request.Incomes.Sum(i => i.OperatorBill));
            var totalCashCollected = Money.Of(request.Incomes.Sum(i => i.CashCollected));

            settlement.TotalIncome        = totalIncome.Amount;
            settlement.TotalCashCollected = totalCashCollected.Amount;

            trace.Add($"Total Income        = {incomeSummary} = {totalIncome}");
            trace.Add($"Cash Collected      = {totalCashCollected}");

            // ── Step 2: Driver Income Share ──────────────────────────────────

            var driverShare = totalIncome.Percent(request.DriverIncomePercent);

            settlement.DriverShare = driverShare.Amount;

            trace.Add($"Driver Share        = {totalIncome.PercentTrace(request.DriverIncomePercent)}  " +
                      $"(Driver {request.DriverIncomePercent}%)");

            // ── Step 3: CNG Split ────────────────────────────────────────────

            var totalCng = Money.Of(
                request.Expenses
                    .Where(e => e.Type == ExpenseType.CNG)
                    .Sum(e => e.Amount));

            decimal ownerCngPercent  = 100m - request.DriverCngPercent;

            var ownerCngShare  = totalCng.Percent(ownerCngPercent);
            var driverCngShare = totalCng.Percent(request.DriverCngPercent);

            settlement.OwnerCngShare  = ownerCngShare.Amount;
            settlement.DriverCngShare = driverCngShare.Amount;

            if (totalCng > Money.Zero)
            {
                trace.Add($"CNG Total           = {totalCng}");
                trace.Add($"Owner CNG Share     = {totalCng.PercentTrace(ownerCngPercent)}  " +
                          $"(Owner {ownerCngPercent}%)");
                trace.Add($"Driver CNG Share    = {totalCng.PercentTrace(request.DriverCngPercent)}  " +
                          $"(Driver {request.DriverCngPercent}%)");
            }
            else
            {
                trace.Add("CNG                 = ₹0 (no CNG entered)");
            }

            // ── Step 4: Owner Expenses (non-driver-fault) ────────────────────

            // BUG-2 fix: DriverChallan (Name="DriverChallan") is treated as a deduction
            // from the driver's net haq — NOT added to owner expense pile.
            // D6-3: also includes the new first-class OwnerChallan type (Phase 6 enum addition).
            // Existing rows stored as Other continue to match the Other branch below.
            var totalOwnerExpenses = Money.Of(
                request.Expenses
                    .Where(e => e.Type == ExpenseType.Toll
                             || e.Type == ExpenseType.Parking
                             || e.Type == ExpenseType.OwnerChallan
                             || (e.Type == ExpenseType.Other && e.Name != "DriverChallan"))
                    .Sum(e => e.Amount));

            settlement.TotalOwnerExpenses = totalOwnerExpenses.Amount;

            if (totalOwnerExpenses > Money.Zero)
                trace.Add($"Owner Expenses      = {totalOwnerExpenses}  (Toll/Parking/Misc)");

            // ── Step 5: Driver-Fault Challan ─────────────────────────────────

            var driverChallanTotal = Money.Of(
                request.Expenses
                    .Where(e => e.Type == ExpenseType.Other && e.Name == "DriverChallan")
                    .Sum(e => e.Amount));

            settlement.DriverChallanTotal = driverChallanTotal.Amount;

            if (driverChallanTotal > Money.Zero)
                trace.Add($"Driver Challan      = −{driverChallanTotal}  " +
                          "(deducted from driver haq — driver-fault)");

            // ── Step 6: Net Driver Payable ───────────────────────────────────
            //
            //   NetDriverPayable = DriverShare
            //                    − DriverChallanTotal   (driver-fault deduction)
            //                    − TotalCashCollected   (already in driver's pocket)
            //                    + OwnerCngShare        (owner refunds fuel portion)
            //                    + TotalOwnerExpenses   (owner refunds expenses driver fronted)
            //
            // (+) → Owner must pay driver
            // (−) → Driver must pay owner

            var netDriverPayable =
                driverShare
                - driverChallanTotal
                - totalCashCollected
                + ownerCngShare
                + totalOwnerExpenses;

            settlement.NetDriverPayable = netDriverPayable.Amount;

            var netDirection = netDriverPayable.IsPositive ? "Owner pays Driver ✓"
                             : netDriverPayable.IsNegative ? "Driver pays Owner ✓"
                             : "Hisaab Barabar (Settled Exactly) ✓";

            trace.Add(
                $"Net Driver Payable  = {driverShare} − {driverChallanTotal} − " +
                $"{totalCashCollected} + {ownerCngShare} + {totalOwnerExpenses} " +
                $"= {netDriverPayable}");
            trace.Add($"Result              → {netDirection}");

            // Phase 3 — stamp integrity hash so every new settlement is tamper-detectable
            new SettlementIntegrityService().StampIntegrity(settlement, CurrentVersion);

            return settlement;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Validation
        // ══════════════════════════════════════════════════════════════════════

        private static void Validate(CalculationRequest request)
        {
            if (request.Driver  is null) throw new ArgumentException("Driver is required.");
            if (request.Vehicle is null) throw new ArgumentException("Vehicle is required.");

            if (request.Incomes is null || !request.Incomes.Any())
                throw new ArgumentException("At least one platform income entry is required.");

            if (request.DriverIncomePercent is < 0m or > 100m)
                throw new ArgumentException(
                    $"DriverIncomePercent must be 0–100. Got: {request.DriverIncomePercent}");

            if (request.DriverCngPercent is < 0m or > 100m)
                throw new ArgumentException(
                    $"DriverCngPercent must be 0–100. Got: {request.DriverCngPercent}");

            foreach (var income in request.Incomes)
            {
                if (income.OperatorBill  < 0m || income.CashCollected < 0m)
                    throw new ArgumentException(
                        $"Income values cannot be negative for '{income.PlatformName}'.");

                if (income.CashCollected > income.OperatorBill)
                    throw new ArgumentException(
                        $"Cash collected (₹{income.CashCollected}) cannot exceed " +
                        $"Operator Bill (₹{income.OperatorBill}) for '{income.PlatformName}'.");
            }

            foreach (var expense in request.Expenses)
            {
                if (expense.Amount < 0m)
                    throw new ArgumentException(
                        $"Expense amount cannot be negative for '{expense.Name}'.");
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Historical Trace — reconstructs from persisted Settlement fields
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Reconstructs a <see cref="CalculationTrace"/> from a <b>persisted</b>
        /// <see cref="Settlement"/> record, without re-running the calculator.
        ///
        /// Use this when displaying formula details for historical settlements where
        /// the original Driver/Vehicle objects may no longer match (name changes, etc.).
        /// All values are read from the immutable audit snapshot fields.
        ///
        /// REQUIRES: <c>settlement.PlatformIncomes</c> and <c>settlement.ExpenseItems</c>
        /// must be loaded (call <c>SettlementRepository.GetSettlementByIdAsync</c> which
        /// invokes <c>LoadCollectionsAsync</c> internally).
        /// </summary>
        public static CalculationTrace TraceFromSettlement(Settlement s)
        {

            var trace = new CalculationTrace();
            bool isSelfDriven = s.DriverTypeSnapshot == DriverType.SelfDriven;

            // ── Step 1: Platform Income Breakdown ────────────────────────────
            string incomeSummary;
            if (s.PlatformIncomes is { Count: > 0 })
            {
                var parts = s.PlatformIncomes.Select(p => $"{p.PlatformName} ₹{p.OperatorBill:N0}");
                incomeSummary = string.Join(" + ", parts);
            }
            else
            {
                incomeSummary = $"₹{s.TotalIncome:N0}";
            }
            trace.Add($"Total Income        = {incomeSummary} = ₹{s.TotalIncome:N0}");
            trace.Add($"Cash Collected      = ₹{s.TotalCashCollected:N0}");

            // ── Step 2: Driver Income Share ──────────────────────────────────
            if (isSelfDriven)
            {
                trace.Add("Driver Type         = Self Driven (100% income → Owner)");
            }
            else
            {
                // Derive percentage from stored values — avoids re-storing the raw percent.
                var pct = s.TotalIncome > 0
                    ? Math.Round(s.DriverShare / s.TotalIncome * 100m, 0)
                    : 0m;
                trace.Add($"Driver Share        = ₹{s.TotalIncome:N0} × {pct}% = ₹{s.DriverShare:N0}");
            }

            // ── Step 3: CNG Split ────────────────────────────────────────────
            var totalCng = s.DriverCngShare + s.OwnerCngShare;
            if (totalCng > 0m)
            {
                var ownerPct = totalCng > 0m
                    ? Math.Round(s.OwnerCngShare / totalCng * 100m, 0)
                    : 0m;
                var driverPct = 100m - ownerPct;
                trace.Add($"CNG Total           = ₹{totalCng:N0}");
                trace.Add($"Owner CNG Share     = ₹{totalCng:N0} × {ownerPct}% = ₹{s.OwnerCngShare:N0}  (Owner {ownerPct}%)");
                trace.Add($"Driver CNG Share    = ₹{totalCng:N0} × {driverPct}% = ₹{s.DriverCngShare:N0}  (Driver {driverPct}%)");
            }
            else
            {
                trace.Add("CNG                 = ₹0 (no CNG entered)");
            }

            // ── Step 4: Owner Expenses ───────────────────────────────────────
            if (s.TotalOwnerExpenses > 0m)
                trace.Add($"Owner Expenses      = ₹{s.TotalOwnerExpenses:N0}  (Toll / Parking / Misc)");

            // ── Step 5: Driver-Fault Challan ─────────────────────────────────
            if (s.DriverChallanTotal > 0m)
                trace.Add($"Driver Challan      = −₹{s.DriverChallanTotal:N0}  (deducted from driver haq — driver-fault)");

            // ── Step 6: Net Formula ──────────────────────────────────────────
            var netDirection = s.NetDriverPayable > 0m ? "Owner pays Driver ✓"
                             : s.NetDriverPayable < 0m ? "Driver pays Owner ✓"
                             : "Hisaab Barabar (Settled Exactly) ✓";

            trace.Add(
                $"Net Driver Payable  = ₹{s.DriverShare:N0} − ₹{s.DriverChallanTotal:N0} − " +
                $"₹{s.TotalCashCollected:N0} + ₹{s.OwnerCngShare:N0} + ₹{s.TotalOwnerExpenses:N0} " +
                $"= ₹{s.NetDriverPayable:N0}");
            trace.Add($"Result              → {netDirection}");

            // ── Metadata ─────────────────────────────────────────────────────
            if (s.CalculatorVersion > 0)
                trace.Add($"Calculator Version  = v{s.CalculatorVersion}  |  Shift = {s.ShiftType}  |  Receipt #{s.Id}");

            return trace;
        }

        // ── ISettlementCalculator explicit instance bridge ────────────────────
        // The interface requires an instance method; the static version is kept for
        // backward compatibility with call sites that use SettlementCalculator.TraceFromSettlement(s).
        CalculationTrace ISettlementCalculator.TraceFromSettlement(Settlement s)
            => TraceFromSettlement(s);
    }
}
