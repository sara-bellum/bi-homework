namespace Decision_support_loans_bank;

public sealed record MonthPoint(DateTime Month, double Volume, double Count);
public sealed record ForecastPoint(string Month, string Kind, double Volume, double Count);

public static class Analytics
{
    public static List<MonthPoint> Monthly(IEnumerable<Loan> loans, DateTime start, DateTime end)
    {
        var buckets=loans.GroupBy(x=>new DateTime(x.Issued.Year,x.Issued.Month,1))
            .ToDictionary(g=>g.Key,g=>new MonthPoint(g.Key,g.Sum(x=>x.Amount),g.Count()));
        var rows=new List<MonthPoint>();
        for(var d=new DateTime(start.Year,start.Month,1);d<=end;d=d.AddMonths(1))
            rows.Add(buckets.GetValueOrDefault(d,new MonthPoint(d,0,0)));
        return rows;
    }
    public static double Predict(double[] y,int ahead)
    {
        if(y.Length<2) return y.Length==0 ? 0 : Math.Max(0,y[0]);
        double meanX=(y.Length-1)/2.0, meanY=y.Average();
        double denominator=Enumerable.Range(0,y.Length).Sum(i=>Math.Pow(i-meanX,2));
        double slope=Enumerable.Range(0,y.Length).Sum(i=>(i-meanX)*(y[i]-meanY))/denominator;
        return Math.Max(0,meanY+slope*(y.Length-1+ahead-meanX));
    }
    public static List<ForecastPoint> Forecast(List<MonthPoint> history,int horizon)
    {
        var rows=history.Select(x=>new ForecastPoint(x.Month.ToString("yyyy-MM"),"Observed",x.Volume,x.Count)).ToList();
        if(history.Count<2) return rows;
        for(int i=1;i<=horizon;i++) rows.Add(new ForecastPoint(history[^1].Month.AddMonths(i).ToString("yyyy-MM"),"Forecast",
            Predict(history.Select(x=>x.Volume).ToArray(),i),Predict(history.Select(x=>x.Count).ToArray(),i)));
        return rows;
    }
    public static double DemandMultiplier(double baselineRate,double deltaPoints,double elasticity)
    {
        if(baselineRate<=0 || baselineRate+deltaPoints<=0) throw new ArgumentException("The baseline and simulated interest rates must be above zero.");
        return Math.Pow((baselineRate+deltaPoints)/baselineRate,elasticity);
    }
    public static string? Product(Loan l)
    {
        if(l.Bad || l.Grade is not ("A" or "B") || l.Delinquencies != 0 || l.Dti is null or > 35) return null;
        if(l.Income>=100000 && l.Cards>=3) return "Premium credit card";
        if(l.Home=="MORTGAGE" || l.Mortgages>0) return "Life insurance";
        return "Savings / term deposit";
    }
}
