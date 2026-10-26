using System.Data;
using Microsoft.Data.Sqlite;

namespace Decision_support_loans_bank;

public static class Db
{
    private static readonly string Cale = Path.Combine(AppContext.BaseDirectory, "credite_banca.db");

    // Ruleaza un SELECT cu parametri si intoarce rezultatul ca tabel (pentru grid-uri si grafice)
    public static DataTable Query(string sql, params (string Nume, object? Valoare)[] parametri)
    {
        if (!File.Exists(Cale))
            throw new FileNotFoundException("Nu gasesc baza de date. Verifica 'Copy to Output Directory' la credite_banca.db.", Cale);

        using var con = new SqliteConnection($"Data Source={Cale};Mode=ReadOnly");
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (nume, valoare) in parametri)
            cmd.Parameters.AddWithValue(nume, valoare ?? DBNull.Value);

        using var r = cmd.ExecuteReader();
        var randuri = new List<object?[]>();
        while (r.Read())
        {
            var rand = new object?[r.FieldCount];
            for (int i = 0; i < r.FieldCount; i++)
                rand[i] = r.IsDBNull(i) ? null : r.GetValue(i);
            randuri.Add(rand);
        }

        // Stabileste tipul fiecarei coloane dupa valori (numar intreg, zecimal sau text)
        var dt = new DataTable();
        for (int i = 0; i < r.FieldCount; i++)
        {
            var valori = randuri.Select(x => x[i]).Where(v => v != null).ToList();
            Type tip = valori.Count == 0 ? typeof(string)
                     : valori.All(v => v is long) ? typeof(long)
                     : valori.All(v => v is long || v is double) ? typeof(double)
                     : typeof(string);
            dt.Columns.Add(r.GetName(i), tip);
        }
        foreach (var rand in randuri)
        {
            var rd = dt.NewRow();
            for (int i = 0; i < rand.Length; i++)
                rd[i] = rand[i] == null ? DBNull.Value : Convert.ChangeType(rand[i], dt.Columns[i].DataType);
            dt.Rows.Add(rd);
        }
        return dt;
    }
}
