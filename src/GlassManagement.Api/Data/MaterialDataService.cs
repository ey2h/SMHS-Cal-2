using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;

namespace GlassManagement.Api.Data;

#region 数据模型

public class RegionData
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public double WindPressure50 { get; set; }
    public double WindPressure100 { get; set; }
    public int SeismicIntensity { get; set; }
    public double PeakAcceleration { get; set; }
    public double AlphaMax { get; set; }
    public double TemperatureDiff { get; set; }
    public double SnowPressure50 { get; set; }
    public double SnowPressure100 { get; set; }
}

public class GlassTypeData
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public double Thickness { get; set; }
    public double DesignStrength { get; set; }
    public double BendingStrength { get; set; }
    public double ElasticModulus { get; set; } = 72000;
    public double PoissonRatio { get; set; } = 0.2;
    public double Density { get; set; } = 2500;
    public double ThermalExpansion { get; set; } = 0.000008;
    public string Remark { get; set; } = "";
}

public class ProfileColumnData
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public double Ix { get; set; }
    public double Iy { get; set; }
    public double Wx1 { get; set; }
    public double Wx2 { get; set; }
    public double WallThickness { get; set; }
    public double Area { get; set; }
    public double StaticMoment { get; set; }
    public double ShearWebWidth { get; set; }
    public string Remark { get; set; } = "";
}

public class ProfileBeamData
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public double Ix { get; set; }
    public double Iy { get; set; }
    public double Wx1 { get; set; }
    public double Wx2 { get; set; }
    public double Wy1 { get; set; }
    public double Wy2 { get; set; }
    public double WallThickness { get; set; }
    public double Area { get; set; }
    public double StaticMomentSx { get; set; }
    public double StaticMomentSy { get; set; }
    public double WebWidthTx { get; set; }
    public double WebWidthTy { get; set; }
    public string Remark { get; set; } = "";
}

public class SteelPipeData
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public double Area { get; set; }
    public double Wx { get; set; }
    public double Wy { get; set; }
    public double Ix { get; set; }
    public double Iy { get; set; }
}

public class BoltData
{
    public int Id { get; set; }
    public double Diameter { get; set; }
    public double EffectiveDiameter { get; set; }
    public double ShearStress { get; set; }
    public double TensileStress { get; set; }
    public string Model { get; set; } = "";
    public string Material { get; set; } = "";
    public string Remark { get; set; } = "";
    public int Category { get; set; }
}

public class PanelMaterialData
{
    public int Id { get; set; }
    public string Category { get; set; } = "";
    public string Model { get; set; } = "";
    public double Thickness { get; set; }
    public double BendingStrength { get; set; }
    public double ShearStrength { get; set; }
    public string Remark { get; set; } = "";
}

public class CurtainWallWeightData
{
    public int Id { get; set; }
    public string Category { get; set; } = "";
    public double ColumnWeight { get; set; }
    public double BeamWeight { get; set; }
    public string Scope { get; set; } = "";
}

/// <summary>钢铝结合钢立柱</summary>
public class SteelAlumSteelColumnData
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public double Ix { get; set; }
    public double Iy { get; set; }
    public double Wx1 { get; set; }
    public double Wx2 { get; set; }
    public double WallThickness { get; set; }
    public double Area { get; set; }
    public double StaticMoment { get; set; }
    public double ShearWebWidth { get; set; }
    public string Remark { get; set; } = "";
}

/// <summary>钢铝结合铝立柱</summary>
public class SteelAlumAluColumnData
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public double Ix { get; set; }
    public double Iy { get; set; }
    public double Wx1 { get; set; }
    public double Wx2 { get; set; }
    public double WallThickness { get; set; }
    public double Area { get; set; }
    public double StaticMoment { get; set; }
    public double ShearWebWidth { get; set; }
    public string Remark { get; set; } = "";
}

/// <summary>钢铝结合钢横框</summary>
public class SteelAlumSteelBeamData
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public double Ix { get; set; }
    public double Iy { get; set; }
    public double Wx1 { get; set; }
    public double Wx2 { get; set; }
    public double Wy1 { get; set; }
    public double Wy2 { get; set; }
    public double WallThickness { get; set; }
    public double Area { get; set; }
    public double StaticMomentSx { get; set; }
    public double StaticMomentSy { get; set; }
    public double WebWidthTx { get; set; }
    public double WebWidthTy { get; set; }
    public string Remark { get; set; } = "";
}

/// <summary>钢铝结合铝横框</summary>
public class SteelAlumAluBeamData
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public double Ix { get; set; }
    public double Iy { get; set; }
    public double Wx1 { get; set; }
    public double Wx2 { get; set; }
    public double Wy1 { get; set; }
    public double Wy2 { get; set; }
    public double WallThickness { get; set; }
    public double Area { get; set; }
    public double StaticMomentSx { get; set; }
    public double StaticMomentSy { get; set; }
    public double WebWidthTx { get; set; }
    public double WebWidthTy { get; set; }
    public string Remark { get; set; } = "";
}

#endregion

/// <summary>材料参数数据库服务 - 支持完整CRUD操作</summary>
public class MaterialDataService
{
    private readonly string _dataDir;
    private readonly object _lock = new(); // 写操作锁

    public MaterialDataService(IWebHostEnvironment env)
    {
        _dataDir = Path.Combine(env.ContentRootPath, "Data");
        if (!Directory.Exists(_dataDir))
            _dataDir = env.ContentRootPath;
        Console.WriteLine($"[MaterialDataService] _dataDir = {_dataDir}");
        Console.WriteLine($"[MaterialDataService] Data dir exists = {Directory.Exists(_dataDir)}");
        if (Directory.Exists(_dataDir)) {
            Console.WriteLine($"[MaterialDataService] Files: {string.Join(", ", Directory.GetFiles(_dataDir, "*.db"))}");
        }
    }

    private string GetDbPath(string tableName)
    {
        var dbName = tableName switch
        {
            "Regions" => "SMHS_Region.db",
            "GlassTypes" => "SMHS_Glass.db",
            "ProfileColumns" or "ProfileBeams" or "SteelPipes"
                or "SteelAlumSteelColumns" or "SteelAlumAluColumns"
                or "SteelAlumSteelBeams" or "SteelAlumAluBeams" => "SMHS_Profile.db",
            "Bolts" => "SMHS_Bolt.db",
            "PanelMaterials" or "CurtainWallWeights" or "AnchorMaterials" => "SMHS_Material.db",
            _ => "SMHS_Material.db"
        };
        var path = Path.Combine(_dataDir, dbName);
        // 如果专用数据库不存在，尝试使用 params.db
        if (!File.Exists(path))
        {
            var paramsPath = Path.Combine(_dataDir, "params.db");
            if (File.Exists(paramsPath))
                path = paramsPath;
        }
        // 如果仍然不存在，返回原始路径（让错误信息更清晰）
        return path;
    }

