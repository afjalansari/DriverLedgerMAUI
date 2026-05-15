using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using DriverLedger.Models;
using DriverLedger.Services;
// Aliases to disambiguate from MAUI's identically-named types
using QColors     = QuestPDF.Helpers.Colors;
using QIContainer = QuestPDF.Infrastructure.IContainer;

namespace DriverLedger.Services
{
    /// <summary>
    /// Generates PDF receipts for settlements using QuestPDF (Community licence).
    /// Each section of the document is extracted into a private static helper to keep
    /// cognitive complexity of the public entry point well below the allowed threshold.
    /// </summary>
    public class PdfService : IPdfService
    {
        private readonly ISettlementCalculator _calculator;

        public PdfService(ISettlementCalculator calculator)
        {
            _calculator = calculator;
            QuestPDF.Settings.License = LicenseType.Community;
        }

        // ── Public entry point ───────────────────────────────────────────────────

        public async Task<string> GenerateSettlementReceiptAsync(Settlement settlement)
        {
            string vehicleNumber = settlement.VehicleNumberSnapshot;
            string fileName      = $"Settlement_{vehicleNumber}_{settlement.Date:yyyyMMdd}_#{settlement.Id}.pdf";

            // AppDataDirectory is safe to share on all Android versions (MAUI wraps it as content://)
            string exportDir = Path.Combine(FileSystem.AppDataDirectory, "Exports");
            Directory.CreateDirectory(exportDir);
            string filePath = Path.Combine(exportDir, fileName);

            // GeneratePdf() is synchronous and CPU-heavy — run on thread pool to avoid ANR.
            await Task.Run(() =>
                Document.Create(c => c.Page(page => BuildPage(page, settlement)))
                        .GeneratePdf(filePath));

            return filePath;
        }

        // ── Page orchestrator ────────────────────────────────────────────────────

        private void BuildPage(PageDescriptor page, Settlement s)
        {
            page.Size(PageSizes.A4);
            page.Margin(1.5f, Unit.Centimetre);
            page.PageColor(QColors.White);
            page.DefaultTextStyle(x => x.FontSize(11).FontFamily(Fonts.Verdana));

            page.Header().Element(c => BuildHeader(c, s));
            page.Content().PaddingVertical(10).Column(col => BuildContent(col, s));
            page.Footer().Element(c => BuildFooter(c, s));
        }

        // ── Section: Header ──────────────────────────────────────────────────────

