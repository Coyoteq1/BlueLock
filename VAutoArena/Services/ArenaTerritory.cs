using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Unity.Mathematics;

namespace VAuto.Arena.Services
{
    public static class ArenaTerritory
    {
        private const string TerritoryJsonFile = "arena_territory.json";
        private const string TerritoryTomlFile = "arena_territory.toml";
        private const string DefaultGlowPrefab = "AB_Chaos_Barrier_AbilityGroup";

        public static float3 ArenaGridCenter = new float3(-1000, 0, 500);
        public static float ArenaGridRadius = 300f;
        public static int ArenaGridIndex = 500;
        public static int ArenaRegionType = 5;
        public static float BlockSize = 10f;

        public static bool EnableGlowBorder { get; private set; } = true;
        public static string GlowPrefab { get; private set; } = DefaultGlowPrefab;
        public static float GlowSpacingMeters { get; private set; } = 3f;

        private static readonly HashSet<int2> ArenaBlocks = new HashSet<int2>();
        private static bool IsInitialized = false;

        public static void InitializeArenaGrid()
        {
            if (IsInitialized) return;

            ResetDefaults();
            LoadConfigIfPresent();
            var centerBlock = ConvertPosToBlockCoord(ArenaGridCenter);
            int blockRadius = (int)(ArenaGridRadius / BlockSize);

            for (int x = -blockRadius; x <= blockRadius; x++)
            {
                for (int z = -blockRadius; z <= blockRadius; z++)
                {
                    var blockCoord = new int2(centerBlock.x + x, centerBlock.y + z);
                    var blockWorldPos = ConvertBlockCoordToPos(blockCoord);

                    if (math.distance(blockWorldPos, ArenaGridCenter) <= ArenaGridRadius)
                    {
                        ArenaBlocks.Add(blockCoord);
                    }
                }
            }

            IsInitialized = true;
            Plugin.Logger?.LogInfo($"Arena territory initialized with {ArenaBlocks.Count} blocks at grid index {ArenaGridIndex}");
        }

        public static void Reload()
        {
            ArenaBlocks.Clear();
            IsInitialized = false;
            InitializeArenaGrid();
        }

        public static bool IsInArenaTerritory(float3 position)
        {
            EnsureInit();
            var blockCoord = ConvertPosToBlockCoord(position);
            return ArenaBlocks.Contains(blockCoord);
        }

        public static int GetArenaRegion(float3 position)
        {
            return IsInArenaTerritory(position) ? ArenaRegionType : 0;
        }

        public static int GetArenaGridIndex(float3 position)
        {
            return IsInArenaTerritory(position) ? ArenaGridIndex : -1;
        }

        public static List<int2> GetArenaBlocks()
        {
            EnsureInit();
            return new List<int2>(ArenaBlocks);
        }

        public static List<float3> GetBorderPoints(float spacingMeters)
        {
            EnsureInit();
            var borders = new List<float3>();
            var stepBlocks = Math.Max(1, (int)Math.Round(Math.Max(0.1f, spacingMeters) / BlockSize));

            int i = 0;
            foreach (var block in ArenaBlocks)
            {
                if (!IsBorderBlock(block)) continue;
                if (i % stepBlocks == 0)
                {
                    borders.Add(ConvertBlockCoordToPos(block));
                }
                i++;
            }
            return borders;
        }

        public static bool ValidateConfig(out string error)
        {
            error = string.Empty;
            var path = GetPreferredConfigPath();
            if (!File.Exists(path))
            {
                error = $"Config not found: {path}";
                return false;
            }

            try
            {
                if (path.EndsWith(".toml", StringComparison.OrdinalIgnoreCase))
                    return ValidateToml(path, out error);

                return ValidateJson(path, out error);
            }
            catch (Exception ex)
            {
                error = $"Failed to parse: {ex.Message}";
                return false;
            }
        }

        public static string GetPreferredConfigPath()
        {
            var (tomlPath, jsonPath) = GetConfigPaths();
            if (File.Exists(tomlPath)) return tomlPath;
            if (File.Exists(jsonPath)) return jsonPath;
            return tomlPath;
        }

        private static void EnsureInit()
        {
            if (!IsInitialized) InitializeArenaGrid();
        }

        private static void ResetDefaults()
        {
            ArenaGridCenter = new float3(-1000, 0, 500);
            ArenaGridRadius = 300f;
            ArenaGridIndex = 500;
            ArenaRegionType = 5;
            BlockSize = 10f;

            EnableGlowBorder = true;
            GlowPrefab = DefaultGlowPrefab;
            GlowSpacingMeters = 3f;
        }

        private static void LoadConfigIfPresent()
        {
            var (tomlPath, jsonPath) = GetConfigPaths();

            // TOML is strict default; if only JSON exists, migrate to TOML once.
            if (File.Exists(tomlPath))
            {
                TryLoadToml(tomlPath);
                return;
            }

            if (File.Exists(jsonPath))
            {
                if (TryLoadJson(jsonPath))
                {
                    TryMigrateJsonToToml(jsonPath, tomlPath);
                }
                return;
            }

            try
            {
                // leave defaults
            }
            catch
            {
                // leave defaults
            }
        }

