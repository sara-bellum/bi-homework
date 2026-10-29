namespace Decision_support_loans_bank;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetDefaultFont(new System.Drawing.Font("Segoe UI", 9F));
        Application.Run(new Form1());
    }
}
