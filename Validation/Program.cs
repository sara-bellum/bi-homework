using Decision_support_loans_bank;
void Check(bool ok,string name) { if(!ok) throw new Exception(name); Console.WriteLine("PASS: "+name); }
void Close(double actual,double expected,string name) => Check(Math.Abs(actual-expected)<0.00001,name);
Close(Analytics.Predict(new double[]{10,20,30},1),40,"OLS linear trend");
Close(Analytics.Predict(new double[]{30,20,10},3),0,"negative forecast floored");
Close(Analytics.Predict(new double[]{7},1),7,"single month helper");
Close(Analytics.DemandMultiplier(10,-1,-1),10.0/9,"rate cut increases assumed demand");
Close(Analytics.DemandMultiplier(10,1,0),1,"zero elasticity unchanged demand");
try { Analytics.DemandMultiplier(5,-5,-1); throw new Exception("invalid rate accepted"); } catch(ArgumentException) { Console.WriteLine("PASS: invalid rate rejected"); }
var paid=new Loan { Closed=true, Funded=1000, Interest=80, Term=12, Installment=100, Issued=new DateTime(2018,1,1),LastPayment=new DateTime(2018,8,1) };
Check(paid.Early,"early repayment classification"); Close(paid.LostInterest,120,"unearned interest excludes principal");
Check(!(paid with { LastPayment=new DateTime(2018,11,1) }).Early,"two months early excluded");
Check(!(paid with { LastPayment=new DateTime(2017,11,1) }).Early,"invalid negative duration excluded");
Close(paid.Net(12,1),0,"funding and operating cost math");
var eligible=new Loan { Grade="A", Delinquencies=0, Dti=20, Income=120000,Cards=4 };
Check(Analytics.Product(eligible)=="Premium credit card","cross-selling premium rule");
Check(Analytics.Product(eligible with { Dti=null })==null,"missing eligibility data excluded");
var months=Analytics.Monthly(new[]{paid},new DateTime(2018,1,1),new DateTime(2018,3,1));
Check(months.Count==3 && months[1].Count==0,"missing months zero filled");
var all=Loan.Load(); Check(all.Count==90427,"supplied database loads every loan");
var personal=all.Where(x=>x.Purpose!="small_business").ToList();
Close(personal.GroupBy(x=>x.Category).Sum(g=>g.Sum(x=>x.Amount))/personal.Sum(x=>x.Amount),1,"portfolio shares reconcile");
Check(all.Count(x=>x.Early)==31914,"early payoff reconciles with source view");
Check(all.All(x=>double.IsFinite(x.Net(3,1))),"net margins finite across complete source");
Console.WriteLine($"DATA: personal={personal.Count}; flagged={personal.Count(x=>x.Bad)}; early={personal.Count(x=>x.Early)}; amount={personal.Sum(x=>x.Amount):F2}; net={personal.Where(x=>x.Closed).Sum(x=>x.Net(3,1)):F2}");

Check(all.All(x=>!string.IsNullOrWhiteSpace(x.Category)) && all.First(x=>x.Purpose=="car").Category=="Auto","English purpose mapping in real loan data");
Check(Presentation.Category("debt_consolidation")=="Debt consolidation","English category label");
Check(Presentation.Status("Does not meet the credit policy. Status:Charged Off")=="Charged off (outside policy)","English outside-policy status");
Check(Presentation.Home("MORTGAGE")=="Mortgaged home","English housing label");
Check(Presentation.Reason(eligible).Contains("100k"),"product recommendation has explanation");
Check(Presentation.Priority(new Loan { Closed=false, Severity=3 })=="Urgent arrears review","arrears action label");
Close(Presentation.Shortfall(new Loan { Closed=true,Bad=true,Funded=1000,Principal=300,Recoveries=100 }),600,"recovery shortfall");
var table=new System.Data.DataTable(); table.Columns.Add("Value",typeof(int));
foreach(int i in Enumerable.Range(1,123)) table.Rows.Add(i);
var pager=new ResultPager(); pager.Set(table);
Check(pager.Count==123 && pager.PageCount==3 && pager.Current().Rows.Count==50,"result paging limits rendered rows");
pager.Move(1); Check((int)pager.Current().Rows[0][0]==51,"second result page");
pager.Move(100); Check(pager.PageIndex==2 && pager.Current().Rows.Count==23,"last page clamped");
pager.Sort("Value",true); Check(pager.PageIndex==0 && (int)pager.Current().Rows[0][0]==123,"sorting applies to all result rows");
pager.Move(1); Check((int)pager.Current().Rows[0][0]==73,"sorted second page");
pager.Set(table.Clone()); Check(pager.Count==0 && pager.PageCount==1 && pager.Current().Rows.Count==0,"empty result paging");
var forecast=Analytics.Forecast(months,2);
Check(forecast.Count==5 && forecast[2].Kind=="Observed" && forecast[3].Kind=="Forecast" && forecast[3].Month=="2018-04","forecast boundary follows final observed month");
Check(Analytics.Forecast(new List<MonthPoint>{months[0]},6).All(x=>x.Kind=="Observed"),"no future forecast for insufficient history");

