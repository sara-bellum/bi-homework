using System.Data;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Decision_support_loans_bank;

public partial class Form1 : Form
{
    private List<Loan> loans=new();
    private readonly DateTimePicker from=new() { Width=115, Format=DateTimePickerFormat.Custom, CustomFormat="yyyy-MM-dd" };
    private readonly DateTimePicker to=new() { Width=115, Format=DateTimePickerFormat.Custom, CustomFormat="yyyy-MM-dd" };
    private readonly ComboBox category=Choices("All categories"), debtStatus=Choices("All flagged statuses"), product=Choices("All suggested products"), grade=Choices("All risk grades","A","B"), earlyGrade=Choices("All risk grades","A","B","C","D","E","F","G"), earlyView=Choices("Repayment impact","Customer profiles");
    private readonly TextBox search=new() { Width=140, PlaceholderText="Customer or loan ID" };
    private readonly CheckBox individuals=new() { Text="Individuals only", Checked=true, AutoSize=true, Margin=new Padding(5,23,10,0) };
    private readonly NumericUpDown lookback=Number(3,120,24,0), horizon=Number(1,24,6,0), delta=Number(0,10,1,2), elasticity=Number(-5,0,-1.2m,2), funding=Number(0,30,3,2), operating=Number(0,20,1,2);
    private readonly FlowLayoutPanel settings=new() { Dock=DockStyle.Fill, AutoScroll=true, WrapContents=true };
    private readonly FlowLayoutPanel filters=new() { Dock=DockStyle.Fill, WrapContents=true, AutoScroll=true };
    private readonly TableLayoutPanel body=new() { Dock=DockStyle.Fill, Padding=new Padding(10,4,10,6), RowCount=8, ColumnCount=1 };
    private readonly Label heading=new() { Dock=DockStyle.Fill, Font=new Font("Segoe UI",13,FontStyle.Bold), ForeColor=SystemColors.ControlText, TextAlign=ContentAlignment.MiddleLeft };
    private readonly Label metrics=new() { Dock=DockStyle.Fill, Font=new Font("Segoe UI",9,FontStyle.Regular), ForeColor=SystemColors.ControlText, Padding=new Padding(2,7,2,2), BackColor=SystemColors.Control };
    private readonly TextBox notes=new() { Dock=DockStyle.Fill, Multiline=true, ReadOnly=true, BorderStyle=BorderStyle.None, BackColor=SystemColors.Control, ScrollBars=ScrollBars.Vertical };
    private readonly DataGridView grid=new() { Dock=DockStyle.Fill, ReadOnly=true, AllowUserToAddRows=false, AllowUserToDeleteRows=false, AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor=Color.White, BorderStyle=BorderStyle.None, RowHeadersVisible=false, SelectionMode=DataGridViewSelectionMode.FullRowSelect, MultiSelect=false };
    private readonly BarChart chart=new();
    private readonly Button refresh=new() { Text="Apply filters", AutoSize=true, Margin=new Padding(3,20,0,0) };
    private readonly Label status=new() { Dock=DockStyle.Bottom, Height=27, Padding=new Padding(12,4,0,0), Text="Loading database..." };
    private readonly TabControl sections=new() { Dock=DockStyle.Fill };
    private string screenExplanation="";
    private readonly ResultPager pager=new();
    private readonly FlowLayoutPanel pagerBar=new() { Dock=DockStyle.Fill, WrapContents=false };
    private readonly Label range=new() { Width=360, Height=30, TextAlign=ContentAlignment.MiddleLeft };
    private readonly Button previous=new() { Text="Previous", AutoSize=true },next=new() { Text="Next", AutoSize=true };
    private readonly Dictionary<string,string> headers=new() {
        ["Customer"]="Customer ID", ["Loan"]="Loan ID", ["Category"]="Loan category", ["Grade"]="Risk grade",
        ["GrantedAmountUSD"]="Granted amount (USD)", ["AmountUSD"]="Granted amount (USD)", ["VolumeSharePct"]="Share of amount (%)", ["CountSharePct"]="Share of loans (%)",
        ["AverageRatePct"]="Average rate (%)", ["AnnualIncomeUSD"]="Annual income (USD)", ["DtiPct"]="Debt / income (%)",
        ["BalanceUSD"]="Balance (USD)", ["ShortfallUSD"]="Principal shortfall (USD)", ["Action"]="Suggested action",
        ["Product"]="Suggested product", ["Reason"]="Why this product", ["Status"]="Loan status", ["Home"]="Housing profile",
        ["MonthsToPayoff"]="Months to payoff", ["MonthsSaved"]="Months early", ["UnearnedInterestUSD"]="Unearned interest (USD)",
        ["OriginalMonths"]="Original term (months)", ["NetMarginUSD"]="Estimated net margin (USD)", ["NetMarginPct"]="Lifetime margin (%)",
        ["RateChangePoints"]="Rate change (pp)", ["NewRatePct"]="Scenario rate (%)", ["DemandChangePct"]="Demand change (%)",
        ["ExpectedLoanCount"]="Expected loans", ["ExpectedVolumeUSD"]="Expected volume (USD)", ["Volume"]="Loan volume (USD)", ["Count"]="Loan count",
        ["Kind"]="Data type", ["FundedUSD"]="Funded amount (USD)", ["ClosedLoans"]="Closed loans", ["EstimatedCostsUSD"]="Assumed costs (USD)",
        ["InterestFeesUSD"]="Interest and fees (USD)", ["RecoveriesUSD"]="Net recoveries (USD)", ["PrincipalLossUSD"]="Principal loss (USD)",
        ["Paid"]="Payoff month", ["Issued"]="Issue month", ["RetentionIdea"]="Retention opportunity", ["DemandMultiplier"]="Demand multiplier",
        ["Cards"]="Card trade lines", ["Mortgages"]="Mortgage accounts", ["Method"]="How to read the results"
    };
    private HashSet<string>? visibleColumns;
    private string sortColumn="";
    private bool sortDescending;
    private int page;
    private bool ready;
    private static NumericUpDown Number(decimal min,decimal max,decimal value,int decimals) => new() { Minimum=min, Maximum=max, Value=value, DecimalPlaces=decimals, Increment=decimals==0?1:0.1m, Width=85 };
    private static ComboBox Choices(params string[] items) { var box=new ComboBox { Width=178,DropDownStyle=ComboBoxStyle.DropDownList }; box.Items.AddRange(items); box.SelectedIndex=0; return box; }