        private void BuildHeader(QIContainer container, Settlement s)
        {
            container.Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("Daily Settlement Report")
                        .FontSize(22).SemiBold().FontColor(QColors.Blue.Medium);
                    col.Item().Text("DriverLedger App")
                        .FontSize(9).Italic().FontColor(QColors.Grey.Medium);
                });
                row.RelativeItem().AlignRight().Column(col =>
                {
                    col.Item().Text("DriverLedger").FontSize(14).SemiBold();
                    col.Item().Text($"Date: {s.Date:dd MMM yyyy}").FontSize(10);
                    col.Item().Text($"Receipt #: {s.Id}").FontSize(9).FontColor(QColors.Grey.Medium);
                });
            });
        }

        // ── Section: Content body ────────────────────────────────────────────────

        private void BuildContent(ColumnDescriptor col, Settlement s)
        {
            bool isSelfDriven = s.DriverTypeSnapshot == DriverType.SelfDriven;

            BuildDriverVehicleCard(col, s, isSelfDriven);
            BuildIncomeSection(col, s);

            if (!isSelfDriven)
                BuildDriverShareSection(col, s);

            BuildCashSection(col, s);
            BuildExpensesSection(col, s);

            col.Item().PaddingTop(30);
            BuildResultBlock(col, s, isSelfDriven);
        }

        // ── Section: Driver / Vehicle identity card ──────────────────────────────

        private void BuildDriverVehicleCard(ColumnDescriptor col, Settlement s, bool isSelfDriven)
        {
            col.Item().Border(1).BorderColor(QColors.Grey.Lighten3).Padding(10).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("DRIVER").FontSize(8).SemiBold().FontColor(QColors.Grey.Medium);
                    c.Item().Text(s.DriverNameSnapshot).FontSize(12).SemiBold();
                    c.Item().Text(isSelfDriven ? "Self Driven" : "Hired Driver").FontSize(9);
                });
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("VEHICLE").FontSize(8).SemiBold().FontColor(QColors.Grey.Medium);
                    c.Item().Text(s.VehicleNumberSnapshot).FontSize(12).SemiBold();
                });
            });
        }

        // ── Section: Total income ───────────────────────────────────────────────────────────────

        private void BuildIncomeSection(ColumnDescriptor col, Settlement s)
        {
            col.Item().PaddingTop(14).Text("1.  Total Kamai  (Total Income)").FontSize(12).SemiBold();
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c => { c.RelativeColumn(); c.ConstantColumn(110); });

                // Phase 3: show each platform aggregator as its own row
                if (s.PlatformIncomes is { Count: > 0 })
                {
                    foreach (var pi in s.PlatformIncomes)
                    {
                        table.Cell().Element(Cell).Text($"{pi.PlatformName}  (Bill: ₹{pi.OperatorBill:N0}  Cash: ₹{pi.CashCollected:N0})");
                        table.Cell().Element(Cell).AlignRight().Text($"₹{pi.OperatorBill:N0}");
                    }
                }
                else
                {
                    table.Cell().Element(Cell).Text("Operator Bill (Uber / Ola / Rapido)");
                    table.Cell().Element(Cell).AlignRight().Text($"₹{s.TotalIncome:N0}");
                }

                table.Cell().Background(QColors.Grey.Lighten4).Padding(5).Text("KULL KAMAI").SemiBold();
                table.Cell().Background(QColors.Grey.Lighten4).Padding(5).AlignRight()
                    .Text($"₹{s.TotalIncome:N0}").SemiBold();
            });
        }

        // ── Section: Driver share (hired drivers only) ───────────────────────────

        private void BuildDriverShareSection(ColumnDescriptor col, Settlement s)
        {
            col.Item().PaddingTop(12).Text("2.  Driver ka Hissa  (Driver Share)").FontSize(12).SemiBold();
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c => { c.RelativeColumn(); c.ConstantColumn(110); });
                var pct = s.TotalIncome > 0
                    ? Math.Round(s.DriverShare / s.TotalIncome * 100, 0)
                    : 0m;
                table.Cell().Element(Cell).Text("Share Percentage");
                table.Cell().Element(Cell).AlignRight().Text($"{pct}%");
                table.Cell().Element(Cell).Text("Driver Share Amount").SemiBold();
                table.Cell().Element(Cell).AlignRight().Text($"₹{s.DriverShare:N0}").SemiBold();
            });
        }

        // ── Section: Cash collected ──────────────────────────────────────────────

        private void BuildCashSection(ColumnDescriptor col, Settlement s)
        {
            col.Item().PaddingTop(12).Text("3.  Driver ne Cash Liya  (Cash Collected)").FontSize(12).SemiBold();
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c => { c.RelativeColumn(); c.ConstantColumn(110); });
                table.Cell().Element(Cell).Text("Total Cash Driver ne rakha");
                table.Cell().Element(Cell).AlignRight().Text($"₹{s.TotalCashCollected:N0}");
            });
        }

        // ── Section: Expenses ────────────────────────────────────────────────────

        private void BuildExpensesSection(ColumnDescriptor col, Settlement s)
        {
            col.Item().PaddingTop(12).Text("4.  Driver ne Kharcha Kiya  (Expenses)").FontSize(12).SemiBold();
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c => { c.RelativeColumn(); c.ConstantColumn(110); });

                if (s.OwnerCngShare > 0)
                {
                    table.Cell().Element(Cell).Text("CNG / Fuel — Owner Share (Refund)").Italic();
                    table.Cell().Element(Cell).AlignRight()
                        .Text($"₹{s.OwnerCngShare:N0}").FontColor(QColors.Green.Medium);
                }
                if (s.TotalOwnerExpenses > 0)
                {
                    table.Cell().Element(Cell).Text("Other Expenses (Toll / Parking / Repair)");
                    table.Cell().Element(Cell).AlignRight().Text($"₹{s.TotalOwnerExpenses:N0}");
                }

                var total = Math.Round(s.OwnerCngShare + s.TotalOwnerExpenses, 2);
                table.Cell().Background(QColors.Grey.Lighten4).Padding(5).Text("OWNER NE MILA DIYA").SemiBold();
                table.Cell().Background(QColors.Grey.Lighten4).Padding(5).AlignRight()
                    .Text($"₹{total:N0}").SemiBold();
            });
        }

        // ── Section: Result block + formula breakdown ────────────────────────────

        private void BuildResultBlock(ColumnDescriptor col, Settlement s, bool isSelfDriven)
        {
            var net = s.NetDriverPayable;
            ResolveResultStyle(net, out string color, out string label, out string bg);

            col.Item().Background(bg).Border(2).BorderColor(color).Padding(16).Column(result =>
            {
                result.Item().AlignCenter().Text($"₹{Math.Abs(net):N0}")
                    .FontSize(52).SemiBold().FontColor(color);
                result.Item().AlignCenter().Text(label)
                    .FontSize(20).SemiBold().FontColor(color);
            });

            col.Item().PaddingTop(14);
            BuildFormulaBreakdown(col, s, net, label, color, isSelfDriven);
        }

        private void ResolveResultStyle(decimal net, out string color, out string label, out string bg)
        {
            if (net > 0)      { color = QColors.Green.Medium; label = "Driver ko milega ✓";  bg = QColors.Green.Lighten5; }
            else if (net < 0) { color = QColors.Red.Medium;   label = "Driver ko dena hai !"; bg = QColors.Red.Lighten5;   }
            else              { color = QColors.Grey.Medium;  label = "Hisaab Barabar ✓";   bg = QColors.Grey.Lighten5;  }
        }

        private void BuildFormulaBreakdown(
            ColumnDescriptor col, Settlement s, decimal net, string label, string color, bool isSelfDriven)
        {
            var trace = _calculator.TraceFromSettlement(s);

            col.Item().Background(QColors.Grey.Lighten5).Border(1)
                .BorderColor(QColors.Grey.Lighten3).Padding(12).Column(formula =>
            {
                formula.Item().Text("HISAAB KAISE HUWA?  (Calculation Breakdown)")
                    .FontSize(10).SemiBold().FontColor(QColors.Grey.Darken2);
                formula.Item().PaddingTop(8);

                // Render each audit trace step as a monospace PDF row
                foreach (var step in trace.Steps)
                {
                    // Highlight the result line
                    bool isResult  = step.StartsWith("Net Driver Payable") || step.StartsWith("Result");
                    bool isChallan = step.StartsWith("Driver Challan");

                    var item = formula.Item().Text(step).FontSize(9);

                    if (isResult)  item.SemiBold().FontColor(color);
                    else if (isChallan) item.FontColor(QColors.Red.Medium);
                    else item.FontColor(QColors.Grey.Darken1);
                }

                formula.Item().PaddingTop(6).LineHorizontal(1).LineColor(QColors.Grey.Medium);
                formula.Item().PaddingTop(6)
                    .Text($"   FINAL  =  ₹{Math.Abs(net):N0}   →  {label}")
                    .FontSize(11).SemiBold().FontColor(color);
            });
        }

        // ── Section: Footer ───────────────────────────────────────────────────────────────

        private void BuildFooter(QIContainer container, Settlement s)
        {
            container.Column(footer =>
            {
                footer.Item().PaddingTop(10).Row(sig =>
                {
                    sig.RelativeItem().Column(c =>
                    {
                        c.Item().PaddingTop(22).LineHorizontal(1);
                        c.Item().AlignCenter().Text("Owner ka Sign").FontSize(8).FontColor(QColors.Grey.Medium);
                    });
                    sig.ConstantItem(40);
                    sig.RelativeItem().Column(c =>
                    {
                        c.Item().PaddingTop(22).LineHorizontal(1);
                        c.Item().AlignCenter().Text("Driver ka Sign").FontSize(8).FontColor(QColors.Grey.Medium);
                    });
                });
                footer.Item().PaddingTop(6).AlignCenter().Text(x =>
                {
                    x.Span("Generated by DriverLedger  •  ").FontSize(8).FontColor(QColors.Grey.Medium);
                    x.Span($"{DateTime.Now:dd MMM yyyy, hh:mm tt}").FontSize(8).FontColor(QColors.Grey.Medium);
                    x.Span($"  •  v{s.CalculatorVersion}").FontSize(8).FontColor(QColors.Grey.Medium);
                });
            });
        }

        // ── Shared helpers ───────────────────────────────────────────────────────

        /// <summary>Standard table cell style: bottom border + vertical padding.</summary>
        private QIContainer Cell(QIContainer c) =>
            c.BorderBottom(1).BorderColor(QColors.Grey.Lighten4).PaddingVertical(6);

        // L3 fix: NotImplementedException replaced with NotSupportedException — a more
        // semantically correct exception for planned-but-not-yet-built features. Callers
        // must catch this or check before calling to avoid silent crashes.
        public Task<string> GenerateMonthlyReportAsync(int year, int month) =>
            throw new NotSupportedException(
                "Monthly Report is not yet available (planned for Phase 4). " +
                "Check IPdfService.GenerateMonthlyReportAsync before calling.");
    }
}
