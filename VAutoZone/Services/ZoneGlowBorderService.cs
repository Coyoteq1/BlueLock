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
using VAuto.Zone.Models;
using VAuto.Zone.Services;
using VAuto.Core;

namespace VAuto.Zone.Services
{
    public static class ZoneGlowBorderService
    {
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
        }

        #region Public API
        public static void BuildAll(bool rebuild = false)
        {
            VRCore.Initialize();
            if (VRCore.ServerWorld == null) return;

            LoadConfig();
            if (rebuild) ClearAll();

            foreach (var zone in _config.Zones.Where(z => z.Enabled))
            {
                BuildZone(zone);
            }
        }

        public static void ClearAll()
        {
            foreach (var zr in _zones.Values)
            {
                DestroyEntities(zr.Markers);
                DestroyEntities(zr.Glows);
            }
            _zones.Clear();
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
            foreach (var kvp in _zones)
            {
                var z = kvp.Value;
                yield return $"{kvp.Key}: markers={z.Markers.Count} glows={z.Glows.Count} prefabIndex={z.ActivePrefabIndex} rotation={(z.Entry.Rotation.Enabled ? "on" : "off")}";
            }
        }
        #endregion

        private static void BuildZone(GlowZoneEntry entry)
        {
            if (string.IsNullOrWhiteSpace(entry.Id)) return;
            var em = VRCore.EntityManager;
            if (em == default) return;

            var runtime = _zones.ContainsKey(entry.Id) ? _zones[entry.Id] : new ZoneRuntime();
            runtime.Entry = entry;

            if (runtime.Markers.Count > 0 || runtime.Glows.Count > 0)
            {
                DestroyEntities(runtime.Markers);
                DestroyEntities(runtime.Glows);
            }

            runtime.ResolvedPrefabs = ResolvePrefabs(entry);
            runtime.ActivePrefabIndex = 0;
            runtime.NextRotationUtc = ComputeNextRotation(entry);

            var points = GeneratePoints(entry);
            foreach (var p in points)
            {
                Entity marker = Entity.Null;
                if (entry.SpawnEmptyMarkers)
                {
                    marker = em.CreateEntity(ComponentType.ReadWrite<LocalTransform>());
                    em.SetComponentData(marker, LocalTransform.FromPositionRotationScale(p, quaternion.identity, 1f));
                    runtime.Markers.Add(marker);

                    // Attach glow directly onto marker entity
                    SpawnGlow(em, p, runtime, marker);
                }
                else
                {
                    // No marker requested → free‑floating glow
                    SpawnGlow(em, p, runtime);
                }
            }

            _zones[entry.Id] = runtime;
        }