var filter=new LoanFilter(new DateTime(2017,1,1),new DateTime(2018,12,1),Category:"Auto",State:"CA",IndividualsOnly:true,Grade:"B");
var actual=LoanRepository.Load(filter);
var expected=all.Where(l=>l.Issued>=filter.From && l.Issued<=filter.Through && l.Category=="Auto" && l.State=="CA" && l.Grade=="B" && l.Purpose!="small_business").ToList();
Check(actual.Select(l=>l.Id).SequenceEqual(expected.Select(l=>l.Id)),"SQL date/category/state/grade filters match independent selection");
var query=LoanRepository.Build(filter);Check(query.Sql.Contains("@state") && !query.Sql.Contains("'CA'") && query.Parameters.Any(p=>p.Name=="@state"&&(string)p.Value! =="CA"),"dynamic SQL binds values as parameters");
Check(!LoanRepository.Build(new LoanFilter()).Sql.Contains("@state"),"unused SQL condition omitted");
Check(LoanRepository.Load(new LoanFilter(State:"CA' OR 1=1 --")).Count==0,"state SQL injection treated as literal");
Check(LoanRepository.Load(new LoanFilter(Search:"%' OR 1=1 --")).Count==0,"search SQL injection treated as literal");
Check(LoanRepository.Load(new LoanFilter(Search:"%")).Count==0,"search percent sign treated literally");
var flagged=LoanRepository.Load(new LoanFilter(FlaggedOnly:true,Status:"Late (31-120 days)"));
Check(flagged.Count>0 && flagged.All(l=>l.Bad && l.Status=="Late (31-120 days)"),"SQL status filter");
foreach(string name in new[]{"Premium credit card","Life insurance","Savings / term deposit"}) {
    var found=LoanRepository.Load(new LoanFilter(LowRiskOnly:true,Product:name));
    Check(found.Select(l=>l.Id).SequenceEqual(all.Where(l=>Analytics.Product(l)==name).Select(l=>l.Id)),"SQL product eligibility: "+name);
}
Check(LoanRepository.Load(new LoanFilter(Category:"Unknown category")).Count==0,"unknown category yields empty result");
try { LoanRepository.Build(new LoanFilter(new DateTime(2018,1,1),new DateTime(2017,1,1)));throw new Exception("invalid date interval accepted"); } catch(ArgumentException){Console.WriteLine("PASS: inverted SQL date interval rejected");}
var geo=GeographyService.Aggregate(personal);Check(geo.Sum(x=>x.Loans)==personal.Count,"geography loan counts reconcile");
Close(geo.Sum(x=>x.Amount),personal.Sum(x=>x.Amount),"geography amounts reconcile");
Check(GeographyService.Shapes.Count==51 && geo.All(x=>GeographyService.Shapes.Any(s=>s.Code==x.Code)),"all source states have embedded geography");
Check(geo.All(x=>Math.Abs(x.AutoAmount+x.ConsolidationAmount+x.OtherAmount-x.Amount)<.00001),"map pie slices reconcile with state volume");
var outDir=Path.GetFullPath(Environment.GetEnvironmentVariable("BI_VALIDATION_OUTPUT")??Path.Combine(Environment.CurrentDirectory,"validation-output"));Directory.CreateDirectory(outDir);
MapHtmlExporter.Write(Path.Combine(outDir,"map-pies.html"),geo,MapMetric.CategoryPies,"Test cohort <filtered>");
var html=File.ReadAllText(Path.Combine(outDir,"map-pies.html"));Check(html.Contains("<svg") && html.Contains("Auto") && html.Contains("&lt;filtered&gt;") && !html.Contains("<script src"),"offline map HTML contains pies, labels and escaped scope");
var view=new System.Data.DataTable();view.Columns.Add("Customer",typeof(string));view.Columns.Add("Amount",typeof(double));view.Rows.Add("CL000001",1000d);
var report=new ReportSnapshot(personal,new DateTime(2007,6,1),new DateTime(2018,12,1),24,6,3,1,0,5,1,-1.2,"All individual loans",new DateTime(2026,11,25));
string xlsx=Path.Combine(outDir,"Bank-Loan-Report.xlsx");ExcelExporter.Write(xlsx,report);
using(var workbook=DocumentFormat.OpenXml.Packaging.SpreadsheetDocument.Open(xlsx,false)) {
    var errors=new DocumentFormat.OpenXml.Validation.OpenXmlValidator().Validate(workbook).ToList();
    foreach(var error in errors.Take(20)) Console.WriteLine($"OPENXML: {error.Description} | {error.Path?.XPath}");
    Check(errors.Count==0,"Excel workbook passes Open XML schema validation");
}
using(var zip=System.IO.Compression.ZipFile.OpenRead(xlsx)) {
    Check(zip.Entries.Count(e=>e.FullName.StartsWith("xl/charts/chart")&&e.FullName.EndsWith(".xml"))==5,"Excel contains five native charts");
    string Read(string name){using var reader=new StreamReader(zip.GetEntry(name)!.Open());return reader.ReadToEnd();}
    Check(Read("xl/worksheets/sheet7.xml").Contains("Settings!$B$6") && Read("xl/worksheets/sheet4.xml").Contains("conditionalFormatting"),"profit Excel formulas and conditional rules exist");
    Check(Read("xl/worksheets/sheet3.xml").Contains("FORECAST(") && Read("xl/worksheets/sheet3.xml").Contains("RSQ("),"forecast Excel formulas and fit statistic exist");
    Check(Read("xl/charts/chart3.xml").Contains("dash"),"Excel future series has dashed styling");
}
ExcelExporter.Write(Path.Combine(outDir,"Empty-Report.xlsx"),report with {Loans=new List<Loan>()});
ExcelExporter.Write(Path.Combine(outDir,"Single-Month-Report.xlsx"),report with {From=new DateTime(2018,12,1),Through=new DateTime(2018,12,1),Loans=personal.Where(l=>l.Issued==new DateTime(2018,12,1)).ToList()});
Check(File.Exists(Path.Combine(outDir,"Single-Month-Report.xlsx")),"export handles insufficient and empty history");
try {ExcelExporter.Write(Path.Combine(outDir,"invalid.xlsx"),report with {LowerMargin=5,UpperMargin=0});throw new Exception("invalid thresholds accepted");}catch(ArgumentException){Console.WriteLine("PASS: invalid Excel thresholds rejected");}
Console.WriteLine("Outputs: "+outDir);

