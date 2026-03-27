using System.Data;
using System.Data.OleDb;
using Microsoft.Data.Sqlite;

string mdbPath = @"D:\Code\Glass\参数库.mdb";
string outputDir = @"D:\Code\Glass\src\GlassManagement.Api\bin\Debug\net10.0\Data";
Directory.CreateDirectory(outputDir);

string connStr = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={mdbPath};";

using var mdbConn = new OleDbConnection(connStr);
mdbConn.Open();

Console.WriteLine("开始转换...");

// 1. 地区参数 -> SMHS_Region.db
CreateRegionDb(mdbConn, Path.Combine(outputDir, "SMHS_Region.db"));
Console.WriteLine("✓ SMHS_Region.db");

// 2. 螺栓 -> SMHS_Bolt.db  
CreateBoltDb(mdbConn, Path.Combine(outputDir, "SMHS_Bolt.db"));
Console.WriteLine("✓ SMHS_Bolt.db");

// 3. 型材 -> SMHS_Profile.db
CreateProfileDb(mdbConn, Path.Combine(outputDir, "SMHS_Profile.db"));
Console.WriteLine("✓ SMHS_Profile.db");

// 4. 面板材料 -> SMHS_Material.db
CreateMaterialDb(mdbConn, Path.Combine(outputDir, "SMHS_Material.db"));
Console.WriteLine("✓ SMHS_Material.db");

// 5. 玻璃类型 -> SMHS_Glass.db (如果存在)
CreateGlassDb(mdbConn, Path.Combine(outputDir, "SMHS_Glass.db"));
Console.WriteLine("✓ SMHS_Glass.db");

Console.WriteLine("\n转换完成!");

void CreateRegionDb(OleDbConnection src, string dest)
{
    using var dst = new SqliteConnection($"Data Source={dest}");
    dst.Open();
    
    var cmd = dst.CreateCommand();
    cmd.CommandText = @"
        DROP TABLE IF EXISTS Regions;
        CREATE TABLE Regions (
            Id INTEGER PRIMARY KEY,
            Name TEXT NOT NULL,
            WindPressure50 REAL,
            WindPressure100 REAL,
            SeismicIntensity TEXT,
            PeakAcceleration REAL,
            AlphaMax REAL,
            TemperatureDiff REAL,
            SnowPressure50 REAL,
            SnowPressure100 REAL
        )";
    cmd.ExecuteNonQuery();
    
    var srcCmd = src.CreateCommand();
    srcCmd.CommandText = "SELECT * FROM 地区参数";
    using var reader = srcCmd.ExecuteReader();
    
    int id = 1;
    while (reader.Read())
    {
        var insertCmd = dst.CreateCommand();
        insertCmd.CommandText = $@"
            INSERT INTO Regions (Id, Name, WindPressure50, WindPressure100, SeismicIntensity, 
                PeakAcceleration, AlphaMax, TemperatureDiff, SnowPressure50, SnowPressure100)
            VALUES ({id}, @Name, @W50, @W100, @Seismic, @Peak, @Alpha, @Temp, @S50, @S100)";
        
        insertCmd.Parameters.AddWithValue("@Name", reader["地区名称"]?.ToString() ?? "");
        insertCmd.Parameters.AddWithValue("@W50", reader["50年基本风压"] ?? 0);
        insertCmd.Parameters.AddWithValue("@W100", reader["100年基本风压"] ?? 0);
        insertCmd.Parameters.AddWithValue("@Seismic", reader["抗震烈度"]?.ToString() ?? "");
        insertCmd.Parameters.AddWithValue("@Peak", reader["基本地震动峰值加速度"] ?? 0);
        insertCmd.Parameters.AddWithValue("@Alpha", reader["αmax"] ?? 0);
        insertCmd.Parameters.AddWithValue("@Temp", reader["极限温差"] ?? 0);
        insertCmd.Parameters.AddWithValue("@S50", reader["50年基本雪压"] ?? 0);
        insertCmd.Parameters.AddWithValue("@S100", reader["100年基本雪压"] ?? 0);
        insertCmd.ExecuteNonQuery();
        id++;
    }
    Console.WriteLine($"  导入 {id-1} 条地区记录");
}