    /// <summary>打开只读连接</summary>
    private SqliteConnection OpenRead(string dbPath)
    {
        var conn = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
        conn.Open();
        return conn;
    }

    /// <summary>打开读写连接</summary>
    private SqliteConnection OpenWrite(string dbPath)
    {
        var conn = new SqliteConnection($"Data Source={dbPath}");
        conn.Open();
        return conn;
    }

    // ========== 地区参数 ==========

    public List<RegionData> GetAllRegions(string? search = null)
    {
        using var conn = OpenRead(GetDbPath("Regions"));
        using var cmd = conn.CreateCommand();
        if (!string.IsNullOrWhiteSpace(search))
        {
            cmd.CommandText = "SELECT Id,Name,WindPressure50,WindPressure100,SeismicIntensity,PeakAcceleration,AlphaMax,TemperatureDiff,SnowPressure50,SnowPressure100 FROM Regions WHERE Name LIKE @s ORDER BY Id";
            cmd.Parameters.AddWithValue("@s", $"%{search}%");
        }
        else
        {
            cmd.CommandText = "SELECT Id,Name,WindPressure50,WindPressure100,SeismicIntensity,PeakAcceleration,AlphaMax,TemperatureDiff,SnowPressure50,SnowPressure100 FROM Regions ORDER BY Id";
        }
        var list = new List<RegionData>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new RegionData
            {
                Id = r.GetInt32(0), Name = r.GetString(1),
                WindPressure50 = r.GetDouble(2), WindPressure100 = r.GetDouble(3),
                SeismicIntensity = r.GetInt32(4), PeakAcceleration = r.GetDouble(5),
                AlphaMax = r.GetDouble(6), TemperatureDiff = r.GetDouble(7),
                SnowPressure50 = r.GetDouble(8), SnowPressure100 = r.GetDouble(9)
            });
        return list;
    }

    public RegionData? GetRegionById(int id)
    {
        using var conn = OpenRead(GetDbPath("Regions"));
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,Name,WindPressure50,WindPressure100,SeismicIntensity,PeakAcceleration,AlphaMax,TemperatureDiff,SnowPressure50,SnowPressure100 FROM Regions WHERE Id=@id";
        cmd.Parameters.AddWithValue("@id", id);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return new RegionData
        {
            Id = r.GetInt32(0), Name = r.GetString(1),
            WindPressure50 = r.GetDouble(2), WindPressure100 = r.GetDouble(3),
            SeismicIntensity = r.GetInt32(4), PeakAcceleration = r.GetDouble(5),
            AlphaMax = r.GetDouble(6), TemperatureDiff = r.GetDouble(7),
            SnowPressure50 = r.GetDouble(8), SnowPressure100 = r.GetDouble(9)
        };
    }

    public RegionData? GetRegionByName(string name)
    {
        using var conn = OpenRead(GetDbPath("Regions"));
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,Name,WindPressure50,WindPressure100,SeismicIntensity,PeakAcceleration,AlphaMax,TemperatureDiff,SnowPressure50,SnowPressure100 FROM Regions WHERE Name=@n";
        cmd.Parameters.AddWithValue("@n", name);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return new RegionData
        {
            Id = r.GetInt32(0), Name = r.GetString(1),
            WindPressure50 = r.GetDouble(2), WindPressure100 = r.GetDouble(3),
            SeismicIntensity = r.GetInt32(4), PeakAcceleration = r.GetDouble(5),
            AlphaMax = r.GetDouble(6), TemperatureDiff = r.GetDouble(7),
            SnowPressure50 = r.GetDouble(8), SnowPressure100 = r.GetDouble(9)
        };
    }

    public int AddRegion(RegionData region)
    {
        lock (_lock)
        {
            using var conn = OpenWrite(GetDbPath("Regions"));
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO Regions(Name,WindPressure50,WindPressure100,SeismicIntensity,PeakAcceleration,AlphaMax,TemperatureDiff,SnowPressure50,SnowPressure100) VALUES(@v1,@v2,@v3,@v4,@v5,@v6,@v7,@v8,@v9); SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@v1", region.Name);
            cmd.Parameters.AddWithValue("@v2", region.WindPressure50);
            cmd.Parameters.AddWithValue("@v3", region.WindPressure100);
            cmd.Parameters.AddWithValue("@v4", region.SeismicIntensity);
            cmd.Parameters.AddWithValue("@v5", region.PeakAcceleration);
            cmd.Parameters.AddWithValue("@v6", region.AlphaMax);
            cmd.Parameters.AddWithValue("@v7", region.TemperatureDiff);
            cmd.Parameters.AddWithValue("@v8", region.SnowPressure50);
            cmd.Parameters.AddWithValue("@v9", region.SnowPressure100);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
    }

    public bool UpdateRegion(RegionData region)
    {
        lock (_lock)
        {
            using var conn = OpenWrite(GetDbPath("Regions"));
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE Regions SET Name=@v1,WindPressure50=@v2,WindPressure100=@v3,SeismicIntensity=@v4,PeakAcceleration=@v5,AlphaMax=@v6,TemperatureDiff=@v7,SnowPressure50=@v8,SnowPressure100=@v9 WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", region.Id);
            cmd.Parameters.AddWithValue("@v1", region.Name);
            cmd.Parameters.AddWithValue("@v2", region.WindPressure50);
            cmd.Parameters.AddWithValue("@v3", region.WindPressure100);
            cmd.Parameters.AddWithValue("@v4", region.SeismicIntensity);
            cmd.Parameters.AddWithValue("@v5", region.PeakAcceleration);
            cmd.Parameters.AddWithValue("@v6", region.AlphaMax);
            cmd.Parameters.AddWithValue("@v7", region.TemperatureDiff);
            cmd.Parameters.AddWithValue("@v8", region.SnowPressure50);
            cmd.Parameters.AddWithValue("@v9", region.SnowPressure100);
            return cmd.ExecuteNonQuery() > 0;
        }
    }

    public bool DeleteRegion(int id)
    {
        lock (_lock)
        {
            using var conn = OpenWrite(GetDbPath("Regions"));
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM Regions WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", id);
            return cmd.ExecuteNonQuery() > 0;
        }
    }

    // ========== 玻璃类型 ==========

    public List<GlassTypeData> GetAllGlassTypes(string? category = null, string? search = null)
    {
        using var conn = OpenRead(GetDbPath("GlassTypes"));
        using var cmd = conn.CreateCommand();
        var where = new List<string>();
        if (!string.IsNullOrWhiteSpace(search))
        {
            where.Add("(Name LIKE @kw OR Code LIKE @kw)");
            cmd.Parameters.AddWithValue("@kw", $"%{search}%");
        }
        if (!string.IsNullOrWhiteSpace(category))
        {
            where.Add("Category = @cat");
            cmd.Parameters.AddWithValue("@cat", category);
        }
        cmd.CommandText = "SELECT Id,Code,Name,Category,Thickness,DesignStrength,BendingStrength,ElasticModulus,PoissonRatio,Density,ThermalExpansion,Remark FROM GlassTypes"
            + (where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : "")
            + " ORDER BY Category,Thickness";
        var list = new List<GlassTypeData>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new GlassTypeData
            {
                Id = r.GetInt32(0), Code = r.GetString(1), Name = r.GetString(2),
                Category = r.GetString(3), Thickness = r.GetDouble(4),
                DesignStrength = r.GetDouble(5), BendingStrength = r.GetDouble(6),
                ElasticModulus = r.GetDouble(7), PoissonRatio = r.GetDouble(8),
                Density = r.GetDouble(9), ThermalExpansion = r.GetDouble(10),
                Remark = r.IsDBNull(11) ? "" : r.GetString(11)
            });
        return list;
    }

    public GlassTypeData? GetGlassTypeById(int id)
    {
        using var conn = OpenRead(GetDbPath("GlassTypes"));
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,Code,Name,Category,Thickness,DesignStrength,BendingStrength,ElasticModulus,PoissonRatio,Density,ThermalExpansion,Remark FROM GlassTypes WHERE Id=@id";
        cmd.Parameters.AddWithValue("@id", id);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return new GlassTypeData
        {
            Id = r.GetInt32(0), Code = r.GetString(1), Name = r.GetString(2),
            Category = r.GetString(3), Thickness = r.GetDouble(4),
            DesignStrength = r.GetDouble(5), BendingStrength = r.GetDouble(6),
            ElasticModulus = r.GetDouble(7), PoissonRatio = r.GetDouble(8),
            Density = r.GetDouble(9), ThermalExpansion = r.GetDouble(10),
            Remark = r.IsDBNull(11) ? "" : r.GetString(11)
        };
    }

    public int AddGlassType(GlassTypeData g)
    {
        lock (_lock)
        {
            using var conn = OpenWrite(GetDbPath("GlassTypes"));
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO GlassTypes(Code,Name,Category,Thickness,DesignStrength,BendingStrength,ElasticModulus,PoissonRatio,Density,ThermalExpansion,Remark) VALUES(@v1,@v2,@v3,@v4,@v5,@v6,@v7,@v8,@v9,@v10,@v11); SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@v1", g.Code);
            cmd.Parameters.AddWithValue("@v2", g.Name);
            cmd.Parameters.AddWithValue("@v3", g.Category);
            cmd.Parameters.AddWithValue("@v4", g.Thickness);
            cmd.Parameters.AddWithValue("@v5", g.DesignStrength);
            cmd.Parameters.AddWithValue("@v6", g.BendingStrength);
            cmd.Parameters.AddWithValue("@v7", g.ElasticModulus);
            cmd.Parameters.AddWithValue("@v8", g.PoissonRatio);
            cmd.Parameters.AddWithValue("@v9", g.Density);
            cmd.Parameters.AddWithValue("@v10", g.ThermalExpansion);
            cmd.Parameters.AddWithValue("@v11", (object?)g.Remark ?? DBNull.Value);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
    }

    public bool UpdateGlassType(GlassTypeData g)
    {
        lock (_lock)
        {
            using var conn = OpenWrite(GetDbPath("GlassTypes"));
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE GlassTypes SET Code=@v1,Name=@v2,Category=@v3,Thickness=@v4,DesignStrength=@v5,BendingStrength=@v6,ElasticModulus=@v7,PoissonRatio=@v8,Density=@v9,ThermalExpansion=@v10,Remark=@v11 WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", g.Id);
            cmd.Parameters.AddWithValue("@v1", g.Code);
            cmd.Parameters.AddWithValue("@v2", g.Name);
            cmd.Parameters.AddWithValue("@v3", g.Category);
            cmd.Parameters.AddWithValue("@v4", g.Thickness);
            cmd.Parameters.AddWithValue("@v5", g.DesignStrength);
            cmd.Parameters.AddWithValue("@v6", g.BendingStrength);
            cmd.Parameters.AddWithValue("@v7", g.ElasticModulus);
            cmd.Parameters.AddWithValue("@v8", g.PoissonRatio);
            cmd.Parameters.AddWithValue("@v9", g.Density);
            cmd.Parameters.AddWithValue("@v10", g.ThermalExpansion);
            cmd.Parameters.AddWithValue("@v11", (object?)g.Remark ?? DBNull.Value);
            return cmd.ExecuteNonQuery() > 0;
        }
    }

    public bool DeleteGlassType(int id)
    {
        lock (_lock)
        {
            using var conn = OpenWrite(GetDbPath("GlassTypes"));
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM GlassTypes WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", id);
            return cmd.ExecuteNonQuery() > 0;
        }
    }

    public List<string> GetGlassCategories()
    {
        using var conn = OpenRead(GetDbPath("GlassTypes"));
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT Category FROM GlassTypes ORDER BY Category";
        var list = new List<string>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(r.GetString(0));
        return list;
    }

    // ========== 立柱型材 ==========

    public List<ProfileColumnData> GetAllProfileColumns(string? search = null)
    {
        using var conn = OpenRead(GetDbPath("ProfileColumns"));
        using var cmd = conn.CreateCommand();
        if (!string.IsNullOrWhiteSpace(search))
        {
            cmd.CommandText = "SELECT Id,Code,Ix,Iy,Wx1,Wx2,WallThickness,Area,StaticMoment,ShearWebWidth,Remark FROM ProfileColumns WHERE Code LIKE @kw ORDER BY Code";
            cmd.Parameters.AddWithValue("@kw", $"%{search}%");
        }
        else
            cmd.CommandText = "SELECT Id,Code,Ix,Iy,Wx1,Wx2,WallThickness,Area,StaticMoment,ShearWebWidth,Remark FROM ProfileColumns ORDER BY Code";
        var list = new List<ProfileColumnData>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new ProfileColumnData
            {
                Id = r.GetInt32(0), Code = r.GetString(1),
                Ix = r.GetDouble(2), Iy = r.GetDouble(3),
                Wx1 = r.GetDouble(4), Wx2 = r.GetDouble(5),
                WallThickness = r.GetDouble(6), Area = r.GetDouble(7),
                StaticMoment = r.GetDouble(8), ShearWebWidth = r.GetDouble(9),
                Remark = r.IsDBNull(10) ? "" : r.GetString(10)
            });
        return list;
    }

    public ProfileColumnData? GetProfileColumnById(int id)
    {
        using var conn = OpenRead(GetDbPath("ProfileColumns"));
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,Code,Ix,Iy,Wx1,Wx2,WallThickness,Area,StaticMoment,ShearWebWidth,Remark FROM ProfileColumns WHERE Id=@id";
        cmd.Parameters.AddWithValue("@id", id);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return new ProfileColumnData
        {
            Id = r.GetInt32(0), Code = r.GetString(1),
            Ix = r.GetDouble(2), Iy = r.GetDouble(3),
            Wx1 = r.GetDouble(4), Wx2 = r.GetDouble(5),
            WallThickness = r.GetDouble(6), Area = r.GetDouble(7),
            StaticMoment = r.GetDouble(8), ShearWebWidth = r.GetDouble(9),
            Remark = r.IsDBNull(10) ? "" : r.GetString(10)
        };
    }

    public int AddProfileColumn(ProfileColumnData p)
    {
        lock (_lock)
        {
            using var conn = OpenWrite(GetDbPath("ProfileColumns"));
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO ProfileColumns(Code,Ix,Iy,Wx1,Wx2,WallThickness,Area,StaticMoment,ShearWebWidth,Remark) VALUES(@v1,@v2,@v3,@v4,@v5,@v6,@v7,@v8,@v9,@v10); SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@v1", p.Code);
            cmd.Parameters.AddWithValue("@v2", p.Ix);
            cmd.Parameters.AddWithValue("@v3", p.Iy);
            cmd.Parameters.AddWithValue("@v4", p.Wx1);
            cmd.Parameters.AddWithValue("@v5", p.Wx2);
            cmd.Parameters.AddWithValue("@v6", p.WallThickness);
            cmd.Parameters.AddWithValue("@v7", p.Area);
            cmd.Parameters.AddWithValue("@v8", p.StaticMoment);
            cmd.Parameters.AddWithValue("@v9", p.ShearWebWidth);
            cmd.Parameters.AddWithValue("@v10", (object?)p.Remark ?? DBNull.Value);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
    }

    public bool UpdateProfileColumn(ProfileColumnData p)
    {
        lock (_lock)
        {
            using var conn = OpenWrite(GetDbPath("ProfileColumns"));
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE ProfileColumns SET Code=@v1,Ix=@v2,Iy=@v3,Wx1=@v4,Wx2=@v5,WallThickness=@v6,Area=@v7,StaticMoment=@v8,ShearWebWidth=@v9,Remark=@v10 WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", p.Id);
            cmd.Parameters.AddWithValue("@v1", p.Code);
            cmd.Parameters.AddWithValue("@v2", p.Ix);
            cmd.Parameters.AddWithValue("@v3", p.Iy);
            cmd.Parameters.AddWithValue("@v4", p.Wx1);
            cmd.Parameters.AddWithValue("@v5", p.Wx2);
            cmd.Parameters.AddWithValue("@v6", p.WallThickness);
            cmd.Parameters.AddWithValue("@v7", p.Area);
            cmd.Parameters.AddWithValue("@v8", p.StaticMoment);
            cmd.Parameters.AddWithValue("@v9", p.ShearWebWidth);
            cmd.Parameters.AddWithValue("@v10", (object?)p.Remark ?? DBNull.Value);
            return cmd.ExecuteNonQuery() > 0;
        }
    }

    public bool DeleteProfileColumn(int id)
    {
        lock (_lock)
        {
            using var conn = OpenWrite(GetDbPath("ProfileColumns"));
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM ProfileColumns WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", id);
            return cmd.ExecuteNonQuery() > 0;
        }
    }

    // ========== 横梁型材 ==========

    public List<ProfileBeamData> GetAllProfileBeams(string? search = null)
    {
        using var conn = OpenRead(GetDbPath("ProfileBeams"));
        using var cmd = conn.CreateCommand();
        if (!string.IsNullOrWhiteSpace(search))
        {
            cmd.CommandText = "SELECT Id,Code,Ix,Iy,Wx1,Wx2,Wy1,Wy2,WallThickness,Area,StaticMomentSx,StaticMomentSy,WebWidthTx,WebWidthTy,Remark FROM ProfileBeams WHERE Code LIKE @kw ORDER BY Code";
            cmd.Parameters.AddWithValue("@kw", $"%{search}%");
        }
        else
            cmd.CommandText = "SELECT Id,Code,Ix,Iy,Wx1,Wx2,Wy1,Wy2,WallThickness,Area,StaticMomentSx,StaticMomentSy,WebWidthTx,WebWidthTy,Remark FROM ProfileBeams ORDER BY Code";
        var list = new List<ProfileBeamData>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new ProfileBeamData
            {
                Id = r.GetInt32(0), Code = r.GetString(1),
                Ix = r.GetDouble(2), Iy = r.GetDouble(3),
                Wx1 = r.GetDouble(4), Wx2 = r.GetDouble(5),
                Wy1 = r.GetDouble(6), Wy2 = r.GetDouble(7),
                WallThickness = r.GetDouble(8),
                Area = r.GetDouble(9), StaticMomentSx = r.GetDouble(10),
                StaticMomentSy = r.GetDouble(11), WebWidthTx = r.GetDouble(12),
                WebWidthTy = r.GetDouble(13),
                Remark = r.IsDBNull(14) ? "" : r.GetString(14)
            });
        return list;
    }

    public ProfileBeamData? GetProfileBeamById(int id)
    {
        using var conn = OpenRead(GetDbPath("ProfileBeams"));
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,Code,Ix,Iy,Wx1,Wx2,Wy1,Wy2,WallThickness,Area,StaticMomentSx,StaticMomentSy,WebWidthTx,WebWidthTy,Remark FROM ProfileBeams WHERE Id=@id";
        cmd.Parameters.AddWithValue("@id", id);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return new ProfileBeamData
        {
            Id = r.GetInt32(0), Code = r.GetString(1),
            Ix = r.GetDouble(2), Iy = r.GetDouble(3),
            Wx1 = r.GetDouble(4), Wx2 = r.GetDouble(5),
            Wy1 = r.GetDouble(6), Wy2 = r.GetDouble(7),
            Area = r.GetDouble(8), StaticMomentSx = r.GetDouble(9),
            StaticMomentSy = r.GetDouble(10), WebWidthTx = r.GetDouble(11),
            WebWidthTy = r.GetDouble(12),
            Remark = r.IsDBNull(13) ? "" : r.GetString(13)
        };
    }

    public int AddProfileBeam(ProfileBeamData p)
    {
        lock (_lock)
        {
            using var conn = OpenWrite(GetDbPath("ProfileBeams"));
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO ProfileBeams(Code,Ix,Iy,Wx1,Wx2,Wy1,Wy2,WallThickness,Area,StaticMomentSx,StaticMomentSy,WebWidthTx,WebWidthTy,Remark) VALUES(@v1,@v2,@v3,@v4,@v5,@v6,@v7,@v7b,@v8,@v9,@v10,@v11,@v12,@v13); SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@v1", p.Code);
            cmd.Parameters.AddWithValue("@v2", p.Ix);
            cmd.Parameters.AddWithValue("@v3", p.Iy);
            cmd.Parameters.AddWithValue("@v4", p.Wx1);
            cmd.Parameters.AddWithValue("@v5", p.Wx2);
            cmd.Parameters.AddWithValue("@v6", p.Wy1);
            cmd.Parameters.AddWithValue("@v7", p.Wy2);
            cmd.Parameters.AddWithValue("@v7b", p.WallThickness);
            cmd.Parameters.AddWithValue("@v8", p.Area);
            cmd.Parameters.AddWithValue("@v9", p.StaticMomentSx);
            cmd.Parameters.AddWithValue("@v10", p.StaticMomentSy);
            cmd.Parameters.AddWithValue("@v11", p.WebWidthTx);
            cmd.Parameters.AddWithValue("@v12", p.WebWidthTy);
            cmd.Parameters.AddWithValue("@v13", (object?)p.Remark ?? DBNull.Value);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
    }

    public bool UpdateProfileBeam(ProfileBeamData p)
    {
        lock (_lock)
        {
            using var conn = OpenWrite(GetDbPath("ProfileBeams"));
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE ProfileBeams SET Code=@v1,Ix=@v2,Iy=@v3,Wx1=@v4,Wx2=@v5,Wy1=@v6,Wy2=@v7,WallThickness=@v7b,Area=@v8,StaticMomentSx=@v9,StaticMomentSy=@v10,WebWidthTx=@v11,WebWidthTy=@v12,Remark=@v13 WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", p.Id);
            cmd.Parameters.AddWithValue("@v1", p.Code);
            cmd.Parameters.AddWithValue("@v2", p.Ix);
            cmd.Parameters.AddWithValue("@v3", p.Iy);
            cmd.Parameters.AddWithValue("@v4", p.Wx1);
            cmd.Parameters.AddWithValue("@v5", p.Wx2);
            cmd.Parameters.AddWithValue("@v6", p.Wy1);
            cmd.Parameters.AddWithValue("@v7", p.Wy2);
            cmd.Parameters.AddWithValue("@v7b", p.WallThickness);
            cmd.Parameters.AddWithValue("@v8", p.Area);
            cmd.Parameters.AddWithValue("@v9", p.StaticMomentSx);
            cmd.Parameters.AddWithValue("@v10", p.StaticMomentSy);
            cmd.Parameters.AddWithValue("@v11", p.WebWidthTx);
            cmd.Parameters.AddWithValue("@v12", p.WebWidthTy);
            cmd.Parameters.AddWithValue("@v13", (object?)p.Remark ?? DBNull.Value);
            return cmd.ExecuteNonQuery() > 0;
        }
    }

    public bool DeleteProfileBeam(int id)
    {
        lock (_lock)
        {
            using var conn = OpenWrite(GetDbPath("ProfileBeams"));
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM ProfileBeams WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", id);
            return cmd.ExecuteNonQuery() > 0;
        }
    }

    // ========== 钢管 ==========

    public List<SteelPipeData> GetAllSteelPipes(string? search = null)
    {
        using var conn = OpenRead(GetDbPath("SteelPipes"));
        using var cmd = conn.CreateCommand();
        if (!string.IsNullOrWhiteSpace(search))
        {
            cmd.CommandText = "SELECT Id,Code,Area,Wx,Wy,Ix,Iy FROM SteelPipes WHERE Code LIKE @kw ORDER BY Code";
            cmd.Parameters.AddWithValue("@kw", $"%{search}%");
        }
        else
            cmd.CommandText = "SELECT Id,Code,Area,Wx,Wy,Ix,Iy FROM SteelPipes ORDER BY Code";
        var list = new List<SteelPipeData>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new SteelPipeData
            {
                Id = r.GetInt32(0), Code = r.GetString(1),
                Area = r.GetDouble(2), Wx = r.GetDouble(3),
                Wy = r.GetDouble(4), Ix = r.GetDouble(5), Iy = r.GetDouble(6)
            });
        return list;
    }

    public SteelPipeData? GetSteelPipeById(int id)
    {
        using var conn = OpenRead(GetDbPath("SteelPipes"));
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,Code,Area,Wx,Wy,Ix,Iy FROM SteelPipes WHERE Id=@id";
        cmd.Parameters.AddWithValue("@id", id);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return new SteelPipeData
        {
            Id = r.GetInt32(0), Code = r.GetString(1),
            Area = r.GetDouble(2), Wx = r.GetDouble(3),
            Wy = r.GetDouble(4), Ix = r.GetDouble(5), Iy = r.GetDouble(6)
        };
    }

    public int AddSteelPipe(SteelPipeData p)
    {
        lock (_lock)
        {
            using var conn = OpenWrite(GetDbPath("SteelPipes"));
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO SteelPipes(Code,Area,Wx,Wy,Ix,Iy) VALUES(@v1,@v2,@v3,@v4,@v5,@v6); SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@v1", p.Code);
            cmd.Parameters.AddWithValue("@v2", p.Area);
            cmd.Parameters.AddWithValue("@v3", p.Wx);
            cmd.Parameters.AddWithValue("@v4", p.Wy);
            cmd.Parameters.AddWithValue("@v5", p.Ix);
            cmd.Parameters.AddWithValue("@v6", p.Iy);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
    }

    public bool UpdateSteelPipe(SteelPipeData p)
    {
        lock (_lock)
        {
            using var conn = OpenWrite(GetDbPath("SteelPipes"));
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE SteelPipes SET Code=@v1,Area=@v2,Wx=@v3,Wy=@v4,Ix=@v5,Iy=@v6 WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", p.Id);
            cmd.Parameters.AddWithValue("@v1", p.Code);
            cmd.Parameters.AddWithValue("@v2", p.Area);
            cmd.Parameters.AddWithValue("@v3", p.Wx);
            cmd.Parameters.AddWithValue("@v4", p.Wy);
            cmd.Parameters.AddWithValue("@v5", p.Ix);
            cmd.Parameters.AddWithValue("@v6", p.Iy);
            return cmd.ExecuteNonQuery() > 0;
        }
    }

    public bool DeleteSteelPipe(int id)
    {
        lock (_lock)
        {
            using var conn = OpenWrite(GetDbPath("SteelPipes"));
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM SteelPipes WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", id);
            return cmd.ExecuteNonQuery() > 0;
        }
    }

    // ========== 螺栓 ==========

    public List<BoltData> GetAllBolts(string? search = null, int? category = null)
    {
        using var conn = OpenRead(GetDbPath("Bolts"));
        using var cmd = conn.CreateCommand();
        var where = new List<string>();
        if (!string.IsNullOrWhiteSpace(search))
        {
            where.Add("(Model LIKE @kw OR Material LIKE @kw)");
            cmd.Parameters.AddWithValue("@kw", $"%{search}%");
        }
        if (category.HasValue)
        {
            where.Add("Category = @cat");
            cmd.Parameters.AddWithValue("@cat", category.Value);
        }
        cmd.CommandText = "SELECT Id,Diameter,EffectiveDiameter,ShearStress,TensileStress,Model,Material,Remark,Category FROM Bolts"
            + (where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : "")
            + " ORDER BY Category,Diameter";
        var list = new List<BoltData>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new BoltData
            {
                Id = r.GetInt32(0), Diameter = r.GetDouble(1),
                EffectiveDiameter = r.GetDouble(2), ShearStress = r.GetDouble(3),
                TensileStress = r.GetDouble(4), Model = r.GetString(5),
                Material = r.GetString(6), Remark = r.IsDBNull(7) ? "" : r.GetString(7),
                Category = r.GetInt32(8)
            });
        return list;
    }

    public BoltData? GetBoltById(int id)
    {
        using var conn = OpenRead(GetDbPath("Bolts"));
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,Diameter,EffectiveDiameter,ShearStress,TensileStress,Model,Material,Remark,Category FROM Bolts WHERE Id=@id";
        cmd.Parameters.AddWithValue("@id", id);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return new BoltData
        {
            Id = r.GetInt32(0), Diameter = r.GetDouble(1),
            EffectiveDiameter = r.GetDouble(2), ShearStress = r.GetDouble(3),
            TensileStress = r.GetDouble(4), Model = r.GetString(5),
            Material = r.GetString(6), Remark = r.IsDBNull(7) ? "" : r.GetString(7),
            Category = r.GetInt32(8)
        };
    }

    public int AddBolt(BoltData b)
    {
        lock (_lock)
        {
            using var conn = OpenWrite(GetDbPath("Bolts"));
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO Bolts(Diameter,EffectiveDiameter,ShearStress,TensileStress,Model,Material,Remark,Category) VALUES(@v1,@v2,@v3,@v4,@v5,@v6,@v7,@v8); SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@v1", b.Diameter);
            cmd.Parameters.AddWithValue("@v2", b.EffectiveDiameter);
            cmd.Parameters.AddWithValue("@v3", b.ShearStress);
            cmd.Parameters.AddWithValue("@v4", b.TensileStress);
            cmd.Parameters.AddWithValue("@v5", b.Model);
            cmd.Parameters.AddWithValue("@v6", b.Material);
            cmd.Parameters.AddWithValue("@v7", (object?)b.Remark ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@v8", b.Category);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
    }

    public bool UpdateBolt(BoltData b)
    {
        lock (_lock)
        {
            using var conn = OpenWrite(GetDbPath("Bolts"));
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE Bolts SET Diameter=@v1,EffectiveDiameter=@v2,ShearStress=@v3,TensileStress=@v4,Model=@v5,Material=@v6,Remark=@v7,Category=@v8 WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", b.Id);
            cmd.Parameters.AddWithValue("@v1", b.Diameter);
            cmd.Parameters.AddWithValue("@v2", b.EffectiveDiameter);
            cmd.Parameters.AddWithValue("@v3", b.ShearStress);
            cmd.Parameters.AddWithValue("@v4", b.TensileStress);
            cmd.Parameters.AddWithValue("@v5", b.Model);
            cmd.Parameters.AddWithValue("@v6", b.Material);
            cmd.Parameters.AddWithValue("@v7", (object?)b.Remark ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@v8", b.Category);
            return cmd.ExecuteNonQuery() > 0;
        }
    }

    public bool DeleteBolt(int id)
    {
        lock (_lock)
        {
            using var conn = OpenWrite(GetDbPath("Bolts"));
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM Bolts WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", id);
            return cmd.ExecuteNonQuery() > 0;
        }
    }

    // ========== 面板材料 ==========

    public List<PanelMaterialData> GetAllPanelMaterials(string? category = null, string? search = null)
    {
        using var conn = OpenRead(GetDbPath("PanelMaterials"));
        using var cmd = conn.CreateCommand();
        var where = new List<string>();
        if (!string.IsNullOrWhiteSpace(search))
        {
            where.Add("(Model LIKE @kw OR Category LIKE @kw)");
            cmd.Parameters.AddWithValue("@kw", $"%{search}%");
        }
        if (!string.IsNullOrWhiteSpace(category))
        {
            where.Add("Category = @cat");
            cmd.Parameters.AddWithValue("@cat", category);
        }
        cmd.CommandText = "SELECT Id,Category,Model,Thickness,BendingStrength,ShearStrength,Remark FROM PanelMaterials"
            + (where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : "")
            + " ORDER BY Category,Thickness";
        var list = new List<PanelMaterialData>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new PanelMaterialData
            {
                Id = r.GetInt32(0), Category = r.GetString(1),
                Model = r.GetString(2), Thickness = r.GetDouble(3),
                BendingStrength = r.GetDouble(4), ShearStrength = r.GetDouble(5),
                Remark = r.IsDBNull(6) ? "" : r.GetString(6)
            });
        return list;
    }

    public PanelMaterialData? GetPanelMaterialById(int id)
    {
        using var conn = OpenRead(GetDbPath("PanelMaterials"));
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,Category,Model,Thickness,BendingStrength,ShearStrength,Remark FROM PanelMaterials WHERE Id=@id";
        cmd.Parameters.AddWithValue("@id", id);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return new PanelMaterialData
        {
            Id = r.GetInt32(0), Category = r.GetString(1),
            Model = r.GetString(2), Thickness = r.GetDouble(3),
            BendingStrength = r.GetDouble(4), ShearStrength = r.GetDouble(5),
            Remark = r.IsDBNull(6) ? "" : r.GetString(6)
        };
    }

    public int AddPanelMaterial(PanelMaterialData p)
    {
        lock (_lock)
        {
            using var conn = OpenWrite(GetDbPath("PanelMaterials"));
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO PanelMaterials(Category,Model,Thickness,BendingStrength,ShearStrength,Remark) VALUES(@v1,@v2,@v3,@v4,@v5,@v6); SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@v1", p.Category);
            cmd.Parameters.AddWithValue("@v2", p.Model);
            cmd.Parameters.AddWithValue("@v3", p.Thickness);
            cmd.Parameters.AddWithValue("@v4", p.BendingStrength);
            cmd.Parameters.AddWithValue("@v5", p.ShearStrength);
            cmd.Parameters.AddWithValue("@v6", (object?)p.Remark ?? DBNull.Value);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
    }

    public bool UpdatePanelMaterial(PanelMaterialData p)
    {
        lock (_lock)
        {
            using var conn = OpenWrite(GetDbPath("PanelMaterials"));
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE PanelMaterials SET Category=@v1,Model=@v2,Thickness=@v3,BendingStrength=@v4,ShearStrength=@v5,Remark=@v6 WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", p.Id);
            cmd.Parameters.AddWithValue("@v1", p.Category);
            cmd.Parameters.AddWithValue("@v2", p.Model);
            cmd.Parameters.AddWithValue("@v3", p.Thickness);
            cmd.Parameters.AddWithValue("@v4", p.BendingStrength);
            cmd.Parameters.AddWithValue("@v5", p.ShearStrength);
            cmd.Parameters.AddWithValue("@v6", (object?)p.Remark ?? DBNull.Value);
            return cmd.ExecuteNonQuery() > 0;
        }
    }

    public bool DeletePanelMaterial(int id)
    {
        lock (_lock)
        {
            using var conn = OpenWrite(GetDbPath("PanelMaterials"));
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM PanelMaterials WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", id);
            return cmd.ExecuteNonQuery() > 0;
        }
    }

    public List<string> GetPanelCategories()
    {
        using var conn = OpenRead(GetDbPath("PanelMaterials"));
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT Category FROM PanelMaterials ORDER BY Category";
        var list = new List<string>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(r.GetString(0));
        return list;
    }

    // ========== 幕墙自重 ==========

    public List<CurtainWallWeightData> GetAllCurtainWallWeights(string? search = null)
    {
        using var conn = OpenRead(GetDbPath("CurtainWallWeights"));
        using var cmd = conn.CreateCommand();
        if (!string.IsNullOrWhiteSpace(search))
        {
            cmd.CommandText = "SELECT Id,Category,ColumnWeight,BeamWeight,Scope FROM CurtainWallWeights WHERE Category LIKE @kw ORDER BY Id";
            cmd.Parameters.AddWithValue("@kw", $"%{search}%");
        }
        else
            cmd.CommandText = "SELECT Id,Category,ColumnWeight,BeamWeight,Scope FROM CurtainWallWeights ORDER BY Id";
        var list = new List<CurtainWallWeightData>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new CurtainWallWeightData
            {
                Id = r.GetInt32(0), Category = r.GetString(1),
                ColumnWeight = r.GetDouble(2), BeamWeight = r.GetDouble(3),
                Scope = r.IsDBNull(4) ? "" : r.GetString(4)
            });
        return list;
    }

    public CurtainWallWeightData? GetCurtainWallWeightById(int id)
    {
        using var conn = OpenRead(GetDbPath("CurtainWallWeights"));
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,Category,ColumnWeight,BeamWeight,Scope FROM CurtainWallWeights WHERE Id=@id";
        cmd.Parameters.AddWithValue("@id", id);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return new CurtainWallWeightData
        {
            Id = r.GetInt32(0), Category = r.GetString(1),
            ColumnWeight = r.GetDouble(2), BeamWeight = r.GetDouble(3),
            Scope = r.IsDBNull(4) ? "" : r.GetString(4)
        };
    }

    public int AddCurtainWallWeight(CurtainWallWeightData w)
    {
        lock (_lock)
        {
            using var conn = OpenWrite(GetDbPath("CurtainWallWeights"));
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO CurtainWallWeights(Category,ColumnWeight,BeamWeight,Scope) VALUES(@v1,@v2,@v3,@v4); SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@v1", w.Category);
            cmd.Parameters.AddWithValue("@v2", w.ColumnWeight);
            cmd.Parameters.AddWithValue("@v3", w.BeamWeight);
            cmd.Parameters.AddWithValue("@v4", (object?)w.Scope ?? DBNull.Value);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
    }

    public bool UpdateCurtainWallWeight(CurtainWallWeightData w)
    {
        lock (_lock)
        {
            using var conn = OpenWrite(GetDbPath("CurtainWallWeights"));
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE CurtainWallWeights SET Category=@v1,ColumnWeight=@v2,BeamWeight=@v3,Scope=@v4 WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", w.Id);
            cmd.Parameters.AddWithValue("@v1", w.Category);
            cmd.Parameters.AddWithValue("@v2", w.ColumnWeight);
            cmd.Parameters.AddWithValue("@v3", w.BeamWeight);
            cmd.Parameters.AddWithValue("@v4", (object?)w.Scope ?? DBNull.Value);
            return cmd.ExecuteNonQuery() > 0;
        }
    }

    public bool DeleteCurtainWallWeight(int id)
    {
        lock (_lock)
        {
            using var conn = OpenWrite(GetDbPath("CurtainWallWeights"));
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM CurtainWallWeights WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", id);
            return cmd.ExecuteNonQuery() > 0;
        }
    }

    // ========== 导入导出 ==========

    /// <summary>从CSV导入数据到指定表</summary>
    public int ImportFromCsv(string tableName, string csvContent)
    {
        var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2) return 0;

        var headers = ParseCsvLine(lines[0]);
        // 排除Id列
        var cols = headers.Where(h => !h.Equals("Id", StringComparison.OrdinalIgnoreCase)).ToList();
        if (cols.Count == 0) return 0;

        int count = 0;
        lock (_lock)
        {
            using var conn = OpenWrite(GetDbPath(tableName));
            using var tx = conn.BeginTransaction();
            try
            {
                for (int i = 1; i < lines.Length; i++)
                {
                    var values = ParseCsvLine(lines[i]);
                    if (values.Length < cols.Count) continue;

                    using var cmd = conn.CreateCommand();
                    cmd.Transaction = tx;
                    var paramNames = cols.Select((c, idx) => $"@p{idx}").ToList();
                    cmd.CommandText = $"INSERT INTO {tableName}({string.Join(",", cols)}) VALUES({string.Join(",", paramNames)})";

                    // 找到非Id列在原始headers中的索引
                    var colIndices = cols.Select(c => Array.FindIndex(headers, h => h.Equals(c, StringComparison.OrdinalIgnoreCase))).ToList();
                    for (int j = 0; j < cols.Count; j++)
                    {
                        var idx = colIndices[j];
                        var val = idx >= 0 && idx < values.Length ? values[idx] : "";
                        cmd.Parameters.AddWithValue($"@p{j}", string.IsNullOrEmpty(val) ? DBNull.Value : val);
                    }
                    cmd.ExecuteNonQuery();
                    count++;
                }
                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }
        return count;
    }

    /// <summary>导出指定表为CSV字符串</summary>
    public string ExportToCsv(string tableName)
    {
        // 验证表名防止SQL注入
        var validTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "Regions", "GlassTypes", "ProfileColumns", "ProfileBeams", "SteelPipes", "Bolts", "PanelMaterials", "CurtainWallWeights" };
        if (!validTables.Contains(tableName))
            throw new ArgumentException($"无效的表名: {tableName}");

        using var conn = OpenRead(GetDbPath(tableName));
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT * FROM {tableName}";
        using var r = cmd.ExecuteReader();

        var sb = new StringBuilder();
        // 表头
        var colNames = new List<string>();
        for (int i = 0; i < r.FieldCount; i++)
            colNames.Add(r.GetName(i));
        sb.AppendLine(string.Join(",", colNames));

        // 数据行
        while (r.Read())
        {
            var vals = new List<string>();
            for (int i = 0; i < r.FieldCount; i++)
            {
                if (r.IsDBNull(i))
                    vals.Add("");
                else
                {
                    var v = r.GetValue(i)?.ToString() ?? "";
                    // 包含逗号或引号的值需要加引号
                    if (v.Contains(',') || v.Contains('"') || v.Contains('\n'))
                        vals.Add($"\"{v.Replace("\"", "\"\"")}\"");
                    else
                        vals.Add(v);
                }
            }
            sb.AppendLine(string.Join(",", vals));
        }
        return sb.ToString();
    }

    /// <summary>备份数据库</summary>
    // ========== 钢铝结合钢立柱 ==========
    public List<SteelAlumSteelColumnData> GetAllSteelAlumSteelColumns(string? search = null)
    {
        using var conn = OpenRead(GetDbPath("SteelAlumSteelColumns"));
        var sql = "SELECT * FROM SteelAlumSteelColumns" + (string.IsNullOrEmpty(search) ? "" : " WHERE Code LIKE @s");
        using var cmd = conn.CreateCommand(); cmd.CommandText = sql;
        if (!string.IsNullOrEmpty(search)) cmd.Parameters.AddWithValue("@s", $"%{search}%");
        var list = new List<SteelAlumSteelColumnData>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(new SteelAlumSteelColumnData { Id=r.GetInt32(0), Code=r.GetString(1), Ix=r.GetDouble(2), Iy=r.GetDouble(3), Wx1=r.GetDouble(4), Wx2=r.GetDouble(5), WallThickness=r.GetDouble(6), Area=r.GetDouble(7), StaticMoment=r.GetDouble(8), ShearWebWidth=r.GetDouble(9), Remark=r.IsDBNull(10)?"":r.GetString(10) });
        return list;
    }

    // ========== 钢铝结合铝立柱 ==========
    public List<SteelAlumAluColumnData> GetAllSteelAlumAluColumns(string? search = null)
    {
        using var conn = OpenRead(GetDbPath("SteelAlumAluColumns"));
        var sql = "SELECT * FROM SteelAlumAluColumns" + (string.IsNullOrEmpty(search) ? "" : " WHERE Code LIKE @s");
        using var cmd = conn.CreateCommand(); cmd.CommandText = sql;
        if (!string.IsNullOrEmpty(search)) cmd.Parameters.AddWithValue("@s", $"%{search}%");
        var list = new List<SteelAlumAluColumnData>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(new SteelAlumAluColumnData { Id=r.GetInt32(0), Code=r.GetString(1), Ix=r.GetDouble(2), Iy=r.GetDouble(3), Wx1=r.GetDouble(4), Wx2=r.GetDouble(5), WallThickness=r.GetDouble(6), Area=r.GetDouble(7), StaticMoment=r.GetDouble(8), ShearWebWidth=r.GetDouble(9), Remark=r.IsDBNull(10)?"":r.GetString(10) });
        return list;
    }

    // ========== 钢铝结合钢横框 ==========
    public List<SteelAlumSteelBeamData> GetAllSteelAlumSteelBeams(string? search = null)
    {
        using var conn = OpenRead(GetDbPath("SteelAlumSteelBeams"));
        var sql = "SELECT * FROM SteelAlumSteelBeams" + (string.IsNullOrEmpty(search) ? "" : " WHERE Code LIKE @s");
        using var cmd = conn.CreateCommand(); cmd.CommandText = sql;
        if (!string.IsNullOrEmpty(search)) cmd.Parameters.AddWithValue("@s", $"%{search}%");
        var list = new List<SteelAlumSteelBeamData>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(new SteelAlumSteelBeamData { Id=r.GetInt32(0), Code=r.GetString(1), Ix=r.GetDouble(2), Iy=r.GetDouble(3), Wx1=r.GetDouble(4), Wx2=r.GetDouble(5), Wy1=r.GetDouble(6), Wy2=r.GetDouble(7), WallThickness=r.GetDouble(8), Area=r.GetDouble(9), StaticMomentSx=r.GetDouble(10), StaticMomentSy=r.GetDouble(11), WebWidthTx=r.GetDouble(12), WebWidthTy=r.GetDouble(13), Remark=r.IsDBNull(14)?"":r.GetString(14) });
        return list;
    }

    // ========== 钢铝结合铝横框 ==========
    public List<SteelAlumAluBeamData> GetAllSteelAlumAluBeams(string? search = null)
    {
        using var conn = OpenRead(GetDbPath("SteelAlumAluBeams"));
        var sql = "SELECT * FROM SteelAlumAluBeams" + (string.IsNullOrEmpty(search) ? "" : " WHERE Code LIKE @s");
        using var cmd = conn.CreateCommand(); cmd.CommandText = sql;
        if (!string.IsNullOrEmpty(search)) cmd.Parameters.AddWithValue("@s", $"%{search}%");
        var list = new List<SteelAlumAluBeamData>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(new SteelAlumAluBeamData { Id=r.GetInt32(0), Code=r.GetString(1), Ix=r.GetDouble(2), Iy=r.GetDouble(3), Wx1=r.GetDouble(4), Wx2=r.GetDouble(5), Wy1=r.GetDouble(6), Wy2=r.GetDouble(7), WallThickness=r.GetDouble(8), Area=r.GetDouble(9), StaticMomentSx=r.GetDouble(10), StaticMomentSy=r.GetDouble(11), WebWidthTx=r.GetDouble(12), WebWidthTy=r.GetDouble(13), Remark=r.IsDBNull(14)?"":r.GetString(14) });
        return list;
    }

    public void BackupDatabase(string backupPath)
    {
        lock (_lock)
        {
            // Backup all SMHS db files
            var dbFiles = new[] { "SMHS_Region.db", "SMHS_Glass.db", "SMHS_Profile.db", "SMHS_Bolt.db", "SMHS_Material.db" };
            var backupDir = Path.GetDirectoryName(backupPath) ?? _dataDir;
            foreach (var dbFile in dbFiles)
            {
                var src = Path.Combine(_dataDir, dbFile);
                if (File.Exists(src))
                    File.Copy(src, Path.Combine(backupDir, dbFile), true);
            }
            // Also backup params.db if it exists
            var paramsDb = Path.Combine(_dataDir, "params.db");
            if (File.Exists(paramsDb))
                File.Copy(paramsDb, backupPath, true);
        }
    }

    // ========== 数据库统计 ==========

    /// <summary>获取数据库统计（调试用）</summary>
    public Dictionary<string, int> GetStats()
    {
        var stats = new Dictionary<string, int>();
        string[] tables = { "Regions", "GlassTypes", "ProfileColumns", "ProfileBeams", "SteelPipes", "Bolts", "PanelMaterials", "CurtainWallWeights", "SteelAlumSteelColumns", "SteelAlumAluColumns", "SteelAlumSteelBeams", "SteelAlumAluBeams" };
        foreach (var t in tables)
        {
            try
            {
                var dbPath = GetDbPath(t);
                Console.WriteLine($"[DEBUG] GetStats: {t} -> {dbPath}, exists={File.Exists(dbPath)}");
                using var conn = OpenRead(dbPath);
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"SELECT COUNT(*) FROM {t}";
                stats[t] = Convert.ToInt32(cmd.ExecuteScalar());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] GetStats error for {t}: {ex.Message}");
                stats[t] = 0;
            }
        }
        return stats;
    }

    // ========== 辅助方法 ==========

    private static string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var current = new StringBuilder();
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                    inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString().Trim());
                current.Clear();
            }
            else if (c != '\r')
                current.Append(c);
        }
        result.Add(current.ToString().Trim());
        return result.ToArray();
    }
}
