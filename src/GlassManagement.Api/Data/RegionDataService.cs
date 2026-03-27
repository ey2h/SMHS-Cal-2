using Microsoft.Data.Sqlite;

namespace GlassManagement.Api.Data;

// RegionData 模型已在 MaterialDataService.cs 中定义

/// <summary>地区参数数据服务 - 从SQLite加载到内存（兼容旧接口）</summary>
public class RegionDataService
{
    private readonly List<RegionData> _regions = new();
    private readonly Dictionary<string, RegionData> _regionMap = new(StringComparer.OrdinalIgnoreCase);

    public RegionDataService(IWebHostEnvironment env)
    {
        var dbPath = Path.Combine(env.ContentRootPath, "Data", "SMHS_Region.db");
        if (!File.Exists(dbPath))
            dbPath = Path.Combine(env.ContentRootPath, "Data", "params.db");
        if (!File.Exists(dbPath))
            dbPath = Path.Combine(env.ContentRootPath, "params.db");

        if (File.Exists(dbPath))
            LoadFromSqlite(dbPath);
    }

    private void LoadFromSqlite(string dbPath)
    {
        using var conn = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,Name,WindPressure50,WindPressure100,SeismicIntensity,PeakAcceleration,AlphaMax,TemperatureDiff,SnowPressure50,SnowPressure100 FROM Regions";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            try
            {
                var r = new RegionData
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    WindPressure50 = reader.GetDouble(2),
                    WindPressure100 = reader.GetDouble(3),
                    SeismicIntensity = reader.GetInt32(4),
                    PeakAcceleration = reader.GetDouble(5),
                    AlphaMax = reader.GetDouble(6),
                    TemperatureDiff = reader.GetDouble(7),
                    SnowPressure50 = reader.GetDouble(8),
                    SnowPressure100 = reader.GetDouble(9),
                };
                _regions.Add(r);
                _regionMap[r.Name] = r;
            }
            catch { }
        }
    }

    public IReadOnlyList<RegionData> GetAll() => _regions;
    public RegionData? GetByName(string name) =>
        _regionMap.TryGetValue(name, out var r) ? r : null;
    public List<RegionData> Search(string keyword) =>
        string.IsNullOrWhiteSpace(keyword) ? _regions.ToList() :
        _regions.Where(r => r.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();
    public int Count => _regions.Count;
}
