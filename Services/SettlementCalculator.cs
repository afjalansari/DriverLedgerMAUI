using DriverLedger.Models;
using System.Collections.Generic;
using System.Linq;

namespace DriverLedger.Services
{
    public class SettlementCalculator
    {
        public class CalculationRequest
        {
            public DateTime Date { get; set; }
            public required Driver Driver { get; set; }
            public required Vehicle Vehicle { get; set; }
            public string ShiftType { get; set; } = "Day";
            public List<PlatformIncome> Incomes { get; set; } = new();
            public List<SettlementExpense> Expenses { get; set; } = new();
            
            // Percentage Splits (Business Rules)
            public decimal DriverIncomePercent { get; set; }
            public decimal DriverCngPercent { get; set; }
        }

        public Settlement Calculate(CalculationRequest request)
        {
            Validate(request);

            var settlement = new Settlement
            {
                Date = request.Date,
                CreatedAt = DateTime.UtcNow,
                ShiftType = request.ShiftType,

                // 1. Capture Snapshots
                DriverId = request.Driver.Id,
                DriverNameSnapshot = request.Driver.DriverName,
                DriverTypeSnapshot = request.Driver.DriverIncomePercent >= 100m
                    ? DriverType.SelfDriven
                    : DriverType.Hired,
                
                VehicleId = request.Vehicle.Id,
                VehicleNumberSnapshot = request.Vehicle.VehicleNumber,

                // 2. Map Collections
                PlatformIncomes = request.Incomes.ToList(),
                ExpenseItems = request.Expenses.ToList()
            };

            // 3. Aggregate Incomes
            settlement.TotalIncome = Math.Round(request.Incomes.Sum(i => i.OperatorBill), 2);
            settlement.TotalCashCollected = Math.Round(request.Incomes.Sum(i => i.CashCollected), 2);

            // 4. Calculate Share
            settlement.DriverShare = Math.Round((settlement.TotalIncome * request.DriverIncomePercent) / 100m, 2);

            // 5. Aggregate Expenses
            var totalCng = Math.Round(request.Expenses.Where(e => e.Type == ExpenseType.CNG).Sum(e => e.Amount), 2);
            
            // Owner's CNG share is what the owner pays (Total - DriverShare)
            // If Driver pays 60%, Owner pays 40%.
            decimal ownerCngPercent = 100m - request.DriverCngPercent;
            settlement.OwnerCngShare = Math.Round((totalCng * ownerCngPercent) / 100m, 2);

            // Owner Expenses: Toll + Parking + Other
            settlement.TotalOwnerExpenses = Math.Round(request.Expenses
                .Where(e => e.Type == ExpenseType.Toll || e.Type == ExpenseType.Parking || e.Type == ExpenseType.Other)
                .Sum(e => e.Amount), 2);

            // 6. FINAL SETTLEMENT FORMULA (Audit Core)
            // NetDriverPayable = DriverShare - TotalCashCollected + OwnerCngShare + TotalOwnerExpenses
            settlement.NetDriverPayable = Math.Round(
                settlement.DriverShare 
                - settlement.TotalCashCollected 
                + settlement.OwnerCngShare 
                + settlement.TotalOwnerExpenses, 2);

            return settlement;
        }

        private void Validate(CalculationRequest request)
        {
            if (request.Driver == null) throw new ArgumentException("Driver is required");
            if (request.Vehicle == null) throw new ArgumentException("Vehicle is required");
            if (request.Incomes == null || !request.Incomes.Any()) 
                throw new ArgumentException("At least one platform entry is required");

            foreach (var income in request.Incomes)
            {
                if (income.CashCollected > income.OperatorBill)
                    throw new ArgumentException($"Cash collected ({income.CashCollected}) cannot exceed Operator Bill ({income.OperatorBill}) for {income.PlatformName}");

                if (income.OperatorBill < 0 || income.CashCollected < 0)
                    throw new ArgumentException("Income values cannot be negative");
            }

            foreach (var expense in request.Expenses)
            {
                if (expense.Amount < 0)
                    throw new ArgumentException("Expense amounts cannot be negative");
            }
        }
    }
}
