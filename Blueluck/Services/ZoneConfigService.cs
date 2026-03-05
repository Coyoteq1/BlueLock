using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text.Json;
using BepInEx;
using BepInEx.Logging;
using Il2CppInterop.Runtime;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VAuto.Services.Interfaces;
using Blueluck.Models;
using VAutomationCore.Core.ECS.Components;

namespace Blueluck.Services
{
    /// <summary>
    /// Service for loading and managing zone configurations.
    /// Implements IService from VAutomationCore.
    /// </summary>
    public class ZoneConfigService : IService
    {
        private static readonly ManualLogSource _log = Logger.CreateLogSource("Blueluck.ZoneConfig");
        private const int FxPresetPoolSize = 400;
        private static readonly System.Reflection.MethodInfo? SetComponentDataGeneric = typeof(EntityManager).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == "SetComponentData" && m.IsGenericMethodDefinition && m.GetParameters().Length == 2 && m.GetParameters()[0].ParameterType == typeof(Entity));
        public bool IsInitialized { get; private set; }
        public ManualLogSource Log => _log;

        private ZonesConfig _config = new();
        private readonly Dictionary<int, ZoneDefinition> _zonesByHash = new();
        private readonly Dictionary<int, ZoneDefinition> _retiredZonesByHash = new();
        private string _configPath;
        private string _buffsNumberedPath = string.Empty;
        private DateTime _lastConfigCheck = DateTime.MinValue;

        public void Initialize()
        {
            Plugin.EnsureConfigFile(
                "zones.json",
                json =>
                {
                    using var doc = JsonDocument.Parse(json);
                    return doc.RootElement.TryGetProperty("zones", out var zones)
                        && zones.ValueKind == JsonValueKind.Array
                        && zones.GetArrayLength() > 0;
                },
                new
                {
                    detection = new { checkIntervalMs = 500, positionThreshold = 1.0f },
                    fxPresetList = Array.Empty<int>(),
                    zones = Array.Empty<object>()
                });
            Plugin.EnsureTextConfigFile("buffs_numbered.txt", text => !string.IsNullOrWhiteSpace(text));

            _configPath = Path.Combine(Paths.ConfigPath, "Blueluck", "zones.json");
            _buffsNumberedPath = ResolveBuffsNumberedPath();
            Directory.CreateDirectory(Path.GetDirectoryName(_configPath) ?? Paths.ConfigPath);
            
            LoadConfig();
            
            IsInitialized = true;
            _log.LogInfo($"[ZoneConfig] Initialized with {_zonesByHash.Count} zones.");
        }

        /// <summary>
        /// Spawns ECS entities for zones. Must be called after ECS world is ready.
        /// This is called automatically from the plugin's ECS initialization.
        /// </summary>
        public void SpawnZoneEntitiesIfReady()
        {
            if (!IsInitialized)
            {
                _log.LogWarning("[ZoneConfig] Cannot spawn zones - not initialized.");
                return;
            }
            
            SpawnZoneEntities();
        }

        public void Cleanup()
        {
            // Clean up spawned zone entities
            CleanupZoneEntities();
            _zonesByHash.Clear();
            _retiredZonesByHash.Clear();
            IsInitialized = false;
            _log.LogInfo("[ZoneConfig] Cleaned up.");
        }

