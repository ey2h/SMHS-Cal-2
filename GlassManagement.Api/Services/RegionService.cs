using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using GlassManagement.Api.Models;
using Microsoft.Data.Sqlite;

namespace GlassManagement.Api.Services {
    public class RegionService : IRegionService
    {
        public async Task<List<RegionInfo>> GetRegionsAsync()
        {
            var regions = new List<RegionInfo>();
            string dbPath = Environment.GetEnvironmentVariable("REGIONS_DB_PATH");
            if (string.IsNullOrWhiteSpace(dbPath))
            {
                // Fallback to a conventional path (relative to app)
                dbPath = "./SMHS_Region.db";
            }
            if (!System.IO.File.Exists(dbPath))
            {
                // Fallback to empty list if DB not found; in production, this path should be ensured
                return regions;
            }
            try
            {
                var connStr = new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();
                using var conn = new SqliteConnection(connStr);
                await conn.OpenAsync();
                // Try common table names
                var tables = new[] { "Regions", "Region", "SMHS_Region" };
                foreach (var t in tables)
                {
                    try
                    {
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = $"SELECT RegionId, Name FROM {t} LIMIT 100";
                        using var reader = await cmd.ExecuteReaderAsync();
                        while (await reader.ReadAsync())
                        {
                            var id = reader.IsDBNull(0) ? null : reader.GetString(0);
                            var name = reader.IsDBNull(1) ? null : reader.GetString(1);
                            if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(name))
                            {
                                regions.Add(new RegionInfo { RegionId = id, Name = name });
                            }
                        }
                        if (regions.Count > 0) break; // found
                    }
                    catch
                    {
                        // try next table
                    }
                }
            }
            catch
            {
                // ignore DB read errors in MVP; return empty list
            }
            // If still empty, fallback to a minimal in-memory placeholder
            if (regions.Count == 0)
            {
                regions.Add(new RegionInfo { RegionId = "default", Name = "默认区域" });
            }
            return regions;
        }
    }
}
