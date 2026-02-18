using System;
using System.Collections.Generic;
using ProjectM;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VAuto.Zone.Core;
using VAuto.Zone.Models;
using VAuto.Zone.Services;
using Stunlock.Core;

namespace VAuto.Zone.Services
{
    public static class GlowTileService
    {
        private static readonly Dictionary<string, List<Entity>> SpawnedGlowTiles = new(StringComparer.OrdinalIgnoreCase);

        public static void PrepareForZoneActivation(string zoneId, EntityManager em)
        {
            ClearZoneGlow(zoneId, em);
        }

        public static TemplateSpawnResult TryAutoSpawnGlowTiles(ZoneDefinition zone, EntityManager em)
        {
            if (zone == null)
            {
                return TemplateFailure(zone?.Id ?? string.Empty, "Zone definition missing");
            }

            if (!zone.GlowTileEnabled)
            {
                return TemplateFailure(zone.Id, "Glow tiles disabled");
            }

            return SpawnGlowTiles(zone.Id, em);
        }

        public static TemplateSpawnResult SpawnGlowTiles(string zoneId, EntityManager em)
        {
            if (string.IsNullOrWhiteSpace(zoneId))
            {
                return TemplateFailure(string.Empty, "Zone id missing");
            }

            var zone = ZoneConfigService.GetZoneById(zoneId);
            if (zone == null)
            {
                return TemplateFailure(zoneId, "Zone not found");
            }

            if (!zone.GlowTileEnabled)
            {
                return TemplateFailure(zone.Id, "Glow tiles disabled");
            }

            var prefab = ResolveGlowPrefab(zone, out var resolveError);
            if (prefab == Entity.Null)
            {
                return TemplateFailure(zone.Id, $"Glow prefab unavailable: {resolveError}");
            }

            var spacing = zone.GlowTileSpacing > 0 ? zone.GlowTileSpacing : 3f;
            var borderPoints = GlowTileGeometry.GetZoneBorderPoints(zone, spacing);
            if (borderPoints.Count == 0)
            {
                return TemplateFailure(zone.Id, "No border points generated");
            }

            var height = zone.CenterY + zone.GlowSpawnHeight + zone.GlowTileHeightOffset;
            ClearZoneGlow(zone.Id, em);

            var spawned = new List<Entity>();
            foreach (var point in borderPoints)
            {
                try
                {
                    var entity = em.Instantiate(prefab);
                    if (entity == Entity.Null || !em.Exists(entity))
                    {
                        continue;
                    }

                    SetTranslation(em, entity, new float3(point.x, height, point.y));
                    spawned.Add(entity);
                }
                catch
                {
                    continue;
                }
            }

            if (spawned.Count > 0)
            {
                SpawnedGlowTiles[zone.Id] = spawned;
            }

            return new TemplateSpawnResult
            {
                Success = spawned.Count > 0,
                ZoneId = zone.Id,
                TemplateName = "GlowTiles",
                Entities = spawned,
                Status = spawned.Count > 0 ? "Glow border spawned" : "No entities spawned"
            };
        }

        public static int ClearGlowTiles(string zoneId, EntityManager em)
        {
            return RemoveSpawned(zoneId, em);
        }

        public static TemplateSpawnResult ClearZoneGlow(string zoneId, EntityManager em)
        {
            var removed = RemoveSpawned(zoneId, em);
            return new TemplateSpawnResult
            {
                Success = true,
                ZoneId = zoneId,
                TemplateName = "GlowTiles",
                Entities = new List<Entity>(),
                Status = removed > 0 ? "Glow cleared" : "No glow to clear"
            };
        }

        private static int RemoveSpawned(string zoneId, EntityManager em)
        {
            if (string.IsNullOrWhiteSpace(zoneId) || !SpawnedGlowTiles.TryGetValue(zoneId, out var entities) || entities.Count == 0)
            {
                return 0;
            }

            var destroyed = 0;
            foreach (var entity in entities)
            {
                if (entity == Entity.Null || !em.Exists(entity))
                {
                    continue;
                }

                try
                {
                    em.DestroyEntity(entity);
                    destroyed++;
                }
                catch
                {
                    // ignore individual destroy errors
                }
            }

            SpawnedGlowTiles.Remove(zoneId);
            return destroyed;
        }

        private static Entity ResolveGlowPrefab(ZoneDefinition zone, out string error)
        {
            error = string.Empty;
            if (zone == null)
            {
                error = "Zone definition missing";
                return Entity.Null;
            }

            if (!string.IsNullOrWhiteSpace(zone.GlowTilePrefab))
            {
                if (ZoneCore.TryResolvePrefabEntity(zone.GlowTilePrefab, out _, out var prefab) && prefab != Entity.Null)
                {
                    return prefab;
                }

                error = $"Name '{zone.GlowTilePrefab}' not resolvable";
            }

            return ZoneCore.TryResolvePrefabEntity("PurpleCarpetsBuildMenuGroup01", out _, out var fallbackEntity) && fallbackEntity != Entity.Null
                ? fallbackEntity
                : Entity.Null;
        }

        private static void SetTranslation(EntityManager em, Entity entity, float3 position)
        {
            if (!em.Exists(entity))
            {
                return;
            }

            if (em.HasComponent<LocalTransform>(entity))
            {
                var transform = em.GetComponentData<LocalTransform>(entity);
                transform.Position = position;
                em.SetComponentData(entity, transform);
            }
            else if (em.HasComponent<Translation>(entity))
            {
                var translation = em.GetComponentData<Translation>(entity);
                translation.Value = position;
                em.SetComponentData(entity, translation);
            }
        }

        private static TemplateSpawnResult TemplateFailure(string zoneId, string error)
        {
            return new TemplateSpawnResult
            {
                Success = false,
                ZoneId = zoneId,
                TemplateName = "GlowTiles",
                Error = error
            };
        }
    }
}
