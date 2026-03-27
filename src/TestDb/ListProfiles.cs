using System;
using Microsoft.Data.Sqlite;

var conn = new SqliteConnection("Data Source=D:/Code/Glass/src/GlassManagement.Api/Data/SMHS_Profile.db");
conn.Open();

var cmd = conn.CreateCommand();
cmd.CommandText = "SELECT ProfileCode, Specification, SectionArea, MomentOfInertiaX, MomentOfInertiaY, SectionModulusX FROM ProfileColumns ORDER BY ProfileCode";

var r = cmd.ExecuteReader();
Console.WriteLine($"{"型号",-25} {"规格",-20} {"面积(cm²)",-12} {"Ix(cm⁴)",-15} {"Iy(cm⁴)",-15} {"Wx(cm³)"}");
Console.WriteLine(new string('=', 100));

var count = 0;
while(r.Read()) {
    Console.WriteLine($"{r.GetString(0),-25} {r.GetString(1),-20} {r.GetDouble(2),-12:F2} {r.GetDouble(3),-15:F0} {r.GetDouble(4),-15:F0} {r.GetDouble(5):F0}");
    count++;
}
Console.WriteLine(new string('=', 100));
Console.WriteLine($"总计: {count} 种型材");
