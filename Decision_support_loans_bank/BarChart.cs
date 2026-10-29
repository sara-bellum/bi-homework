using System.Drawing.Drawing2D;
using System.Globalization;

namespace Decision_support_loans_bank;

public enum ChartMode { Bars, Donut, Forecast, Scenarios }
public sealed record ScenarioVisual(string Name,double Rate,double Volume,double Count,double ChangePct);

public sealed class BarChart : Control
{
    public List<(string Label,double Value)> Items { get; set; }=new();
    public List<ForecastPoint> ForecastData { get; set; }=new();
    public List<ScenarioVisual> Scenarios { get; set; }=new();
    public ChartMode Mode { get; set; }
    public string Caption { get; set; }="";
    public string Unit { get; set; }="USD";
    public event Action<string>? ItemSelected;
    private readonly List<(RectangleF Area,string Tip,string? Key)> hits=new();
    private readonly ToolTip tooltip=new() { InitialDelay=100, ReshowDelay=100, AutoPopDelay=15000 };
    private string lastTip="";
    private static readonly Color Ink=SystemColors.ControlText, Teal=Color.SteelBlue, Orange=Color.DarkOrange;
    private static readonly Color[] Palette={Teal,Color.DarkSeaGreen,Color.Tan,Color.SlateGray,Color.IndianRed,Color.LightGray};
    public BarChart() { DoubleBuffered=true; BackColor=Color.White; Dock=DockStyle.Fill; }
    protected override void Dispose(bool disposing) { if(disposing) tooltip.Dispose(); base.Dispose(disposing); }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e); var hit=hits.FirstOrDefault(h=>h.Area.Contains(e.Location));
        Cursor=hit.Key!=null?Cursors.Hand:Cursors.Default;
        if(hit.Tip!=lastTip) { lastTip=hit.Tip??""; tooltip.SetToolTip(this,lastTip); }
    }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); tooltip.Hide(this); lastTip=""; }
    protected override void OnMouseClick(MouseEventArgs e) { base.OnMouseClick(e); var key=hits.FirstOrDefault(h=>h.Area.Contains(e.Location)).Key; if(key!=null) ItemSelected?.Invoke(key); }
    public static string Compact(double number) => Math.Abs(number)>=1e9?$"{number/1e9:0.##}bn":Math.Abs(number)>=1e6?$"{number/1e6:0.##}m":Math.Abs(number)>=1e3?$"{number/1e3:0.#}k":number.ToString("N0");
    private static void DrawText(Graphics g,string text,Font font,Color color,RectangleF box,StringAlignment alignment=StringAlignment.Near)
    {
        using var brush=new SolidBrush(color); using var format=new StringFormat { Alignment=alignment,LineAlignment=StringAlignment.Center,Trimming=StringTrimming.EllipsisCharacter,FormatFlags=StringFormatFlags.NoWrap };
        g.DrawString(text,font,brush,box,format);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); hits.Clear(); var g=e.Graphics; g.SmoothingMode=SmoothingMode.AntiAlias;
        using var title=new Font("Segoe UI",9,FontStyle.Bold); using var small=new Font("Segoe UI",9);
        DrawText(g,Caption,title,Ink,new RectangleF(16,5,Width-32,30));
        if(Width<260||Height<125) return;
        if(Mode==ChartMode.Forecast) DrawForecast(g,small);
        else if(Mode==ChartMode.Scenarios) DrawScenarios(g,small);
        else if(Mode==ChartMode.Donut) DrawDonut(g,small,title);
        else DrawBars(g,small);
    }
    private void Empty(Graphics g,Font font,string message="No results. Adjust the filters to see data.") => DrawText(g,message,font,Color.SlateGray,new RectangleF(20,50,Width-40,40));
    private void DrawBars(Graphics g,Font font)
    {
        if(Items.Count==0) { Empty(g,font); return; }
        var items=Items.Take(14).ToList(); float label=Math.Min(230,Width*0.28f), right=105, top=43;
        float row=Math.Min(29,(Height-top-12)/items.Count), width=Width-label-right-25;
        double max=Math.Max(1,items.Max(x=>Math.Abs(x.Value))); bool negative=items.Any(x=>x.Value<0);
        float zero=label+(negative?width/2:0), span=negative?width/2:width;
        using var gridPen=new Pen(Color.FromArgb(223,230,238)); g.DrawLine(gridPen,zero,top,zero,Height-10);
        for(int i=0;i<items.Count;i++) {
            var x=items[i]; float y=top+i*row; var area=new RectangleF(12,y,Width-24,row);
            DrawText(g,x.Label,font,Ink,new RectangleF(15,y,label-24,row));
            float bar=(float)(Math.Abs(x.Value)/max*span);
            using var brush=new SolidBrush(x.Value<0?Color.IndianRed:Teal);
            g.FillRectangle(brush,x.Value<0?zero-bar:zero,y+4,bar,Math.Max(3,row-9));
            DrawText(g,Compact(x.Value),font,Ink,new RectangleF(label+width+8,y,right,row));
            hits.Add((area,$"{x.Label}: {x.Value:N2} {Unit}",x.Label));
        }
    }
    private void DrawDonut(Graphics g,Font font,Font bold)
    {
        var ordered=Items.Where(x=>x.Value>0).OrderByDescending(x=>x.Value).ToList();
        double total=ordered.Sum(x=>x.Value); if(total<=0) { Empty(g,font); return; }
        var slices=ordered.Take(5).ToList(); if(ordered.Count>5) slices.Add(("Other categories",ordered.Skip(5).Sum(x=>x.Value)));
        float size=Math.Min(Height-68,Width*0.39f), x=35,y=48;
        var box=new RectangleF(x,y,size,size); float angle=-90;
        for(int i=0;i<slices.Count;i++) {
            float sweep=(float)(360*slices[i].Value/total); using var brush=new SolidBrush(Palette[i]);
            g.FillPie(brush,box.X,box.Y,box.Width,box.Height,angle,sweep); angle+=sweep;
        }
        float lx=Math.Max(x+size+28,Width*.47f), row=Math.Min(35,(Height-55f)/slices.Count);
        for(int i=0;i<slices.Count;i++) {
            var item=slices[i]; float ly=48+i*row;
            using var brush=new SolidBrush(Palette[i]); g.FillRectangle(brush,lx,ly+9,12,12);
            DrawText(g,$"{item.Label}  •  {100*item.Value/total:N1}%",font,Ink,new RectangleF(lx+23,ly,Width-lx-40,row));
            hits.Add((new RectangleF(lx,ly,Width-lx-15,row),$"{item.Label}: {item.Value:N2} USD ({100*item.Value/total:N2}%)",null));
        }
    }
    private void DrawForecast(Graphics g,Font font)
    {
        if(ForecastData.Count==0) { Empty(g,font); return; }
        var list=ForecastData; int last=list.FindLastIndex(p=>p.Kind=="Observed");
        var plot=new RectangleF(80,85,Width-112,Height-136); if(plot.Height<25) return;
        double maximum=Math.Max(1,list.Max(x=>x.Volume)*1.15);
        float X(int i)=>plot.Left+(list.Count==1?plot.Width/2:plot.Width*i/(list.Count-1));
        float Y(double n)=>plot.Bottom-(float)(n/maximum*plot.Height);
        if(last>=0 && last<list.Count-1) {
            using var shade=new SolidBrush(Color.FromArgb(255,246,229));
            g.FillRectangle(shade,X(last),plot.Top,plot.Right-X(last),plot.Height);
        }
        using var pale=new Pen(Color.FromArgb(226,232,240));
        for(int i=0;i<=4;i++) { float yy=plot.Bottom-plot.Height*i/4; g.DrawLine(pale,plot.Left,yy,plot.Right,yy); DrawText(g,Compact(maximum*i/4),font,Color.SlateGray,new RectangleF(4,yy-10,67,20),StringAlignment.Far); }
        using var observed=new Pen(Teal,2.6f); using var forecast=new Pen(Orange,2.8f) { DashStyle=DashStyle.Dash };
        g.DrawLine(observed,18,48,50,48); DrawText(g,"Recorded lending",font,Ink,new RectangleF(57,37,160,25));
        g.DrawLine(forecast,224,48,256,48); DrawText(g,"Forecast (assumed trend)",font,Ink,new RectangleF(264,37,220,25));
        if(last>=0) {
            using var cutoff=new Pen(Color.SlateGray,1) { DashStyle=DashStyle.Dot };
            g.DrawLine(cutoff,X(last),plot.Top,X(last),plot.Bottom);
            DrawText(g,$"Last actual: {list[last].Month}  |  First forecast: {(last+1<list.Count?list[last+1].Month:"not available")}",font,Ink,new RectangleF(280,62,Math.Max(50,Width-295),20),StringAlignment.Far);
        }
        for(int i=1;i<list.Count;i++) g.DrawLine(i<=last?observed:forecast,X(i-1),Y(list[i-1].Volume),X(i),Y(list[i].Volume));
        for(int i=0;i<list.Count;i++) {
            float xx=X(i),yy=Y(list[i].Volume); using var brush=new SolidBrush(i<=last?Teal:Orange);
            if(list.Count<=45||i==last||i==list.Count-1) g.FillEllipse(brush,xx-3,yy-3,6,6);
            hits.Add((new RectangleF(xx-7,plot.Top,14,plot.Height),$"{list[i].Month} • {(i<=last?"Historical":"Forecast")}\nVolume: {list[i].Volume:N0} USD\nLoans: {list[i].Count:N1}",null));
        }
        int step=Math.Max(1,(int)Math.Ceiling(list.Count/(double)Math.Max(2,(int)(plot.Width/100))));
        for(int i=0;i<list.Count;i+=step) DrawText(g,list[i].Month,font,Color.SlateGray,new RectangleF(X(i)-42,plot.Bottom+8,84,25),StringAlignment.Center);
        if((list.Count-1)%step!=0) DrawText(g,list[^1].Month,font,Color.SlateGray,new RectangleF(plot.Right-76,plot.Bottom+8,90,25),StringAlignment.Center);
        DrawText(g,"Monthly volume • USD (hover for values)",font,Color.SlateGray,new RectangleF(18,62,Width-36,20));
    }
    private void DrawScenarios(Graphics g,Font font)
    {
        if(Scenarios.Count==0) { Empty(g,font,"No scenario available. Select at least two history months with loans."); return; }
        float gap=14, width=(Width-36-gap*(Scenarios.Count-1))/Scenarios.Count;
        double max=Math.Max(1,Scenarios.Max(x=>x.Volume));
        using var strong=new Font("Segoe UI",11,FontStyle.Bold);
        for(int i=0;i<Scenarios.Count;i++) {
            var s=Scenarios[i]; float x=18+i*(width+gap),top=48; var rect=new RectangleF(x,top,width,Height-top-14);
            Color color=s.Name=="Lower rate"?Teal:s.Name=="Higher rate"?Orange:Color.FromArgb(61,104,185);
            using var bg=new SolidBrush(Color.White); using var border=new Pen(Color.LightGray,1);
            g.FillRectangle(bg,rect); g.DrawRectangle(border,rect.X,rect.Y,rect.Width,rect.Height);
            DrawText(g,s.Name,font,color,new RectangleF(x+14,top+7,width-28,24));
            DrawText(g,$"Interest rate: {s.Rate:N2}%",font,Ink,new RectangleF(x+14,top+33,width-28,25));
            DrawText(g,$"Volume: {Compact(s.Volume)} USD",strong,Ink,new RectangleF(x+14,top+64,width-28,35));
            DrawText(g,$"{s.Count:N0} expected loans",font,Color.SlateGray,new RectangleF(x+14,top+104,width-28,25));
            DrawText(g,s.Name=="Unchanged rate"?"Baseline forecast":$"{s.ChangePct:+0.0;-0.0;0.0}% demand vs baseline",font,color,new RectangleF(x+14,top+133,width-28,25));
            if(rect.Height>=190) {
                using var track=new SolidBrush(Color.FromArgb(222,230,239)); using var bar=new SolidBrush(color);
                g.FillRectangle(track,x+14,rect.Bottom-24,width-28,8); g.FillRectangle(bar,x+14,rect.Bottom-24,(float)((width-28)*s.Volume/max),8);
            }
            hits.Add((rect,$"{s.Name}\nRate: {s.Rate:N2}%\nExpected volume: {s.Volume:N2} USD\nExpected loans: {s.Count:N1}\nDemand change: {s.ChangePct:+0.00;-0.00;0.00}%",null));
        }
    }
}