        /// <summary>
        /// Spawns ECS entities with ZoneComponent for each configured zone.
        /// This enables the ZoneDetectionSystem to detect player transitions.
        /// </summary>
        private void SpawnZoneEntities()
        {
            try
            {
                var world = World.DefaultGameObjectInjectionWorld;
                if (world == null || !world.IsCreated)
                {
                    _log.LogWarning("[ZoneConfig] World not ready - cannot spawn zone entities.");
                    return;
                }

                var em = world.EntityManager;
                var spawnedCount = 0;
                var zoneType = Il2CppType.Of<ZoneComponent>(throwOnFailure: false);
                var localToWorldType = Il2CppType.Of<LocalToWorld>(throwOnFailure: false);

                if (zoneType == null || localToWorldType == null)
                {
                    _log.LogWarning("[ZoneConfig] Zone entity spawn skipped: required IL2CPP component types unavailable.");
                    return;
                }

                var zoneComponentType = new ComponentType(zoneType, ComponentType.AccessMode.ReadWrite);
                var localToWorldComponentType = new ComponentType(localToWorldType, ComponentType.AccessMode.ReadWrite);

                foreach (var zone in _zonesByHash.Values)
                {
                    if (!zone.Enabled || zone.Hash == 0)
                        continue;

                    // Create entity with required components up front to avoid IL2CPP generic add trampolines.
                    var zoneEntity = em.CreateEntity(
                        zoneComponentType,
                        localToWorldComponentType);

                    // Set up the ZoneComponent
                    var zoneComponent = new ZoneComponent
                    {
                        ZoneHash = zone.Hash,
                        Priority = zone.Priority,
                        Center = zone.GetCenterFloat3(),
                        EntryRadius = zone.EntryRadius,
                        ExitRadius = zone.ExitRadius,
                        EntryRadiusSq = zone.EntryRadius * zone.EntryRadius,
                        ExitRadiusSq = zone.ExitRadius * zone.ExitRadius
                    };

                    WriteComponentData(em, zoneEntity, zoneComponent);
                    WriteComponentData(em, zoneEntity, new LocalToWorld
                    {
                        Value = float4x4Translate(zoneComponent.Center)
                    });

                    spawnedCount++;
                    _log.LogInfo($"[ZoneConfig] Spawned zone entity: {zone.Name} (hash={zone.Hash}) at {zone.Center[0]},{zone.Center[1]},{zone.Center[2]}");
                }

                _log.LogInfo($"[ZoneConfig] Spawned {spawnedCount} zone entities for detection.");
            }
            catch (Exception ex)
            {
                _log.LogError($"[ZoneConfig] Failed to spawn zone entities: {ex.Message}");
            }
        }

