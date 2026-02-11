using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Entities;
using ProjectM;
using ProjectM.Network;
using VAuto.Zone.Models;
using UnityEngine.Tilemaps;
using VAuto.Zone.Core;
using VAutomationCore;
using VAutomationCore.Core.Logging;
using VAutomationCore.Core.Config;
using VAutomationCore.Core.ECS;
using VAutomationCore.Core.Services;

namespace VAuto.Zone.Services
{
    public static class ArenaTerritory
    {
        private const string TerritoryJsonFile = "arena_territory.json";
        private const string TerritoryTomlFile = "arena_territory.toml";
        private const string DefaultGlowPrefab = "Chaos";

        // Zone radius constraints
        public const float MinZoneRadius = 3f;
        public const float MaxZoneRadius = 150f;

        // Zone ID - replaces gridIndex for consistency with other zones
        public static string ZoneId { get; private set; } = "0";
        
        public static float3 ArenaGridCenter = new float3(-1000, 5, -500);
        public static float ArenaGridRadius = 30f;
        private static int _arenaRegionType = 5;
        public static int ArenaRegionType => _arenaRegionType;
        public static float BlockSize = 1f;

        public static bool EnableGlowBorder { get; set; } = true;
        public static string GlowPrefab { get; set; } = DefaultGlowPrefab;
        public static float GlowSpacingMeters { get; set; } = 1f;
        public static float GlowCornerRadius { get; set; } = 2f;
        public static bool SpawnGlowInCorners { get; set; } = true;

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
            ZoneCore.LogInfo($"Arena territory '{ZoneId}' initialized with {ArenaBlocks.Count} blocks");
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

        public static string GetArenaZoneId(float3 position)
        {
            return IsInArenaTerritory(position) ? ZoneId : string.Empty;
        }

        /// <summary>
        /// Gets the grid index for a position within the arena territory.
        /// Returns -1 if position is outside the arena.
        /// </summary>
        public static int GetArenaGridIndex(float3 position)
        {
            if (!IsInArenaTerritory(position)) return -1;
            var blockCoord = ConvertPosToBlockCoord(position);
            var centerBlock = ConvertPosToBlockCoord(ArenaGridCenter);
            return blockCoord.x - centerBlock.x + (blockCoord.y - centerBlock.y) * (int)(ArenaGridRadius * 2 / BlockSize);
        }

        #region Lifecycle Methods

