namespace Decision_support_loans_bank;

public partial class Form1 : Form
{
    private readonly DataGridView grid = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
    };

    public Form1()
    {
        InitializeComponent();
        Text = "Test conexiune - ponderea tipurilor de credit";
        Width = 900;
        Height = 500;
        Controls.Add(grid);
        Load += (s, e) => IncarcaDate();
    }

    private void IncarcaDate()
    {
        try
        {
            grid.DataSource = Db.Query("SELECT * FROM v_PondereTipuri");
        }
        catch (Exception ex)
        {
            MessageBox.Show("Eroare la citirea bazei: " + ex.Message, "Eroare",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
