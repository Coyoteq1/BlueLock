using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using ProjectM;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VAuto.Zone.Core;
using VAuto.Zone.Models;
using VAutomationCore.Core;
using VAutomationCore.Core.Config;
using VAutomationCore.Core.ECS;
using VAutomationCore.Core.Logging;
using VAutomationCore.Core.Services;

namespace VAuto.Zone.Services
{
    public static class ZoneGlowBorderService
    {
        private static readonly Dictionary<string, ZoneRuntime> _zones = new();
        private static GlowZonesConfig _config = new();
        private const string ConfigFileName = "glow_zones.json";
        private static readonly CoreLogger _log = new CoreLogger("ZoneGlowBorderService");
        
        // Real carpet prefab for glow fallback
        private static readonly PrefabGUID CarpetPrefabGuid = new PrefabGUID(-298064854);

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
            _log.Info("Initializing ZoneGlowBorderService");
            LoadConfig();
            _log.Info($"ZoneGlowBorderService initialized with {_config.Zones.Count} zones");
        }

        private static bool Validate()
        {
            return UnifiedCore.IsInitialized && _config != null;
        }

        #endregion

        #region Public API
        
        public static void BuildAll(bool rebuild = false)
        {
            if (!rebuild && _zones.Count > 0)
            {
                _log.Info("ZoneGlowBorderService already built, use rebuild=true");
                return;
            }

            _log.Info($"Building all glow borders (rebuild: {rebuild})");

            if (UnifiedCore.Server == null)
            {
                _log.Warning("Server not available, cannot build glow borders");
                return;
            }

            LoadConfig();
            if (rebuild) ClearAll();

            foreach (var zone in _config.Zones.Where(z => z.Enabled))
            {
                BuildZone(zone);
            }

            _log.Info($"Built {_zones.Count} glow zones");
        }

        public static void ClearAll()
        {
            _log.Info("Clearing all glow zones");

            foreach (var zr in _zones.Values)
            {
                // Use fallback cleanup only (EntitySpawner not available)
                if (zr.SpawnedEntities.IsCreated)
                {
                    zr.SpawnedEntities.Dispose();
                }
                DestroyEntities(zr.Markers);
                DestroyEntities(zr.Glows);
            }
            _zones.Clear();
            
            _log.Info("All glow zones cleared");
        }

        public static void RotateAll()
        {
            foreach (var zr in _zones.Values)
            {
                RotateZone(zr, force: true);
            }
        }

        public static void RotateDueZones()
        {
            var now = DateTime.UtcNow;
            foreach (var zr in _zones.Values)
            {
                if (!zr.Entry.Rotation.Enabled) continue;
                if (now >= zr.NextRotationUtc)
                {
                    RotateZone(zr, force: false);
                }
            }
        }

        public static IEnumerable<string> Status()
        {
            yield return $"ZoneGlowBorderService: {_zones.Count} zones loaded";
            
            foreach (var kvp in _zones)
            {
                var z = kvp.Value;
                yield return $"{kvp.Key}: markers={z.Markers.Count} glows={z.Glows.Count} entitySpawner={z.SpawnedEntities.Length} rotation={(z.Entry.Rotation.Enabled ? "on" : "off")}";
            }
        }
        
        #endregion

        private static void BuildZone(GlowZoneEntry entry)
        {
            if (string.IsNullOrWhiteSpace(entry.Id))
            {
                _log.Warning("Zone entry has null/empty ID, skipping");
                return;
            }

            var runtime = _zones.ContainsKey(entry.Id) ? _zones[entry.Id] : new ZoneRuntime();
            runtime.Entry = entry;

            // Clear existing entities
            DestroyEntities(runtime.Markers);
            DestroyEntities(runtime.Glows);

            runtime.NextRotationUtc = ComputeNextRotation(entry);
            runtime.ActivePrefabIndex = 0;

            var points = GeneratePoints(entry);
            if (points.Count == 0)
            {
                _log.Warning($"No points generated for zone {entry.Id}");
                _zones[entry.Id] = runtime;
                return;
            }

            _log.Info($"Building zone {entry.Id} with {points.Count} points");
            
            // EntitySpawner not available - using fallback method
            BuildZoneFallback(entry, runtime, points);

            _zones[entry.Id] = runtime;
        }

