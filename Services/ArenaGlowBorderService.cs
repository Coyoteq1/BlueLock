using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using ProjectM;
using Stunlock.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VAuto.Zone.Core;
using VAuto.Zone.Models;
using VAutomationCore.Core;
using VAutomationCore.Core.ECS;
using VAutomationCore.Core.Services;

namespace VAuto.Zone.Services
{
    public static class ArenaGlowBorderService
    {
        private static readonly List<Entity> _markers = new();
        private static readonly List<Entity> _glows = new();
        private const string DefaultPrefabName = "AB_Chaos_Barrier_AbilityGroup";
        private const string PrefabConfigJsonFileName = "arena_glow_prefabs.json";
        private const string PrefabConfigTomlFileName = "arena_glow_prefabs.toml";
        private const string ServiceName = "ArenaGlowBorderService";

        #region Initialization

        static ArenaGlowBorderService()
        {
            ServiceInitializer.RegisterInitializer(ServiceName, Initialize);
            ServiceInitializer.RegisterValidator(ServiceName, Validate);
        }

        private static void Initialize()
        {
            ZoneCore.LogInfo("Initializing ArenaGlowBorderService");
            ZoneCore.LogInfo("ArenaGlowBorderService initialized");
        }

        private static bool Validate()
        {
            return ZoneCore.IsInitialized;
        }

        #endregion

        public static string GetDefaultPrefabName()
        {
            if (TryLoadPrefabConfig(out var config) && !string.IsNullOrWhiteSpace(config.DefaultPrefab))
            {
                return config.DefaultPrefab;
            }
            return "Chaos";
        }

        public static int GetBorderPointCount(float spacing)
        {
            return ArenaTerritory.GetBorderPoints(spacing).Count;
        }

        /// <summary>
        /// Calculates the central zone and radius from border points.
        /// Returns the point(s) closest to center and the distance as radius.
        /// </summary>
        public static (List<float3> centralZone, float radius) CalculateCentralZone(float spacing)
        {
            var borderPoints = ArenaTerritory.GetBorderPoints(spacing);
            var centralZone = new List<float3>();
            
            if (borderPoints.Count == 0)
                return (centralZone, 0f);

            // Calculate center of all border points in meters
            float3 center = float3.zero;
            foreach (var p in borderPoints)
            {
                center += BlocksToMeters(p);
            }
            center /= borderPoints.Count;

            // Find minimum distance from center in meters
            float minDistance = float.MaxValue;
            foreach (var point in borderPoints)
            {
                var distance = math.distance(center, BlocksToMeters(point));
                if (distance < minDistance)
                {
                    minDistance = distance;
                }
            }

            // Collect all points at minimum distance (central zone)
            const float epsilon = 0.1f;
            foreach (var point in borderPoints)
            {
                var distance = math.distance(center, BlocksToMeters(point));
                if (Math.Abs(distance - minDistance) < epsilon)
                {
                    centralZone.Add(point);
                }
            }

            return (centralZone, minDistance);
        }

        private static float3 BlocksToMeters(float3 blockPos)
        {
            return new float3(blockPos.x * ArenaTerritory.BlockSize, blockPos.y, blockPos.z * ArenaTerritory.BlockSize);
        }

        public static bool ValidateConfig(string configPath, out List<ArenaZoneDef> zones, out string error)
        {
            zones = new List<ArenaZoneDef>();
            return ArenaTerritory.ValidateConfig(out error);
        }