        private static (string tomlPath, string jsonPath) GetConfigPaths()
        {
            var configDir = Path.Combine(BepInEx.Paths.ConfigPath, "VAuto.Arena");
            var tomlPath = Path.Combine(configDir, TerritoryTomlFile);
            var jsonPath = Path.Combine(configDir, TerritoryJsonFile);

            if (File.Exists(tomlPath) || File.Exists(jsonPath))
                return (tomlPath, jsonPath);

            var asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
            var fallbackDir = Path.Combine(asmDir, "config", "VAuto.Arena");
            var fallbackToml = Path.Combine(fallbackDir, TerritoryTomlFile);
            var fallbackJson = Path.Combine(fallbackDir, TerritoryJsonFile);

            if (File.Exists(fallbackToml) || File.Exists(fallbackJson))
                return (fallbackToml, fallbackJson);

            // If no config exists anywhere, default to the BepInEx config path (preferred writable location).
            return (tomlPath, jsonPath);
        }

        private static void TryLoadToml(string path)
        {
            try
            {
                var toml = File.ReadAllText(path);
                var parsed = SimpleToml.Parse(toml);
                var core = GetCoreTable(parsed);
                var optional = GetOptionalFeaturesTable(parsed);

                if (core.TryGetValue("center", out var centerObj) && centerObj is object[] cArr && cArr.Length == 3)
                {
                    ArenaGridCenter = new float3(ToFloat(cArr[0]), ToFloat(cArr[1]), ToFloat(cArr[2]));
                }

                if (core.TryGetValue("radius", out var radiusObj))
                    ArenaGridRadius = ToFloat(radiusObj);
                if (core.TryGetValue("gridIndex", out var gridObj))
                    ArenaGridIndex = ToInt(gridObj);
                if (core.TryGetValue("regionType", out var regionObj))
                    ArenaRegionType = ToInt(regionObj);
                if (core.TryGetValue("blockSize", out var blockObj))
                    BlockSize = ToFloat(blockObj);

                if (core.TryGetValue("glowPrefab", out var glowPrefabObj) && glowPrefabObj is string gp && !string.IsNullOrWhiteSpace(gp))
                    GlowPrefab = gp;
                if (core.TryGetValue("glowSpacing", out var glowSpacingObj))
                    GlowSpacingMeters = ToFloat(glowSpacingObj);

                if (optional.TryGetValue("enableGlowBorder", out var enableObj))
                    EnableGlowBorder = ToBool(enableObj);
            }
            catch
            {
                // leave defaults
            }
        }

