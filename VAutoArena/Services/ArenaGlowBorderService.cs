using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using ProjectM;
using Stunlock.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VAuto.Core;

namespace VAuto.Arena.Services
{
    internal static class ArenaGlowBorderService
    {
        private static readonly List<Entity> _spawned = new List<Entity>();
        private const string DefaultPrefabName = "AB_Chaos_Barrier_AbilityGroup";
        private const string PrefabConfigJsonFileName = "arena_glow_prefabs.json";
        private const string PrefabConfigTomlFileName = "arena_glow_prefabs.toml";

        public static string GetDefaultPrefabName()
        {
            if (TryLoadPrefabConfig(out var config) && !string.IsNullOrWhiteSpace(config.DefaultPrefab))
            {
                return config.DefaultPrefab;
            }
            return DefaultPrefabName;
        }

        public static bool ValidateConfig(string configPath, out List<ArenaZoneDef> zones, out string error)
        {
            zones = new List<ArenaZoneDef>();
            return ArenaTerritory.ValidateConfig(out error);
        }

        public static int GetBorderPointCount(ArenaZoneDef zone, float spacing)
        {
            return ArenaTerritory.GetBorderPoints(spacing).Count;
        }

        public static bool SpawnBorderGlows(string configPath, string prefabName, float spacing, out string error)
        {
            error = string.Empty;

            if (!ValidateConfig(configPath, out _, out error))
                return false;

            if (!TryResolvePrefabGuidFromConfig(prefabName, out var prefabGuid) &&
                !TryResolvePrefabGuid(prefabName, out prefabGuid))
            {
                error = $"Prefab not found: {prefabName}";
                return false;
            }

            if (!TryGetPrefabEntity(prefabGuid, out var prefabEntity))
            {
                error = $"Prefab entity not resolved for: {prefabName} ({prefabGuid.GuidHash})";
                return false;
            }

            var em = VRCore.EntityManager;
            if (em == default)
            {
                error = "EntityManager not available.";
                return false;
            }

            ClearAll();

            var points = ArenaTerritory.GetBorderPoints(spacing);
            foreach (var p in points)
            {
                var entity = em.Instantiate(prefabEntity);
                if (em.HasComponent<LocalTransform>(entity))
                {
                    var t = em.GetComponentData<LocalTransform>(entity);
                    t.Position = p;
                    em.SetComponentData(entity, t);
                }
                else if (em.HasComponent<Translation>(entity))
                {
                    var t = em.GetComponentData<Translation>(entity);
                    t.Value = p;
                    em.SetComponentData(entity, t);
                }

                _spawned.Add(entity);
            }

            return true;
        }

        public static void ClearAll()
        {
            var em = VRCore.EntityManager;
            if (em == default)
            {
                _spawned.Clear();
                return;
            }

            foreach (var entity in _spawned.ToArray())
            {
                if (em.Exists(entity))
                {
                    em.DestroyEntity(entity);
                }
            }
            _spawned.Clear();
        }

        private static bool TryResolvePrefabGuid(string prefabName, out PrefabGUID guid)
        {
            guid = default;
            try
            {
                var system = VRCore.ServerWorld?.GetExistingSystemManaged<PrefabCollectionSystem>();
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
            catch
            {
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
            catch
            {
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
            catch
            {
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

        private static bool TryGetPrefabEntity(PrefabGUID guid, out Entity prefabEntity)
        {
            prefabEntity = Entity.Null;
            try
            {
                var system = VRCore.ServerWorld?.GetExistingSystemManaged<PrefabCollectionSystem>();
                if (system == null)
                    return false;

                var method = system.GetType().GetMethod("GetPrefab", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(PrefabGUID) }, null);
                if (method == null)
                    return false;

                var result = method.Invoke(system, new object[] { guid });
                if (result is Entity e)
                {
                    prefabEntity = e;
                    return e != Entity.Null;
                }
            }
            catch
            {
                return false;
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
