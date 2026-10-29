using System.Data;

namespace Decision_support_loans_bank;

public static class Presentation
{
    public static string Category(string code) => code switch {
        "debt_consolidation"=>"Debt consolidation", "credit_card"=>"Credit card refinancing",
        "home_improvement"=>"Home improvement", "other"=>"Personal needs", "major_purchase"=>"Major purchases",
        "medical"=>"Medical expenses", "small_business"=>"Small business", "car"=>"Auto",
        "vacation"=>"Vacation", "moving"=>"Moving", "house"=>"Home purchase", "wedding"=>"Wedding",
        "renewable_energy"=>"Renewable energy", "educational"=>"Education", _=>"Other purpose"
    };
    public static string Status(string status) => status switch {
        "In Grace Period"=>"Grace period", "Late (16-30 days)"=>"Late: 16–30 days",
        "Late (31-120 days)"=>"Late: 31–120 days", "Charged Off"=>"Charged off", "Default"=>"Default",
        "Fully Paid"=>"Fully paid", "Current"=>"Current",
        "Does not meet the credit policy. Status:Fully Paid"=>"Fully paid (outside policy)",
        "Does not meet the credit policy. Status:Charged Off"=>"Charged off (outside policy)", _=>"Unknown status"
    };
    public static string Home(string home) => home switch { "MORTGAGE"=>"Mortgaged home", "RENT"=>"Rented home", "OWN"=>"Owned home", "OTHER"=>"Other", "NONE"=>"Not recorded", "ANY"=>"Not specified", _=>"Unknown" };
    public static string Reason(Loan l) => Analytics.Product(l) switch {
        "Premium credit card"=>"Income ≥ $100k; at least 3 card trade lines",
        "Life insurance"=>"Mortgage-related customer profile",
        _=>"Low-risk profile; savings relationship review"
    };
    public static string Priority(Loan l) => l.Closed ? "Recovery review" : l.Severity>=3 ? "Urgent arrears review" : l.Severity==2 ? "Payment follow-up" : "Early warning";
    public static double Shortfall(Loan l) => l.Closed && l.Bad ? Math.Max(0,l.Funded-l.Principal-l.Recoveries):0;
    public static string Retention(Loan l) => l.Income>=100000 ? "Discuss deposits / investment needs" : "Review savings and future borrowing needs";
}

// Page and sort the entire result, never just the fifty currently rendered rows.
public sealed class ResultPager
{
    public const int PageSize=50;
    public DataView View { get; private set; }=new DataView(new DataTable());
    public int PageIndex { get; private set; }
    public int Count=>View.Count;
    public int PageCount=>Math.Max(1,(Count+PageSize-1)/PageSize);
    public void Set(DataTable table) { View=new DataView(table); PageIndex=0; }
    public void Move(int offset) => PageIndex=Math.Clamp(PageIndex+offset,0,PageCount-1);
    public void Sort(string column,bool descending) { View.Sort="["+column.Replace("]","\\]")+ "] "+(descending?"DESC":"ASC"); PageIndex=0; }
    public DataTable Current()
    {
        var result=View.Table!.Clone();
        foreach(var row in View.Cast<DataRowView>().Skip(PageIndex*PageSize).Take(PageSize)) result.ImportRow(row.Row);
        return result;
    }
}
