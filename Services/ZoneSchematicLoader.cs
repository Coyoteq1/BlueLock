using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using BepInEx;
using VAuto.Zone.Models;

namespace VAuto.Zone.Services
{
    /// <summary>
    /// Loads and caches schematic files from BepInEx/config/KindredSchematics/.
    /// </summary>
    public static class ZoneSchematicLoader
    {
        private static readonly string SchematicDir = Path.Combine(Paths.ConfigPath, "KindredSchematics");
        private static readonly Dictionary<string, SchematicData> Cache = new(StringComparer.OrdinalIgnoreCase);

        public static SchematicData LoadSchematic(string schematicName)
        {
            if (string.IsNullOrWhiteSpace(schematicName)) return null;
            if (Cache.TryGetValue(schematicName, out var cached)) return cached;
            var path = TryGetSchematicPath(schematicName);
            if (path == null || !File.Exists(path)) return null;
            try
            {
                var json = File.ReadAllText(path);
                var data = JsonSerializer.Deserialize<SchematicData>(json, ZoneJsonOptions.WithUnityMathConverters);
                if (data != null) Cache[schematicName] = data;
                return data;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static string TryGetSchematicPath(string schematicName)
        {
            if (string.IsNullOrWhiteSpace(schematicName)) return null;
            var file = schematicName.EndsWith(".schematic", StringComparison.OrdinalIgnoreCase)
                ? schematicName
                : schematicName + ".schematic";
            var path = Path.Combine(SchematicDir, file);
            return File.Exists(path) ? path : null;
        }
    }
}
