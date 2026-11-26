using System.Data;
using System.Globalization;

namespace Decision_support_loans_bank;

public partial class Form1
{
    private LoanFilter ActiveFilter()
    {
        string? statusCode=debtStatus.SelectedIndex<=0?null:loans.Select(l=>l.Status).Distinct().FirstOrDefault(code=>Presentation.Status(code)==debtStatus.Text);
        return new LoanFilter(from.Value.Date,to.Value.Date,category.SelectedIndex<=0?null:category.Text,
            state.SelectedIndex<=0?null:state.Text,individuals.Checked,
            Status:page==1?statusCode:null,Grade:page==5&&grade.SelectedIndex>0?grade.Text:page==6&&earlyGrade.SelectedIndex>0?earlyGrade.Text:null,
            Search:page is 1 or 5 or 6?search.Text:null,FlaggedOnly:page==1,LowRiskOnly:page==5,
            Product:page==5&&product.SelectedIndex>0?product.Text:null);
    }
    private string Scope()
    {
        var f=ActiveFilter();var text=$"Tab: {sections.SelectedTab?.Text}; issued {from.Value:yyyy-MM-dd} to {to.Value:yyyy-MM-dd}; category: {category.Text}; state: {state.Text}; individuals only: {individuals.Checked}";
        if(f.Status!=null)text+="; status: "+Presentation.Status(f.Status);if(f.Grade!=null)text+="; grade: "+f.Grade;
        if(f.FlaggedOnly)text+="; flagged loans only";if(f.LowRiskOnly)text+="; low-risk eligible profiles only";
        if(f.Product!=null)text+="; product: "+f.Product;if(!string.IsNullOrWhiteSpace(f.Search))text+="; ID search: "+f.Search;
        return text;
    }
    private string ReportScope() => $"Complete report; issued {from.Value:yyyy-MM-dd} to {to.Value:yyyy-MM-dd}; category: {category.Text}; state: {state.Text}; individuals only: {individuals.Checked}";
    private ReportSnapshot CaptureCompleteReport()
    {
        var data=LoanRepository.Load(ActiveFilter().ForCompleteReport());
        return new ReportSnapshot(data,from.Value.Date,to.Value.Date,(int)lookback.Value,(int)horizon.Value,
            (double)funding.Value,(double)operating.Value,(double)lowerMargin.Value,(double)upperMargin.Value,
            (double)delta.Value,(double)elasticity.Value,ReportScope(),DateTime.Now);
    }
    private void Geographical(List<Loan> data)
    {
        Params(("Map representation",mapMetric));var rows=GeographyService.Aggregate(data);
        int unmapped=rows.Where(r=>!GeographyService.Shapes.Any(s=>s.Code==r.Code)).Sum(r=>r.Loans);
        Header("Geographical analysis",$"Loans: {data.Count:N0}   Granted: {data.Sum(l=>l.Amount):N0} USD   States with loans: {rows.Count:N0}   Unmapped loans: {unmapped:N0}","Historical US-state sample. Rate is flagged loans / selected loans in each state, including grace period. Category pies split amounts into Auto, Debt consolidation and all other purposes. Gray states have no selected loans. AK/HI are insets; locations are not exact customer addresses. Small samples are not population estimates.");
        ShowTable(rows.Select(r=>new {StateCode=r.Code,StateName=r.State,LoanCount=r.Loans,AmountUSD=r.Amount,FlaggedLoans=r.Flagged,FlaggedRatePct=100*r.FlaggedRate,AutoUSD=r.AutoAmount,ConsolidationUSD=r.ConsolidationAmount,OtherUSD=r.OtherAmount}),"StateCode","StateName","LoanCount","AmountUSD","FlaggedLoans","FlaggedRatePct");
        map.Data=rows;map.Metric=(MapMetric)mapMetric.SelectedIndex;map.FilterDescription=Scope();
    }
    private void ExportCompleteReport()
    {
        SaveExcel("Bank-Loan-Report",path=> {
            if(from.Value.Date>to.Value.Date)throw new ArgumentException("The start date must be on or before the end date.");
            var report=CaptureCompleteReport();
            if(report.Loans.Count==0)throw new ArgumentException("No loans match the global filters. Adjust them before exporting.");
            ExcelExporter.Write(path,report);
        });
    }
    private void ExportCurrentTable()
    {
        if(!RenderSafe()||pager.View.Table==null)return;
        var columns=grid.Columns.Cast<DataGridViewColumn>().Where(c=>c.Visible).OrderBy(c=>c.DisplayIndex).Select(c=>c.DataPropertyName).ToArray();
        if(columns.Length==0)return;
        var table=pager.View.ToTable(false,columns);
        foreach(DataColumn column in table.Columns)column.Caption=Heading(column.ColumnName);
        string scope=page==8?"Full-source methods; global filters do not apply.":Scope();
        SaveExcel("Bank-Loan-Table",path=>ExcelExporter.WriteCurrentTable(path,table,heading.Text,scope));
    }
    private void SaveExcel(string name,Action<string> write)
    {
        using var dialog=new SaveFileDialog {Filter="Excel workbook (*.xlsx)|*.xlsx",FileName=$"{name}-{DateTime.Now:yyyyMMdd-HHmm}.xlsx"};
        if(dialog.ShowDialog(this)!=DialogResult.OK)return;
        try {Cursor=Cursors.WaitCursor;write(dialog.FileName);status.Text="Excel export saved.";
            if(MessageBox.Show(this,"The Excel file was saved. Open it now?","Excel export",MessageBoxButtons.YesNo,MessageBoxIcon.Information)==DialogResult.Yes)System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dialog.FileName){UseShellExecute=true});
        }catch(Exception ex){MessageBox.Show(this,"Could not save or open the file. If it is already open in Excel, close it or choose another filename.\n\n"+ex.Message,"Excel export",MessageBoxButtons.OK,MessageBoxIcon.Warning);}finally{Cursor=Cursors.Default;}
    }
    private void ExportMap(bool png)
    {
        using var dialog=new SaveFileDialog {Filter=png?"PNG image (*.png)|*.png":"Offline HTML report (*.html)|*.html",FileName=$"Bank-Loan-Map-{DateTime.Now:yyyyMMdd-HHmm}."+(png?"png":"html")};if(dialog.ShowDialog(this)!=DialogResult.OK)return;
        try {
            if(from.Value.Date>to.Value.Date)throw new ArgumentException("The start date must be on or before the end date.");
            var data=LoanRepository.Load(ActiveFilter().ForCompleteReport());
            var exportMap=new MapControl {Data=GeographyService.Aggregate(data),Metric=(MapMetric)mapMetric.SelectedIndex,FilterDescription=ReportScope()};
            using(exportMap){if(png)exportMap.ExportPng(dialog.FileName);else exportMap.ExportHtml(dialog.FileName);}
            status.Text="Geographical report exported with the global filters and legend.";
        }catch(Exception ex){MessageBox.Show(this,ex.Message,"Map export failed",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
    }
    private void MarginThresholds()
    {
        using var dialog=new Form {Text="Excel conditional formatting",ClientSize=new Size(470,170),StartPosition=FormStartPosition.CenterParent,Font=Font};
        var low=Number(-100,100,lowerMargin.Value,2);var high=Number(-100,100,upperMargin.Value,2);
        var panel=new FlowLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(12)};AddField(panel,"Lower margin threshold (%)",low);AddField(panel,"Upper margin threshold (%)",high);
        panel.Controls.Add(new Label{Text="Red below lower; amber between thresholds; green at or above upper. Applies to lifetime margin in exported Excel.",Width=430,Height=45});
        var save=new Button {Text="Apply",AutoSize=true};save.Click+=(_,_)=>{if(low.Value>=high.Value){MessageBox.Show(dialog,"The lower threshold must be below the upper threshold.");return;}lowerMargin.Value=low.Value;upperMargin.Value=high.Value;dialog.DialogResult=DialogResult.OK;};panel.Controls.Add(save);dialog.Controls.Add(panel);dialog.ShowDialog(this);
    }
}