        /// <summary>
        /// Fallback method using old glow spawning when EntitySpawner is not available.
        /// </summary>
        private static void BuildZoneFallback(GlowZoneEntry entry, ZoneRuntime runtime, List<float3> points)
        {
            var em = UnifiedCore.EntityManager;
            if (em == default)
            {
                CoreLogger.Warning("EntityManager not available", ServiceName);
                return;
            }

            runtime.ResolvedPrefabs = ResolvePrefabs(entry);

            foreach (var p in points)
            {
                Entity marker = Entity.Null;
                if (entry.SpawnEmptyMarkers)
                {
                    marker = em.CreateEntity(ComponentType.ReadWrite<LocalTransform>());
                    em.SetComponentData(marker, LocalTransform.FromPositionRotationScale(p, quaternion.identity, 1f));
                    runtime.Markers.Add(marker);
                    SpawnGlow(em, p, runtime, marker);
                }
                else
                {
                    SpawnGlow(em, p, runtime);
                }
            }
        }

        private static void RotateZone(ZoneRuntime zr, bool force)
        {
            if (!zr.Entry.Rotation.Enabled && !force) return;

            // EntitySpawner not available - use fallback rotation method
            if (zr.ResolvedPrefabs.Length == 0)
            {
                CoreLogger.Warning("No resolved prefabs for rotation", ServiceName);
                return;
            }

            zr.ActivePrefabIndex = (zr.ActivePrefabIndex + 1) % zr.ResolvedPrefabs.Length;
            zr.NextRotationUtc = ComputeNextRotation(zr.Entry);

            var em = UnifiedCore.EntityManager;
            DestroyEntities(zr.Glows);
            zr.Glows.Clear();

            var positions = zr.Markers.Count > 0
                ? zr.Markers.Select(m => em.GetComponentData<LocalTransform>(m).Position).ToList()
                : GeneratePoints(zr.Entry);

            foreach (var pos in positions)
            {
                var index = positions.IndexOf(pos);
                if (zr.Markers.Count > index)
                {
                    SpawnGlow(em, pos, zr, zr.Markers[index]);
                }
                else
                {
                    SpawnGlow(em, pos, zr);
                }
            }
            
            CoreLogger.Info($"Rotated zone to prefab index {zr.ActivePrefabIndex}", ServiceName);
        }

        // Spawns or attaches a glow effect for a given position/marker
        // Falls back to carpet spawn + attach when glow prefabs fail
        private static void SpawnGlow(EntityManager em, float3 position, ZoneRuntime runtime, Entity? attachTo = null)
        {
            if (runtime.ResolvedPrefabs.Length == 0)
            {
                CoreLogger.Warning("No resolved prefabs for glow spawning", ServiceName);
                return;
            }

            var prefabGuid = runtime.ResolvedPrefabs[runtime.ActivePrefabIndex % runtime.ResolvedPrefabs.Length];

            if (attachTo != null && em.Exists(attachTo.Value))
            {
                if (TryAttachGlowToMarker(em, attachTo.Value, prefabGuid, runtime))
                {
                    CoreLogger.Debug($"Attached glow to marker at ({position.x:F0}, {position.z:F0})", ServiceName);
                    return;
                }
                if (TrySpawnGlowDirect(em, position, prefabGuid, runtime))
                {
                    return;
                }
            }
            else
            {
                if (TrySpawnGlowDirect(em, position, prefabGuid, runtime))
                {
                    return;
                }
            }

            var carpetPrefab = GetCarpetPrefab();
            if (!carpetPrefab.IsEmpty() && TrySpawnGlowOnCarpet(em, position, prefabGuid, carpetPrefab, runtime))
            {
                CoreLogger.Debug($"Spawned glow on carpet fallback at ({position.x:F0}, {position.z:F0})", ServiceName);
                return;
            }

            CreateEmptyMarker(em, position, runtime);
            CoreLogger.Warning($"Created empty marker at ({position.x:F0}, {position.z:F0})", ServiceName);
        }

        private static bool TrySpawnGlowDirect(EntityManager em, float3 position, PrefabGUID prefabGuid, ZoneRuntime runtime)
        {
            if (!UnifiedCore.TryGetPrefabEntity(prefabGuid, out var prefabEntity))
            {
                return false;
            }

            try
            {
                var glowEntity = em.Instantiate(prefabEntity);
                SetEntityPosition(em, glowEntity, position);
                runtime.Glows.Add(glowEntity);
                return true;
            }
            catch (Exception ex)
            {
                CoreLogger.Exception(ex, ServiceName);
                return false;
            }
        }

