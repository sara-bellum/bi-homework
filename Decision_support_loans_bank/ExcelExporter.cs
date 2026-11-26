using System.Data;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace Decision_support_loans_bank;

public sealed record ReportSnapshot(List<Loan> Loans, DateTime From, DateTime Through, int Lookback, int Horizon,
    double Funding, double Operating, double LowerMargin, double UpperMargin, double RateChange, double Elasticity,
    string Scope, DateTime CreatedAt);

// Writes standard Office Open XML directly. Excel is not needed to generate the report.
// Formulas have verified cached values and the workbook requests full recalculation on opening.
public static class ExcelExporter
{
    private static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace C = "http://schemas.openxmlformats.org/drawingml/2006/chart";
    private static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private static readonly XNamespace Xdr = "http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing";
    private static readonly XNamespace P = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace T = "http://schemas.openxmlformats.org/package/2006/content-types";
    private sealed record Cell(int Row, int Column, object? Value, int Style = 0, string? Formula = null);
    private sealed class Sheet(string name)
    {
        public string Name { get; } = name;
        public List<Cell> Cells { get; } = new();
        public List<XElement> Rules { get; } = new();
        public string? Filter { get; set; }
        public bool Hidden { get; set; }
        public double[] Widths { get; set; } = Array.Empty<double>();
        public int TotalRow { get; set; }
        public string? Chart { get; set; }
        public string ChartKind { get; set; } = "bar";
        public string ChartTitle { get; set; } = "";
        public List<(string Name, string Categories, string Values, List<string> Labels, List<double> Numbers)> Series { get; } = new();
        public int ChartRow { get; set; } = 23;
        public void Put(int row, int col, object? value, int style = 0, string? formula = null) => Cells.Add(new Cell(row, col, value, style, formula));
        public void Intro(string title, string note) { Put(1, 1, title, 1); Put(2, 1, note, 9); }
        public void Headers(params string[] names) { for (int i = 0; i < names.Length; i++) Put(4, i + 1, names[i], 2); }
    }
    public static void Write(string path, ReportSnapshot snapshot)
    {
        if (snapshot.LowerMargin >= snapshot.UpperMargin) throw new ArgumentException("The lower margin threshold must be below the upper threshold.");
        var technical = new Sheet("Technical") { Hidden = true };
        technical.Intro("Calculation details", "Supporting data and formulas. Unhide this sheet in Excel to inspect the calculations.");
        var sheets = new List<Sheet> { Settings(snapshot), Portfolio(snapshot, technical), Forecast(snapshot, technical), Profit(snapshot, technical), Scenarios(snapshot), Geography(snapshot), technical, Debtors(snapshot), CrossSelling(snapshot), EarlyRepayment(snapshot) };
        WriteWorkbook(path, sheets, 1);
    }
    public static void WriteCurrentTable(string path, DataTable table, string title, string scope)
        => WriteWorkbook(path, new List<Sheet> { Current(table, title, scope) }, 0);
    private static void WriteWorkbook(string path, List<Sheet> sheets, int activeTab)
    {
        AtomicFile.Write(path, temp =>
        {
            using var zip = ZipFile.Open(temp, ZipArchiveMode.Create); var content = new XElement(T + "Types", new XElement(T + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")), new XElement(T + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")));
            void Type(string name, string mime) => content.Add(new XElement(T + "Override", new XAttribute("PartName", "/" + name), new XAttribute("ContentType", mime)));
            Type("xl/workbook.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"); Type("xl/styles.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml");
            var bookSheets = new XElement(S + "sheets"); var relationships = new XElement(P + "Relationships");
            for (int i = 0; i < sheets.Count; i++)
            {
                var sheet = sheets[i]; int index = i + 1; bookSheets.Add(new XElement(S + "sheet", new XAttribute("name", sheet.Name), new XAttribute("sheetId", index), new XAttribute("state", sheet.Hidden ? "hidden" : "visible"), new XAttribute(R + "id", "rId" + index)));
                relationships.Add(Rel("rId" + index, "worksheet", "worksheets/sheet" + index + ".xml"));
                Type($"xl/worksheets/sheet{index}.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml");
                PutXml(zip, $"xl/worksheets/sheet{index}.xml", Worksheet(sheet, index));
                var sheetRelationships = new XElement(P + "Relationships");
                if (sheet.Series.Count > 0)
                {
                    Type($"xl/charts/chart{index}.xml", "application/vnd.openxmlformats-officedocument.drawingml.chart+xml"); Type($"xl/drawings/drawing{index}.xml", "application/vnd.openxmlformats-officedocument.drawing+xml");
                    sheetRelationships.Add(Rel("rIdChart", "drawing", $"../drawings/drawing{index}.xml"));
                    PutXml(zip, $"xl/drawings/_rels/drawing{index}.xml.rels", new XElement(P + "Relationships", Rel("rId1", "chart", $"../charts/chart{index}.xml")));
                    PutXml(zip, $"xl/drawings/drawing{index}.xml", Drawing(sheet.ChartRow, index)); PutXml(zip, $"xl/charts/chart{index}.xml", Chart(sheet));
                }
                if (sheet.Filter != null)
                {
                    Type($"xl/tables/table{index}.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.table+xml");
                    sheetRelationships.Add(Rel("rIdTable", "table", $"../tables/table{index}.xml"));
                    PutXml(zip, $"xl/tables/table{index}.xml", Table(sheet, index));
                }
                if (sheetRelationships.HasElements) PutXml(zip, $"xl/worksheets/_rels/sheet{index}.xml.rels", sheetRelationships);
            }
            relationships.Add(Rel("rIdStyles", "styles", "styles.xml"));
            PutXml(zip, "_rels/.rels", new XElement(P + "Relationships", Rel("rId1", "officeDocument", "xl/workbook.xml")));
            PutXml(zip, "xl/workbook.xml", new XElement(S + "workbook", new XAttribute(XNamespace.Xmlns + "r", R), new XElement(S + "bookViews", new XElement(S + "workbookView", new XAttribute("activeTab", activeTab))), bookSheets, new XElement(S + "calcPr", new XAttribute("calcId", 191029), new XAttribute("calcMode", "auto"), new XAttribute("fullCalcOnLoad", 1), new XAttribute("forceFullCalc", 1))));
            PutXml(zip, "xl/_rels/workbook.xml.rels", relationships); PutXml(zip, "xl/styles.xml", Styles()); PutXml(zip, "[Content_Types].xml", content);
        });
    }
    private static Sheet Settings(ReportSnapshot x)
    {
        var s = new Sheet("Settings") { Widths = new[] { 35d, 22, 15, 15, 15, 15 } };
        s.Intro("Report settings", "Blue cells can be edited. Generate a new report to change the dates or selected loans.");
        var values = new (string Label, object Value, int Style)[]{
            ("Issue dates",$"{x.From:dd MMM yyyy} to {x.Through:dd MMM yyyy}",16),
            ("Selected data",x.Scope,16),("Selected loans",x.Loans.Count,5),
            ("Annual funding cost",x.Funding/100,6),("Operating cost per loan",x.Operating/100,6),
            ("Lower margin threshold",x.LowerMargin/100,6),("Upper margin threshold",x.UpperMargin/100,6),
            ("History window (months)",x.Lookback,5),("Forecast horizon (months)",x.Horizon,5),
            ("Rate change (percentage points)",x.RateChange,7),("Demand elasticity",x.Elasticity,7),
            ("Report generated",x.CreatedAt.ToString("dd MMM yyyy HH:mm"),16),
            ("Source","Supplied Lending Club sample. Amounts in USD. Customer IDs are anonymized.",16),
            ("Cost assumptions","Funding uses original principal and estimated duration. Operating cost applies once over the loan lifetime.",16),
            ("Repayment status","The source snapshot records outcomes. The issue-date filter does not reconstruct historical statuses.",16),
            ("Rate scenarios","Elasticity is assumed. Application and rejected-request histories are unavailable.",16),
            ("Product suggestions","Profile rules are used. Actual purchase histories are unavailable.",16),
            ("Forecast","Linear sample trend. No seasonality or prediction interval is modeled.",16)};
        for (int i = 0; i < values.Length; i++) { s.Put(i + 3, 1, values[i].Label); s.Put(i + 3, 2, values[i].Value, values[i].Style); }
        var start = new DateTime(x.Through.Year, x.Through.Month, 1).AddMonths(1 - x.Lookback); if (start < x.From) start = x.From;
        var window = x.Loans.Where(l => l.Issued >= start).ToList(); double amount = window.Sum(l => l.Amount);
        s.Put(22, 1, "Historical weighted interest rate"); s.Put(22, 2, amount == 0 ? 0 : window.Sum(l => l.Amount * l.Rate) / amount / 100, 4);
        return s;
    }
    private static Sheet Portfolio(ReportSnapshot x, Sheet technical)
    {
        var s = new Sheet("Portfolio") { Widths = new[] { 32d, 12, 23, 16, 16, 15 } };
        s.Intro("Loan portfolio", "Amounts in USD. Shares refer to the selected loans.");
        s.Headers("Loan category", "Loans", "Amount (USD)", "Amount share", "Loan share", "Average rate");
        var groups = x.Loans.GroupBy(l => l.Category).OrderByDescending(g => g.Sum(l => l.Amount)).ToList(); int total = 5 + groups.Count; s.TotalRow = total;
        double amount = x.Loans.Sum(l => l.Amount);
        for (int i = 0; i < groups.Count; i++)
        {
            int r = i + 5; var g = groups[i]; double value = g.Sum(l => l.Amount);
            s.Put(r, 1, g.Key); s.Put(r, 2, g.Count(), 5); s.Put(r, 3, value, 3); s.Put(r, 4, amount == 0 ? 0 : value / amount, 4, $"IFERROR(C{r}/$C${total},0)"); s.Put(r, 5, x.Loans.Count == 0 ? 0 : (double)g.Count() / x.Loans.Count, 4, $"IFERROR(B{r}/$B${total},0)"); s.Put(r, 6, g.Average(l => l.Rate) / 100, 4);
        }
        s.Put(total, 1, "Total"); s.Put(total, 2, x.Loans.Count, 5, groups.Count == 0 ? "0" : $"SUM(B5:B{total - 1})"); s.Put(total, 3, amount, 3, groups.Count == 0 ? "0" : $"SUM(C5:C{total - 1})");
        if (groups.Count > 0)
        {
            s.Filter = $"A4:F{total - 1}"; s.Rules.Add(DataBars($"C5:C{total - 1}")); s.ChartKind = "pie"; s.ChartTitle = "Share of granted amount"; s.ChartRow = total + 2;
            var top = groups.Take(5).Select(g => (g.Key, g.Sum(l => l.Amount))).ToList(); if (groups.Count > 5) top.Add(("Other categories", groups.Skip(5).Sum(g => g.Sum(l => l.Amount))));
            technical.Put(4, 17, "Pie category", 2); technical.Put(4, 18, "Amount", 2);
            for (int i = 0; i < top.Count; i++) { int r = i + 5; technical.Put(r, 17, top[i].Item1); technical.Put(r, 18, top[i].Item2, 3, i < 5 ? $"Portfolio!C{r}" : $"SUM(Portfolio!C10:C{total - 1})"); }
            s.Series.Add(("Granted amount", $"'Technical'!$Q$5:$Q${top.Count + 4}", $"'Technical'!$R$5:$R${top.Count + 4}", top.Select(t => t.Item1).ToList(), top.Select(t => t.Item2).ToList()));
        }
        return s;
    }
    private static Sheet Forecast(ReportSnapshot x, Sheet technical)
    {
        var s = new Sheet("Forecast") { Widths = new[] { 18d, 24, 24, 17, 18, 3, 25, 22 } };
        s.Intro("Monthly loan forecast", "Amounts in USD. Recorded values and forecasts are shown in separate columns.");
        s.Headers("Month", "Recorded amount", "Forecast amount", "Recorded loans", "Expected loans");
        var end = new DateTime(x.Through.Year, x.Through.Month, 1); var start = end.AddMonths(1 - x.Lookback); var selectedStart = new DateTime(x.From.Year, x.From.Month, 1); if (start < selectedStart) start = selectedStart;
        var history = Analytics.Monthly(x.Loans, start, end); var points = Analytics.Forecast(history, x.Horizon); int last = history.Count + 4, lastAll = points.Count + 4;
        technical.Put(4, 13, "Month index", 2); technical.Put(4, 14, "Month label", 2); technical.Put(4, 15, "Forecast plot", 2);
        for (int i = 0; i < points.Count; i++)
        {
            int r = i + 5; var point = points[i]; s.Put(r, 1, DateTime.ParseExact(point.Month, "yyyy-MM", CultureInfo.InvariantCulture), 8); technical.Put(r, 13, i, 5); technical.Put(r, 14, DateTime.ParseExact(point.Month, "yyyy-MM", CultureInfo.InvariantCulture).ToString("MMM yyyy", CultureInfo.InvariantCulture));
            if (point.Kind == "Observed") { s.Put(r, 2, point.Volume, 3); s.Put(r, 4, point.Count, 5); if (i == history.Count - 1 && history.Count >= 2) technical.Put(r, 15, point.Volume, 3, $"Forecast!B{r}"); }
            else { s.Put(r, 3, point.Volume, 3, $"MAX(0,FORECAST('Technical'!M{r},$B$5:$B${last},'Technical'!$M$5:$M${last}))"); s.Put(r, 5, point.Count, 5, $"MAX(0,FORECAST('Technical'!M{r},$D$5:$D${last},'Technical'!$M$5:$M${last}))"); technical.Put(r, 15, point.Volume, 3, $"Forecast!C{r}"); }
        }
        s.Put(4, 7, "Forecast summary", 15); s.Put(5, 7, "Historical fit (R²)"); double mean = history.Average(h => h.Volume), sst = history.Sum(h => Math.Pow(h.Volume - mean, 2));
        if (history.Count >= 2 && sst > 0) { double mx = (history.Count - 1) / 2.0, den = Enumerable.Range(0, history.Count).Sum(i => Math.Pow(i - mx, 2)); double slope = Enumerable.Range(0, history.Count).Sum(i => (i - mx) * (history[i].Volume - mean)) / den; double sse = Enumerable.Range(0, history.Count).Sum(i => Math.Pow(history[i].Volume - (mean + slope * (i - mx)), 2)); s.Put(5, 8, Math.Clamp(1 - sse / sst, 0, 1), 10, $"RSQ(B5:B{last},'Technical'!M5:M{last})"); } else s.Put(5, 8, "Not available");
        s.Put(7, 7, "R² describes historical fit.", 9); s.Put(8, 7, "It does not measure future accuracy.", 9);
        s.Put(10, 7, "Forecast amount (USD)"); s.Put(10, 8, points.Where(p => p.Kind == "Forecast").Sum(p => p.Volume), 3, points.Count > history.Count ? $"SUM(C{last + 1}:C{lastAll})" : "0");
        s.Put(13, 7, "Expected loans"); s.Put(13, 8, points.Where(p => p.Kind == "Forecast").Sum(p => p.Count), 5, points.Count > history.Count ? $"SUM(E{last + 1}:E{lastAll})" : "0");
        s.Filter = $"A4:E{lastAll}"; s.ChartKind = "line"; s.ChartTitle = "Recorded and forecast lending (USD)"; s.ChartRow = Math.Max(lastAll + 2, 16);
        var labels = points.Select(p => DateTime.ParseExact(p.Month, "yyyy-MM", CultureInfo.InvariantCulture).ToString("MMM yyyy", CultureInfo.InvariantCulture)).ToList();
        s.Series.Add(("Recorded", $"'Technical'!$N$5:$N${lastAll}", $"'Forecast'!$B$5:$B${lastAll}", labels, points.Select(p => p.Kind == "Observed" ? p.Volume : double.NaN).ToList()));
        if (points.Count > history.Count) s.Series.Add(("Forecast", $"'Technical'!$N$5:$N${lastAll}", $"'Technical'!$O$5:$O${lastAll}", labels, points.Select((p, i) => i >= history.Count - 1 ? p.Volume : double.NaN).ToList())); return s;
    }
    private static Sheet Profit(ReportSnapshot x, Sheet technical)
    {
        var s = new Sheet("Profitability") { Widths = new[] { 32d, 14, 24, 23, 24, 19 } };
        s.Intro("Loan profitability", "USD. Closed loans only. Margin covers the full loan lifetime, not one year.");
        s.Headers("Loan category", "Closed loans", "Funded amount", "Assumed costs", "Net margin", "Lifetime margin");
        technical.Headers("Loan category", "Closed loans", "Funded principal", "Interest and penalties", "Net recoveries", "Principal loss", "Principal × years", "Funding cost", "Operating cost", "Net margin", "Lifetime margin");
        var groups = x.Loans.Where(l => l.Closed).GroupBy(l => l.Category).OrderByDescending(g => g.Sum(l => l.Net(x.Funding, x.Operating))).ToList(); var labels = new List<string>(); var margins = new List<double>();
        for (int i = 0; i < groups.Count; i++)
        {
            int r = i + 5; var g = groups[i]; double funded = g.Sum(l => l.Funded), exposure = g.Sum(l => l.Funded * l.ExposureYears), margin = g.Sum(l => l.Net(x.Funding, x.Operating)), cost = exposure * x.Funding / 100 + funded * x.Operating / 100;
            technical.Put(r, 1, g.Key); technical.Put(r, 2, g.Count(), 5); technical.Put(r, 3, funded, 3); technical.Put(r, 4, g.Sum(l => l.Interest + l.Fees), 3); technical.Put(r, 5, g.Sum(l => l.Recoveries - l.CollectionCost), 3); technical.Put(r, 6, g.Where(l => l.Bad).Sum(l => Math.Max(0, l.Funded - l.Principal)), 3); technical.Put(r, 7, exposure, 3); technical.Put(r, 8, exposure * x.Funding / 100, 3, $"G{r}*Settings!$B$6"); technical.Put(r, 9, funded * x.Operating / 100, 3, $"C{r}*Settings!$B$7"); technical.Put(r, 10, margin, 3, $"D{r}+E{r}-F{r}-H{r}-I{r}"); technical.Put(r, 11, funded == 0 ? 0 : margin / funded, 4, $"IFERROR(J{r}/C{r},0)");
            s.Put(r, 1, g.Key); s.Put(r, 2, g.Count(), 5); s.Put(r, 3, funded, 3); s.Put(r, 4, cost, 3, $"'Technical'!H{r}+'Technical'!I{r}"); s.Put(r, 5, margin, 3, $"'Technical'!J{r}"); s.Put(r, 6, funded == 0 ? 0 : margin / funded, 4, $"IFERROR(E{r}/C{r},0)"); labels.Add(g.Key); margins.Add(margin);
        }
        int total = 5 + groups.Count; s.TotalRow = total; s.Put(total, 1, "Total");
        for (int col = 2; col <= 5; col++) { string letter = Column(col); double value = s.Cells.Where(c => c.Column == col && c.Row >= 5 && c.Row < total).Sum(c => Convert.ToDouble(c.Value, CultureInfo.InvariantCulture)); s.Put(total, col, value, col == 2 ? 5 : 3, groups.Count == 0 ? "0" : $"SUM({letter}5:{letter}{total - 1})"); }
        double allFunded = groups.SelectMany(g => g).Sum(l => l.Funded); s.Put(total, 6, allFunded == 0 ? 0 : margins.Sum() / allFunded, 4, $"IFERROR(E{total}/C{total},0)");
        if (groups.Count > 0) { s.Filter = $"A4:F{total - 1}"; s.Rules.Add(Margins($"F5:F{total - 1}")); s.ChartTitle = "Estimated net margin by category (USD)"; s.ChartRow = total + 2; s.Series.Add(("Net margin", $"'Profitability'!$A$5:$A${total - 1}", $"'Profitability'!$E$5:$E${total - 1}", labels, margins)); }
        return s;
    }
    private static Sheet Scenarios(ReportSnapshot x)
    {
        var s = new Sheet("Interest scenarios") { Widths = new[] { 24d, 19, 21, 20, 26 } };
        s.Intro("Interest rate comparison", "USD. Demand response uses the elasticity assumption on Settings.");
        s.Headers("Scenario", "Interest rate", "Demand change", "Expected loans", "Expected amount");
        var start = new DateTime(x.Through.Year, x.Through.Month, 1).AddMonths(1 - x.Lookback); if (start < x.From) start = x.From; var window = x.Loans.Where(l => l.Issued >= start).ToList(); double amount = window.Sum(l => l.Amount), rate = amount == 0 ? 0 : window.Sum(l => l.Amount * l.Rate) / amount;
        var future = Analytics.Forecast(Analytics.Monthly(x.Loans, new DateTime(start.Year, start.Month, 1), new DateTime(x.Through.Year, x.Through.Month, 1)), x.Horizon).Where(p => p.Kind == "Forecast").ToList();
        if (rate <= 0 || future.Count == 0) { s.Put(5, 1, "Insufficient history for rate scenarios.", 9); return s; }
        if (rate - x.RateChange <= 0) throw new ArgumentException("Rate-change size must be smaller than the historical baseline rate.");
        var labels = new List<string>(); var volumes = new List<double>(); for (int i = 0; i < 3; i++)
        {
            int r = i + 5; string name = new[] { "Lower rate", "Unchanged rate", "Higher rate" }[i]; double change = (i - 1) * x.RateChange, factor = Analytics.DemandMultiplier(rate, change, x.Elasticity), volume = future.Sum(p => p.Volume) * factor;
            string multiplier = $"IF(AND(Settings!$B$22>0,B{r}>0),(B{r}/Settings!$B$22)^Settings!$B$13,NA())";
            s.Put(r, 1, name); s.Put(r, 2, (rate + change) / 100, 4, $"Settings!$B$22{(i == 0 ? "-Settings!$B$12/100" : i == 2 ? "+Settings!$B$12/100" : "")}"); s.Put(r, 3, factor - 1, 4, multiplier + "-1"); s.Put(r, 4, future.Sum(p => p.Count) * factor, 5, $"Forecast!$H$13*({multiplier})"); s.Put(r, 5, volume, 3, $"Forecast!$H$10*({multiplier})"); labels.Add(name); volumes.Add(volume);
        }
        s.Filter = "A4:E7"; s.ChartTitle = "Projected lending by interest rate (USD)"; s.ChartRow = 10; s.Series.Add(("Expected amount", "'Interest scenarios'!$A$5:$A$7", "'Interest scenarios'!$E$5:$E$7", labels, volumes)); return s;
    }
    private static Sheet Geography(ReportSnapshot x)
    {
        var s = new Sheet("Geography") { Widths = new[] { 28d, 14, 25, 18, 19 } };
        s.Intro("Lending by US state", "USD. Flagged loans include grace-period cases. Rates describe the selected sample.");
        s.Headers("US state", "Loans", "Amount (USD)", "Flagged loans", "Flagged rate"); var rows = GeographyService.Aggregate(x.Loans);
        for (int i = 0; i < rows.Count; i++) { int r = i + 5; var row = rows[i]; s.Put(r, 1, row.State); s.Put(r, 2, row.Loans, 5); s.Put(r, 3, row.Amount, 3); s.Put(r, 4, row.Flagged, 5); s.Put(r, 5, row.FlaggedRate, 4, $"IFERROR(D{r}/B{r},0)"); }
        int total = rows.Count + 5; s.TotalRow = total; s.Put(total, 1, "Total"); s.Put(total, 2, x.Loans.Count, 5, rows.Count == 0 ? "0" : $"SUM(B5:B{total - 1})"); s.Put(total, 3, x.Loans.Sum(l => l.Amount), 3, rows.Count == 0 ? "0" : $"SUM(C5:C{total - 1})"); s.Put(total, 4, x.Loans.Count(l => l.Bad), 5, rows.Count == 0 ? "0" : $"SUM(D5:D{total - 1})"); s.Put(total, 5, x.Loans.Count == 0 ? 0 : (double)x.Loans.Count(l => l.Bad) / x.Loans.Count, 4, $"IFERROR(D{total}/B{total},0)");
        if (rows.Count > 0) { s.Filter = $"A4:E{total - 1}"; s.Rules.Add(DataBars($"C5:C{total - 1}")); s.ChartTitle = "Top 10 states by granted amount (USD)"; s.ChartRow = total + 2; s.Series.Add(("Granted amount", $"'Geography'!$A$5:$A${Math.Min(10, rows.Count) + 4}", $"'Geography'!$C$5:$C${Math.Min(10, rows.Count) + 4}", rows.Take(10).Select(r => r.State).ToList(), rows.Take(10).Select(r => r.Amount).ToList())); }
        return s;
    }
    private static Sheet Debtors(ReportSnapshot x)
    {
        var s = new Sheet("Debtors and arrears") { Widths = new[] { 34d, 15, 19, 25, 26, 28 } };
        s.Intro("Debtors and arrears", "Flagged loans grouped by status. Portfolio share uses all loans in the global selection, including unflagged loans.");
        s.Headers("Payment status", "Flagged loans", "Portfolio share", "Reported balance", "Principal shortfall", "Review action");
        var groups = x.Loans.Where(l => l.Bad).GroupBy(l => l.Status).OrderByDescending(g => g.Max(l => l.Severity)).ToList();
        for (int i = 0; i < groups.Count; i++) { int r = i + 5; var g = groups[i]; s.Put(r, 1, Presentation.Status(g.Key)); s.Put(r, 2, g.Count(), 5); s.Put(r, 3, x.Loans.Count == 0 ? 0 : (double)g.Count() / x.Loans.Count, 4, $"IFERROR(B{r}/Settings!$B$5,0)"); s.Put(r, 4, g.Sum(l => l.Balance), 3); s.Put(r, 5, g.Sum(Presentation.Shortfall), 3); s.Put(r, 6, Presentation.Priority(g.First()), 16); }
        int total = groups.Count + 5; s.TotalRow = total; s.Put(total, 1, "Total flagged");
        foreach (int col in new[] { 2, 4, 5 }) { double value = s.Cells.Where(c => c.Column == col && c.Row >= 5).Sum(c => Convert.ToDouble(c.Value, CultureInfo.InvariantCulture)); s.Put(total, col, value, col == 2 ? 5 : 3, groups.Count == 0 ? "0" : $"SUM({Column(col)}5:{Column(col)}{total - 1})"); }
        s.Put(total, 3, x.Loans.Count == 0 ? 0 : (double)x.Loans.Count(l => l.Bad) / x.Loans.Count, 4, $"IFERROR(B{total}/Settings!$B$5,0)"); if (groups.Count > 0) s.Filter = $"A4:F{total - 1}"; return s;
    }
    private static Sheet CrossSelling(ReportSnapshot x)
    {
        var s = new Sheet("Cross-selling") { Widths = new[] { 30d, 18, 25, 23, 24 } };
        s.Intro("Cross-selling opportunities", "Profile-based suggestions for eligible low-risk borrowers. These are not observed purchases or verified product ownership.");
        s.Headers("Suggested product", "Eligible profiles", "Average annual income", "Average DTI", "Loan amount (USD)");
        var groups = x.Loans.Where(l => Analytics.Product(l) != null).GroupBy(l => Analytics.Product(l)!).OrderByDescending(g => g.Count()).ToList();
        for (int i = 0; i < groups.Count; i++) { int r = i + 5; var g = groups[i]; s.Put(r, 1, g.Key); s.Put(r, 2, g.Count(), 5); s.Put(r, 3, g.Average(l => l.Income), 3); s.Put(r, 4, g.Average(l => l.Dti!.Value) / 100, 4); s.Put(r, 5, g.Sum(l => l.Amount), 3); }
        if (groups.Count > 0) s.Filter = $"A4:E{groups.Count + 4}"; else s.Put(5, 1, "No eligible profiles.", 9); return s;
    }
    private static Sheet EarlyRepayment(ReportSnapshot x)
    {
        var s = new Sheet("Early repayment") { Widths = new[] { 32d, 17, 26, 23, 25, 28 } };
        s.Intro("Early repayment impact", "Fully paid at least three months before maturity. Unearned interest is an estimate, not a net loss. Export the tab table for profiles.");
        s.Headers("Loan category", "Early payoffs", "Unearned interest (USD)", "Interest share", "Average months saved", "Average annual income");
        var early = x.Loans.Where(l => l.Early).ToList(); var groups = early.GroupBy(l => l.Category).OrderByDescending(g => g.Sum(l => l.LostInterest)).ToList(); int total = groups.Count + 5; double interest = early.Sum(l => l.LostInterest);
        for (int i = 0; i < groups.Count; i++) { int r = i + 5; var g = groups[i]; s.Put(r, 1, g.Key); s.Put(r, 2, g.Count(), 5); s.Put(r, 3, g.Sum(l => l.LostInterest), 3); s.Put(r, 4, interest == 0 ? 0 : g.Sum(l => l.LostInterest) / interest, 4, $"IFERROR(C{r}/C{total},0)"); s.Put(r, 5, g.Average(l => l.Term - l.MonthsPaid!.Value), 17); s.Put(r, 6, g.Average(l => l.Income), 3); }
        s.TotalRow = total; s.Put(total, 1, "Total early payoffs"); s.Put(total, 2, early.Count, 5, groups.Count == 0 ? "0" : $"SUM(B5:B{total - 1})"); s.Put(total, 3, interest, 3, groups.Count == 0 ? "0" : $"SUM(C5:C{total - 1})"); if (groups.Count > 0) { s.Filter = $"A4:F{total - 1}"; s.Rules.Add(DataBars($"C5:C{total - 1}")); }
        return s;
    }
    private static Sheet Current(DataTable table, string title, string scope)
    {
        var s = new Sheet("Current table"); s.Intro(title, scope + " All matching rows in the displayed order; visible columns only."); var cols = table.Columns.Cast<DataColumn>().ToArray(); s.Headers(cols.Select(c => c.Caption).ToArray());
        s.Widths = cols.Select(c => c.DataType == typeof(string) ? Math.Clamp(Math.Max(c.Caption.Length + 3, table.AsEnumerable().Take(100).Select(r => Convert.ToString(r[c])?.Length ?? 0).DefaultIfEmpty().Max() + 2), 16, 45) : Math.Clamp(c.Caption.Length + 3, 15, 24)).Select(n => (double)n).ToArray();
        for (int row = 0; row < table.Rows.Count; row++) for (int col = 0; col < cols.Length; col++)
        {
            var value = table.Rows[row][col]; if (value == DBNull.Value) value = null; bool percent = cols[col].ColumnName.EndsWith("Pct", StringComparison.OrdinalIgnoreCase); if (percent && value != null) value = Convert.ToDouble(value) / 100;
            s.Put(row + 5, col + 1, value, percent ? 4 : value is double && (cols[col].ColumnName.Contains("Count") || cols[col].ColumnName == "Count") ? 17 : value is double ? 3 : value is int or long ? 5 : value is DateTime ? 8 : value is string t && t.Length > 45 ? 16 : 0);
        }
        if (cols.Length > 0 && table.Rows.Count > 0) s.Filter = $"A4:{Column(cols.Length)}{table.Rows.Count + 4}"; return s;
    }
    private static XElement DataBars(string range) => new(S + "conditionalFormatting", new XAttribute("sqref", range), new XElement(S + "cfRule", new XAttribute("type", "dataBar"), new XAttribute("priority", 10), new XElement(S + "dataBar", new XElement(S + "cfvo", new XAttribute("type", "min")), new XElement(S + "cfvo", new XAttribute("type", "max")), new XElement(S + "color", new XAttribute("rgb", "FF4682B4")))));
    private static XElement Margins(string range)
    {
        // INDIRECT permits cross-sheet thresholds in legacy conditional-formatting formulas.
        string lower = "INDIRECT(\"Settings!$B$8\")", upper = "INDIRECT(\"Settings!$B$9\")", first = range.Split(':')[0];
        return new XElement(S + "conditionalFormatting", new XAttribute("sqref", range),
            Rule(0, 1, $"{first}<{lower}"), Rule(1, 2, $"AND({first}>={lower},{first}<{upper})"), Rule(2, 3, $"{first}>={upper}"));
    }
    private static XElement Rule(int style, int priority, string formula) => new(S + "cfRule", new XAttribute("type", "expression"), new XAttribute("dxfId", style), new XAttribute("priority", priority), new XElement(S + "formula", formula));
    private static XElement Worksheet(Sheet sheet, int index)
    {
        int max = Math.Max(sheet.Widths.Length, sheet.Cells.Select(c => c.Column).DefaultIfEmpty(1).Max());
        int titleEnd = sheet.Name == "Forecast" ? 5 : sheet.Name == "Settings" ? 6 : max;
        var view = new XElement(S + "sheetView", new XAttribute("workbookViewId", 0), new XAttribute("showGridLines", 0), new XAttribute("zoomScale", 100));
        if (sheet.Filter != null) view.Add(new XElement(S + "pane", new XAttribute("xSplit", 1), new XAttribute("ySplit", 4), new XAttribute("topLeftCell", "B5"), new XAttribute("activePane", "bottomRight"), new XAttribute("state", "frozen")));
        var root = new XElement(S + "worksheet", new XAttribute(XNamespace.Xmlns + "r", R), new XElement(S + "sheetPr", new XElement(S + "pageSetUpPr", new XAttribute("fitToPage", 1))), new XElement(S + "sheetViews", view), new XElement(S + "sheetFormatPr", new XAttribute("defaultRowHeight", 24)));
        var columns = new XElement(S + "cols"); for (int col = 1; col <= max; col++) columns.Add(new XElement(S + "col", new XAttribute("min", col), new XAttribute("max", col), new XAttribute("width", col <= sheet.Widths.Length ? sheet.Widths[col - 1] : col == 1 ? 32 : 20), new XAttribute("customWidth", 1))); root.Add(columns);
        var data = new XElement(S + "sheetData");
        foreach (var cells in sheet.Cells.GroupBy(c => c.Row).OrderBy(g => g.Key))
        {
            double height = cells.Key == 1 ? 32 : cells.Key == 2 ? (sheet.Name == "Current table" ? 72 : 32) : cells.Key == 4 && sheet.Filter != null ? 34 : 24;
            if (sheet.Name == "Settings" && cells.Any(c => c.Column == 2 && c.Value is string))
            {
                int length = cells.Where(c => c.Column == 2).Select(c => (c.Value as string)?.Length ?? 0).DefaultIfEmpty().Max(); height = length > 170 ? 64 : length > 90 ? 46 : 28;
            }
            if (sheet.Name == "Forecast" && cells.Key is 7 or 8) height = 28;
            if (sheet.Name == "Current table" && cells.Key >= 5 && cells.Any(c => c.Style == 16)) height = 44;
            var row = new XElement(S + "row", new XAttribute("r", cells.Key), new XAttribute("ht", height), new XAttribute("customHeight", 1));
            foreach (var cell in cells.OrderBy(c => c.Column))
            {
                int style = cell.Style; if (cell.Row == sheet.TotalRow) style = style == 3 ? 12 : style == 5 ? 13 : style == 4 ? 14 : 11;
                var element = new XElement(S + "c", new XAttribute("r", Column(cell.Column) + cell.Row), new XAttribute("s", style));
                if (cell.Formula != null) element.Add(new XElement(S + "f", cell.Formula));
                if (cell.Value is string text) { element.SetAttributeValue("t", "inlineStr"); element.Add(new XElement(S + "is", new XElement(S + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), text))); }
                else if (cell.Value is DateTime date) element.Add(new XElement(S + "v", date.ToOADate().ToString("R", CultureInfo.InvariantCulture)));
                else if (cell.Value != null) element.Add(new XElement(S + "v", Convert.ToString(cell.Value, CultureInfo.InvariantCulture)));
                row.Add(element);
            }
            data.Add(row);
        }
        root.Add(data);
        var merges = new List<string> { $"A1:{Column(Math.Max(2, titleEnd))}1", $"A2:{Column(Math.Max(2, titleEnd))}2" };
        if (sheet.Name == "Settings") foreach (int row in new[] { 3, 4, 14, 15, 16, 17, 18, 19, 20 }) merges.Add($"B{row}:F{row}");
        if (sheet.Name == "Forecast") foreach (int row in new[] { 4, 7, 8 }) merges.Add($"G{row}:H{row}");
        root.Add(new XElement(S + "mergeCells", new XAttribute("count", merges.Count), merges.Select(range => new XElement(S + "mergeCell", new XAttribute("ref", range)))));
        foreach (var rule in sheet.Rules) root.Add(rule);
        if (sheet.Name == "Settings")
        {
            XElement Validation(string range, string op, string first, string? second = null) => new(S + "dataValidation", new XAttribute("type", "decimal"), new XAttribute("operator", op), new XAttribute("allowBlank", 0), new XAttribute("showErrorMessage", 1), new XAttribute("errorTitle", "Invalid assumption"), new XAttribute("error", "Check the permitted range. The lower margin threshold must be below the upper threshold."), new XAttribute("sqref", range), new XElement(S + "formula1", first), second == null ? null : new XElement(S + "formula2", second));
            root.Add(new XElement(S + "dataValidations", new XAttribute("count", 5), Validation("B6:B7", "between", "0", "1"), Validation("B8", "lessThan", "B9"), Validation("B9", "greaterThan", "B8"), Validation("B12", "between", "0", "MAX(0,B22*100-0.000001)"), Validation("B13", "between", "-5", "0")));
        }
        root.Add(new XElement(S + "pageMargins", new XAttribute("left", .3), new XAttribute("right", .3), new XAttribute("top", .5), new XAttribute("bottom", .5), new XAttribute("header", .2), new XAttribute("footer", .2)), new XElement(S + "pageSetup", new XAttribute("paperSize", 9), new XAttribute("orientation", "landscape"), new XAttribute("fitToWidth", 1), new XAttribute("fitToHeight", 0)));
        if (sheet.Series.Count > 0) root.Add(new XElement(S + "drawing", new XAttribute(R + "id", "rIdChart")));
        if (sheet.Filter != null) root.Add(new XElement(S + "tableParts", new XAttribute("count", 1), new XElement(S + "tablePart", new XAttribute(R + "id", "rIdTable")))); return root;
    }
    private static XElement Table(Sheet sheet, int index)
    {
        var headers = sheet.Cells.Where(c => c.Row == 4 && c.Column <= ColumnIndex(sheet.Filter!.Split(':')[1])).OrderBy(c => c.Column).ToList();
        var columns = new XElement(S + "tableColumns", new XAttribute("count", headers.Count)); var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in headers) { string name = Convert.ToString(header.Value) ?? "Column"; while (!used.Add(name)) name += " " + header.Column; columns.Add(new XElement(S + "tableColumn", new XAttribute("id", header.Column), new XAttribute("name", name))); }
        return new XElement(S + "table", new XAttribute("id", index), new XAttribute("name", "ReportTable" + index), new XAttribute("displayName", "ReportTable" + index), new XAttribute("ref", sheet.Filter!), new XAttribute("totalsRowShown", 0), new XElement(S + "autoFilter", new XAttribute("ref", sheet.Filter!)), columns, new XElement(S + "tableStyleInfo", new XAttribute("name", "TableStyleMedium2"), new XAttribute("showFirstColumn", 0), new XAttribute("showLastColumn", 0), new XAttribute("showRowStripes", 1), new XAttribute("showColumnStripes", 0)));
    }
    private static int ColumnIndex(string address) { int result = 0; foreach (char c in address.TakeWhile(char.IsLetter)) result = result * 26 + (char.ToUpperInvariant(c) - 'A' + 1); return result; }
    private static XElement Styles()
    {
        XElement Font(int size, bool bold = false, string? color = null) => new(S + "font", bold ? new XElement(S + "b") : null, new XElement(S + "sz", new XAttribute("val", size)), color == null ? null : new XElement(S + "color", new XAttribute("rgb", color)), new XElement(S + "name", new XAttribute("val", "Arial")));
        XElement Fill(string? color) => new(S + "fill", new XElement(S + "patternFill", new XAttribute("patternType", color == null ? "none" : color == "gray125" ? "gray125" : "solid"), color == null || color == "gray125" ? null : new XElement(S + "fgColor", new XAttribute("rgb", color)), color == null || color == "gray125" ? null : new XElement(S + "bgColor", new XAttribute("indexed", 64))));
        XElement Xf(int font = 0, int fill = 0, int number = 0, int border = 0, bool wrap = false, string horizontal = "general") => new(S + "xf", new XAttribute("numFmtId", number), new XAttribute("fontId", font), new XAttribute("fillId", fill), new XAttribute("borderId", border), new XAttribute("xfId", 0), new XAttribute("applyNumberFormat", 1), new XAttribute("applyAlignment", 1), new XAttribute("applyFill", 1), new XElement(S + "alignment", new XAttribute("vertical", "center"), new XAttribute("horizontal", horizontal), new XAttribute("wrapText", wrap ? 1 : 0)));
        var styles = new[] { Xf(), Xf(1), Xf(2, 2, wrap: true, horizontal: "center"), Xf(number: 164, horizontal: "right"), Xf(number: 165, horizontal: "right"), Xf(number: 3, horizontal: "right"), Xf(3, 3, 10, horizontal: "right"), Xf(3, 3, 2, horizontal: "right"), Xf(number: 166), Xf(4, wrap: true), Xf(number: 167, horizontal: "right"), Xf(5, 4, border: 1), Xf(5, 4, 164, 1, horizontal: "right"), Xf(5, 4, 3, 1, horizontal: "right"), Xf(5, 4, 165, 1, horizontal: "right"), Xf(5, 4), Xf(wrap: true), Xf(number: 168, horizontal: "right") };
        return new XElement(S + "styleSheet",
            new XElement(S + "numFmts", new XAttribute("count", 5), new XElement(S + "numFmt", new XAttribute("numFmtId", 164), new XAttribute("formatCode", "#,##0;[Red](#,##0);\"-\"")), new XElement(S + "numFmt", new XAttribute("numFmtId", 165), new XAttribute("formatCode", "0.0%;[Red](0.0%);0.0%")), new XElement(S + "numFmt", new XAttribute("numFmtId", 166), new XAttribute("formatCode", "mmm yyyy")), new XElement(S + "numFmt", new XAttribute("numFmtId", 167), new XAttribute("formatCode", "0.000")), new XElement(S + "numFmt", new XAttribute("numFmtId", 168), new XAttribute("formatCode", "0.0"))),
            new XElement(S + "fonts", new XAttribute("count", 6), Font(11), Font(15, true), Font(11, true, "FFFFFFFF"), Font(11, false, "FF164E9A"), Font(10, false, "FF59636F"), Font(11, true)),
            new XElement(S + "fills", new XAttribute("count", 5), Fill(null), Fill("gray125"), Fill("FF28465A"), Fill("FFEAF2FC"), Fill("FFEDF1F5")),
            new XElement(S + "borders", new XAttribute("count", 2), new XElement(S + "border", new XElement(S + "left"), new XElement(S + "right"), new XElement(S + "top"), new XElement(S + "bottom"), new XElement(S + "diagonal")), new XElement(S + "border", new XElement(S + "left"), new XElement(S + "right"), new XElement(S + "top", new XAttribute("style", "thin"), new XElement(S + "color", new XAttribute("rgb", "FFB8C6D1"))), new XElement(S + "bottom"), new XElement(S + "diagonal"))),
            new XElement(S + "cellStyleXfs", new XAttribute("count", 1), new XElement(S + "xf", new XAttribute("numFmtId", 0), new XAttribute("fontId", 0), new XAttribute("fillId", 0), new XAttribute("borderId", 0))),
            new XElement(S + "cellXfs", new XAttribute("count", styles.Length), styles), new XElement(S + "cellStyles", new XAttribute("count", 1), new XElement(S + "cellStyle", new XAttribute("name", "Normal"), new XAttribute("xfId", 0), new XAttribute("builtinId", 0))),
            new XElement(S + "dxfs", new XAttribute("count", 3), new[] { ("FF9C0006", "FFFFE3E6"), ("FF8A5B00", "FFFFF1CD"), ("FF22633D", "FFE3F1E8") }.Select(pair => new XElement(S + "dxf", new XElement(S + "font", new XElement(S + "color", new XAttribute("rgb", pair.Item1))), Fill(pair.Item2)))), new XElement(S + "tableStyles", new XAttribute("count", 0), new XAttribute("defaultTableStyle", "TableStyleMedium2"), new XAttribute("defaultPivotStyle", "PivotStyleLight16")));
    }
    private static XElement Drawing(int row, int id) => new(Xdr + "wsDr", new XAttribute(XNamespace.Xmlns + "a", A), new XAttribute(XNamespace.Xmlns + "r", R), new XElement(Xdr + "oneCellAnchor", new XElement(Xdr + "from", new XElement(Xdr + "col", 0), new XElement(Xdr + "colOff", 0), new XElement(Xdr + "row", row), new XElement(Xdr + "rowOff", 0)), new XElement(Xdr + "ext", new XAttribute("cx", 8001000), new XAttribute("cy", 4191000)), new XElement(Xdr + "graphicFrame", new XAttribute("macro", ""), new XElement(Xdr + "nvGraphicFramePr", new XElement(Xdr + "cNvPr", new XAttribute("id", id), new XAttribute("name", "Chart " + id)), new XElement(Xdr + "cNvGraphicFramePr")), new XElement(Xdr + "xfrm", new XElement(A + "off", new XAttribute("x", 0), new XAttribute("y", 0)), new XElement(A + "ext", new XAttribute("cx", 0), new XAttribute("cy", 0))), new XElement(A + "graphic", new XElement(A + "graphicData", new XAttribute("uri", C.NamespaceName), new XElement(C + "chart", new XAttribute(R + "id", "rId1"))))), new XElement(Xdr + "clientData")));
    private static XElement Chart(Sheet sheet)
    {
        XElement Val(string name, object value) => new(C + name, new XAttribute("val", value));
        var plot = new XElement(C + "plotArea", new XElement(C + "layout")); var seriesRoot = new XElement(C + (sheet.ChartKind == "pie" ? "pieChart" : sheet.ChartKind == "line" ? "lineChart" : "barChart"));
        if (sheet.ChartKind == "bar") seriesRoot.Add(Val("barDir", "bar")); if (sheet.ChartKind != "pie") seriesRoot.Add(Val("grouping", "standard")); seriesRoot.Add(Val("varyColors", sheet.ChartKind == "pie" ? 1 : 0));
        for (int i = 0; i < sheet.Series.Count; i++)
        {
            var series = sheet.Series[i]; var ser = new XElement(C + "ser", Val("idx", i), Val("order", i), new XElement(C + "tx", new XElement(C + "v", series.Name)));
            string color = i == 0 ? "4682B4" : "E08B29";
            if (sheet.ChartKind == "line") ser.Add(new XElement(C + "spPr", new XElement(A + "ln", new XAttribute("w", 25400), new XElement(A + "solidFill", new XElement(A + "srgbClr", new XAttribute("val", color))), new XElement(A + "prstDash", new XAttribute("val", i == 0 ? "solid" : "dash")))));
            var cats = new XElement(C + "strCache", Val("ptCount", series.Labels.Count)); for (int j = 0; j < series.Labels.Count; j++) cats.Add(new XElement(C + "pt", new XAttribute("idx", j), new XElement(C + "v", series.Labels[j])));
            var nums = new XElement(C + "numCache", new XElement(C + "formatCode", "#,##0"), Val("ptCount", series.Numbers.Count)); for (int j = 0; j < series.Numbers.Count; j++) if (double.IsFinite(series.Numbers[j])) nums.Add(new XElement(C + "pt", new XAttribute("idx", j), new XElement(C + "v", series.Numbers[j].ToString("R", CultureInfo.InvariantCulture))));
            ser.Add(new XElement(C + "cat", new XElement(C + "strRef", new XElement(C + "f", series.Categories), cats)), new XElement(C + "val", new XElement(C + "numRef", new XElement(C + "f", series.Values), nums)));
            if (sheet.ChartKind == "line") ser.Add(Val("smooth", 0)); seriesRoot.Add(ser);
        }
        if (sheet.ChartKind != "pie") seriesRoot.Add(Val("axId", 100), Val("axId", 200)); plot.Add(seriesRoot);
        if (sheet.ChartKind != "pie")
        {
            plot.Add(new XElement(C + "catAx", Val("axId", 100), new XElement(C + "scaling", Val("orientation", "minMax")), Val("axPos", sheet.ChartKind == "bar" ? "l" : "b"), Val("tickLblPos", sheet.ChartKind == "bar" ? "low" : "nextTo"), Val("crossAx", 200), Val("crosses", "autoZero"), Val("auto", 1), Val("lblAlgn", "ctr"), Val("lblOffset", 100), sheet.ChartKind == "line" ? Val("tickLblSkip", Math.Max(1, (int)Math.Ceiling(sheet.Series[0].Labels.Count / 8.0))) : null), new XElement(C + "valAx", Val("axId", 200), new XElement(C + "scaling", Val("orientation", "minMax")), Val("axPos", sheet.ChartKind == "bar" ? "b" : "l"), new XElement(C + "majorGridlines"), new XElement(C + "numFmt", new XAttribute("formatCode", "0.0,,\"m\""), new XAttribute("sourceLinked", 0)), Val("tickLblPos", "nextTo"), Val("crossAx", 100), Val("crosses", "autoZero"), Val("crossBetween", "between")));
        }
        var title = new XElement(C + "title", new XElement(C + "tx", new XElement(C + "rich", new XElement(A + "bodyPr"), new XElement(A + "lstStyle"), new XElement(A + "p", new XElement(A + "r", new XElement(A + "rPr", new XAttribute("lang", "en-US")), new XElement(A + "t", sheet.ChartTitle))))), new XElement(C + "layout"), Val("overlay", 0));
        return new XElement(C + "chartSpace", new XAttribute(XNamespace.Xmlns + "a", A), new XAttribute(XNamespace.Xmlns + "r", R), new XElement(C + "chart", title, Val("autoTitleDeleted", 0), plot, new XElement(C + "legend", Val("legendPos", sheet.ChartKind == "pie" ? "r" : "b"), new XElement(C + "layout"), Val("overlay", 0)), Val("plotVisOnly", 0), Val("dispBlanksAs", "gap")));
    }
    private static XElement Rel(string id, string type, string target) => new(P + "Relationship", new XAttribute("Id", id), new XAttribute("Type", R.NamespaceName + "/" + type), new XAttribute("Target", target));
    private static void PutXml(ZipArchive zip, string path, XElement xml) { using var stream = zip.CreateEntry(path, CompressionLevel.Optimal).Open(); new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), xml).Save(stream); }
    private static string Column(int number) { string result = ""; while (number > 0) { number--; result = (char)('A' + number % 26) + result; number /= 26; } return result; }
}
