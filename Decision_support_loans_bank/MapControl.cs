using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Net;
using System.Text;

namespace Decision_support_loans_bank;

public sealed class MapControl : Control
{
    public List<StateSummary> Data { get; set; } = new();
    public MapMetric Metric { get; set; }
    public string FilterDescription { get; set; } = "";
    public event Action<string>? StateSelected;
    private readonly ToolTip tip = new() { InitialDelay = 100, AutoPopDelay = 20000 };
    private readonly List<(GraphicsPath Path, StateShape State)> hitAreas = new();
    private Matrix? inverse;
    private string lastCode = "";
    private static readonly Color[] PieColors = { Color.SteelBlue, Color.DarkSeaGreen, Color.Tan };
    public MapControl() { DoubleBuffered = true; BackColor = Color.White; Dock = DockStyle.Fill; }
    protected override void Dispose(bool disposing) { if (disposing) { tip.Dispose(); inverse?.Dispose(); foreach (var h in hitAreas) h.Path.Dispose(); } base.Dispose(disposing); }
    private static string Title(MapMetric metric) => metric switch { MapMetric.FlaggedRate => "Flagged-loan rate by state", MapMetric.LoanVolume => "Granted loan volume by state", _ => "Loan-category amounts by state" };
    protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e); PaintMap(e.Graphics, ClientSize, true); }
    private void PaintMap(Graphics g, Size size, bool hitTest)
    {
        g.Clear(Color.White); g.SmoothingMode = SmoothingMode.AntiAlias;
        using var font = new Font("Segoe UI", 9); using var bold = new Font("Segoe UI", 10, FontStyle.Bold); using var ink = new SolidBrush(Color.FromArgb(35, 35, 35));
        g.DrawString(Title(Metric), bold, ink, 12, 6);
        if (size.Width < 150 || size.Height < 90) return;
        float scale = Math.Min((size.Width - 16) / 1000f, (size.Height - 45) / 580f), tx = (size.Width - 1000 * scale) / 2, ty = 32;
        var save = g.Save(); g.TranslateTransform(tx, ty); g.ScaleTransform(scale, scale);
        if (hitTest) { foreach (var h in hitAreas) h.Path.Dispose(); hitAreas.Clear(); inverse?.Dispose(); inverse = new Matrix(scale, 0, 0, scale, tx, ty); inverse.Invert(); }
        double maximum = Math.Max(1, Data.Select(x => x.Amount).DefaultIfEmpty().Max());
        foreach (var shape in GeographyService.Shapes)
        {
            using var path = PathFor(shape); var row = Data.FirstOrDefault(x => x.Code == shape.Code);
            using var fill = new SolidBrush(Fill(row)); using var border = new Pen(Color.White, 1.2f); g.FillPath(fill, path); g.DrawPath(border, path);
            if (hitTest) hitAreas.Add(((GraphicsPath)path.Clone(), shape));
            var center = GeographyService.LabelPoint(shape); float x = (float)center.X, y = (float)center.Y;
            if (row != null && row.Amount > 0 && Metric != MapMetric.FlaggedRate)
            {
                float radius = Metric == MapMetric.LoanVolume ? (float)(5 + 27 * Math.Sqrt(row.Amount / maximum)) : shape.Code is "RI" or "DE" or "DC" or "CT" or "NJ" or "MA" or "MD" or "NH" or "VT" ? 7 : 17;
                if (Metric == MapMetric.LoanVolume) { using var circle = new SolidBrush(Color.FromArgb(170, 45, 105, 160)); g.FillEllipse(circle, x - radius, y - radius, 2 * radius, 2 * radius); }
                else
                {
                    double[] values = { row.AutoAmount, row.ConsolidationAmount, row.OtherAmount }; float angle = -90;
                    for (int i = 0; i < 3; i++) { float sweep = (float)(360 * values[i] / row.Amount); if (sweep > 0) { using var brush = new SolidBrush(PieColors[i]); g.FillPie(brush, x - radius, y - radius, 2 * radius, 2 * radius, angle, sweep); } angle += sweep; }
                    using var outline = new Pen(Color.DimGray, .8f); g.DrawEllipse(outline, x - radius, y - radius, 2 * radius, 2 * radius);
                }
            }
            if (shape.Code is not ("RI" or "DE" or "DC" or "CT" or "NJ" or "MA" or "MD" or "NH" or "VT"))
            {
                using var stateFont = new Font("Segoe UI", 13, FontStyle.Bold); using var text = new SolidBrush(Color.Black);
                g.DrawString(shape.Code, stateFont, text, x - 11, y + (Metric == MapMetric.FlaggedRate ? -8 : 16));
            }
        }
        using var legendFont = new Font("Segoe UI", 14); float legendY = 50;
        void Legend(Color color, string text) { using var brush = new SolidBrush(color); g.FillRectangle(brush, 835, legendY, 18, 18); g.DrawString(text, legendFont, ink, 861, legendY - 4); legendY += 31; }
        if (Metric == MapMetric.FlaggedRate) { Legend(RateColor(.025), "< 5%"); Legend(RateColor(.075), "5–10%"); Legend(RateColor(.15), "10–20%"); Legend(RateColor(.25), "≥ 20%"); }
        else if (Metric == MapMetric.CategoryPies) { Legend(PieColors[0], "Auto"); Legend(PieColors[1], "Consolidation"); Legend(PieColors[2], "Other categories"); }
        else g.DrawString("Circle area grows\nwith loan volume.\nMaximum:\n" + Data.Select(x => x.Amount).DefaultIfEmpty().Max().ToString("N0") + " USD", legendFont, ink, 835, legendY);
        Legend(Color.FromArgb(230, 230, 230), "No selected loans");
        g.DrawString("AK and HI are inset. DC/small states: use the table.\nHover for values; click a state to filter.", legendFont, ink, 30, 565);
        g.Restore(save);
    }
    private Color Fill(StateSummary? row) => row == null ? Color.FromArgb(230, 230, 230) : Metric == MapMetric.FlaggedRate ? RateColor(row.FlaggedRate) : Color.FromArgb(220, 228, 234);
    private static Color RateColor(double rate) => rate < .05 ? Color.FromArgb(214, 234, 248) : rate < .10 ? Color.FromArgb(133, 193, 233) : rate < .20 ? Color.FromArgb(46, 134, 193) : Color.FromArgb(26, 82, 118);
    private static GraphicsPath PathFor(StateShape shape)
    {
        var path = new GraphicsPath(FillMode.Alternate);
        foreach (var ring in shape.Rings) { var points = ring.Select(p => GeographyService.Project(shape.Code, p)).Select(p => new PointF((float)p.X, (float)p.Y)).ToArray(); if (points.Length >= 3) path.AddPolygon(points); }
        return path;
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e); if (inverse == null) return; var points = new[] { new PointF(e.X, e.Y) }; inverse.TransformPoints(points);
        var hit = hitAreas.FirstOrDefault(h => h.Path.IsVisible(points[0])); string code = hit.State?.Code ?? "";
        Cursor = code == "" ? Cursors.Default : Cursors.Hand; if (code == lastCode) return; lastCode = code;
        var row = Data.FirstOrDefault(x => x.Code == code);
        tip.SetToolTip(this, hit.State == null ? "" : row == null ? hit.State.Name + ": no selected loans" : $"{row.State} ({row.Code})\nLoans: {row.Loans:N0}\nAmount: {row.Amount:N0} USD\nFlagged: {row.Flagged:N0} ({row.FlaggedRate:P1})\nAuto: {row.AutoAmount:N0} USD\nDebt consolidation: {row.ConsolidationAmount:N0} USD\nOther: {row.OtherAmount:N0} USD");
    }
    protected override void OnMouseClick(MouseEventArgs e) { base.OnMouseClick(e); if (inverse == null) return; var p = new[] { new PointF(e.X, e.Y) }; inverse.TransformPoints(p); var hit = hitAreas.FirstOrDefault(h => h.Path.IsVisible(p[0])); if (hit.State != null) StateSelected?.Invoke(hit.State.Code); }
    public void ExportPng(string path)
    {
        using var bitmap = new Bitmap(1800, 1150); using (var g = Graphics.FromImage(bitmap))
        {
            PaintMap(g, new Size(1800, 1050), false); using var font = new Font("Segoe UI", 12); g.DrawString(FilterDescription + "\nHistorical Lending Club sample, USD. Flagged includes grace period. Boundaries: Leaflet / Mike Bostock.\nAK/HI shown as insets. Sample proportions are not population estimates.", font, Brushes.Black, new RectangleF(20, 1055, 1750, 90));
        }
        AtomicFile.Write(path, temp => bitmap.Save(temp, ImageFormat.Png));
    }
    public void ExportHtml(string path) => MapHtmlExporter.Write(path, Data, Metric, FilterDescription);
}