using(var zip=System.IO.Compression.ZipFile.OpenRead(xlsx)) {
 string Xml(string name){using var r=new StreamReader(zip.GetEntry(name)!.Open());return r.ReadToEnd();}
 Check(Xml("xl/workbook.xml").Contains("name=\"Technical\"") && Xml("xl/workbook.xml").Contains("state=\"hidden\""),"technical calculation sheet hidden by default");
 Check(!Xml("xl/workbook.xml").Contains("Loan details"),"summary excludes individual detail rows by default");
 Check(zip.Entries.Count(e=>e.FullName.StartsWith("xl/tables/table")&&e.FullName.EndsWith(".xml"))==8,"eight native Excel summary tables with filter headers");
 Check(Xml("xl/worksheets/sheet3.xml").Contains("showGridLines=\"0\"") && Xml("xl/styles.xml").Contains("mmm yyyy"),"clean sheet views and typed month format");
}
string current=Path.Combine(outDir,"Current-Table.xlsx");
view.Columns["Customer"]!.Caption="Customer ID";
ExcelExporter.WriteCurrentTable(current,view,"Debtors and arrears","Payment status: late; all matching rows");
using(var workbook=DocumentFormat.OpenXml.Packaging.SpreadsheetDocument.Open(current,false)) {
 Check(!new DocumentFormat.OpenXml.Validation.OpenXmlValidator().Validate(workbook).Any(),"current-table export passes Open XML validation");
 var sheets=workbook.WorkbookPart!.Workbook.Sheets!.Elements<DocumentFormat.OpenXml.Spreadsheet.Sheet>().ToList();
 Check(sheets.Count==1 && sheets[0].Name=="Current table","current-table export contains just one sheet");
}
using(var zip=System.IO.Compression.ZipFile.OpenRead(current)) {
 using var reader=new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open());string xml=reader.ReadToEnd();
 Check(xml.Contains("Customer ID") && xml.Contains("CL000001") && xml.Contains("Payment status: late"),"current table keeps readable captions, rows and scope");
 Check(!zip.Entries.Any(e=>e.FullName.StartsWith("xl/charts/")),"current table excludes report charts and settings");
}
using(var zip=System.IO.Compression.ZipFile.OpenRead(xlsx)) {
 var text=string.Join("\n",zip.Entries.Where(e=>e.FullName.EndsWith(".xml")).Select(e=>{using var r=new StreamReader(e.Open());return r.ReadToEnd();}));
 Check(!text.Contains("Executed SQL")&&!text.Contains("Bound parameters")&&!text.Contains("SELECT "),"complete report contains no executed SQL or parameters");
 Check(text.Contains("name=\"Debtors and arrears\"")&&text.Contains("name=\"Cross-selling\"")&&text.Contains("name=\"Early repayment\""),"complete report covers all analysis tabs");
 Check(text.Contains("IFERROR(B5/Settings!$B$5,0)"),"debtor portfolio shares use all selected loans as denominator");
}
var global=new LoanFilter(new DateTime(2017,1,1),new DateTime(2018,12,1),State:"CA",IndividualsOnly:true);
var restricted=global with {Status="Late (31-120 days)",Grade="A",Search="42",FlaggedOnly=true,LowRiskOnly=true,Product="Life insurance"};
Check(restricted.ForCompleteReport()==global,"complete report drops every tab-specific restriction and keeps global filters");
var common=LoanRepository.Load(restricted.ForCompleteReport());
Check(common.Count>0 && common.Any(l=>!l.Bad) && common.Any(l=>l.Bad),"report from debtor filters includes both flagged and unflagged loans");
Check(common.Select(l=>l.Id).SequenceEqual(LoanRepository.Load(global).Select(l=>l.Id)),"complete-report cohort is independent of active analysis tab");
var many=new System.Data.DataTable();many.Columns.Add("Customer",typeof(string));many.Columns.Add("Status",typeof(string));many.Columns.Add("HiddenDetail",typeof(string));
foreach(int i in Enumerable.Range(1,123))many.Rows.Add("CL"+i,"Late","private detail");
var sorted=new System.Data.DataView(many){Sort="Customer DESC"};var visible=sorted.ToTable(false,"Customer","Status");
string paged=Path.Combine(outDir,"Current-Table-All-Rows.xlsx");ExcelExporter.WriteCurrentTable(paged,visible,"Debtors","Filtered and sorted");
using(var zip=System.IO.Compression.ZipFile.OpenRead(paged)) {
 using var reader=new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open());string xml=reader.ReadToEnd();
 var doc=System.Xml.Linq.XDocument.Parse(xml);System.Xml.Linq.XNamespace ns="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
 Check(doc.Descendants(ns+"row").Count(r=>int.Parse(r.Attribute("r")!.Value)>=5)==123,"current table exports all matching pages");
 Check(!xml.Contains("private detail")&&!xml.Contains("HiddenDetail"),"current table excludes hidden detail columns");
 Check(doc.Descendants(ns+"c").First(c=>c.Attribute("r")!.Value=="A5").Value==Convert.ToString(visible.Rows[0][0]),"current table preserves displayed sort order");
}
ExcelExporter.WriteCurrentTable(Path.Combine(outDir,"Empty-Table.xlsx"),view.Clone(),"No results","No matches");
using(var workbook=DocumentFormat.OpenXml.Packaging.SpreadsheetDocument.Open(Path.Combine(outDir,"Empty-Table.xlsx"),false))
 Check(!new DocumentFormat.OpenXml.Validation.OpenXmlValidator().Validate(workbook).Any(),"empty current table exports valid headers");