void CreateBoltDb(OleDbConnection src, string dest)
{
    using var dst = new SqliteConnection($"Data Source={dest}");
    dst.Open();
    
    var cmd = dst.CreateCommand();
    cmd.CommandText = @"
        DROP TABLE IF EXISTS Bolts;
        CREATE TABLE Bolts (
            Id INTEGER PRIMARY KEY,
            Category TEXT NOT NULL,
            Model TEXT,
            Material TEXT,
            Diameter REAL,
            EffectiveDiameter REAL,
            TensionStress REAL,
            ShearStress REAL,
            Remark TEXT
        )";
    cmd.ExecuteNonQuery();
    
    int id = 1;
    for (int i = 1; i <= 7; i++)
    {
        var srcCmd = src.CreateCommand();
        srcCmd.CommandText = $"SELECT * FROM 螺栓{i}";
        using var reader = srcCmd.ExecuteReader();
        
        while (reader.Read())
        {
            var insertCmd = dst.CreateCommand();
            insertCmd.CommandText = $@"
                INSERT INTO Bolts (Id, Category, Model, Material, Diameter, EffectiveDiameter, 
                    TensionStress, ShearStress, Remark)
                VALUES ({id}, @Cat, @Model, @Mat, @Dia, @EDia, @Tension, @Shear, @Remark)";
            
            insertCmd.Parameters.AddWithValue($"@Cat", $"螺栓{i}");
            insertCmd.Parameters.AddWithValue("@Model", GetField(reader, "型号"));
            insertCmd.Parameters.AddWithValue("@Mat", GetField(reader, "材质"));
            insertCmd.Parameters.AddWithValue("@Dia", GetField(reader, "公称直径"));
            insertCmd.Parameters.AddWithValue("@EDia", GetField(reader, "有效直径"));
            insertCmd.Parameters.AddWithValue("@Tension", GetField(reader, "拉应力"));
            insertCmd.Parameters.AddWithValue("@Shear", GetField(reader, "剪应力"));
            insertCmd.Parameters.AddWithValue("@Remark", GetField(reader, "备注"));
            insertCmd.ExecuteNonQuery();
            id++;
        }
    }
    Console.WriteLine($"  导入 {id-1} 条螺栓记录");
}

