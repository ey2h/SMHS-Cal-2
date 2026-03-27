using Microsoft.Data.Sqlite;
var conn = new SqliteConnection("Data Source=D:/Code/Glass/src/GlassManagement.Api/Data/SMHS_Profile.db");
conn.Open();
var cmd = conn.CreateCommand();
cmd.CommandText = "SELECT ProfileCode, Specification, SectionArea, MomentOfInertiaX FROM ProfileColumns ORDER BY ProfileCode";
var r = cmd.ExecuteReader();
Console.WriteLine($"{"型号",-30} {"规格",-20} {"面积",-10} {"Ix"}");
Console.WriteLine(new string('-', 80));
while(r.Read()) {
    Console.WriteLine($"{r.GetString(0),-30} {r.GetString(1),-20} {r.GetDouble(2),-10:F2} {r.GetDouble(3):F0}");
}