        /// <summary>
        /// Execute full lifecycle enter for a player - all steps in one call.
        /// Triggers onEnterArenaZone stage with proper position tracking.
        /// </summary>
        public static void ExecuteLifecycleEnter(Entity userEntity, Entity characterEntity, float3 position)
        {
            try
            {
                Plugin.Logger.LogInfo($"[ArenaTerritory] Executing lifecycle enter for player at ({position.x:F0}, {position.y:F0}, {position.z:F0})");
                
                // Ensure glow zones are built
                EnsureGlowZonesBuilt();
                
                // Trigger lifecycle via ArenaLifecycleManager
                var lifecycleManager = GetLifecycleManager();
                if (lifecycleManager != null)
                {
                    InvokeLifecycleMethod(lifecycleManager, "OnPlayerEnter", new object[] { userEntity, characterEntity, ZoneId, position });
                }
                else
                {
                    Plugin.Logger.LogWarning("[ArenaTerritory] Lifecycle manager not available");
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[ArenaTerritory] Lifecycle enter failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Execute full lifecycle exit for a player - all steps in one call.
        /// Triggers onExitArenaZone stage with proper position tracking.
        /// </summary>
        public static void ExecuteLifecycleExit(Entity userEntity, Entity characterEntity, float3 position)
        {
            try
            {
                Plugin.Logger.LogInfo($"[ArenaTerritory] Executing lifecycle exit for player at ({position.x:F0}, {position.y:F0}, {position.z:F0})");
                
                // Trigger lifecycle via ArenaLifecycleManager
                var lifecycleManager = GetLifecycleManager();
                if (lifecycleManager != null)
                {
                    InvokeLifecycleMethod(lifecycleManager, "OnPlayerExit", new object[] { userEntity, characterEntity, ZoneId, position });
                }
                else
                {
                    Plugin.Logger.LogWarning("[ArenaTerritory] Lifecycle manager not available");
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[ArenaTerritory] Lifecycle exit failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Teleport a player to the arena center.
        /// </summary>
        public static void TeleportToArena(Entity characterEntity)
        {
            try
            {
                var em = ZoneCore.EntityManager;
                var position = ArenaGridCenter;
                
                if (em.HasComponent<LocalTransform>(characterEntity))
                {
                    var transform = em.GetComponentData<LocalTransform>(characterEntity);
                    transform.Position = position;
                    em.SetComponentData(characterEntity, transform);
                    ZoneCore.LogInfo($"[ArenaTerritory] Teleported player to ({position.x:F0}, {position.y:F0}, {position.z:F0})");
                }
            }
            catch (Exception ex)
            {
                ZoneCore.LogError($"[ArenaTerritory] Teleport failed: {ex.Message}");
            }
        }

        private static void EnsureGlowZonesBuilt()
        {
            try
            {
                ZoneGlowBorderService.BuildAll();
            }
            catch (Exception ex)
            {
                ZoneCore.LogWarning($"[ArenaTerritory] Glow zones not loaded, using fallback: {ex.Message}");
            }
        }

        private static object GetLifecycleManager()
        {
            try
            {
                // Try Vlifecycle assembly first (actual assembly name)
                var type = Type.GetType("VAuto.Core.Lifecycle.ArenaLifecycleManager, Vlifecycle");
                if (type != null)
                {
                    var prop = type.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    return prop?.GetValue(null);
                }
                
                // Fallback: try loading from all available assemblies
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (var asm in assemblies)
                {
                    try
                    {
                        type = asm.GetType("VAuto.Core.Lifecycle.ArenaLifecycleManager");
                        if (type != null)
                        {
                            var prop = type.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                            var instance = prop?.GetValue(null);
                            if (instance != null)
                            {
                                ZoneCore.LogInfo($"[ArenaTerritory] Found ArenaLifecycleManager in assembly: {asm.GetName().Name}");
                                return instance;
                            }
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                ZoneCore.LogWarning($"[ArenaTerritory] Could not find lifecycle manager: {ex.Message}");
            }
            return null;
        }

        private static void InvokeLifecycleMethod(object manager, string methodName, object[] args)
        {
            try
            {
                var method = manager.GetType().GetMethod(methodName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                method?.Invoke(manager, args);
            }
            catch (Exception ex)
            {
                ZoneCore.LogError($"[ArenaTerritory] Failed to invoke {methodName}: {ex.Message}");
            }
        }

        #endregion

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

        /// <summary>
        /// Gets corner points for spawning glows at corners of the arena territory.
        /// </summary>
        public static List<float3> GetCornerPoints()
        {
            EnsureInit();
            var corners = new List<float3>();
            
            if (!SpawnGlowInCorners) return corners;
            
            foreach (var block in ArenaBlocks)
            {
                if (!IsBorderBlock(block)) continue;
                if (IsCornerBlock(block))
                {
                    corners.Add(ConvertBlockCoordToPos(block));
                }
            }
            
            return corners;
        }

        /// <summary>
        /// Converts this territory to a GlowZoneEntry for consistency with other zones.
        /// </summary>
        public static GlowZoneEntry ToGlowZoneEntry()
        {
            EnsureInit();
            return new GlowZoneEntry
            {
                Id = ZoneId,
                Enabled = EnableGlowBorder,
                Center = ArenaGridCenter,
                Radius = ArenaGridRadius,
                BorderSpacing = GlowSpacingMeters,
                CornerOffset = GlowCornerRadius,
                SpawnEmptyMarkers = SpawnGlowInCorners,
                GlowPrefabs = new List<string> { GlowPrefab },
                Rotation = new GlowRotationConfig { Enabled = false },
                MapIcon = new MapIconConfig { Enabled = true, PrefabName = "ZoneIcon_Arena" }
            };
        }

        private static bool IsCornerBlock(int2 block)
        {
            if (!ArenaBlocks.Contains(block)) return false;
            
            int neighborCount = 0;
            var neighbors = new[]
            {
                new int2(block.x + 1, block.y),
                new int2(block.x - 1, block.y),
                new int2(block.x, block.y + 1),
                new int2(block.x, block.y - 1)
            };
            
            foreach (var n in neighbors)
            {
                if (ArenaBlocks.Contains(n)) neighborCount++;
            }
            
            return neighborCount == 2;
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
            ZoneId = "0";
            ArenaGridCenter = new float3(-1000, 5, -500);
            ArenaGridRadius = 300f;
            _arenaRegionType = 5;
            BlockSize = 10f;

            EnableGlowBorder = true;
            GlowPrefab = DefaultGlowPrefab;
            GlowSpacingMeters = 3f;
            GlowCornerRadius = 2f;
            SpawnGlowInCorners = true;
        }

        private static void LoadConfigIfPresent()
        {
            var (tomlPath, jsonPath) = GetConfigPaths();

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

                if (core.TryGetValue("id", out var idObj) && idObj is string id && !string.IsNullOrWhiteSpace(id))
                    ZoneId = id;

                if (core.TryGetValue("center", out var centerObj) && centerObj is object[] cArr && cArr.Length == 3)
                {
                    ArenaGridCenter = new float3(ToFloat(cArr[0]), ToFloat(cArr[1]), ToFloat(cArr[2]));
                }

                if (core.TryGetValue("radius", out var radiusObj))
                    ArenaGridRadius = ToFloat(radiusObj);
                if (core.TryGetValue("regionType", out var regionObj))
                    _arenaRegionType = ToInt(regionObj);
                if (core.TryGetValue("blockSize", out var blockObj))
                    BlockSize = ToFloat(blockObj);

                if (core.TryGetValue("glowPrefab", out var glowPrefabObj) && glowPrefabObj is string gp && !string.IsNullOrWhiteSpace(gp))
                    GlowPrefab = gp;
                if (core.TryGetValue("glowSpacing", out var glowSpacingObj))
                    GlowSpacingMeters = ToFloat(glowSpacingObj);

                if (optional.TryGetValue("enableGlowBorder", out var enableObj))
                    EnableGlowBorder = ToBool(enableObj);
                if (optional.TryGetValue("glowCornerRadius", out var cornerRadiusObj))
                    GlowCornerRadius = ToFloat(cornerRadiusObj);
                if (optional.TryGetValue("spawnGlowInCorners", out var cornersObj))
                    SpawnGlowInCorners = ToBool(cornersObj);
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

                if (root.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String)
                    ZoneId = idEl.GetString() ?? "0";

                if (TryReadFloat3(root, "center", out var center)) ArenaGridCenter = center;
                if (root.TryGetProperty("radius", out var radiusEl) && radiusEl.ValueKind == JsonValueKind.Number)
                    ArenaGridRadius = radiusEl.GetSingle();
                if (root.TryGetProperty("regionType", out var regionEl) && regionEl.ValueKind == JsonValueKind.Number)
                    _arenaRegionType = regionEl.GetInt32();
                if (root.TryGetProperty("blockSize", out var blockEl) && blockEl.ValueKind == JsonValueKind.Number)
                    BlockSize = blockEl.GetSingle();

                if (root.TryGetProperty("glowPrefab", out var glowPrefabEl) && glowPrefabEl.ValueKind == JsonValueKind.String)
                    GlowPrefab = glowPrefabEl.GetString() ?? DefaultGlowPrefab;
                if (root.TryGetProperty("glowSpacing", out var glowSpacingEl) && glowSpacingEl.ValueKind == JsonValueKind.Number)
                    GlowSpacingMeters = glowSpacingEl.GetSingle();
                if (root.TryGetProperty("glowCornerRadius", out var cornerEl) && cornerEl.ValueKind == JsonValueKind.Number)
                    GlowCornerRadius = cornerEl.GetSingle();
                if (root.TryGetProperty("spawnGlowInCorners", out var cornersEl))
                    SpawnGlowInCorners = cornersEl.ValueKind == JsonValueKind.True;
                if (root.TryGetProperty("enableGlowBorder", out var glowEnabledEl))
                    EnableGlowBorder = glowEnabledEl.ValueKind == JsonValueKind.True;

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
                    ZoneId,
                    center,
                    ArenaGridRadius,
                    ArenaRegionType,
                    BlockSize,
                    GlowPrefab,
                    GlowSpacingMeters,
                    GlowCornerRadius,
                    SpawnGlowInCorners,
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

                if (!core.TryGetValue("id", out var idObj) || idObj is not string)
                {
                    error = "Missing or invalid core.id.";
                    return false;
                }

                if (!TryReadFloat3(core, "center", out _))
                {
                    error = "Missing core.center [x,y,z].";
                    return false;
                }

                if (!core.TryGetValue("radius", out var rObj) || ToFloat(rObj) < MinZoneRadius || ToFloat(rObj) > MaxZoneRadius)
                {
                    error = $"Invalid radius. Must be between {MinZoneRadius} and {MaxZoneRadius}.";
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

                if (!root.TryGetProperty("id", out var idEl) || idEl.ValueKind != JsonValueKind.String)
                {
                    error = "Missing or invalid id.";
                    return false;
                }

                if (!TryReadFloat3(root, "center", out _)) { error = "Missing center [x,y,z]."; return false; }
                if (!root.TryGetProperty("radius", out var r) || r.ValueKind != JsonValueKind.Number || r.GetSingle() < MinZoneRadius || r.GetSingle() > MaxZoneRadius)
                { error = $"Invalid radius. Must be between {MinZoneRadius} and {MaxZoneRadius}."; return false; }
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
