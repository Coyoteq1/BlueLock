using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using VAuto.Zone.Models;

namespace VAuto.Zone.Services
{
    /// <summary>
    /// Loads and caches structure files from BepInEx/config/KindredSchematics/.
    /// </summary>
    public static class ZoneStructureLoader
    {
        private static readonly string StructureDir = Path.Combine("BepInEx", "config", "KindredSchematics");
        private static readonly Dictionary<string, ZoneStructureData> Cache = new(StringComparer.OrdinalIgnoreCase);

        public static ZoneStructureData LoadStructure(string structureName)
        {
            if (string.IsNullOrWhiteSpace(structureName)) return null;
            if (Cache.TryGetValue(structureName, out var cached)) return cached;
            var path = TryGetStructurePath(structureName);
            if (path == null || !File.Exists(path)) return null;
            try
            {
                var json = File.ReadAllText(path);
                var data = JsonSerializer.Deserialize<ZoneStructureData>(json, ZoneJsonOptions.WithUnityMathConverters);
                if (data != null) Cache[structureName] = data;
                return data;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static string TryGetStructurePath(string structureName)
        {
            if (string.IsNullOrWhiteSpace(structureName)) return null;
            var file = structureName.EndsWith(".structure", StringComparison.OrdinalIgnoreCase)
                ? structureName
                : structureName + ".structure";
            var path = Path.Combine(StructureDir, file);
            return File.Exists(path) ? path : null;
        }
    }
}
