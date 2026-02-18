using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using BepInEx;
using ProjectM;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VAuto.Zone.Core;
using VAuto.Zone.Models;
using VAutomationCore.Core.Config;
using VAutomationCore.Core.Services;

namespace VAuto.Zone.Services
{
    public static class ZoneGlowBorderService
    {
        public const string ServiceName = "ZoneGlowBorderService";
        private const string DefaultMarkerPrefabToken = "CarpetPrefab";

        // Avoid EntityManager.HasComponent<T> (IL2CPP generic trampoline instability early in boot).
        // ComponentType.ReadOnly<T>() is used to build non-generic HasComponent(Entity, ComponentType) checks.
        private static readonly ComponentType BuffType = ComponentType.ReadOnly<Buff>();
        private static readonly ComponentType SpawnTransformType = ComponentType.ReadOnly<SpawnTransform>();
        private static readonly ComponentType LocalTransformType = ComponentType.ReadOnly<LocalTransform>();
        private static readonly ComponentType TranslationType = ComponentType.ReadOnly<Translation>();
        private static readonly ComponentType LastTranslationType = ComponentType.ReadOnly<LastTranslation>();
        private static readonly ComponentType HeightType = ComponentType.ReadOnly<Height>();

        private static readonly Dictionary<string, ZoneRuntime> _zones = new();
        private static GlowZonesConfig _config = new();
        private const string ConfigFileName = "glow_zones.json";

        private class ZoneRuntime
        {
            public GlowZoneEntry Entry { get; set; } = new();
            public List<Entity> Markers { get; } = new();
            public List<Entity> Glows { get; } = new();
            public int ActivePrefabIndex { get; set; }
            public DateTime NextRotationUtc { get; set; } = DateTime.MaxValue;
            public PrefabGUID[] ResolvedPrefabs { get; set; } = Array.Empty<PrefabGUID>();
            public NativeList<Entity> SpawnedEntities { get; } = new NativeList<Entity>(Allocator.Persistent);
        }

        #region Initialization

        static ZoneGlowBorderService()
        {
            ServiceInitializer.RegisterInitializer(ServiceName, Initialize);
            ServiceInitializer.RegisterValidator(ServiceName, Validate);
        }

        private static void Initialize()
        {
            // Check if service is enabled in config
            LoadConfig();
            
            if (!_config.Enabled)
            {
                ZoneCore.LogInfo("ZoneGlowBorderService disabled in config (Enabled=false). Use Plugin.cs RebuildAllZoneBorders instead.");
                return;
            }

            ZoneCore.LogInfo("Initializing ZoneGlowBorderService");
            SyncRuntimeZonesFromConfig();
            ZoneCore.LogInfo($"ZoneGlowBorderService initialized with {_config.Zones.Count} zones");
        }

        private static bool Validate()
        {
            // If service is disabled, validation passes (nothing to validate)
            if (!_config.Enabled)
            {
                return true;
            }

            return _config != null && _config.Zones != null && _zones.Count == _config.Zones.Count;
        }

        private static void LoadConfig()
        {
            var configPath = Path.Combine(ZoneCore.ConfigPath, ConfigFileName);
            TypedJsonConfigManager.TryLoadOrCreate(
                configPath,
                GenerateDefaultConfig,
                out _config,
                out var createdDefault,
                CreateGlowSerializerOptions(writeIndented: true),
                ValidateGlowZonesConfig,
                ZoneCore.LogInfo,
                ZoneCore.LogWarning,
                ZoneCore.LogError);

            NormalizeGlowZonesConfig();

            if (createdDefault)
            {
                ZoneCore.LogInfo($"Created default glow zones config at {configPath}");
            }
        }

        private static void SyncRuntimeZonesFromConfig()
        {
            foreach (var zone in _zones.Values)
            {
                DisposeZoneRuntime(zone);
            }

            _zones.Clear();

            if (_config?.Zones == null)
            {
                return;
            }

            foreach (var entry in _config.Zones)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Id))
                {
                    continue;
                }