    public Form1()
    {
        InitializeComponent(); Text="Bank Loan Analysis";
        Size=new Size(1380,920); MinimumSize=new Size(1180,800); StartPosition=FormStartPosition.CenterScreen;
        Font=new Font("Segoe UI",9); BackColor=SystemColors.Control;
        var menu=new MenuStrip();
        var file=new ToolStripMenuItem("File");
        file.DropDownItems.Add("Export filtered results...",null,(_,_)=>Export());
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add("Exit",null,(_,_)=>Close());
        var help=new ToolStripMenuItem("Help");
        help.DropDownItems.Add("About this analysis...",null,(_,_)=>MessageBox.Show(this,screenExplanation,heading.Text,MessageBoxButtons.OK,MessageBoxIcon.Information));
        help.DropDownItems.Add("About the project...",null,(_,_)=>MessageBox.Show(this,"Bank loan analysis\nBusiness Intelligence project\n\n.NET 8 Windows Forms and SQLite\nHistorical Lending Club sample, amounts in USD.\nCustomer IDs are anonymized.","About",MessageBoxButtons.OK,MessageBoxIcon.Information));
        menu.Items.AddRange(new ToolStripItem[]{file,help}); MainMenuStrip=menu;
        string[] names={"Portfolio","Debtors and arrears","Forecast","Interest rates","Profitability","Cross-selling","Early repayment","Methods and data"};
        foreach(string name in names) sections.TabPages.Add(new TabPage(name) { BackColor=SystemColors.Control,Padding=new Padding(4) });
        sections.TabPages[0].Controls.Add(body);
        sections.SelectedIndexChanged+=(_,_)=> { page=sections.SelectedIndex; sections.SelectedTab!.Controls.Add(body); search.Clear(); RenderSafe(); };
        foreach(float height in new float[]{35,65,65,50,35}) body.RowStyles.Add(new RowStyle(SizeType.Absolute,height));
        body.RowStyles.Add(new RowStyle(SizeType.Percent,53)); body.RowStyles.Add(new RowStyle(SizeType.Percent,47)); body.RowStyles.Add(new RowStyle(SizeType.Absolute,37));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); category.Width=210;
        AddField(filters,"Loans issued from",from); AddField(filters,"Loans issued through",to); AddField(filters,"Loan category",category); filters.Controls.Add(individuals); filters.Controls.Add(refresh);
        refresh.Click+=(_,_)=>RenderSafe();
        body.Controls.Add(heading,0,0); body.Controls.Add(filters,0,1); body.Controls.Add(settings,0,2); body.Controls.Add(metrics,0,3); body.Controls.Add(notes,0,4); body.Controls.Add(chart,0,5); body.Controls.Add(grid,0,6); body.Controls.Add(pagerBar,0,7);
        previous.Click+=(_,_)=> { pager.Move(-1); BindPage(); }; next.Click+=(_,_)=> { pager.Move(1); BindPage(); };
        var details=new Button { Text="Details...",AutoSize=true }; details.Click+=(_,_)=>Details();
        pagerBar.Controls.AddRange(new Control[]{previous,next,range,details});
        Controls.Add(sections); Controls.Add(status); Controls.Add(menu);
        status.BorderStyle=BorderStyle.Fixed3D; status.BackColor=SystemColors.Control;
        grid.EnableHeadersVisualStyles=true; grid.ColumnHeadersDefaultCellStyle.BackColor=SystemColors.Control;
        grid.ColumnHeadersDefaultCellStyle.Font=Font; grid.ColumnHeadersDefaultCellStyle.WrapMode=DataGridViewTriState.True;
        grid.ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing; grid.ColumnHeadersHeight=45; grid.RowTemplate.Height=29;
        grid.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(248,248,248);
        grid.BorderStyle=BorderStyle.Fixed3D; grid.GridColor=Color.LightGray;
        grid.CellFormatting+=(_,e)=> {
            if(e.Value is double d) { e.Value=d.ToString(grid.Columns[e.ColumnIndex].Name=="Count"?"N1":"N2"); e.FormattingApplied=true; }
            if(grid.Columns[e.ColumnIndex].Name=="Product") { e.CellStyle!.BackColor=Color.FromArgb(235,242,250); e.CellStyle.ForeColor=SystemColors.ControlText; }
            if(grid.Columns[e.ColumnIndex].Name=="Kind" && Convert.ToString(e.Value)=="Forecast") { e.CellStyle!.BackColor=Color.FromArgb(255,240,217); e.CellStyle.ForeColor=Color.FromArgb(146,84,10); }
        };
        grid.ColumnHeaderMouseClick+=(_,e)=> {
            string column=grid.Columns[e.ColumnIndex].DataPropertyName; sortDescending=sortColumn==column&&!sortDescending; sortColumn=column;
            pager.Sort(column,sortDescending); BindPage();
        };
        grid.CellDoubleClick+=(_,e)=> { if(e.RowIndex>=0) Details(); };
        chart.ItemSelected+=key=> {
            if(page==1 && debtStatus.Items.Contains(key)) { debtStatus.SelectedItem=key; RenderSafe(); }
            else if(page==5 && product.Items.Contains(key)) { product.SelectedItem=key; RenderSafe(); }
            else if(page==6 && category.Items.Contains(key)) { category.SelectedItem=key; RenderSafe(); }
        };
        Shown+=async (_,_)=>await LoadData();
    }
    private static void AddField(FlowLayoutPanel panel,string label,Control control)
    {
        var group=new FlowLayoutPanel { AutoSize=true, FlowDirection=FlowDirection.TopDown, WrapContents=false, Margin=new Padding(3,0,12,3) };
        group.Controls.Add(new Label { Text=label, AutoSize=true }); group.Controls.Add(control); panel.Controls.Add(group);
    }
    private async Task LoadData()
    {
        refresh.Enabled=false;
        try {
            loans=await Task.Run(Loan.Load); if(loans.Count==0) throw new InvalidDataException("The database contains no loans.");
            from.Value=loans.Min(x=>x.Issued); to.Value=loans.Max(x=>x.Issued);
            category.Items.AddRange(loans.Select(x=>x.Category).Distinct().OrderBy(x=>x).Cast<object>().ToArray());
            debtStatus.Items.AddRange(loans.Where(x=>x.Bad).Select(x=>Presentation.Status(x.Status)).Distinct().OrderBy(x=>x).Cast<object>().ToArray());
            product.Items.AddRange(new object[]{"Premium credit card","Life insurance","Savings / term deposit"});
            ready=true; RenderSafe();
        } catch(Exception ex) { MessageBox.Show(this,ex.Message,"Database could not be loaded",MessageBoxButtons.OK,MessageBoxIcon.Error); status.Text="Database unavailable. See README for setup."; }
        finally { refresh.Enabled=true; }
    }
    private void RenderSafe()
    {
        if(!ready) return;
        try { Cursor=Cursors.WaitCursor; RenderPage(); }
        catch(Exception ex) { MessageBox.Show(this,ex.Message,"Check filters and assumptions",MessageBoxButtons.OK,MessageBoxIcon.Warning); }
        finally { Cursor=Cursors.Default; }
    }
    private void RenderPage()
    {
        if(page!=7 && from.Value.Date>to.Value.Date) throw new ArgumentException("The start date must be on or before the end date.");
        var selected=loans.Where(l=>l.Issued>=from.Value.Date && l.Issued<=to.Value.Date && (!individuals.Checked || l.Purpose!="small_business") && (category.SelectedIndex<=0 || l.Category==(string)category.SelectedItem!)).ToList();
        foreach(var c in new Control[]{lookback,horizon,delta,elasticity,funding,operating,debtStatus,product,grade,earlyGrade,earlyView,search}) c.Parent?.Controls.Remove(c);
        while(settings.Controls.Count>0) { var old=settings.Controls[0]; settings.Controls.Remove(old); old.Dispose(); }
        filters.Visible=page!=7; settings.Visible=page is 1 or 2 or 3 or 4 or 5 or 6;
        body.RowStyles[1].Height=filters.Visible?65:0; body.RowStyles[2].Height=settings.Visible?65:0;
        chart.Mode=ChartMode.Bars; chart.Items=new(); chart.ForecastData=new(); chart.Scenarios=new(); chart.Unit="USD";
        status.Text=page==7?$"Full source: {loans.Count:N0} loans | Filters do not apply to this screen":$"{selected.Count:N0} loans in selected issue-date cohort | Historical status snapshot | USD | Customer IDs are anonymized";
        grid.DefaultCellStyle.WrapMode=DataGridViewTriState.False; grid.AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.None;
        switch(page) { case 0: Portfolio(selected); break; case 1: Debtors(selected); break; case 2: Forecast(selected); break; case 3: Scenarios(selected); break; case 4: Profit(selected); break; case 5: CrossSell(selected); break; case 6: EarlyRepayment(selected); break; default: Methods(); break; }
        chart.Invalidate();
    }
    private void Header(string title,string summary,string explanation)
    {
        heading.Text=title; metrics.Text=summary.Replace("  |  ","     "); screenExplanation=explanation;
        notes.Text=page switch {
            0=>"Share of total granted amount. Smaller categories are grouped in the pie chart.",
            1=>"Filter by status or search an ID. Double-click a row for more information.",
            2=>"Blue: recorded lending. Dashed orange: forecast. See Help for the calculation method.",
            3=>"Compare the same forecast period at three interest rates. Demand elasticity is an assumption.",
            4=>"Closed loans only. Negative margins are shown in red. Double-click for the cost breakdown.",
            5=>"Select a product to review matching customers. Recommendations are based on profile rules.",
            6=>"Fully paid at least three months early. Unearned interest is an estimate, not net loss.",
            _=>"Source and calculation notes. Date and category filters do not apply here."
        };
    }
    private void Params(params (string Label,Control Control)[] fields)
    {
        foreach(var f in fields) AddField(settings,f.Label,f.Control);
        var run=new Button { Text=page is 1 or 5 or 6?"Apply":"Recalculate", AutoSize=true, Margin=new Padding(8,18,0,0) }; run.Click+=(_,_)=>RenderSafe(); settings.Controls.Add(run);
    }
    private string Heading(string key) => headers.GetValueOrDefault(key,Regex.Replace(key,"([a-z])([A-Z])","$1 $2"));
    private void ShowTable<T>(IEnumerable<T> source,params string[] visible)
    {
        var properties=typeof(T).GetProperties(); var table=new DataTable();
        foreach(var p in properties) table.Columns.Add(p.Name,Nullable.GetUnderlyingType(p.PropertyType)??p.PropertyType);
        foreach(var item in source) table.Rows.Add(properties.Select(p=>p.GetValue(item)??DBNull.Value).ToArray());
        visibleColumns=visible.Length==0?null:visible.ToHashSet(); sortColumn=""; pager.Set(table); BindPage();
    }
    private void BindPage()
    {
        grid.DataSource=pager.Current();
        foreach(DataGridViewColumn column in grid.Columns) {
            column.HeaderText=Heading(column.Name); column.SortMode=DataGridViewColumnSortMode.Programmatic;
            column.Visible=visibleColumns==null||visibleColumns.Contains(column.Name);
            column.MinimumWidth=column.Name is "Reason" or "RetentionIdea"?190:column.Name is "Category" or "Product" or "Status" or "Action"?125:75;
            column.FillWeight=column.Name is "Reason" or "RetentionIdea"?190:column.Name is "Category" or "Product" or "Status" or "Action"?140:100;
            column.DefaultCellStyle.NullValue="Not recorded";
            if(column.Name==sortColumn) column.HeaderCell.SortGlyphDirection=sortDescending?SortOrder.Descending:SortOrder.Ascending;
        }
        previous.Enabled=pager.PageIndex>0; next.Enabled=pager.PageIndex<pager.PageCount-1;
        range.Text=pager.Count==0?"No matching results":$"Rows {pager.PageIndex*ResultPager.PageSize+1:N0}–{Math.Min((pager.PageIndex+1)*ResultPager.PageSize,pager.Count):N0} of {pager.Count:N0} • Page {pager.PageIndex+1}/{pager.PageCount}";
        if(pager.Count==0) range.Text+=" • Adjust the filters";
    }
    private void Details()
    {
        if(grid.CurrentRow?.DataBoundItem is not DataRowView row) return;
        using var dialog=new Form { Text="Result details", Size=new Size(690,690), StartPosition=FormStartPosition.CenterParent, Font=Font };
        var text=new TextBox { Dock=DockStyle.Fill, Multiline=true, ReadOnly=true, ScrollBars=ScrollBars.Vertical, BackColor=Color.White, Font=new Font("Segoe UI",11), Text=string.Join(Environment.NewLine+Environment.NewLine,row.Row.Table.Columns.Cast<DataColumn>().Select(c=>$"{Heading(c.ColumnName)}: {(row[c.ColumnName] is double value?value.ToString("N2"):row[c.ColumnName]==DBNull.Value?"Not recorded":row[c.ColumnName])}")) };
        dialog.Controls.Add(text); dialog.ShowDialog(this);
    }
    private bool MatchesSearch(Loan loan) => string.IsNullOrWhiteSpace(search.Text)||loan.Customer.Contains(search.Text.Trim(),StringComparison.OrdinalIgnoreCase)||loan.Id.ToString().Contains(search.Text.Trim(),StringComparison.OrdinalIgnoreCase);
    private void Bars(IEnumerable<(string,double)> rows,string caption,string unit="USD") { chart.Items=rows.ToList(); chart.Caption=caption; chart.Unit=unit; }
    private void Portfolio(List<Loan> data)
    {
        double total=data.Sum(x=>x.Amount);
        var rows=data.GroupBy(x=>x.Category).Select(g=>new { Category=g.Key, Loans=g.Count(), AmountUSD=g.Sum(x=>x.Amount), VolumeSharePct=total==0?0:100*g.Sum(x=>x.Amount)/total, CountSharePct=data.Count==0?0:100.0*g.Count()/data.Count, AverageRatePct=g.Average(x=>x.Rate) }).OrderByDescending(x=>x.AmountUSD).ToList();
        Header("Loan portfolio",$"Total granted: {total:N0} USD  |  Loans: {data.Count:N0}  |  Loan categories: {rows.Count}","The pie chart shows shares of granted money, not loan counts. Five largest categories are shown separately; smaller categories are combined. The table contains every category.");
        ShowTable(rows); Bars(rows.Select(x=>(x.Category,x.AmountUSD)),"Loan categories by granted amount"); chart.Mode=ChartMode.Donut;
    }
    private void Debtors(List<Loan> data)
    {
        Params(("Payment status",debtStatus),("Find customer or loan",search));
        var all=data.Where(x=>x.Bad).ToList();
        var bad=all.Where(x=>(debtStatus.SelectedIndex==0||Presentation.Status(x.Status)==debtStatus.Text)&&MatchesSearch(x)).OrderByDescending(x=>x.Severity).ThenByDescending(x=>Math.Max(x.Balance,Presentation.Shortfall(x))).ToList();
        Header("Debtors and arrears",$"Matching flagged loans: {bad.Count:N0} of {all.Count:N0}  |  Reported balance: {bad.Sum(x=>x.Balance):N0} USD  |  Closed-loan shortfall: {bad.Sum(Presentation.Shortfall):N0} USD", "Choose a payment status or click a status bar to narrow the list. Fifty results per page; highest severity appears first. Grace period is an early warning. Charged-off loans may have zero balance but an unrecovered principal shortfall. Details... for income and borrowing information.");
        ShowTable(bad.Select(x=>new { Customer=x.Customer, Status=Presentation.Status(x.Status), Category=x.Category, BalanceUSD=x.Balance, ShortfallUSD=Presentation.Shortfall(x), Action=Presentation.Priority(x), Loan=x.Id, Grade=x.Grade, AnnualIncomeUSD=x.Income, DtiPct=x.Dti, GrantedAmountUSD=x.Amount }),"Customer","Status","Category","BalanceUSD","ShortfallUSD","Action");
        Bars(bad.GroupBy(x=>Presentation.Status(x.Status)).Select(g=>(g.Key,(double)g.Count())).OrderByDescending(x=>x.Item2),"Flagged loans by status (click to filter)","loans");
    }
    private List<MonthPoint> History(List<Loan> data)
    {
        var end=new DateTime(to.Value.Year,to.Value.Month,1); var start=end.AddMonths(1-(int)lookback.Value); var selectedStart=new DateTime(from.Value.Year,from.Value.Month,1); if(start<selectedStart) start=selectedStart; return Analytics.Monthly(data,start,end);
    }
    private void Forecast(List<Loan> data)
    {
        Params(("History window (months)",lookback),("Forecast horizon (months)",horizon));
        var history=History(data); var result=Analytics.Forecast(history,(int)horizon.Value); var future=result.Where(x=>x.Kind=="Forecast").ToList();
        Header("Loan forecast",future.Count==0?"Not enough history: select at least two months.":$"History ends {history[^1].Month:MMM yyyy}  |  Forecast: {future[0].Month} to {future[^1].Month}  |  Expected volume: {future.Sum(x=>x.Volume):N0} USD", "Solid blue = historical lending. Dashed orange = forecast; the pale orange area marks future months. Hover over the graph for monthly values. This is a straight-line trend model, so its forecast is straight, not artificially curved. Missing months are zero-filled; predictions are sample estimates, not guaranteed lending.");
        ShowTable(result.Select(x=>new { Month=x.Month, Kind=x.Kind=="Observed"?"Historical":"Forecast", Volume=x.Volume, Count=x.Count }));
        chart.Mode=ChartMode.Forecast; chart.ForecastData=result; chart.Caption="Monthly loan volume (USD)";
    }
    private void Scenarios(List<Loan> data)
    {
        Params(("History (months)",lookback),("Horizon (months)",horizon),("Rate change (± pp)",delta),("Demand elasticity",elasticity));
        var history=History(data); var future=Analytics.Forecast(history,(int)horizon.Value).Where(x=>x.Kind=="Forecast").ToList();
        var window=data.Where(x=>x.Issued>=history[0].Month).ToList(); double amount=window.Sum(x=>x.Amount), rate=amount==0?0:window.Sum(x=>x.Amount*x.Rate)/amount;
        if(rate>0&&rate-(double)delta.Value<=0) throw new ArgumentException("The lower-rate scenario must stay above zero. Reduce the rate-change size.");
        var rows=new List<ScenarioRow>();
        if(rate>0&&future.Count>0) foreach(var (name,change) in new[]{("Lower rate",-(double)delta.Value),("Unchanged rate",0d),("Higher rate",(double)delta.Value)}) {
            double factor=Analytics.DemandMultiplier(rate,change,(double)elasticity.Value);
            rows.Add(new ScenarioRow(name,change,rate+change,100*(factor-1),future.Sum(x=>x.Count)*factor,future.Sum(x=>x.Volume)*factor));
        }
        Header("Interest rate comparison",$"Baseline interest rate: {rate:N2}%  |  Assumed elasticity: {elasticity.Value:N2}  |  Forecast horizon: {horizon.Value} months", "Compare three offers: lower, unchanged and higher interest rates. Each card shows its rate, projected lending and demand change against the baseline. Percentage points are not percent changes. Demand response is an adjustable assumption; the source has granted loans, not observed applications.");
        ShowTable(rows,"Scenario","NewRatePct","DemandChangePct","ExpectedLoanCount","ExpectedVolumeUSD");
        chart.Mode=ChartMode.Scenarios; chart.Scenarios=rows.Select(x=>new ScenarioVisual(x.Scenario,x.NewRatePct,x.ExpectedVolumeUSD,x.ExpectedLoanCount,x.DemandChangePct)).ToList(); chart.Caption="Comparison of interest rate scenarios";
    }
    private sealed record ScenarioRow(string Scenario,double RateChangePoints,double NewRatePct,double DemandChangePct,double ExpectedLoanCount,double ExpectedVolumeUSD);
    private void Profit(List<Loan> data)
    {
        Params(("Annual funding cost (%)",funding),("Lifetime operating cost (%)",operating)); double f=(double)funding.Value,o=(double)operating.Value;
        var closed=data.Where(x=>x.Closed).ToList();
        var rows=closed.GroupBy(x=>x.Category).Select(g=>new { Category=g.Key, ClosedLoans=g.Count(), NetMarginUSD=g.Sum(x=>x.Net(f,o)), NetMarginPct=g.Sum(x=>x.Funded)==0?0:100*g.Sum(x=>x.Net(f,o))/g.Sum(x=>x.Funded), FundedUSD=g.Sum(x=>x.Funded), EstimatedCostsUSD=g.Sum(x=>x.Funded*(f/100*x.ExposureYears+o/100)), InterestFeesUSD=g.Sum(x=>x.Interest+x.Fees), RecoveriesUSD=g.Sum(x=>x.Recoveries-x.CollectionCost), PrincipalLossUSD=g.Where(x=>x.Bad).Sum(x=>Math.Max(0,x.Funded-x.Principal)) }).OrderByDescending(x=>x.NetMarginUSD).ToList();
        Header("Loan profitability",$"Closed loans: {closed.Count:N0}  |  Estimated lifetime net margin: {closed.Sum(x=>x.Net(f,o)):N0} USD", "Which categories contribute profit after losses and assumed costs? Blue bars indicate positive margins; red bars indicate negative margins. Closed loans only. Margin is lifetime margin, not annual return. Funding uses original principal and approximate duration; open a row for the revenue and loss breakdown.");
        ShowTable(rows,"Category","ClosedLoans","FundedUSD","NetMarginUSD","NetMarginPct","EstimatedCostsUSD"); Bars(rows.Select(x=>(x.Category,x.NetMarginUSD)),"Estimated net margin by category (USD)");
    }
    private void CrossSell(List<Loan> data)
    {
        Params(("Suggested product",product),("Risk grade",grade),("Find customer or loan",search));
        var all=data.Where(x=>Analytics.Product(x)!=null).ToList();
        var candidates=all.Where(x=>(product.SelectedIndex==0||Analytics.Product(x)==product.Text)&&(grade.SelectedIndex==0||x.Grade==grade.Text)&&MatchesSearch(x)).OrderBy(x=>Analytics.Product(x)).ThenByDescending(x=>x.Income).ToList();
        Header("Cross-selling",$"Matching candidates: {candidates.Count:N0} of {all.Count:N0} eligible profiles  |  Product suggestions are for human review", "The suggested product and its reason appear first. Choose a product or click its bar to focus the list. Eligibility: grade A/B, no flagged status, zero recorded delinquencies and known debt-to-income ratio ≤ 35%. Suggestions use profile rules; current product ownership and actual purchases are unavailable.");
        ShowTable(candidates.Select(x=>new { Product=Analytics.Product(x), Customer=x.Customer, Reason=Presentation.Reason(x), AnnualIncomeUSD=x.Income, DtiPct=x.Dti, Grade=x.Grade, Loan=x.Id, Category=x.Category, Status=Presentation.Status(x.Status), Cards=x.Cards, Mortgages=x.Mortgages, Home=Presentation.Home(x.Home) }),"Product","Customer","Reason","AnnualIncomeUSD","DtiPct","Grade");
        Bars(candidates.GroupBy(x=>Analytics.Product(x)!).Select(g=>(g.Key,(double)g.Count())),"Candidates by suggested product (click to filter)","candidates");
    }
    private void EarlyRepayment(List<Loan> data)
    {
        Params(("Table view",earlyView),("Risk grade",earlyGrade),("Find customer or loan",search));
        var cohort=data.Where(x=>(earlyGrade.SelectedIndex==0||x.Grade==earlyGrade.Text)&&MatchesSearch(x)).ToList();
        var paid=cohort.Where(x=>x.Closed&&!x.Bad).ToList(); var early=paid.Where(x=>x.Early).ToList();
        int unknown=paid.Count(x=>x.MonthsPaid is null or <0); int other=paid.Count-early.Count-unknown;
        Header("Early repayment",$"Early: {early.Count:N0}  |  Other fully paid: {other:N0}  |  Unknown duration: {unknown:N0}\nEstimated unearned interest: {early.Sum(x=>x.LostInterest):N0} USD", "Early means fully paid at least three months before maturity. Other fully paid includes near-maturity and later payoffs; it is not an exact on-time classification. The chart shows estimated interest forgone, not net loss. Recent loan cohorts have limited maturity follow-up. Switch to Customer profiles to compare income, housing and grade alongside months early. Open details for exact source months and the original term.");
        ShowTable(early.OrderByDescending(x=>x.LostInterest).Select(x=>new { Customer=x.Customer, Category=x.Category, UnearnedInterestUSD=x.LostInterest, MonthsToPayoff=x.MonthsPaid, MonthsSaved=x.Term-x.MonthsPaid, RetentionIdea=Presentation.Retention(x), Loan=x.Id, Grade=x.Grade, AnnualIncomeUSD=x.Income, Home=Presentation.Home(x.Home), DtiPct=x.Dti, Issued=x.Issued.ToString("yyyy-MM"), Paid=x.LastPayment?.ToString("yyyy-MM"), OriginalMonths=x.Term }),earlyView.SelectedIndex==0?new[]{"Customer","Category","UnearnedInterestUSD","MonthsToPayoff","MonthsSaved","RetentionIdea"}:new[]{"Customer","Grade","AnnualIncomeUSD","Home","MonthsSaved","RetentionIdea"});
        Bars(early.GroupBy(x=>x.Category).Select(g=>(g.Key,g.Sum(x=>x.LostInterest))).OrderByDescending(x=>x.Item2),"Estimated unearned interest by category (USD)");
    }
    private void Methods()
    {
        Header("Methods and source data",$"Full source: {loans.Count:N0} loans  |  Issued {loans.Min(x=>x.Issued):MMM yyyy}–{loans.Max(x=>x.Issued):MMM yyyy}  |  USD", "This page describes the entire source, so date and category filters are hidden. All dashboard text and categories are in English. Customer names are absent from the source; the application retains anonymized customer IDs. Amounts are historical US dollars, not Romanian branch figures.");
        ShowTable(new[] {
            new { Topic="Portfolio shares", Method="Amount share and loan-count share within the selected portfolio. Pie chart: five largest categories plus all remaining categories together." },
            new { Topic="Arrears", Method="Source payment statuses identify warning, late and charged-off loans. Status filters, search and 50-row pages keep the review list focused." },
            new { Topic="Forecast", Method="Straight-line regression over monthly volume and count. Solid blue history; dashed orange future. Zero-filled missing months; no confidence interval or seasonality model." },
            new { Topic="Rate scenarios", Method="Assumed constant elasticity scales the baseline forecast. It is not a causal demand model estimated from applications or rejected requests." },
            new { Topic="Profitability", Method="Closed-loan interest, penalties and net recoveries minus principal loss and assumed funding / operating costs. A lifetime estimate, not audited profit." },
            new { Topic="Cross-selling", Method="Grade A/B and payment/DTI eligibility rules suggest one product per profile. No actual purchase baskets or verified current product ownership exist." },
            new { Topic="Early payoff", Method="Fully paid at least three calendar months before original maturity. Estimated unearned interest = max(installment × term − funded principal − interest received, 0)." },
            new { Topic="Dates and identities", Method="Status reflects the dataset snapshot, not the issue-date endpoint. Artificial customer IDs represent loan-linked profiles, not verified repeat customer histories." }
        });
        grid.Columns["Topic"].FillWeight=25; grid.Columns["Method"].FillWeight=75; grid.DefaultCellStyle.WrapMode=DataGridViewTriState.True; grid.AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.AllCells;
        Bars(loans.GroupBy(x=>x.Issued.Year).OrderBy(x=>x.Key).Select(g=>(g.Key.ToString(),(double)g.Count())),"Loans by issue year (full dataset)","loans");
    }
    private void Export()
    {
        if(pager.View.Table==null||pager.View.Table.Columns.Count==0) return;
        using var dialog=new SaveFileDialog { Filter="CSV file (*.csv)|*.csv", FileName=$"loanscope-{page+1}-{DateTime.Now:yyyyMMdd}.csv" }; if(dialog.ShowDialog(this)!=DialogResult.OK) return;
        try {
            static string Cell(object? value) { var text=value==null||value==DBNull.Value?"":Convert.ToString(value,CultureInfo.InvariantCulture)??""; if(value is string&&text.TrimStart().Length>0&&"=+-@".Contains(text.TrimStart()[0])) text="'"+text; return "\""+text.Replace("\"","\"\"")+"\""; }
            using var writer=new StreamWriter(dialog.FileName,false,new UTF8Encoding(true)); var columns=pager.View.Table.Columns.Cast<DataColumn>().ToArray();
            writer.WriteLine(string.Join(",",columns.Select(c=>Cell(Heading(c.ColumnName)))));
            foreach(DataRowView row in pager.View) writer.WriteLine(string.Join(",",columns.Select(c=>Cell(row[c.ColumnName]))));
            status.Text=$"Exported all {pager.Count:N0} filtered results, including detail columns, in the selected sort order.";
        } catch(Exception ex) { MessageBox.Show(this,ex.Message,"Export failed",MessageBoxButtons.OK,MessageBoxIcon.Error); }
    }
}