        private static bool TryAttachGlowToMarker(EntityManager em, Entity marker, PrefabGUID prefabGuid, ZoneRuntime runtime)
        {
            if (!UnifiedCore.TryGetPrefabEntity(prefabGuid, out var prefabEntity))
            {
                return false;
            }

            try
            {
                var glowEntity = em.Instantiate(prefabEntity);
                
                if (em.HasComponent<LocalTransform>(marker))
                {
                    var markerPos = em.GetComponentData<LocalTransform>(marker).Position;
                    SetEntityPosition(em, glowEntity, markerPos);
                }
                
                em.AddComponentData(glowEntity, new Parent { Value = marker });
                em.AddComponent<LocalToParent>(glowEntity);
                
                runtime.Glows.Add(glowEntity);
                return true;
            }
            catch (Exception ex)
            {
                CoreLogger.Exception(ex, ServiceName);
                return false;
            }
        }

        private static bool TrySpawnGlowOnCarpet(EntityManager em, float3 position, PrefabGUID glowGuid, PrefabGUID carpetGuid, ZoneRuntime runtime)
        {
            if (!UnifiedCore.TryGetPrefabEntity(carpetGuid, out var carpetEntity))
            {
                return false;
            }

            try
            {
                var carpet = em.Instantiate(carpetEntity);
                var carpetTransform = LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f);
                em.SetComponentData(carpet, carpetTransform);
                runtime.Markers.Add(carpet);

                if (UnifiedCore.TryGetPrefabEntity(glowGuid, out var glowEntity))
                {
                    var glow = em.Instantiate(glowEntity);
                    SetEntityPosition(em, glow, float3.zero);
                    
                    em.AddComponentData(glow, new Parent { Value = carpet });
                    em.AddComponent<LocalToParent>(glow);
                    runtime.Glows.Add(glow);
                }

                return true;
            }
            catch (Exception ex)
            {
                CoreLogger.Exception(ex, ServiceName);
                return false;
            }
        }

        private static void CreateEmptyMarker(EntityManager em, float3 position, ZoneRuntime runtime)
        {
            var marker = em.CreateEntity(ComponentType.ReadWrite<LocalTransform>());
            em.SetComponentData(marker, LocalTransform.FromPosition(position));
            runtime.Markers.Add(marker);
        }

        private static void SetEntityPosition(EntityManager em, Entity entity, float3 position)
        {
            if (em.HasComponent<LocalTransform>(entity))
            {
                var t = em.GetComponentData<LocalTransform>(entity);
                t.Position = position;
                em.SetComponentData(entity, t);
            }
            else if (em.HasComponent<Translation>(entity))
            {
                var t = em.GetComponentData<Translation>(entity);
                t.Value = position;
                em.SetComponentData(entity, t);
            }
        }

        private static PrefabGUID GetCarpetPrefab()
        {
            return CarpetPrefabGuid;
        }

        private static void AttachGlowToEntity(EntityManager em, Entity target, PrefabGUID prefabGuid, float3 localOffset)
        {
            if (!UnifiedCore.TryGetPrefabEntity(prefabGuid, out var prefabEntity))
            {
                CoreLogger.Warning($"Prefab {prefabGuid.GuidHash} not resolved; cannot attach.", ServiceName);
                return;
            }

            var glowEntity = em.Instantiate(prefabEntity);

            if (em.HasComponent<LocalTransform>(glowEntity))
            {
                var t = em.GetComponentData<LocalTransform>(glowEntity);
                t.Position = localOffset;
                em.SetComponentData(glowEntity, t);
            }

            em.AddComponentData(glowEntity, new Parent { Value = target });
            em.AddComponent<LocalToParent>(glowEntity);

            if (!em.HasComponent<ZoneGlowTag>(glowEntity))
                em.AddComponentData(glowEntity, new ZoneGlowTag { ParentZone = target });

            CoreLogger.Info($"Attached glow prefab {prefabGuid.GuidHash} → parent entity {target.Index}", ServiceName);
        }

        public struct ZoneGlowTag
        {
            public Entity ParentZone;
        }

        private static DateTime ComputeNextRotation(GlowZoneEntry entry)
        {
            if (!entry.Rotation.Enabled || entry.Rotation.IntervalSeconds <= 0) return DateTime.MaxValue;
            return DateTime.UtcNow.AddSeconds(entry.Rotation.IntervalSeconds);
        }

        private static List<float3> GeneratePoints(GlowZoneEntry entry)
        {
            var points = new List<float3>();
            var center = entry.Center;
            var spacing = entry.BorderSpacing <= 0 ? 3f : entry.BorderSpacing;

            if (entry.Radius.HasValue && entry.Radius.Value > 0)
            {
                var r = entry.Radius.Value;
                var count = Math.Max(4, (int)math.floor((2 * math.PI * r) / spacing));
                for (int i = 0; i < count; i++)
                {
                    var angle = (2 * math.PI * i) / count;
                    var p = new float3(center.x + r * math.cos(angle), center.y, center.z + r * math.sin(angle));
                    points.Add(p);
                }
                points.Add(new float3(center.x - r, center.y, center.z - r));
                points.Add(new float3(center.x - r, center.y, center.z + r));
                points.Add(new float3(center.x + r, center.y, center.z - r));
                points.Add(new float3(center.x + r, center.y, center.z + r));
            }
            else if (entry.HalfExtents.HasValue)
            {
                var h = entry.HalfExtents.Value;
                var minX = center.x - h.x;
                var maxX = center.x + h.x;
                var minZ = center.z - h.y;
                var maxZ = center.z + h.y;

                for (float x = minX; x <= maxX; x += spacing) points.Add(new float3(x, center.y, minZ));
                for (float x = minX; x <= maxX; x += spacing) points.Add(new float3(x, center.y, maxZ));
                for (float z = minZ; z <= maxZ; z += spacing) points.Add(new float3(minX, center.y, z));
                for (float z = minZ; z <= maxZ; z += spacing) points.Add(new float3(maxX, center.y, z));

                points.Add(new float3(minX, center.y, minZ));
                points.Add(new float3(minX, center.y, maxZ));
                points.Add(new float3(maxX, center.y, minZ));
                points.Add(new float3(maxX, center.y, maxZ));
            }

            var unique = new List<float3>();
            const float eps = 0.05f;
            foreach (var p in points)
            {
                if (unique.Any(u => math.distance(u, p) < eps)) continue;
                unique.Add(p);
            }
            return unique;
        }

        private static void DestroyEntities(List<Entity> list)
        {
            var em = UnifiedCore.EntityManager;
            foreach (var e in list)
            {
                if (em.Exists(e)) em.DestroyEntity(e);
            }
            list.Clear();
        }

        #region Prefab/Config helpers
        
        private static void LoadConfig()
        {
            try
            {
                _config = ConfigService.GetConfig<GlowZonesConfig>();
                CoreLogger.Debug($"Loaded {_config.Zones.Count} glow zone configurations", ServiceName);
            }
            catch (Exception ex)
            {
                CoreLogger.Exception(ex, ServiceName);
                _config = new GlowZonesConfig();
            }
        }

        private static PrefabGUID[] ResolvePrefabs(GlowZoneEntry entry)
        {
            var names = entry.GlowPrefabs != null && entry.GlowPrefabs.Count > 0
                ? entry.GlowPrefabs
                : new List<string> { _config.DefaultGlowPrefab ?? "Chaos" };

            var resolved = new List<PrefabGUID>();
            var glowService = new GlowService();
            
            foreach (var name in names)
            {
                var prefab = glowService.GetGlowPrefab(name);
                if (!prefab.IsEmpty())
                {
                    resolved.Add(prefab);
                }
                else if (int.TryParse(name, out var intGuid))
                {
                    var guid = new PrefabGUID(intGuid);
                    var prefabSystem = UnifiedCore.PrefabCollection;
                    if (prefabSystem != null)
                    {
                        // Use reflection to access the dictionary
                        var field = typeof(PrefabCollectionSystem).GetField(
                            "_PrefabGuidToEntityDictionary", 
                            BindingFlags.NonPublic | BindingFlags.Instance);
                        if (field != null)
                        {
                            var dictionary = field.GetValue(prefabSystem) as System.Collections.IDictionary;
                            if (dictionary != null && dictionary.Contains(guid))
                            {
                                resolved.Add(guid);
                            }
                        }
                    }
                }
                else if (long.TryParse(name, out var longGuid))
                {
                    var guid = new PrefabGUID((int)longGuid);
                    var prefabSystem = UnifiedCore.PrefabCollection;
                    if (prefabSystem != null)
                    {
                        var field = typeof(PrefabCollectionSystem).GetField(
                            "_PrefabGuidToEntityDictionary", 
                            BindingFlags.NonPublic | BindingFlags.Instance);
                        if (field != null)
                        {
                            var dictionary = field.GetValue(prefabSystem) as System.Collections.IDictionary;
                            if (dictionary != null && dictionary.Contains(guid))
                            {
                                resolved.Add(guid);
                            }
                        }
                    }
                }
            }
            
            if (resolved.Count == 0)
            {
                var fallbackPrefab = new GlowService().GetGlowPrefab("Chaos");
                if (!fallbackPrefab.IsEmpty())
                {
                    resolved.Add(fallbackPrefab);
                }
            }
            
            CoreLogger.Debug($"Resolved {resolved.Count} prefabs for zone", ServiceName);
            return resolved.ToArray();
        }
        
        #endregion
    }
}