void CreateProfileDb(OleDbConnection src, string dest)
{
    using var dst = new SqliteConnection($"Data Source={dest}");
    dst.Open();
    
    var cmd = dst.CreateCommand();
    cmd.CommandText = @"
        DROP TABLE IF EXISTS ProfileColumns;
        DROP TABLE IF EXISTS ProfileBeams;
        CREATE TABLE ProfileColumns (
            Id INTEGER PRIMARY KEY,
            ProfileCode TEXT NOT NULL,
            Specification TEXT,
            Usage TEXT,
            SectionArea REAL,
            MomentOfInertiaX REAL,
            MomentOfInertiaY REAL,
            SectionModulusX REAL,
            SectionModulusY REAL,
            RadiusOfGyrationX REAL,
            RadiusOfGyrationY REAL,
            ShearArea REAL,
            Remark TEXT
        );
        
        CREATE TABLE ProfileBeams (
            Id INTEGER PRIMARY KEY,
            ProfileCode TEXT NOT NULL,
            Specification TEXT,
            Usage TEXT,
            SectionArea REAL,
            MomentOfInertiaX REAL,
            MomentOfInertiaY REAL,
            SectionModulusX REAL,
            SectionModulusY REAL,
            RadiusOfGyrationX REAL,
            RadiusOfGyrationY REAL,
            ShearAreaX REAL,
            ShearAreaY REAL,
            Remark TEXT
        )";
    cmd.ExecuteNonQuery();
    
    int id = 1;
    
    // 立柱 - use index instead of name
    var srcCmd = src.CreateCommand();
    srcCmd.CommandText = "SELECT * FROM 立柱";
    using (var reader = srcCmd.ExecuteReader())
    {
        Console.WriteLine($"  立柱表字段数: {reader.FieldCount}");
        while (reader.Read())
        {
            try {
                var insertCmd = dst.CreateCommand();
                insertCmd.CommandText = $@"
                    INSERT INTO ProfileColumns (Id, ProfileCode, Specification, Usage, SectionArea, 
                        MomentOfInertiaX, MomentOfInertiaY, SectionModulusX, SectionModulusY, 
                        RadiusOfGyrationX, RadiusOfGyrationY, ShearArea, Remark)
                    VALUES ({id}, @Code, @Spec, @Usage, @Area, @Ix, @Iy, @Wx1, @Wx2, @Rx, @Ry, @Shear, @Remark)";
                
                insertCmd.Parameters.AddWithValue("@Code", GetField(reader, "型材代号"));
                insertCmd.Parameters.AddWithValue("@Spec", "");
                insertCmd.Parameters.AddWithValue("@Usage", "立柱");
                insertCmd.Parameters.AddWithValue("@Area", GetField(reader, "截面积"));
                insertCmd.Parameters.AddWithValue("@Ix", GetField(reader, "Ix惯性矩"));
                insertCmd.Parameters.AddWithValue("@Iy", GetField(reader, "Iy惯性矩"));
                insertCmd.Parameters.AddWithValue("@Wx1", GetField(reader, "Wx1抵抗矩"));
                insertCmd.Parameters.AddWithValue("@Wx2", GetField(reader, "Wx2抵抗矩"));
                insertCmd.Parameters.AddWithValue("@Rx", 0);
                insertCmd.Parameters.AddWithValue("@Ry", 0);
                insertCmd.Parameters.AddWithValue("@Shear", GetField(reader, "受剪腹板总宽度"));
                insertCmd.Parameters.AddWithValue("@Remark", GetField(reader, "备注"));
                insertCmd.ExecuteNonQuery();
                id++;
            } catch (Exception ex) {
                Console.WriteLine($"  错误: {ex.Message}");
                break;
            }
        }
    }
    Console.WriteLine($"  导入 {id-1} 条立柱记录");
    
    int beamId = id;
    
    // 横框
    srcCmd = src.CreateCommand();
    srcCmd.CommandText = "SELECT * FROM 横框";
    using (var reader = srcCmd.ExecuteReader())
    {
        Console.WriteLine($"  横框表字段数: {reader.FieldCount}");
        while (reader.Read())
        {
            try {
                var insertCmd = dst.CreateCommand();
                insertCmd.CommandText = $@"
                    INSERT INTO ProfileBeams (Id, ProfileCode, Specification, Usage, SectionArea, 
                        MomentOfInertiaX, MomentOfInertiaY, SectionModulusX, SectionModulusY, 
                        RadiusOfGyrationX, RadiusOfGyrationY, ShearAreaX, ShearAreaY, Remark)
                    VALUES ({beamId}, @Code, @Spec, @Usage, @Area, @Ix, @Iy, @Wx1, @Wx2, @Rx, @Ry, @ShearX, @ShearY, @Remark)";
                
                insertCmd.Parameters.AddWithValue("@Code", GetField(reader, "型材代号"));
                insertCmd.Parameters.AddWithValue("@Spec", "");
                insertCmd.Parameters.AddWithValue("@Usage", "横框");
                insertCmd.Parameters.AddWithValue("@Area", GetField(reader, "截面积"));
                insertCmd.Parameters.AddWithValue("@Ix", GetField(reader, "Ix惯性矩"));
                insertCmd.Parameters.AddWithValue("@Iy", GetField(reader, "Iy惯性矩"));
                insertCmd.Parameters.AddWithValue("@Wx1", GetField(reader, "Wx1抵抗矩"));
                insertCmd.Parameters.AddWithValue("@Wx2", GetField(reader, "Wx2抵抗矩"));
                insertCmd.Parameters.AddWithValue("@Rx", 0);
                insertCmd.Parameters.AddWithValue("@Ry", 0);
                insertCmd.Parameters.AddWithValue("@ShearX", GetField(reader, "腹板总宽度tx"));
                insertCmd.Parameters.AddWithValue("@ShearY", GetField(reader, "腹板总宽度ty"));
                insertCmd.Parameters.AddWithValue("@Remark", GetField(reader, "备注"));
                insertCmd.ExecuteNonQuery();
                beamId++;
            } catch (Exception ex) {
                Console.WriteLine($"  错误: {ex.Message}");
                break;
            }
        }
    }
    Console.WriteLine($"  导入 {beamId - id} 条横框记录");
}