        private static bool TryLoadJson(string path)
        {
            try
            {
                var json = File.ReadAllText(path);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (TryReadFloat3(root, "center", out var center)) ArenaGridCenter = center;
                if (root.TryGetProperty("radius", out var radiusEl) && radiusEl.ValueKind == JsonValueKind.Number)
                    ArenaGridRadius = radiusEl.GetSingle();
                if (root.TryGetProperty("gridIndex", out var gridEl) && gridEl.ValueKind == JsonValueKind.Number)
                    ArenaGridIndex = gridEl.GetInt32();
                if (root.TryGetProperty("regionType", out var regionEl) && regionEl.ValueKind == JsonValueKind.Number)
                    ArenaRegionType = regionEl.GetInt32();
                if (root.TryGetProperty("blockSize", out var blockEl) && blockEl.ValueKind == JsonValueKind.Number)
                    BlockSize = blockEl.GetSingle();

                if (root.TryGetProperty("glowPrefab", out var glowPrefabEl) && glowPrefabEl.ValueKind == JsonValueKind.String)
                    GlowPrefab = glowPrefabEl.GetString() ?? DefaultGlowPrefab;
                if (root.TryGetProperty("glowSpacing", out var glowSpacingEl) && glowSpacingEl.ValueKind == JsonValueKind.Number)
                    GlowSpacingMeters = glowSpacingEl.GetSingle();
                if (root.TryGetProperty("enableGlowBorder", out var glowEnabledEl) && glowEnabledEl.ValueKind == JsonValueKind.True)
                    EnableGlowBorder = true;
                else if (root.TryGetProperty("enableGlowBorder", out glowEnabledEl) && glowEnabledEl.ValueKind == JsonValueKind.False)
                    EnableGlowBorder = false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void TryMigrateJsonToToml(string jsonPath, string tomlPath)
        {
            try
            {
                if (File.Exists(tomlPath)) return;
                var dir = Path.GetDirectoryName(tomlPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var center = new[] { ArenaGridCenter.x, ArenaGridCenter.y, ArenaGridCenter.z };
                var toml = SimpleToml.SerializeTerritory(
                    center,
                    ArenaGridRadius,
                    ArenaGridIndex,
                    ArenaRegionType,
                    BlockSize,
                    GlowPrefab,
                    GlowSpacingMeters,
                    EnableGlowBorder);
                File.WriteAllText(tomlPath, toml);
            }
            catch
            {
                // ignore migration failures
            }
        }

        private static float ToFloat(object v)
        {
            return v switch
            {
                float f => f,
                double d => (float)d,
                int i => i,
                long l => l,
                _ => Convert.ToSingle(v)
            };
        }

        private static int ToInt(object v)
        {
            return v switch
            {
                int i => i,
                long l => (int)l,
                float f => (int)f,
                double d => (int)d,
                _ => Convert.ToInt32(v)
            };
        }

        private static bool ToBool(object v)
        {
            return v switch
            {
                bool b => b,
                string s => s.Equals("true", StringComparison.OrdinalIgnoreCase),
                int i => i != 0,
                long l => l != 0,
                float f => Math.Abs(f) > float.Epsilon,
                double d => Math.Abs(d) > double.Epsilon,
                _ => Convert.ToBoolean(v)
            };
        }

        private static int2 ConvertPosToBlockCoord(float3 position)
        {
            return new int2(
                (int)math.floor(position.x / BlockSize),
                (int)math.floor(position.z / BlockSize)
            );
        }

        private static float3 ConvertBlockCoordToPos(int2 blockCoord)
        {
            return new float3(blockCoord.x * BlockSize + (BlockSize / 2f), 0, blockCoord.y * BlockSize + (BlockSize / 2f));
        }

        private static bool IsBorderBlock(int2 block)
        {
            if (!ArenaBlocks.Contains(block)) return false;

            var n1 = new int2(block.x + 1, block.y);
            var n2 = new int2(block.x - 1, block.y);
            var n3 = new int2(block.x, block.y + 1);
            var n4 = new int2(block.x, block.y - 1);

            return !ArenaBlocks.Contains(n1) || !ArenaBlocks.Contains(n2) || !ArenaBlocks.Contains(n3) || !ArenaBlocks.Contains(n4);
        }

        private static bool TryReadFloat3(JsonElement root, string property, out float3 value)
        {
            value = float3.zero;
            if (!root.TryGetProperty(property, out var el) || el.ValueKind != JsonValueKind.Array) return false;

            var arr = new List<float>(3);
            foreach (var item in el.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Number) return false;
                arr.Add(item.GetSingle());
            }
            if (arr.Count != 3) return false;

            value = new float3(arr[0], arr[1], arr[2]);
            return true;
        }

        private static bool TryReadFloat3(Dictionary<string, object> root, string property, out float3 value)
        {
            value = float3.zero;
            if (!root.TryGetValue(property, out var el) || el is not object[] arr || arr.Length != 3) return false;
            value = new float3(ToFloat(arr[0]), ToFloat(arr[1]), ToFloat(arr[2]));
            return true;
        }

        private static Dictionary<string, object> GetCoreTable(Dictionary<string, object> root)
        {
            if (root.TryGetValue("core", out var coreObj) && coreObj is Dictionary<string, object> coreDict)
                return coreDict;
            return root;
        }

        private static Dictionary<string, object> GetOptionalFeaturesTable(Dictionary<string, object> root)
        {
            if (root.TryGetValue("optionalFeatures", out var optObj) && optObj is Dictionary<string, object> optDict)
                return optDict;
            return new Dictionary<string, object>(StringComparer.Ordinal);
        }

        private static bool ValidateToml(string path, out string error)
        {
            error = string.Empty;
            try
            {
                var toml = File.ReadAllText(path);
                var parsed = SimpleToml.Parse(toml);
                var core = GetCoreTable(parsed);

                if (!TryReadFloat3(core, "center", out _))
                {
                    error = "Missing core.center [x,y,z].";
                    return false;
                }

                if (!core.TryGetValue("radius", out var rObj) || ToFloat(rObj) <= 0)
                {
                    error = "Invalid core.radius.";
                    return false;
                }

                if (core.TryGetValue("blockSize", out var bObj) && ToFloat(bObj) <= 0)
                {
                    error = "Invalid core.blockSize.";
                    return false;
                }

                if (core.TryGetValue("glowSpacing", out var sObj) && ToFloat(sObj) <= 0)
                {
                    error = "Invalid core.glowSpacing.";
                    return false;
                }

                if (core.TryGetValue("glowPrefab", out var pObj) && pObj is not string)
                {
                    error = "Invalid core.glowPrefab.";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = $"Failed to parse TOML: {ex.Message}";
                return false;
            }
        }

        private static bool ValidateJson(string path, out string error)
        {
            error = string.Empty;
            try
            {
                var json = File.ReadAllText(path);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!TryReadFloat3(root, "center", out _)) { error = "Missing center [x,y,z]."; return false; }
                if (!root.TryGetProperty("radius", out var r) || r.ValueKind != JsonValueKind.Number || r.GetSingle() <= 0)
                { error = "Invalid radius."; return false; }
                if (root.TryGetProperty("blockSize", out var b) && (b.ValueKind != JsonValueKind.Number || b.GetSingle() <= 0))
                { error = "Invalid blockSize."; return false; }

                return true;
            }
            catch (Exception ex)
            {
                error = $"Failed to parse JSON: {ex.Message}";
                return false;
            }
        }
    }
}
