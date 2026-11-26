using System.Reflection;
using System.Text.Json;

namespace Decision_support_loans_bank;

public sealed record StateSummary(string Code, string State, int Loans, double Amount, int Flagged, double FlaggedRate,
    double AutoAmount, double ConsolidationAmount, double OtherAmount);
public sealed record GeoPoint(double Longitude, double Latitude);
public sealed record StateShape(string Code, string Name, List<List<GeoPoint>> Rings);
public enum MapMetric { FlaggedRate, LoanVolume, CategoryPies }

public static class GeographyService
{
    public static IReadOnlyList<StateShape> Shapes { get; } = LoadShapes();
    public static List<StateSummary> Aggregate(IEnumerable<Loan> loans) => loans.GroupBy(x => x.State).Select(g => new StateSummary(
        g.Key, Shapes.FirstOrDefault(s => s.Code == g.Key)?.Name ?? "Unknown location", g.Count(), g.Sum(x => x.Amount), g.Count(x => x.Bad),
        (double)g.Count(x => x.Bad) / g.Count(), g.Where(x => x.Purpose == "car").Sum(x => x.Amount),
        g.Where(x => x.Purpose == "debt_consolidation").Sum(x => x.Amount), g.Where(x => x.Purpose is not ("car" or "debt_consolidation")).Sum(x => x.Amount)
    )).OrderByDescending(x => x.Amount).ToList();
    private static List<StateShape> LoadShapes()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("BankLoanAnalysis.us-states.geojson")
            ?? throw new FileNotFoundException("Embedded state boundaries are missing.");
        using var doc = JsonDocument.Parse(stream); var result = new List<StateShape>();
        foreach (var feature in doc.RootElement.GetProperty("features").EnumerateArray())
        {
            var property = feature.GetProperty("properties"); var geometry = feature.GetProperty("geometry"); var rings = new List<List<GeoPoint>>();
            void ReadPolygon(JsonElement polygon) { foreach (var ring in polygon.EnumerateArray()) rings.Add(ring.EnumerateArray().Select(p => new GeoPoint(p[0].GetDouble(), p[1].GetDouble())).ToList()); }
            if (geometry.GetProperty("type").GetString() == "Polygon") ReadPolygon(geometry.GetProperty("coordinates"));
            else foreach (var polygon in geometry.GetProperty("coordinates").EnumerateArray()) ReadPolygon(polygon);
            result.Add(new StateShape(property.GetProperty("code").GetString()!, property.GetProperty("name").GetString()!, rings));
        }
        return result;
    }
    // A compact illustrative US layout, with Alaska/Hawaii insets. Not a distance/area measurement projection.
    public static (double X, double Y) Project(string code, GeoPoint point)
    {
        double lon = point.Longitude > 0 ? point.Longitude - 360 : point.Longitude;
        return code switch
        {
            "AK" => (30 + (lon + 190) / 60 * 240, 440 + (73 - point.Latitude) / 23 * 120),
            "HI" => (310 + (lon + 161) / 7 * 150, 470 + (23 - point.Latitude) / 5 * 90),
            _ => (30 + (lon + 125) / 59 * 770, 35 + (50 - point.Latitude) / 25 * 390)
        };
    }
    public static (double X, double Y) LabelPoint(StateShape shape)
    {
        var ring = shape.Rings.OrderByDescending(r => Math.Abs(Area(r))).First();
        var points = ring.Select(p => Project(shape.Code, p)).ToList();
        return ((points.Min(x => x.X) + points.Max(x => x.X)) / 2, (points.Min(x => x.Y) + points.Max(x => x.Y)) / 2);
    }
    private static double Area(List<GeoPoint> points) => points.Zip(points.Skip(1), (a, b) => a.Longitude * b.Latitude - b.Longitude * a.Latitude).Sum();
}