                if (_zones.ContainsKey(entry.Id))
                {
                    ZoneCore.LogWarning($"Duplicate glow zone id '{entry.Id}' in config; keeping last definition.");
                }

                _zones[entry.Id] = new ZoneRuntime
                {
                    Entry = entry,
                    ActivePrefabIndex = 0,
                    NextRotationUtc = DateTime.UtcNow.AddSeconds(Math.Max(1, entry.Rotation.IntervalSeconds))
                };
            }
        }

        private static void SaveConfig()
        {
            var configPath = Path.Combine(ZoneCore.ConfigPath, ConfigFileName);
            TypedJsonConfigManager.TrySave(
                configPath,
                _config,
                CreateGlowSerializerOptions(writeIndented: true),
                ZoneCore.LogDebug,
                ZoneCore.LogError);
        }

        private static GlowZonesConfig GenerateDefaultConfig()
        {
            return new GlowZonesConfig
            {
                Zones = new List<GlowZoneEntry>
                {
                    new()
                    {
                        Id = "Arena Glow",
                        Center = new float3(0, 0, 0),
                        Radius = 25f,
                        GlowPrefabs = new List<string> { DefaultMarkerPrefabToken },
                        Rotation = new GlowRotationConfig { IntervalSeconds = 60 }
                    }
                }
            };
        }

        private static JsonSerializerOptions CreateGlowSerializerOptions(bool writeIndented)
        {
            return new JsonSerializerOptions(ZoneJsonOptions.WithUnityMathConverters)
            {
                WriteIndented = writeIndented
            };
        }

        private static (bool IsValid, string Error) ValidateGlowZonesConfig(GlowZonesConfig config)
        {
            if (config == null)
            {
                return (false, "Config object is null");
            }

            if (config.Zones == null)
            {
                return (false, "Zones collection is null");
            }

            return (true, string.Empty);
        }

        private static void NormalizeGlowZonesConfig()
        {
            _config ??= new GlowZonesConfig();
            _config.Zones ??= new List<GlowZoneEntry>();

            var normalized = new List<GlowZoneEntry>(_config.Zones.Count);
            foreach (var zone in _config.Zones)
            {
                if (zone == null || string.IsNullOrWhiteSpace(zone.Id))
                {
                    continue;
                }

                zone.GlowPrefabs ??= new List<string>();
                zone.Rotation ??= new GlowRotationConfig();
                zone.MapIcon ??= new MapIconConfig();
                zone.BorderSpacing = Math.Max(0.25f, zone.BorderSpacing);

                // Allow config to specify the glow buff by name (GlowService choice), so operators don't need GUIDs.
                if (!zone.BuffId.HasValue || zone.BuffId.Value == 0)
                {
                    var token = zone.BuffToken?.Trim();
                    if (!string.IsNullOrWhiteSpace(token) && GlowService.TryResolve(token, out var buffGuid) && buffGuid != PrefabGUID.Empty)
                    {
                        zone.BuffId = buffGuid.GuidHash;
                    }
                }

                normalized.Add(zone);
            }

            _config.Zones = normalized;
        }

        #endregion

        #region Rotation Methods

        public static void RotateDueZones()
        {
            foreach (var zone in _zones.Values)
            {
                if (!zone.Entry.Rotation.Enabled || zone.ResolvedPrefabs.Length <= 1)
                {
                    continue;
                }

                if (DateTime.UtcNow >= zone.NextRotationUtc)
                {
                    RotateZone(zone);
                }
            }
        }

        public static void RotateAll()
        {
            foreach (var zone in _zones.Values)
            {
                if (!zone.Entry.Rotation.Enabled || zone.ResolvedPrefabs.Length <= 1)
                {
                    continue;
                }

                RotateZone(zone);
            }
        }

        private static void RotateZone(ZoneRuntime zone)
        {
            if (zone.ResolvedPrefabs.Length == 0) return;
            
            zone.ActivePrefabIndex = (zone.ActivePrefabIndex + 1) % zone.ResolvedPrefabs.Length;
            zone.NextRotationUtc = DateTime.UtcNow.AddSeconds(Math.Max(1, zone.Entry.Rotation.IntervalSeconds));
            BuildZone(zone);
            
            ZoneCore.LogDebug($"Rotated zone {zone.Entry.Id} to prefab index {zone.ActivePrefabIndex}");
        }

        public static void BuildAll()
        {
            foreach (var zone in _zones.Values)
            {
                BuildZone(zone);
            }
        }

        public static void ReloadConfigAndRebuild()
        {
            LoadConfig();
            SyncRuntimeZonesFromConfig();
            BuildAll();
        }

        private static void BuildZone(ZoneRuntime zone)
        {
            ZoneCore.LogInfo($"Building zone: {zone.Entry.Id}");

            var em = ZoneCore.EntityManager;
            if (!IsWorldReady(em))
            {
                ZoneCore.LogWarning($"Zone {zone.Entry.Id}: world not ready; skipping build.");
                return;
            }

            if (!zone.Entry.Enabled)
            {
                ClearSpawnedEntities(zone, em);
                ZoneCore.LogInfo($"Skipping disabled zone: {zone.Entry.Id}");
                return;
            }

            // Clear existing entities first
            ClearSpawnedEntities(zone, em);
            
            // Resolve prefabs from config
            zone.ResolvedPrefabs = ResolvePrefabs(zone.Entry.GlowPrefabs, _config?.DefaultGlowPrefab);
            ZoneCore.LogInfo($"Zone {zone.Entry.Id}: Resolved {zone.ResolvedPrefabs.Length} prefabs");
            if (zone.ResolvedPrefabs.Length == 0)
            {
                ZoneCore.LogWarning($"Zone {zone.Entry.Id}: no valid glow prefabs resolved; skipping border build.");
                return;
            }
            
            // Build border entities around the zone perimeter
            var borderEntities = BuildZoneBorder(zone);
            
            ZoneCore.LogInfo($"Zone {zone.Entry.Id} built with {borderEntities} border entities");
        }

        private static int BuildZoneBorder(ZoneRuntime zone)
        {
            try
            {
                var em = ZoneCore.EntityManager;
                if (!IsWorldReady(em)) return 0;

                var count = 0;
                var borderPositions = BuildBorderPoints(zone.Entry);
                if (borderPositions.Count == 0)
                {
                    ZoneCore.LogWarning($"Zone {zone.Entry.Id}: no border positions were generated.");
                    return 0;
                }

                if (zone.ActivePrefabIndex < 0 || zone.ActivePrefabIndex >= zone.ResolvedPrefabs.Length)
                {
                    zone.ActivePrefabIndex = 0;
                }

                var activePrefabGuid = zone.ResolvedPrefabs[zone.ActivePrefabIndex];
                if (activePrefabGuid == PrefabGUID.Empty)
                {
                    ZoneCore.LogWarning($"Zone {zone.Entry.Id}: active glow prefab guid is empty; skipping border build.");
                    return 0;
                }

                if (!ZoneCore.TryGetPrefabEntity(activePrefabGuid, out var activePrefabEntity) || activePrefabEntity == Entity.Null)
                {
                    ZoneCore.LogWarning($"Zone {zone.Entry.Id}: failed to resolve active glow prefab entity ({activePrefabGuid.GuidHash}); skipping border build.");
                    return 0;
                }

                if (IsBuffPrefab(em, activePrefabEntity))
                {
                    ZoneCore.LogWarning($"Zone {zone.Entry.Id}: active glow prefab ({activePrefabGuid.GuidHash}) resolves to Buff prefab; configure a visual marker prefab.");
                    return 0;
                }

                var hasBuff = zone.Entry.BuffId.HasValue && zone.Entry.BuffId.Value != 0;
                var buffGuid = hasBuff ? new PrefabGUID(zone.Entry.BuffId.Value) : PrefabGUID.Empty;
                var buffWarningLogged = false;
                
                for (int i = 0; i < borderPositions.Count; i++)
                {
                    var position = borderPositions[i];
                    
                    var entity = CreateGlowEntity(em, position, activePrefabGuid, activePrefabEntity);
                    if (entity == Entity.Null)
                    {
                        ZoneCore.LogWarning($"Zone {zone.Entry.Id}: failed to spawn marker at border index {i}; skipping point.");
                        continue;
                    }

                    zone.Markers.Add(entity);
                    var trackedMarker = entity;
                    zone.SpawnedEntities.Add(ref trackedMarker);
                    count++;

                    if (hasBuff)
                    {
                        if (GameActionService.TryApplyCleanBuff(entity, buffGuid, out var buffEntity, -1f))
                        {
                            zone.Glows.Add(buffEntity);
                            zone.SpawnedEntities.Add(ref buffEntity);
                        }
                        else if (!buffWarningLogged)
                        {
                            ZoneCore.LogWarning($"Zone {zone.Entry.Id}: failed to apply glow buff {buffGuid.GuidHash}; continuing with marker-only visuals.");
                            buffWarningLogged = true;
                        }
                    }
                }
                
                return count;
            }
            catch (Exception ex)
            {
                ZoneCore.LogException($"Failed to build zone border: {ex.Message}", ex);
                return 0;
            }
        }

        private static List<float3> BuildBorderPoints(GlowZoneEntry entry)
        {
            var points = new List<float3>();
            var center = entry.Center;

            if (entry.GridCenter.HasValue)
            {
                var gridCenter = entry.GridCenter.Value;
                center = new float3(gridCenter.x, center.y, gridCenter.y);
            }

            var spacing = Math.Max(0.25f, entry.BorderSpacing);

            if (entry.GridHalfExtents.HasValue || entry.HalfExtents.HasValue)
            {
                var halfX = entry.GridHalfExtents.HasValue
                    ? Math.Max(1f, entry.GridHalfExtents.Value.x)
                    : Math.Max(1f, entry.HalfExtents?.x ?? 1f);
                var halfZ = entry.GridHalfExtents.HasValue
                    ? Math.Max(1f, entry.GridHalfExtents.Value.y)
                    : Math.Max(1f, entry.HalfExtents?.y ?? 1f);

                var minX = center.x - halfX;
                var maxX = center.x + halfX;
                var minZ = center.z - halfZ;
                var maxZ = center.z + halfZ;

                for (var x = minX; x <= maxX; x += spacing)
                {
                    points.Add(new float3(x, center.y, minZ));
                    points.Add(new float3(x, center.y, maxZ));
                }

                for (var z = minZ + spacing; z < maxZ; z += spacing)
                {
                    points.Add(new float3(minX, center.y, z));
                    points.Add(new float3(maxX, center.y, z));
                }

                return points;
            }

            var radius = Math.Max(0.5f, entry.Radius ?? 25f);
            var pointCount = Math.Max(1, (int)(Math.PI * 2 * radius / spacing));

            for (var i = 0; i < pointCount; i++)
            {
                var angle = (i / (float)pointCount) * Math.PI * 2;
                points.Add(new float3(
                    center.x + (float)Math.Cos(angle) * radius,
                    center.y,
                    center.z + (float)Math.Sin(angle) * radius));
            }

            return points;
        }

        private static Entity CreateGlowEntity(EntityManager em, float3 position, PrefabGUID prefabGuid, Entity prefabEntity)
        {
            try
            {
                if (prefabGuid == PrefabGUID.Empty || prefabEntity == Entity.Null || !em.Exists(prefabEntity))
                {
                    ZoneCore.LogWarning($"CreateGlowEntity received invalid prefab ({prefabGuid.GuidHash}).");
                    return Entity.Null;
                }

                var entity = em.Instantiate(prefabEntity);
                if (entity == Entity.Null || !em.Exists(entity))
                {
                    ZoneCore.LogWarning($"Failed to instantiate glow prefab entity for guid {prefabGuid.GuidHash}.");
                    return Entity.Null;
                }

                TrySetEntityPosition(em, entity, position);
                return entity;
            }
            catch (Exception ex)
            {
                ZoneCore.LogException($"Failed to create glow entity: {ex.Message}", ex);
                return Entity.Null;
            }
        }

        private static PrefabGUID[] ResolvePrefabs(List<string> prefabNames, string? defaultPrefabToken)
        {
            var tokens = new List<string>();
            if (prefabNames != null && prefabNames.Count > 0)
            {
                tokens.AddRange(prefabNames);
            }

            if (tokens.Count == 0)
            {
                var def = string.IsNullOrWhiteSpace(defaultPrefabToken) ? DefaultMarkerPrefabToken : defaultPrefabToken.Trim();
                if (!string.IsNullOrWhiteSpace(def))
                {
                    tokens.Add(def);
                }
            }

            var result = new List<PrefabGUID>(tokens.Count);
            var seen = new HashSet<int>();

            foreach (var token in tokens)
            {
                if (string.IsNullOrWhiteSpace(token))
                {
                    continue;
                }

                if (TryResolveGlowPrefabToken(token.Trim(), out var guid))
                {
                    // Guard: operators sometimes put glow buff names (Chaos/Cursed/etc) here.
                    // Those resolve to Buff prefabs and cannot be instantiated as marker entities.
                    if (ZoneCore.TryGetPrefabEntity(guid, out var prefabEntity) && prefabEntity != Entity.Null && IsBuffPrefab(ZoneCore.EntityManager, prefabEntity))
                    {
                        ZoneCore.LogWarning($"Glow prefab token '{token}' resolved to a Buff ({guid.GuidHash}). Configure a visual marker prefab in GlowPrefabs and set BuffToken/BuffId for the glow buff.");
                        continue;
                    }

                    if (guid != PrefabGUID.Empty && seen.Add(guid.GuidHash))
                    {
                        result.Add(guid);
                    }
                }
                else
                {
                    ZoneCore.LogWarning($"Could not resolve glow prefab token '{token}'; skipping.");
                }
            }

            return result.ToArray();
        }

        private static bool TryResolveGlowPrefabToken(string token, out PrefabGUID guid)
        {
            guid = PrefabGUID.Empty;

            // 1) All-prefab map lookup by exact prefab name (handles non-spawnables).
            if (TryResolveFromAllPrefabs(token, out guid))
            {
                ZoneCore.LogDebug($"Resolved glow token '{token}' via PrefabResolver ({guid.GuidHash}).");
                return true;
            }

            // 2) Spawnable/runtime resolver.
            if (ZoneCore.TryResolvePrefabEntity(token, out guid, out _))
            {
                ZoneCore.LogDebug($"Resolved glow token '{token}' from spawnable/runtime resolver ({guid.GuidHash}).");
                return true;
            }

            // 3) Glow service aliases.
            if (GlowService.TryResolve(token, out guid))
            {
                return ZoneCore.TryGetPrefabEntity(guid, out _);
            }

            // 4) Catalog fallback.
            if (PrefabReferenceCatalog.TryResolve(token, out guid,
                    PrefabCatalogDomain.Glow,
                    PrefabCatalogDomain.Ability,
                    PrefabCatalogDomain.Spell))
            {
                return ZoneCore.TryGetPrefabEntity(guid, out _);
            }

            if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericGuid) &&
                numericGuid != 0)
            {
                guid = new PrefabGUID(numericGuid);
                return ZoneCore.TryGetPrefabEntity(guid, out _);
            }

            return false;
        }

        private static bool TryResolveFromAllPrefabs(string prefabName, out PrefabGUID guid)
        {
            guid = PrefabGUID.Empty;
            if (string.IsNullOrWhiteSpace(prefabName))
            {
                return false;
            }

            if (!PrefabResolver.TryResolve(prefabName, out guid))
            {
                return false;
            }

            return ZoneCore.TryGetPrefabEntity(guid, out _);
        }
        private static bool IsBuffPrefab(EntityManager em, Entity prefabEntity)
        {
            try
            {
                return prefabEntity != Entity.Null &&
                       em.Exists(prefabEntity) &&
                       em.HasComponent(prefabEntity, BuffType);
            }
            catch
            {
                return false;
            }
        }

        private static void TrySetEntityPosition(EntityManager em, Entity entity, float3 targetPosition)
        {
            if (entity == Entity.Null || !em.Exists(entity))
            {
                return;
            }

            if (em.HasComponent(entity, SpawnTransformType))
            {
                var spawn = em.GetComponentData<SpawnTransform>(entity);
                spawn.Position = targetPosition;
                em.SetComponentData(entity, spawn);
            }

            if (em.HasComponent(entity, LocalTransformType))
            {
                var local = em.GetComponentData<LocalTransform>(entity);
                local.Position = targetPosition;
                em.SetComponentData(entity, local);
            }

            if (em.HasComponent(entity, TranslationType))
            {
                var translation = em.GetComponentData<Translation>(entity);
                translation.Value = targetPosition;
                em.SetComponentData(entity, translation);
            }

            if (em.HasComponent(entity, LastTranslationType))
            {
                var last = em.GetComponentData<LastTranslation>(entity);
                last.Value = targetPosition;
                em.SetComponentData(entity, last);
            }

            if (em.HasComponent(entity, HeightType))
            {
                var height = em.GetComponentData<Height>(entity);
                height.LastPosition = targetPosition;
                em.SetComponentData(entity, height);
            }
        }

        private static bool IsWorldReady(EntityManager em)
        {
            try
            {
                if (em == default)
                {
                    return false;
                }

                var world = em.World;
                return world != null && world.IsCreated;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Cleanup

        /// <summary>
        /// Dispose all spawned entities and cleanup resources.
        /// </summary>
        public static void DisposeZones()
        {
            foreach (var zone in _zones.Values)
            {
                DisposeZoneRuntime(zone);
            }
            
            _zones.Clear();
            ZoneCore.LogInfo("ZoneGlowBorderService disposed all zones");
        }

        /// <summary>
        /// Shutdown the service.
        /// </summary>
        public static void Shutdown()
        {
            DisposeZones();
            ZoneCore.LogInfo("ZoneGlowBorderService shutdown complete");
        }

        private static void ClearSpawnedEntities(ZoneRuntime zone, EntityManager em)
        {
            if (!zone.SpawnedEntities.IsCreated || em == default)
            {
                return;
            }

            var arr = zone.SpawnedEntities.AsArray();
            for (var i = 0; i < arr.Length; i++)
            {
                if (em.Exists(arr[i]))
                {
                    em.DestroyEntity(arr[i]);
                }
            }

            zone.SpawnedEntities.Clear();
            zone.Markers.Clear();
            zone.Glows.Clear();
        }

        private static void DisposeZoneRuntime(ZoneRuntime zone)
        {
            try
            {
                var em = ZoneCore.EntityManager;
                if (em != default)
                {
                    ClearSpawnedEntities(zone, em);
                }

                if (zone.SpawnedEntities.IsCreated)
                {
                    zone.SpawnedEntities.Dispose();
                }
            }
            catch (Exception ex)
            {
                ZoneCore.LogException($"Failed to dispose zone {zone.Entry.Id}: {ex.Message}", ex);
            }
        }

        #endregion
    }
}