        public static bool SpawnBorderGlows(string configPath, string prefabName, float spacing, out string error)
        {
            error = string.Empty;

            var em = ZoneCore.EntityManager;
            if (em == default)
            {
                error = "EntityManager not available.";
                ZoneCore.LogError(error);
                return false;
            }

            ClearAll();

            // Get border points from territory
            var borderPoints = ArenaTerritory.GetBorderPoints(spacing);
            var cornerPoints = ArenaTerritory.GetCornerPoints();
            
            // Combine border and corner points, removing duplicates
            var allPoints = borderPoints.Concat(cornerPoints).Distinct().ToList();

            if (allPoints.Count == 0)
            {
                error = "No border points to spawn.";
                ZoneCore.LogError(error);
                return false;
            }

            // Create empty marker entities first
            foreach (var p in allPoints)
            {
                var marker = em.CreateEntity(ComponentType.ReadWrite<LocalTransform>());
                em.SetComponentData(marker, LocalTransform.FromPositionRotationScale(p, quaternion.identity, 1f));
                _markers.Add(marker);
            }

            // Spawn glow prefabs on marker positions
            var prefabGuid = new PrefabGUID(0);
            if (!string.IsNullOrWhiteSpace(prefabName))
            {
                var glowService = new GlowService();
                var prefab = glowService.GetGlowPrefab(prefabName);
                if (!prefab.IsEmpty())
                {
                    prefabGuid = prefab;
                }
                else if (int.TryParse(prefabName, out var intGuid))
                {
                    prefabGuid = new PrefabGUID(intGuid);
                    if (!ZoneCore.PrefabCollection._PrefabGuidToEntityMap.ContainsKey(prefabGuid))
                    {
                        error = $"Prefab not found: {prefabName}";
                        return false;
                    }
                }
                else if (!TryResolvePrefabGuidFromConfig(prefabName, out prefabGuid))
                {
                    error = $"Prefab not found: {prefabName}";
                    return false;
                }
            }
            else
            {
                var glowService = new GlowService();
                prefabGuid = glowService.GetGlowPrefab("Chaos");
                if (prefabGuid.IsEmpty())
                {
                    error = "Default glow prefab not found";
                    return false;
                }
            }

            foreach (var marker in _markers)
            {
                if (!em.Exists(marker)) continue;
                
                SpawnGlow(em, prefabGuid, marker);
            }

            ZoneCore.LogInfo($"Spawned {_glows.Count} border glows");
            return true;
        }

        private static bool SpawnGlow(EntityManager em, PrefabGUID prefabGuid, Entity marker)
        {
            if (!ZoneCore.TryGetPrefabEntity(prefabGuid, out var prefabEntity))
            {
                ZoneCore.LogWarning($"Could not resolve prefab entity for {prefabGuid.GuidHash}");
                return false;
            }

            // Get marker position
            float3 position = float3.zero;
            if (em.HasComponent<LocalTransform>(marker))
            {
                position = em.GetComponentData<LocalTransform>(marker).Position;
            }
            else if (em.HasComponent<Translation>(marker))
            {
                position = em.GetComponentData<Translation>(marker).Value;
            }

            var glow = em.Instantiate(prefabEntity);

            // Position glow slightly above ground (0.3m) for carpet attachment effect
            float3 glowPosition = new float3(position.x, position.y + 0.3f, position.z);

            // Set LocalTransform if available
            if (em.HasComponent<LocalTransform>(glow))
            {
                var t = em.GetComponentData<LocalTransform>(glow);
                t.Position = glowPosition;
                em.SetComponentData(glow, t);
            }
            else if (em.HasComponent<Translation>(glow))
            {
                var t = em.GetComponentData<Translation>(glow);
                t.Value = glowPosition;
                em.SetComponentData(glow, t);
            }

            // Link to marker using LinkedEntityGroup
            if (em.HasComponent<LinkedEntityGroup>(marker))
            {
                var linkBuffer = em.GetBuffer<LinkedEntityGroup>(marker);
                linkBuffer.Add(new LinkedEntityGroup { Value = glow });
            }
            else
            {
                em.AddComponent<LinkedEntityGroup>(marker);
                var linkBuffer = em.GetBuffer<LinkedEntityGroup>(marker);
                linkBuffer.Add(new LinkedEntityGroup { Value = glow });
            }

            _glows.Add(glow);
            return true;
        }

        public static void ClearAll()
        {
            var em = ZoneCore.EntityManager;
            
            DestroyEntities(_glows);
            DestroyEntities(_markers);
            
            _glows.Clear();
            _markers.Clear();
            
            ZoneCore.LogInfo("Cleared all arena glow borders");
        }

        private static void DestroyEntities(List<Entity> list)
        {
            var em = ZoneCore.EntityManager;
            foreach (var e in list.ToArray())
            {
                if (em.Exists(e)) em.DestroyEntity(e);
            }
            list.Clear();
        }

        /// <summary>
        /// Returns all currently spawned border glow entities.
        /// </summary>
        public static IReadOnlyList<Entity> GetAllSpawnedEntities()
        {
            return _glows;
        }

        public static int GetSpawnedCount()
        {
            return _glows.Count;
        }

