using System.Globalization;
using System.Net;
using System.Text;

namespace Decision_support_loans_bank;

public static class MapHtmlExporter
{
    public static void Write(string path, List<StateSummary> rows, MapMetric metric, string description)
    {
        static string E(string s) => WebUtility.HtmlEncode(s);
        static string N(double n) => n.ToString("0.###", CultureInfo.InvariantCulture);
        var data = rows.ToDictionary(x => x.Code); var svg = new StringBuilder();
        var colors = new[] { "#4682b4", "#8fbc8f", "#d2b48c" }; double max = Math.Max(1, rows.Select(x => x.Amount).DefaultIfEmpty().Max());
        foreach (var state in GeographyService.Shapes)
        {
            data.TryGetValue(state.Code, out var row);
            string fill = row == null ? "#e6e6e6" : metric != MapMetric.FlaggedRate ? "#dce4ea" : row.FlaggedRate < .05 ? "#d6eaf8" : row.FlaggedRate < .10 ? "#85c1e9" : row.FlaggedRate < .20 ? "#2e86c1" : "#1a5276";
            string tooltip = row == null ? $"{state.Name}: no selected loans" : $"{row.State}: {row.Loans:N0} loans; {row.Amount:N0} USD; flagged {row.FlaggedRate:P1}; Auto {row.AutoAmount:N0} USD; consolidation {row.ConsolidationAmount:N0} USD; other {row.OtherAmount:N0} USD";
            svg.Append("<g><title>").Append(E(tooltip)).Append("</title><path fill=\"").Append(fill).Append("\" stroke=\"white\" fill-rule=\"evenodd\" d=\"");
            foreach (var ring in state.Rings)
            {
                bool first = true; foreach (var point in ring) { var p = GeographyService.Project(state.Code, point); svg.Append(first ? 'M' : 'L').Append(N(p.X)).Append(' ').Append(N(p.Y)).Append(' '); first = false; }
                svg.Append("Z ");
            }
            svg.Append("\"/>");
            var center = GeographyService.LabelPoint(state);
            if (row != null && row.Amount > 0 && metric == MapMetric.LoanVolume) svg.Append($"<circle cx=\"{N(center.X)}\" cy=\"{N(center.Y)}\" r=\"{N(5 + 27 * Math.Sqrt(row.Amount / max))}\" fill=\"#2d69a0\" fill-opacity=\".7\"/>");
            if (row != null && row.Amount > 0 && metric == MapMetric.CategoryPies)
            {
                double[] values = { row.AutoAmount, row.ConsolidationAmount, row.OtherAmount }; double angle = -Math.PI / 2;
                double pieRadius = state.Code is "RI" or "DE" or "DC" or "CT" or "NJ" or "MA" or "MD" or "NH" or "VT" ? 7 : 17;
                for (int i = 0; i < 3; i++)
                {
                    double sweep = 2 * Math.PI * values[i] / row.Amount;
                    if (sweep > 0)
                    {
                        if (sweep >= 2 * Math.PI - .000001) svg.Append($"<circle cx=\"{N(center.X)}\" cy=\"{N(center.Y)}\" r=\"{N(pieRadius)}\" fill=\"{colors[i]}\"/>");
                        else
                        {
                            double x1 = center.X + pieRadius * Math.Cos(angle), y1 = center.Y + pieRadius * Math.Sin(angle), x2 = center.X + pieRadius * Math.Cos(angle + sweep), y2 = center.Y + pieRadius * Math.Sin(angle + sweep);
                            svg.Append($"<path d=\"M{N(center.X)} {N(center.Y)} L{N(x1)} {N(y1)} A{N(pieRadius)} {N(pieRadius)} 0 {(sweep > Math.PI ? 1 : 0)} 1 {N(x2)} {N(y2)} Z\" fill=\"{colors[i]}\" stroke=\"#555\" stroke-width=\".3\"/>");
                        }
                    }
                    angle += sweep;
                }
            }
            if (state.Code is not ("RI" or "DE" or "DC" or "CT" or "NJ" or "MA" or "MD" or "NH" or "VT"))
                svg.Append($"<text x=\"{N(center.X)}\" y=\"{N(center.Y + (metric == MapMetric.FlaggedRate ? 4 : 26))}\" text-anchor=\"middle\" font-family=\"Arial\" font-size=\"11\" fill=\"#222\">{state.Code}</text>");
            svg.Append("</g>");
        }
        string legend = metric == MapMetric.CategoryPies ? "Blue: Auto; green: Debt consolidation; tan: all other categories. Pie slices represent granted amounts. Pies have equal size." : metric == MapMetric.LoanVolume ? "Larger circles indicate larger granted amounts. Hover for exact amounts." : "Light to dark: &lt;5%, 5–10%, 10–20%, ≥20% flagged loans. Gray: no selected loans.";
        var table = new StringBuilder(); foreach (var row in rows) table.Append($"<tr><td>{E(row.State)} ({E(row.Code)})</td><td>{row.Loans:N0}</td><td>{row.Amount:N2}</td><td>{row.Flagged:N0}</td><td>{row.FlaggedRate:P2}</td></tr>");
        string html = "<!doctype html><html lang=\"en\"><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width\"><title>Bank loan geographical report</title><style>body{font:15px Segoe UI,Arial;margin:24px;color:#222}svg{width:100%;max-height:650px;border:1px solid #ccc}table{border-collapse:collapse;width:100%}th,td{padding:8px;border:1px solid #ddd;text-align:right}th:first-child,td:first-child{text-align:left}p{max-width:1100px;line-height:1.5}svg g:hover{opacity:.7}</style><h1>Bank loan geographical analysis</h1><p>" + E(description) + "</p><p>" + legend + "</p><svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 1000 600\" role=\"img\" aria-label=\"United States loan analysis\">" + svg + "<text x=\"30\" y=\"585\" font-size=\"13\">Alaska and Hawaii are insets. Small states: use the table. Hover for values.</text></svg><p>Historical Lending Club sample, USD. Flagged includes grace period; these are sample proportions, not population estimates. Boundaries adapted from Leaflet's US-states example (Mike Bostock). No online services required.</p><table><thead><tr><th>State</th><th>Loans</th><th>Granted amount (USD)</th><th>Flagged loans</th><th>Flagged rate</th></tr></thead><tbody>" + table + "</tbody></table></html>";
        AtomicFile.Write(path, temp => File.WriteAllText(temp, html, new UTF8Encoding(false)));
    }
}

public static class AtomicFile
{
    public static void Write(string path, Action<string> write)
    {
        string target = Path.GetFullPath(path), temporary = Path.Combine(Path.GetDirectoryName(target)!, "." + Path.GetFileName(target) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try { write(temporary); File.Move(temporary, target, true); } finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
