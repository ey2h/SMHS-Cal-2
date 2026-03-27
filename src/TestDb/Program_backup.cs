using Microsoft.Data.Sqlite;

var dbPath = Path.Combine(
    "D:", "Code", "Glass", "src", "GlassManagement.Api", "Data", "SMHS_Profile.db");

Console.WriteLine($"Profile DB: {dbPath}");
if (!File.Exists(dbPath))
{
    Console.WriteLine("Database not found.");
    return;
}

var profiles = LoadProfiles(dbPath);
Console.WriteLine($"Loaded profiles: {profiles.Count}");
if (profiles.Count == 0)
{
    Console.WriteLine("No profile rows found in ProfileColumns.");
    return;
}

var scenarios = BuildScenarios();
var overallPass = 0;
var overallFail = 0;

for (var model = 1; model <= 6; model++)
{
    CaseResult? passCase = null;
    CaseResult? failCase = null;

    foreach (var s in scenarios)
    {
        foreach (var p in profiles)
        {
            var r = Evaluate(model, p, s);
            if (r.OverallPass && passCase is null) passCase = r;
            if (!r.OverallPass && failCase is null) failCase = r;
            if (passCase is not null && failCase is not null) break;
        }

        if (passCase is not null && failCase is not null) break;
    }

    Console.WriteLine();
    Console.WriteLine($"=== 受力模型 {model} ===");

    if (passCase is not null)
    {
        overallPass++;
        PrintCase("PASS", passCase);
    }
    else
    {
        Console.WriteLine("PASS: 未找到通过样例");
    }

    if (failCase is not null)
    {
        overallFail++;
        PrintCase("FAIL", failCase);
    }
    else
    {
        Console.WriteLine("FAIL: 未找到不通过样例");
    }
}

Console.WriteLine();
Console.WriteLine($"Summary: models with PASS sample = {overallPass}/6, models with FAIL sample = {overallFail}/6");

static List<Scenario> BuildScenarios()
{
    var list = new List<Scenario>();
    var windKPa = new[] { 0.8, 1.2, 1.6, 2.0, 2.6, 3.2, 4.0 };
    var spanMm = new[] { 3000.0, 3600.0, 4500.0, 6000.0 };
    var widthMm = new[] { 1000.0, 1200.0, 1500.0 };

    foreach (var wk in windKPa)
    foreach (var l in spanMm)
    foreach (var b in widthMm)
        list.Add(new Scenario(wk, l, b));

    return list;
}

static void PrintCase(string label, CaseResult c)
{
    Console.WriteLine(
        $"{label}: 型材={c.Profile.Code}, wk={c.Scenario.WindKPa:F2}kPa, L={c.Scenario.SpanMm:F0}mm, B={c.Scenario.WidthMm:F0}mm, " +
        $"σ={c.BendingStress:F2}/{c.AllowableStress:F2}MPa, " +
        $"τ={c.ShearStress:F2}/{c.AllowableShear:F2}MPa, " +
        $"δ={c.Deflection:F2}/{c.DeflectionLimit:F2}mm, " +
        $"overall={(c.OverallPass ? "PASS" : "FAIL")}");
}

static List<ProfileColumn> LoadProfiles(string dbPath)
{
    using var conn = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
    conn.Open();

    using var schemaCmd = conn.CreateCommand();
    schemaCmd.CommandText = "SELECT * FROM ProfileColumns LIMIT 1";

    using var schemaReader = schemaCmd.ExecuteReader();
    var names = new List<string>();
    for (var i = 0; i < schemaReader.FieldCount; i++)
        names.Add(schemaReader.GetName(i));

    Console.WriteLine("ProfileColumns columns: " + string.Join(", ", names));

    var codeIdx = FindIndex(names, "ProfileCode", "Code", "code", "型号", "代号", "名称");
    var ixIdx = FindIndex(names, "MomentOfInertiaX", "Ix", "ix", "惯性矩x", "ix(cm4)");
    var wx1Idx = FindIndex(names, "SectionModulusX", "Wx1", "wx1", "抵抗矩", "wx");
    var twIdx = FindIndex(names, "WallThickness", "wallthickness", "tw", "壁厚");
    var sxIdx = FindIndex(names, "StaticMoment", "ShearArea", "staticmoment", "sheararea", "sx", "面积矩");

    // 回退到常见列序号(Id,Code,Ix,Iy,Wx1,Wx2,WallThickness,Area,StaticMoment,...)
    codeIdx = codeIdx >= 0 ? codeIdx : 1;
    ixIdx = ixIdx >= 0 ? ixIdx : 2;
    wx1Idx = wx1Idx >= 0 ? wx1Idx : 4;
    twIdx = twIdx >= 0 ? twIdx : 6;
    sxIdx = sxIdx >= 0 ? sxIdx : 8;

    using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT * FROM ProfileColumns";

    using var reader = cmd.ExecuteReader();
    var list = new List<ProfileColumn>();
    while (reader.Read())
    {
        var code = SafeString(reader, codeIdx, $"Profile#{list.Count + 1}");
        var ix = SafeDouble(reader, ixIdx, 0);
        var wx1 = SafeDouble(reader, wx1Idx, 0);
        var tw = SafeDouble(reader, twIdx, 3.0);
        var sx = SafeDouble(reader, sxIdx, wx1 * 0.5);

        if (ix <= 0 || wx1 <= 0) continue;
        list.Add(new ProfileColumn(code, ix, wx1, tw, sx));
    }

    return list;
}

