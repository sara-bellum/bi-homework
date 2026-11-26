using System.Globalization;
using System.Text;

namespace Decision_support_loans_bank;

public sealed record LoanFilter(DateTime? From=null, DateTime? Through=null, string? Category=null,
    string? State=null, bool IndividualsOnly=false, string? Status=null, string? Grade=null,
    string? Search=null, bool FlaggedOnly=false, bool LowRiskOnly=false, string? Product=null)
{
    // Reports share the global selection; table-only restrictions stay in the current table.
    public LoanFilter ForCompleteReport() => new(From,Through,Category,State,IndividualsOnly);
}
public sealed record LoanQuery(string Sql,(string Name,object? Value)[] Parameters);

public static class LoanRepository
{
    public static LoanQuery? LastQuery { get; private set; }
    private const string ProductCase="""
        CASE WHEN cl.venit_anual >= 100000 AND cl.nr_carduri >= 3 THEN 'Premium credit card'
             WHEN cl.tip_locuinta = 'MORTGAGE' OR cl.nr_ipoteci > 0 THEN 'Life insurance'
             ELSE 'Savings / term deposit' END
        """;
    public static LoanQuery Build(LoanFilter filter)
    {
        if(filter.From>filter.Through) throw new ArgumentException("The start date must not follow the end date.");
        var sql=new StringBuilder("""
            SELECT c.*, cl.stat, cl.venit_anual, cl.tip_locuinta, cl.grad_indatorare,
                   cl.intarzieri_2ani, cl.nr_carduri, cl.nr_ipoteci,
                   t.denumire AS categorie, t.cod AS scop, s.cod AS status_cod,
                   s.rau_platnic, s.inchis, s.gravitate
            FROM Credite c JOIN Clienti cl USING(id_client)
            JOIN TipuriCredit t USING(id_tip) JOIN StariCredit s USING(id_stare)
            WHERE 1=1
            """);
        var parameters=new List<(string,object?)>();
        void Add(string condition,string name,object value) { sql.Append("\n AND ").Append(condition); parameters.Add((name,value)); }
        if(filter.From is {} start) Add("c.data_acordarii >= @from","@from",start.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture));
        if(filter.Through is {} end) Add("c.data_acordarii <= @through","@through",end.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture));
        if(filter.IndividualsOnly) sql.Append("\n AND t.cod <> 'small_business'");
        if(!string.IsNullOrEmpty(filter.Category)) {
            var code=Db.Query("SELECT cod FROM TipuriCredit").Rows.Cast<System.Data.DataRow>()
                .Select(r=>Convert.ToString(r[0])!).FirstOrDefault(c=>Presentation.Category(c)==filter.Category);
            Add("t.cod = @purpose","@purpose",code??"__unknown_purpose__");
        }
        if(!string.IsNullOrEmpty(filter.State)) Add("cl.stat = @state","@state",filter.State);
        if(!string.IsNullOrEmpty(filter.Grade)) Add("c.grad_risc = @grade","@grade",filter.Grade);
        if(filter.FlaggedOnly) sql.Append("\n AND s.rau_platnic = 1");
        if(!string.IsNullOrEmpty(filter.Status)) Add("s.cod = @status","@status",filter.Status);
        if(!string.IsNullOrWhiteSpace(filter.Search)) {
            // instr treats percent signs, underscores and quotes literally, not as SQL or LIKE wildcards.
            Add("(instr(lower(c.id_client), lower(@search)) > 0 OR instr(CAST(c.id_credit AS TEXT), @search) > 0)","@search",filter.Search.Trim());
        }
        if(filter.LowRiskOnly || filter.Product!=null) sql.Append("\n AND c.grad_risc IN ('A','B') AND s.rau_platnic = 0 AND cl.intarzieri_2ani = 0 AND cl.grad_indatorare IS NOT NULL AND cl.grad_indatorare <= 35");
        if(filter.Product!=null) Add("("+ProductCase+") = @product","@product",filter.Product);
        sql.Append("\n ORDER BY c.id_credit");
        return new LoanQuery(sql.ToString(),parameters.ToArray());
    }
    public static List<Loan> Load(LoanFilter filter) {
        var query=Build(filter); var table=Db.Query(query.Sql,query.Parameters); LastQuery=query; return Loan.FromTable(table);
    }
}