        private static void RotateZone(ZoneRuntime zr, bool force)
        {
            if (!zr.Entry.Rotation.Enabled && !force) return;
            if (zr.ResolvedPrefabs.Length == 0) return;

            zr.ActivePrefabIndex = (zr.ActivePrefabIndex + 1) % zr.ResolvedPrefabs.Length;
            zr.NextRotationUtc = ComputeNextRotation(zr.Entry);

            var em = VRCore.EntityManager;
            DestroyEntities(zr.Glows);
            zr.Glows.Clear();

            // Use existing marker positions; if no markers, regenerate points from zone entry.
            var positions = zr.Markers.Count > 0
                ? zr.Markers.Select(m => em.GetComponentData<LocalTransform>(m).Position).ToList()
                : GeneratePoints(zr.Entry);

            foreach (var pos in positions)
            {
                // If markers exist, attach to them; otherwise spawn free-floating
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
        }

        // Spawns or attaches a glow effect for a given position/marker
        private static void SpawnGlow(EntityManager em, float3 position, ZoneRuntime runtime, Entity? attachTo = null)
        {
            if (runtime.ResolvedPrefabs.Length == 0) return;

            var prefabGuid = runtime.ResolvedPrefabs[runtime.ActivePrefabIndex % runtime.ResolvedPrefabs.Length];

            // If attachTo provided, try attaching instead of free spawn
            if (attachTo.HasValue)
            {
                AttachGlowToEntity(em, attachTo.Value, prefabGuid, float3.zero);
                return;
            }

            // --- Standard free‑floating glow fallback ---
            if (!TryGetPrefabEntity(prefabGuid, out var prefabEntity))
            {
                Plugin.Log.LogWarning($"[GlowZone] Prefab {prefabGuid.GuidHash} not found; creating placeholder glow entity.");
                var fallback = em.CreateEntity(ComponentType.ReadWrite<LocalTransform>());
                em.SetComponentData(fallback, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
                runtime.Glows.Add(fallback);
                return;
            }

            var glowEntity = em.Instantiate(prefabEntity);

            // Ensure position is set
            if (em.HasComponent<LocalTransform>(glowEntity))
            {
                var t = em.GetComponentData<LocalTransform>(glowEntity);
                t.Position = position;
                em.SetComponentData(glowEntity, t);
            }
            else if (em.HasComponent<Translation>(glowEntity))
            {
                var t = em.GetComponentData<Translation>(glowEntity);
                t.Value = position;
                em.SetComponentData(glowEntity, t);
            }

            runtime.Glows.Add(glowEntity);
        }

        // Helper: attaches existing prefab as child of target entity
        private static void AttachGlowToEntity(EntityManager em, Entity target, PrefabGUID prefabGuid, float3 localOffset)
        {
            if (!TryGetPrefabEntity(prefabGuid, out var prefabEntity))
            {
                Plugin.Log.LogWarning($"[GlowZone] Prefab {prefabGuid.GuidHash} not resolved; cannot attach.");
                return;
            }

            var glowEntity = em.Instantiate(prefabEntity);

            // Offset relative to parent
            if (em.HasComponent<LocalTransform>(glowEntity))
            {
                var t = em.GetComponentData<LocalTransform>(glowEntity);
                t.Position = localOffset;
                em.SetComponentData(glowEntity, t);
            }

            // Establish parent/child relationship
            em.AddComponentData(glowEntity, new Parent { Value = target });
            em.AddComponent<LocalToParent>(glowEntity);

            // Optional custom tag for later cleanup
            if (!em.HasComponent<ZoneGlowTag>(glowEntity))
                em.AddComponentData(glowEntity, new ZoneGlowTag { ParentZone = target });

            Plugin.Log.LogInfo($"[GlowZone] Attached glow prefab {prefabGuid.GuidHash} → parent entity {target.Index}");
        }

        // Optional tag for tracking glow entities
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
                // Corners of the bounding square
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

                // Bottom edge
                for (float x = minX; x <= maxX; x += spacing) points.Add(new float3(x, center.y, minZ));
                // Top edge
                for (float x = minX; x <= maxX; x += spacing) points.Add(new float3(x, center.y, maxZ));
                // Left edge
                for (float z = minZ; z <= maxZ; z += spacing) points.Add(new float3(minX, center.y, z));
                // Right edge
                for (float z = minZ; z <= maxZ; z += spacing) points.Add(new float3(maxX, center.y, z));

                points.Add(new float3(minX, center.y, minZ));
                points.Add(new float3(minX, center.y, maxZ));
                points.Add(new float3(maxX, center.y, minZ));
                points.Add(new float3(maxX, center.y, maxZ));
            }

            // De-dup
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
            var em = VRCore.EntityManager;
            foreach (var e in list)
            {
                if (em.Exists(e)) em.DestroyEntity(e);
            }
            list.Clear();
        }

        #region Prefab/Config helpers
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
                    if (VRCore.PrefabCollection._PrefabGuidToEntityMap.ContainsKey(guid))
                    {
                        resolved.Add(guid);
                    }
                }
                else if (long.TryParse(name, out var longGuid))
                {
                    var guid = new PrefabGUID((int)longGuid);
                    if (VRCore.PrefabCollection._PrefabGuidToEntityMap.ContainsKey(guid))
                    {
                        resolved.Add(guid);
                    }
                }
            }
            
            // Fallback to Chaos if no valid prefabs found
            if (resolved.Count == 0)
            {
                var fallbackPrefab = glowService.GetGlowPrefab("Chaos");
                if (!fallbackPrefab.IsEmpty())
                {
                    resolved.Add(fallbackPrefab);
                }
            }
            
            return resolved.ToArray();
        }

        private static bool TryGetPrefabEntity(PrefabGUID guid, out Entity prefabEntity)
        {
            prefabEntity = Entity.Null;
            var em = VRCore.EntityManager;
            if (em == default) return false;

            try
            {
                var system = VRCore.ServerWorld?.GetExistingSystemManaged<PrefabCollectionSystem>();
                if (system == null) return false;
                if (system._PrefabGuidToEntityMap.TryGetValue(guid, out prefabEntity))
                {
                    return true;
                }
            }
            catch
            {
                return false;
            }
            return false;
        }

        private static void LoadConfig()
        {
            var path = GetPreferredConfigPath();
            try
            {
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    _config = JsonSerializer.Deserialize<GlowZonesConfig>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true
                    }) ?? new GlowZonesConfig();
                    return;
                }
            }
            catch
            {
                // ignore and fallback to defaults
            }
            _config = new GlowZonesConfig();
        }

        public static string GetPreferredConfigPath()
        {
            var configDir = Path.Combine(BepInEx.Paths.ConfigPath, "VAuto.Arena");
            var primary = Path.Combine(configDir, ConfigFileName);
            if (File.Exists(primary)) return primary;

            var asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
            var fallback = Path.Combine(asmDir, "config", "VAuto.Arena", ConfigFileName);
            return fallback;
        }
        #endregion
    }
}
