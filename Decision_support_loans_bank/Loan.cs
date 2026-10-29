using System.Data;
using System.Globalization;

namespace Decision_support_loans_bank;

public sealed record Loan
{
    public long Id { get; init; }
    public string Customer { get; init; } = "";
    public string Category { get; init; } = "";
    public string Purpose { get; init; } = "";
    public string Status { get; init; } = "";
    public string Grade { get; init; } = "";
    public string State { get; init; } = "";
    public string Home { get; init; } = "";
    public bool Closed { get; init; }
    public int Severity { get; init; }
    public bool Bad { get; init; }
    public double Amount { get; init; }
    public double Funded { get; init; }
    public double Rate { get; init; }
    public int Term { get; init; }
    public double Installment { get; init; }
    public DateTime Issued { get; init; }
    public DateTime? LastPayment { get; init; }
    public double Principal { get; init; }
    public double Interest { get; init; }
    public double Fees { get; init; }
    public double Recoveries { get; init; }
    public double CollectionCost { get; init; }
    public double Balance { get; init; }
    public double Income { get; init; }
    public double? Dti { get; init; }
    public double? Delinquencies { get; init; }
    public double? Cards { get; init; }
    public double? Mortgages { get; init; }
    public int? MonthsPaid => LastPayment is {} d ? (d.Year-Issued.Year)*12+d.Month-Issued.Month : null;
    public bool Early => Closed && !Bad && MonthsPaid is >= 0 && MonthsPaid < Term-2;
    public double LostInterest => Early ? Math.Max(0, Installment*Term-Funded-Interest) : 0;
    public double ExposureYears => Math.Min(Term, Math.Max(1, MonthsPaid ?? Term))/12.0;
    public double Net(double funding, double operating) => Interest+Fees+Recoveries-CollectionCost
        -(Bad ? Math.Max(0,Funded-Principal) : 0)-Funded*funding/100*ExposureYears-Funded*operating/100;

    public static List<Loan> Load()
    {
        var table = Db.Query("""
            SELECT c.*, cl.stat, cl.venit_anual, cl.tip_locuinta, cl.grad_indatorare,
                   cl.intarzieri_2ani, cl.nr_carduri, cl.nr_ipoteci,
                   t.denumire AS categorie, t.cod AS scop, s.cod AS status_cod,
                   s.rau_platnic, s.inchis, s.gravitate
            FROM Credite c JOIN Clienti cl USING(id_client)
            JOIN TipuriCredit t USING(id_tip) JOIN StariCredit s USING(id_stare)
            """);
        return table.AsEnumerable().Select(r => new Loan {
            Id=(long)r["id_credit"], Customer=S(r,"id_client"), Category=Presentation.Category(S(r,"scop")),
            Purpose=S(r,"scop"), Status=S(r,"status_cod"), Grade=S(r,"grad_risc"),
            State=S(r,"stat"), Home=S(r,"tip_locuinta"), Closed=N(r,"inchis")==1,
            Bad=N(r,"rau_platnic")==1, Severity=(int)N(r,"gravitate"), Amount=N(r,"suma"),
            Funded=N(r,"suma_finantata"), Rate=N(r,"dobanda"), Term=(int)N(r,"durata_luni"),
            Installment=N(r,"rata_lunara"), Issued=DateTime.ParseExact(S(r,"data_acordarii"),"yyyy-MM-dd",CultureInfo.InvariantCulture),
            LastPayment=DateTime.TryParseExact(S(r,"data_ultimei_plati"),"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var d) ? d : null,
            Principal=N(r,"principal_rambursat"), Interest=N(r,"dobanda_incasata"), Fees=N(r,"penalitati"),
            Recoveries=N(r,"recuperari"), CollectionCost=N(r,"cost_recuperare"), Balance=N(r,"sold_ramas"),
            Income=N(r,"venit_anual"), Dti=Optional(r,"grad_indatorare"), Delinquencies=Optional(r,"intarzieri_2ani"),
            Cards=Optional(r,"nr_carduri"), Mortgages=Optional(r,"nr_ipoteci")
        }).ToList();
    }
    private static string S(DataRow r,string k) => Convert.ToString(r[k],CultureInfo.InvariantCulture) ?? "";
    private static double N(DataRow r,string k) => r.IsNull(k) ? 0 : Convert.ToDouble(r[k],CultureInfo.InvariantCulture);
    private static double? Optional(DataRow r,string k) => r.IsNull(k) ? null : N(r,k);
}
