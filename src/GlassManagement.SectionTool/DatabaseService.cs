using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;

namespace GlassManagement.SectionTool;

public class DatabaseService
{
    private static readonly Dictionary<string, string> CategoryTableMap = new()
    {
        ["立柱"] = "立柱",
        ["横框"] = "横框",
        ["钢铝结合铝立柱"] = "钢铝结合铝立柱",
        ["钢铝结合铝横框"] = "钢铝结合铝横框",
        ["钢铝结合钢立柱"] = "钢铝结合钢立柱",
        ["钢铝结合钢横框"] = "钢铝结合钢横框",
        ["立梃一(门窗用)"] = "立梃一",
        ["立梃二(门窗用,含钢芯)"] = "立梃二",
        ["横梃一(门窗用)"] = "横梃一",
        ["横梃二(门窗用,含钢芯)"] = "横梃二",
        ["单元立柱一"] = "单元立柱一",
        ["单元立柱二"] = "单元立柱二",
        ["单元横框一"] = "单元横框一",
        ["单元横框二"] = "单元横框二",
        ["单元横框三(中横框)"] = "单元横框三",
        ["U型玻璃"] = "U型玻璃",
        ["百叶叶片"] = "百叶叶片",
        ["采光顶副龙骨"] = "采光顶副龙骨",
        ["采光顶主龙骨"] = "采光顶主龙骨",
        ["吊顶副龙骨"] = "吊顶副龙骨",
        ["吊顶主龙骨"] = "吊顶主龙骨",
        ["钢管点玻龙骨"] = "钢管点玻龙骨",
        ["栏杆横龙骨"] = "栏杆横龙骨",
        ["栏杆竖龙骨"] = "栏杆竖龙骨",
        ["雨篷龙骨"] = "雨篷龙骨",
    };

    private static string GetTableName(string category)
        => CategoryTableMap.TryGetValue(category, out var t) ? t : category;

    private static string GetConnectionString(string dbPath)
        => new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();

    private void EnsureTable(SqliteConnection conn, string tableName)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"CREATE TABLE IF NOT EXISTS [{tableName}] (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            名称 TEXT NOT NULL,
            Ix REAL,
            Iy REAL,
            Wx1 REAL,
            Wx2 REAL,
            Wy1 REAL,
            Wy2 REAL,
            Sx REAL,
            Sy REAL,
            A REAL,
            壁厚 REAL,
            腹板宽度 REAL
        )";
        cmd.ExecuteNonQuery();
    }

    public void ImportProfile(string dbPath, string category, ProfileRecord record)
    {
        var tableName = GetTableName(category);
        using var conn = new SqliteConnection(GetConnectionString(dbPath));
        conn.Open();
        EnsureTable(conn, tableName);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"INSERT INTO [{tableName}] (名称, Ix, Iy, Wx1, Wx2, Wy1, Wy2, Sx, Sy, A, 壁厚, 腹板宽度)
            VALUES ($name, $ix, $iy, $wx1, $wx2, $wy1, $wy2, $sx, $sy, $a, $wall, $web)";
        cmd.Parameters.AddWithValue("$name", record.名称);
        cmd.Parameters.AddWithValue("$ix", record.Ix);
        cmd.Parameters.AddWithValue("$iy", record.Iy);
        cmd.Parameters.AddWithValue("$wx1", record.Wx1);
        cmd.Parameters.AddWithValue("$wx2", record.Wx2);
        cmd.Parameters.AddWithValue("$wy1", record.Wy1);
        cmd.Parameters.AddWithValue("$wy2", record.Wy2);
        cmd.Parameters.AddWithValue("$sx", record.Sx);
        cmd.Parameters.AddWithValue("$sy", record.Sy);
        cmd.Parameters.AddWithValue("$a", record.A);
        cmd.Parameters.AddWithValue("$wall", record.壁厚);
        cmd.Parameters.AddWithValue("$web", record.腹板宽度);
        cmd.ExecuteNonQuery();
    }

    public List<ProfileRecord> GetProfiles(string dbPath, string category)
    {
        var tableName = GetTableName(category);
        var list = new List<ProfileRecord>();
        using var conn = new SqliteConnection(GetConnectionString(dbPath));
        conn.Open();
        EnsureTable(conn, tableName);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT Id, 名称, Ix, Iy, Wx1, Wx2, Wy1, Wy2, Sx, Sy, A, 壁厚, 腹板宽度 FROM [{tableName}]";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new ProfileRecord
            {
                Id = reader.GetInt32(0),
                名称 = reader.GetString(1),
                Ix = reader.IsDBNull(2) ? 0 : reader.GetDouble(2),
                Iy = reader.IsDBNull(3) ? 0 : reader.GetDouble(3),
                Wx1 = reader.IsDBNull(4) ? 0 : reader.GetDouble(4),
                Wx2 = reader.IsDBNull(5) ? 0 : reader.GetDouble(5),
                Wy1 = reader.IsDBNull(6) ? 0 : reader.GetDouble(6),
                Wy2 = reader.IsDBNull(7) ? 0 : reader.GetDouble(7),
                Sx = reader.IsDBNull(8) ? 0 : reader.GetDouble(8),
                Sy = reader.IsDBNull(9) ? 0 : reader.GetDouble(9),
                A = reader.IsDBNull(10) ? 0 : reader.GetDouble(10),
                壁厚 = reader.IsDBNull(11) ? 0 : reader.GetDouble(11),
                腹板宽度 = reader.IsDBNull(12) ? 0 : reader.GetDouble(12),
            });
        }
        return list;
    }

    public void DeleteProfile(string dbPath, string category, int id)
    {
        var tableName = GetTableName(category);
        using var conn = new SqliteConnection(GetConnectionString(dbPath));
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DELETE FROM [{tableName}] WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public List<string> GetTables(string dbPath)
    {
        var list = new List<string>();
        if (!File.Exists(dbPath)) return list;
        using var conn = new SqliteConnection(GetConnectionString(dbPath));
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add(reader.GetString(0));
        return list;
    }

    public void CreateDatabase(string dbPath)
    {
        using var conn = new SqliteConnection(GetConnectionString(dbPath));
        conn.Open();
        // Create all category tables
        foreach (var category in ProfileCategories.All)
        {
            EnsureTable(conn, GetTableName(category));
        }
    }
}