        private static bool TryResolvePrefabGuid(string prefabName, out PrefabGUID guid)
        {
            guid = default;
            try
            {
                var system = ZoneCore.Server?.GetExistingSystemManaged<PrefabCollectionSystem>();
                if (system == null)
                    return false;

                var type = system.GetType();
                var members = type.GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (var member in members)
                {
                    object value = member switch
                    {
                        FieldInfo f => f.GetValue(system),
                        PropertyInfo p => p.GetValue(system),
                        _ => null
                    };

                    if (value == null) continue;

                    if (TryGetGuidFromDictionary(value, prefabName, out guid))
                        return true;
                }
            }
            catch (Exception ex)
            {
                ZoneCore.LogException("Failed to resolve prefab GUID", ex);
                return false;
            }

            return false;
        }

        private static bool TryResolvePrefabGuidFromConfig(string prefabName, out PrefabGUID guid)
        {
            guid = default;
            if (!TryLoadPrefabConfig(out var config)) return false;
            if (config.Prefabs == null) return false;

            if (!config.Prefabs.TryGetValue(prefabName, out var longGuid)) return false;
            guid = new PrefabGUID((int)longGuid);
            return true;
        }

        private static bool TryLoadPrefabConfig(out GlowPrefabConfig config)
        {
            config = null;
            var (tomlPath, jsonPath) = GetPrefabConfigPaths();
            if (File.Exists(tomlPath))
            {
                return TryLoadPrefabToml(tomlPath, out config);
            }

            if (!File.Exists(jsonPath)) return false;

            try
            {
                var json = File.ReadAllText(jsonPath);
                config = JsonSerializer.Deserialize<GlowPrefabConfig>(json);
                return config != null;
            }
            catch (Exception ex)
            {
                ZoneCore.LogException("Failed to load prefab config", ex);
                return false;
            }
        }

        private static (string tomlPath, string jsonPath) GetPrefabConfigPaths()
        {
            var configDir = Path.Combine(BepInEx.Paths.ConfigPath, "VAuto.Arena");
            var tomlPath = Path.Combine(configDir, PrefabConfigTomlFileName);
            var jsonPath = Path.Combine(configDir, PrefabConfigJsonFileName);

            if (File.Exists(tomlPath) || File.Exists(jsonPath))
                return (tomlPath, jsonPath);

            var asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
            var fallbackDir = Path.Combine(asmDir, "config", "VAuto.Arena");
            return (Path.Combine(fallbackDir, PrefabConfigTomlFileName), Path.Combine(fallbackDir, PrefabConfigJsonFileName));
        }

        private static bool TryLoadPrefabToml(string path, out GlowPrefabConfig config)
        {
            config = null;
            try
            {
                var toml = File.ReadAllText(path);
                var parsed = SimpleToml.Parse(toml);
                var core = GetCoreTable(parsed);

                var c = new GlowPrefabConfig();
                if (core.TryGetValue("defaultPrefab", out var d) && d is string s) c.DefaultPrefab = s;

                if (parsed.TryGetValue("prefabs", out var pObj) && pObj is Dictionary<string, object> pTable)
                {
                    c.Prefabs = new Dictionary<string, long>();
                    foreach (var kvp in pTable)
                    {
                        var longGuid = kvp.Value is int i ? i : Convert.ToInt64(kvp.Value);
                        c.Prefabs[kvp.Key] = longGuid;
                    }
                }

                config = c;
                return true;
            }
            catch (Exception ex)
            {
                ZoneCore.LogException("Failed to load prefab TOML", ex);
                return false;
            }
        }

        private static Dictionary<string, object> GetCoreTable(Dictionary<string, object> root)
        {
            if (root.TryGetValue("core", out var coreObj) && coreObj is Dictionary<string, object> coreDict)
                return coreDict;
            return root;
        }

        private static bool TryGetGuidFromDictionary(object value, string prefabName, out PrefabGUID guid)
        {
            guid = default;
            var valueType = value.GetType();
            if (!valueType.IsGenericType) return false;

            var genericArgs = valueType.GetGenericArguments();
            if (genericArgs.Length != 2) return false;

            if (genericArgs[0] != typeof(string)) return false;

            if (value is System.Collections.IDictionary dict)
            {
                if (!dict.Contains(prefabName)) return false;

                var dictValue = dict[prefabName];
                if (dictValue is PrefabGUID pg)
                {
                    guid = pg;
                    return true;
                }

                if (dictValue is int intGuid)
                {
                    guid = new PrefabGUID((int)intGuid);
                    return true;
                }
            }

            return false;
        }

        private sealed class GlowPrefabConfig
        {
            public string DefaultPrefab { get; set; }
            public Dictionary<string, long> Prefabs { get; set; }
        }
    }
}