void CreateMaterialDb(OleDbConnection src, string dest)
{
    using var dst = new SqliteConnection($"Data Source={dest}");
    dst.Open();
    
    var cmd = dst.CreateCommand();
    cmd.CommandText = @"
        DROP TABLE IF EXISTS PanelMaterials;
        DROP TABLE IF EXISTS CurtainWallWeights;
        DROP TABLE IF EXISTS AnchorMaterials;
        CREATE TABLE PanelMaterials (
            Id INTEGER PRIMARY KEY,
            Category TEXT NOT NULL,
            Model TEXT,
            Thickness REAL,
            BendingStrength REAL,
            ShearStrength REAL,
            Density REAL,
            ElasticModulus REAL,
            ThermalExpansion REAL,
            PoissonRatio REAL,
            Remark TEXT
        );
        
        CREATE TABLE CurtainWallWeights (
            Id INTEGER PRIMARY KEY,
            Category TEXT NOT NULL,
            ColumnWeight REAL,
            BeamWeight REAL,
            ApplicationRange TEXT,
            Remark TEXT
        );
        
        CREATE TABLE AnchorMaterials (
            Id INTEGER PRIMARY KEY,
            Material TEXT,
            Model TEXT,
            Diameter REAL,
            EffectiveArea REAL,
            TensionStrength REAL,
            ShearStrength REAL,
            Fyk REAL,
            Fstk REAL,
            FudT REAL,
            FudV REAL,
            Remark TEXT
        )";
    cmd.ExecuteNonQuery();
    
    int id = 1;
    
    // 铝单板
    var srcCmd = src.CreateCommand();
    srcCmd.CommandText = "SELECT * FROM 铝单板";
    using (var reader = srcCmd.ExecuteReader())
    {
        while (reader.Read())
        {
            var insertCmd = dst.CreateCommand();
            insertCmd.CommandText = $@"
                INSERT INTO PanelMaterials (Id, Category, Model, Thickness, BendingStrength, 
                    ShearStrength, Remark)
                VALUES ({id}, '铝单板', @Model, @Thickness, @Bend, @Shear, @Remark)";
            
            insertCmd.Parameters.AddWithValue("@Model", GetField(reader, "型号"));
            insertCmd.Parameters.AddWithValue("@Thickness", GetField(reader, "厚度"));
            insertCmd.Parameters.AddWithValue("@Bend", GetField(reader, "抗弯强度"));
            insertCmd.Parameters.AddWithValue("@Shear", GetField(reader, "抗剪强度"));
            insertCmd.Parameters.AddWithValue("@Remark", GetField(reader, "备注"));
            insertCmd.ExecuteNonQuery();
            id++;
        }
    }
    Console.WriteLine($"  导入 {id-1} 条铝单板记录");
    
    // 后锚固材料
    srcCmd = src.CreateCommand();
    srcCmd.CommandText = "SELECT * FROM 后锚固材料参数";
    using (var reader = srcCmd.ExecuteReader())
    {
        while (reader.Read())
        {
            var insertCmd = dst.CreateCommand();
            insertCmd.CommandText = $@"
                INSERT INTO AnchorMaterials (Id, Material, Model, Diameter, EffectiveArea, 
                    TensionStrength, ShearStrength, Fyk, Fstk, FudT, FudV, Remark)
                VALUES ({id}, @Mat, @Model, @Dia, @Area, @Tension, @Shear, @Fyk, @Fstk, @FudT, @FudV, @Remark)";
            
            insertCmd.Parameters.AddWithValue("@Mat", GetField(reader, "材料"));
            insertCmd.Parameters.AddWithValue("@Model", GetField(reader, "代号"));
            insertCmd.Parameters.AddWithValue("@Dia", GetField(reader, "螺杆直径d"));
            insertCmd.Parameters.AddWithValue("@Area", GetField(reader, "有效截面积As"));
            insertCmd.Parameters.AddWithValue("@Tension", 0);
            insertCmd.Parameters.AddWithValue("@Shear", 0);
            insertCmd.Parameters.AddWithValue("@Fyk", GetField(reader, "fyk，JGJ145规范用"));
            insertCmd.Parameters.AddWithValue("@Fstk", GetField(reader, "fstk，JGJ145规范用"));
            insertCmd.Parameters.AddWithValue("@FudT", GetField(reader, "fud,t，GB50367规范用"));
            insertCmd.Parameters.AddWithValue("@FudV", GetField(reader, "fud,v，GB50367规范用"));
            insertCmd.Parameters.AddWithValue("@Remark", GetField(reader, "备注"));
            insertCmd.ExecuteNonQuery();
            id++;
        }
    }
    Console.WriteLine($"  导入 {id-1} 条锚固材料记录");
    
    // 幕墙自重
    srcCmd = src.CreateCommand();
    srcCmd.CommandText = "SELECT * FROM 幕墙自重参数";
    using (var reader = srcCmd.ExecuteReader())
    {
        while (reader.Read())
        {
            var insertCmd = dst.CreateCommand();
            insertCmd.CommandText = $@"
                INSERT INTO CurtainWallWeights (Id, Category, ColumnWeight, BeamWeight, 
                    ApplicationRange, Remark)
                VALUES ({id}, @Cat, @Col, @Beam, @Range, @Remark)";
            
            insertCmd.Parameters.AddWithValue("@Cat", GetField(reader, "幕墙类别"));
            insertCmd.Parameters.AddWithValue("@Col", GetField(reader, "立柱及主体自重"));
            insertCmd.Parameters.AddWithValue("@Beam", GetField(reader, "计算横梁时自重"));
            insertCmd.Parameters.AddWithValue("@Range", GetField(reader, "软件初始默认值适用范围"));
            insertCmd.Parameters.AddWithValue("@Remark", "");
            insertCmd.ExecuteNonQuery();
            id++;
        }
    }
    Console.WriteLine($"  导入 {id-1} 条幕墙自重记录");
}

void CreateGlassDb(OleDbConnection src, string dest)
{
    using var dst = new SqliteConnection($"Data Source={dest}");
    dst.Open();
    
    var cmd = dst.CreateCommand();
    cmd.CommandText = @"
        DROP TABLE IF EXISTS GlassTypes;
        CREATE TABLE GlassTypes (
            Id INTEGER PRIMARY KEY,
            Code TEXT,
            Name TEXT NOT NULL,
            Category TEXT,
            Thickness REAL,
            DesignStrength REAL,
            BendingStrength REAL,
            ElasticModulus REAL,
            PoissonRatio REAL,
            Density REAL,
            ThermalExpansion REAL,
            Remark TEXT
        )";
    cmd.ExecuteNonQuery();
    Console.WriteLine("  创建空玻璃类型表");
}

object GetField(OleDbDataReader reader, string fieldName)
{
    try
    {
        var val = reader[fieldName];
        if (val == DBNull.Value || val == null) return 0;
        return val;
    }
    catch
    {
        return 0;
    }
}