        /// <summary>
        /// Cleans up all spawned zone entities.
        /// </summary>
        private void CleanupZoneEntities()
        {
            try
            {
                var world = World.DefaultGameObjectInjectionWorld;
                if (world == null || !world.IsCreated)
                    return;

                var em = world.EntityManager;
                var zoneType = Il2CppType.Of<ZoneComponent>(throwOnFailure: false);
                if (zoneType == null)
                {
                    return;
                }

                // Query for all ZoneComponent entities using the proper query builder
                var query = em.CreateEntityQuery(new ComponentType(zoneType, ComponentType.AccessMode.ReadOnly));

                if (query.CalculateEntityCount() == 0)
                    return;

                var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
                try
                {
                    foreach (var entity in entities)
                    {
                        if (em.Exists(entity))
                        {
                            em.DestroyEntity(entity);
                        }
                    }
                    _log.LogInfo($"[ZoneConfig] Cleaned up {entities.Length} zone entities.");
                }
                finally
                {
                    entities.Dispose();
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning($"[ZoneConfig] Failed to cleanup zone entities: {ex.Message}");
            }
        }

        private static float4x4 float4x4Translate(float3 position)
        {
            return new float4x4(
                1, 0, 0, 0,
                0, 1, 0, 0,
                0, 0, 1, 0,
                position.x, position.y, position.z, 1
            );
        }

        private static void WriteComponentData<T>(EntityManager em, Entity entity, T value) where T : struct
        {
            try
            {
                if (em.HasComponent<T>(entity))
                {
                    em.SetComponentData(entity, value);
                    return;
                }

                if (SetComponentDataGeneric != null)
                {
                    SetComponentDataGeneric.MakeGenericMethod(typeof(T)).Invoke(em, new object[] { entity, value });
                    return;
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning($"[ZoneConfig] Failed to write component {typeof(T).Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets all configured zones.
        /// </summary>
        public IReadOnlyList<ZoneDefinition> GetZones()
        {
            var zones = new List<ZoneDefinition>();
            foreach (var zone in _zonesByHash.Values)
            {
                if (zone.Enabled)
                    zones.Add(zone);
            }
            return zones;
        }

        /// <summary>
        /// Gets a zone by its hash.
        /// </summary>
        public bool TryGetZoneByHash(int hash, out ZoneDefinition zone)
        {
            return _zonesByHash.TryGetValue(hash, out zone) || _retiredZonesByHash.TryGetValue(hash, out zone);
        }

        public bool IsActiveZoneHash(int hash)
        {
            return _zonesByHash.ContainsKey(hash);
        }

        public void ReleaseRetiredZone(int hash)
        {
            if (hash == 0 || _zonesByHash.ContainsKey(hash))
            {
                return;
            }

            if (_retiredZonesByHash.Remove(hash))
            {
                _log.LogInfo($"[ZoneConfig] Released retired zone definition hash={hash}.");
            }
        }

        /// <summary>
        /// Gets detection configuration.
        /// </summary>
        public ZoneDetectionConfig GetDetectionConfig()
        {
            return _config.Detection ?? new ZoneDetectionConfig();
        }

        /// <summary>
        /// Reloads configuration from disk.
        /// </summary>
        public void Reload()
        {
            // Clean up old zone entities first
            CleanupZoneEntities();
            LoadConfig();
            // Re-spawn zone entities with new config
            SpawnZoneEntities();
            _log.LogInfo("[ZoneConfig] Configuration reloaded.");
        }

        /// <summary>
        /// Checks if config file has been modified and reloads if needed.
        /// </summary>
        public void CheckForChanges()
        {
            if (!File.Exists(_configPath))
                return;

            var lastWrite = File.GetLastWriteTime(_configPath);
            if (lastWrite > _lastConfigCheck)
            {
                Reload();
                _lastConfigCheck = lastWrite;
            }
        }

        private void LoadConfig()
        {
            var previousZones = new Dictionary<int, ZoneDefinition>(_zonesByHash);

            try
            {
                if (!File.Exists(_configPath))
                {
                    CreateDefaultConfig();
                    UpdateRetiredZones(previousZones);
                    return;
                }

                var json = File.ReadAllText(_configPath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve,
                    MaxDepth = 128
                };
                options.Converters.Add(new ZoneDefinitionJsonConverter());

                _config = JsonSerializer.Deserialize<ZonesConfig>(json, options) ?? new ZonesConfig();
                var normalized = NormalizeFxPresets();

                _zonesByHash.Clear();
                foreach (var zone in _config.Zones ?? Array.Empty<ZoneDefinition>())
                {
                    if (zone.Enabled && zone.Hash != 0)
                    {
                        _zonesByHash[zone.Hash] = zone;
                    }
                }

                UpdateRetiredZones(previousZones);
                if (normalized)
                {
                    PersistConfig();
                }

                _log.LogInfo($"[ZoneConfig] Loaded {_zonesByHash.Count} zones from config.");
            }
            catch (Exception ex)
            {
                _log.LogError($"[ZoneConfig] Failed to load config: {ex.Message}");
                _config ??= new ZonesConfig();
                _zonesByHash.Clear();
                foreach (var pair in previousZones)
                {
                    _zonesByHash[pair.Key] = pair.Value;
                }
                _log.LogWarning($"[ZoneConfig] Preserved {previousZones.Count} previously loaded zones after config load failure.");
            }
        }

        private void CreateDefaultConfig()
        {
            var fxPresetList = BuildDefaultFxPresetList();
            _config = new ZonesConfig
            {
                FxPresetList = fxPresetList,
                Detection = new ZoneDetectionConfig
                {
                    CheckIntervalMs = 500,
                    PositionThreshold = 1.0f
                },
                Zones = new ZoneDefinition[]
                {
                    new BossZoneConfig
                    {
                        Type = "BossZone",
                        Name = "Example Boss Zone",
                        Hash = 1001,
                        Center = new float[] { 500, 0, -300 },
                        EntryRadius = 80,
                        ExitRadius = 100,
                        Enabled = false,
                        Priority = 1,
                        FxPresetIndex = ComputePreAssignedFxPresetIndex(1001, fxPresetList.Length),
                        FlowOnEnter = "boss_enter",
                        FlowOnExit = "boss_exit",
                        AbilitySet = "boss",
                        NoProgress = true,
                        ForceJoinClan = true,
                        ShuffleClan = true,
                        BossPrefab = "CHAR_Gloomrot_Purifier_VBlood",
                        BossQuantity = 1,
                        RandomSpawn = true,
                        BorderVisual = new BorderVisualConfig
                        {
                            Effect = "custom",
                            BuffPrefabs = new[] { fxPresetList[Math.Max(0, ComputePreAssignedFxPresetIndex(1001, fxPresetList.Length) - 1)].ToString() },
                            Range = 10f,
                            IntensityMax = 3,
                            RemoveOnExit = true
                        }
                    },
                    new ArenaZoneConfig
                    {
                        Type = "ArenaZone",
                        Name = "Example Arena",
                        Hash = 2001,
                        Center = new float[] { 1000, 0, -500 },
                        EntryRadius = 150,
                        ExitRadius = 160,
                        Enabled = false,
                        Priority = 0,
                        FxPresetIndex = ComputePreAssignedFxPresetIndex(2001, fxPresetList.Length),
                        KitOnEnter = "arena_enter",
                        KitOnExit = "arena_exit",
                        FlowOnEnter = "arena_enter",
                        FlowOnExit = "arena_exit",
                        AbilitySet = "arena",
                        SaveProgress = true,
                        RestoreOnExit = true,
                        PvpEnabled = true,
                        BorderVisual = new BorderVisualConfig
                        {
                            Effect = "custom",
                            BuffPrefabs = new[] { fxPresetList[Math.Max(0, ComputePreAssignedFxPresetIndex(2001, fxPresetList.Length) - 1)].ToString() },
                            Range = 12f,
                            IntensityMax = 3,
                            RemoveOnExit = true
                        }
                    }
                }
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            };
            options.Converters.Add(new ZoneDefinitionJsonConverter());

            var json = JsonSerializer.Serialize(_config, options);
            File.WriteAllText(_configPath, json);
            _log.LogInfo($"[ZoneConfig] Created default config at {_configPath}");

            foreach (var zone in _config.Zones)
            {
                if (zone.Enabled && zone.Hash != 0)
                {
                    _zonesByHash[zone.Hash] = zone;
                }
            }
        }

        private void UpdateRetiredZones(Dictionary<int, ZoneDefinition> previousZones)
        {
            foreach (var activeHash in _zonesByHash.Keys)
            {
                _retiredZonesByHash.Remove(activeHash);
            }

            foreach (var pair in previousZones)
            {
                if (!_zonesByHash.ContainsKey(pair.Key))
                {
                    _retiredZonesByHash[pair.Key] = pair.Value;
                }
            }
        }

        private string ResolveBuffsNumberedPath()
        {
            var configDataCandidate = Path.Combine(Paths.ConfigPath, "Blueluck", "Data", "buffs_numbered.txt");
            if (File.Exists(configDataCandidate))
            {
                return configDataCandidate;
            }

            var pluginDataCandidate = Path.Combine(Paths.PluginPath, "Blueluck", "Data", "buffs_numbered.txt");
            if (File.Exists(pluginDataCandidate))
            {
                return pluginDataCandidate;
            }

            var configCandidate = Path.Combine(Paths.ConfigPath, "Blueluck", "buffs_numbered.txt");
            if (File.Exists(configCandidate))
            {
                return configCandidate;
            }

            return configCandidate;
        }

        private static int ComputePreAssignedFxPresetIndex(int zoneHash, int poolSize)
        {
            if (poolSize <= 0)
            {
                return 1;
            }

            var value = zoneHash == 0 ? 1 : Math.Abs(zoneHash);
            return ((value - 1) % poolSize) + 1;
        }

        private int[] BuildDefaultFxPresetList()
        {
            var result = new List<int>(FxPresetPoolSize);
            try
            {
                if (File.Exists(_buffsNumberedPath))
                {
                    foreach (var line in File.ReadLines(_buffsNumberedPath))
                    {
                        if (result.Count >= FxPresetPoolSize)
                        {
                            break;
                        }

                        var match = Regex.Match(line, @"^\s*\d+\.\s*(-?\d+)\s*$");
                        if (!match.Success || !int.TryParse(match.Groups[1].Value, out var value))
                        {
                            continue;
                        }

                        result.Add(value);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning($"[ZoneConfig] Failed reading FX source '{_buffsNumberedPath}': {ex.Message}");
            }

            while (result.Count < FxPresetPoolSize)
            {
                result.Add(0);
            }

            return result.Take(FxPresetPoolSize).ToArray();
        }

        private bool NormalizeFxPresets()
        {
            var changed = false;
            _config ??= new ZonesConfig();
            _config.Zones ??= Array.Empty<ZoneDefinition>();

            if (_config.FxPresetList == null || _config.FxPresetList.Length != FxPresetPoolSize)
            {
                _config.FxPresetList = BuildDefaultFxPresetList();
                changed = true;
            }

            for (var i = 0; i < _config.Zones.Length; i++)
            {
                var zone = _config.Zones[i];
                if (zone == null)
                {
                    continue;
                }

                var normalizedEntry = zone.EntryRadius > 0.001f ? zone.EntryRadius : 50f;
                if (Math.Abs(zone.EntryRadius - normalizedEntry) > 0.001f)
                {
                    zone.EntryRadius = normalizedEntry;
                    changed = true;
                }

                var normalizedExit = zone.ExitRadius > 0.001f ? zone.ExitRadius : normalizedEntry;
                if (normalizedExit < normalizedEntry)
                {
                    normalizedExit = normalizedEntry;
                }

                if (Math.Abs(zone.ExitRadius - normalizedExit) > 0.001f)
                {
                    zone.ExitRadius = normalizedExit;
                    changed = true;
                }

                if (zone.FxPresetIndex <= 0)
                {
                    zone.FxPresetIndex = ComputePreAssignedFxPresetIndex(zone.Hash, _config.FxPresetList.Length);
                    changed = true;
                }

                if (zone.FxPresetIndex > _config.FxPresetList.Length)
                {
                    zone.FxPresetIndex = _config.FxPresetList.Length;
                    changed = true;
                }

                var presetGuid = _config.FxPresetList[zone.FxPresetIndex - 1];
                zone.BorderVisual ??= new BorderVisualConfig();

                var needsDefaultPrefab = zone.BorderVisual.BuffPrefabs == null || zone.BorderVisual.BuffPrefabs.Length == 0;
                if (string.IsNullOrWhiteSpace(zone.BorderVisual.Effect) && needsDefaultPrefab)
                {
                    zone.BorderVisual.Effect = "custom";
                    changed = true;
                }

                if (needsDefaultPrefab)
                {
                    zone.BorderVisual.BuffPrefabs = new[] { presetGuid.ToString() };
                    changed = true;
                }

                var availableTiers = zone.BorderVisual.BuffPrefabs?.Length ?? 0;
                if (zone.BorderVisual.IntensityMax <= 0)
                {
                    zone.BorderVisual.IntensityMax = Math.Max(1, availableTiers);
                    changed = true;
                }
                else if (availableTiers > 0 && zone.BorderVisual.IntensityMax > availableTiers)
                {
                    zone.BorderVisual.IntensityMax = availableTiers;
                    changed = true;
                }
            }

            return changed;
        }

        private void PersistConfig()
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            };
            options.Converters.Add(new ZoneDefinitionJsonConverter());
            File.WriteAllText(_configPath, JsonSerializer.Serialize(_config, options));
        }
    }
}