static int FindIndex(List<string> cols, params string[] keys)
{
    for (var i = 0; i < cols.Count; i++)
    {
        var c = cols[i];
        foreach (var k in keys)
        {
            if (c.Equals(k, StringComparison.OrdinalIgnoreCase) ||
                c.Contains(k, StringComparison.OrdinalIgnoreCase))
                return i;
        }
    }
    return -1;
}

static string SafeString(SqliteDataReader reader, int idx, string fallback)
{
    if (idx < 0 || idx >= reader.FieldCount || reader.IsDBNull(idx)) return fallback;
    return reader.GetValue(idx)?.ToString() ?? fallback;
}

static double SafeDouble(SqliteDataReader reader, int idx, double fallback)
{
    if (idx < 0 || idx >= reader.FieldCount || reader.IsDBNull(idx)) return fallback;

    var v = reader.GetValue(idx);
    return v switch
    {
        double d => d,
        float f => f,
        long l => l,
        int i => i,
        decimal m => (double)m,
        _ => double.TryParse(v.ToString(), out var p) ? p : fallback
    };
}

static CaseResult Evaluate(int model, ProfileColumn p, Scenario s)
{
    const double e = 70000.0;
    const double allowStress = 85.0; // 6063-T5
    const double allowShear = 50.0;  // 6063-T5

    var q = s.WindKPa * s.WidthMm / 1000.0; // N/mm

    var momentCoeff = model switch
    {
        1 or 2 => 1.0 / 8.0,
        3 => 1.0 / 10.0,
        4 => 1.0 / 8.0,
        5 => 9.0 / 128.0,
        6 => 1.0 / 12.0,
        _ => 1.0 / 8.0
    };

    var deflCoeff = model switch
    {
        1 or 2 => 5.0 / 384.0,
        3 => 1.0 / 145.0,
        4 => 5.0 / 384.0,
        5 => 1.0 / 185.0,
        6 => 1.0 / 384.0,
        _ => 5.0 / 384.0
    };

    var m = momentCoeff * q * s.SpanMm * s.SpanMm;
    var ixMm4 = p.IxCm4 * 1e4;
    var wx1Mm3 = p.Wx1Cm3 * 1e3;
    var sxMm3 = p.SxCm3 * 1e3;

    var bendingStress = m / wx1Mm3;
    var deflectionLimit = s.SpanMm / 180.0;
    var deflection = deflCoeff * q * Math.Pow(s.SpanMm, 4) / (e * ixMm4);

    var v = q * s.SpanMm / 2.0;
    var shearStress = v * sxMm3 / (ixMm4 * (p.TwMm > 0 ? p.TwMm : 3.0));

    var bendingPass = bendingStress <= allowStress;
    var deflPass = deflection <= deflectionLimit;
    var shearPass = shearStress <= allowShear;

    return new CaseResult(
        p,
        s,
        allowStress,
        allowShear,
        bendingStress,
        shearStress,
        deflection,
        deflectionLimit,
        bendingPass && deflPass && shearPass);
}

record ProfileColumn(string Code, double IxCm4, double Wx1Cm3, double TwMm, double SxCm3);
record Scenario(double WindKPa, double SpanMm, double WidthMm);
record CaseResult(
    ProfileColumn Profile,
    Scenario Scenario,
    double AllowableStress,
    double AllowableShear,
    double BendingStress,
    double ShearStress,
    double Deflection,
    double DeflectionLimit,
    bool OverallPass);
