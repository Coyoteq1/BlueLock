using System;
using System.Collections.Generic;
using System.Linq;
using ProjectM;
using Stunlock.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VAuto.Zone.Core;
using VAuto.Zone.Models;

namespace VAuto.Zone.Services
{
    public static class GlowTileService
    {
        private const string TemplateType = "glowTM";
        private const int MaxTileEntities = 500;

        public static TemplateSpawnResult SpawnGlowTiles(string zoneId, EntityManager em)
        {
            var result = new TemplateSpawnResult
            {
                ZoneId = zoneId,
                TemplateType = TemplateType
            };

            if (em == default || em.World == null || !em.World.IsCreated)
            {
                result.Error = "EntityManager world not ready";
                return result;
            }

            var zone = ZoneConfigService.GetZoneById(zoneId);
            if (zone == null)
            {
                result.Error = $"Zone '{zoneId}' not found";
                return result;
            }

            if (!zone.GlowTileEnabled)
            {
                result.Error = "Glow tiles are disabled for this zone";
                return result;
            }

            if (ZoneTemplateRegistry.IsSpawned(zoneId, TemplateType))
            {
                result.Success = true;
                result.Status = "AlreadySpawned";
                return result;
            }

            if (!TryResolveGlowPrefab(zone, out var prefabEntity, out var prefabToken))
            {
                result.Error = $"Glow tile prefab not configured or unavailable (zone={zone.Id}, glowTilePrefab='{zone.GlowTilePrefab}', glowTilePrefabId={zone.GlowTilePrefabId}, glowPrefab='{zone.GlowPrefab}', glowPrefabId={zone.GlowPrefabId})";
                return result;
            }

            var centerY = zone.CenterY + zone.GlowTileHeightOffset;
            var positions = GenerateGlowTilePositions(zone);
            if (positions.Count == 0)
            {
                result.Error = "No glow tile positions generated";
                return result;
            }

            var spawned = new List<Entity>();
            foreach (var (x, y, z) in positions)
            {
                var point = new float3(x, y, z);
                var entity = em.Instantiate(prefabEntity);
                if (entity == Entity.Null || !em.Exists(entity))
                {
                    continue;
                }

                TrySetTranslation(em, entity, point);
                spawned.Add(entity);
            }

            if (spawned.Count == 0)
            {
                result.Error = "Failed to spawn glow tiles";
                return result;
            }

            result.Entities = spawned;
            result.TemplateName = prefabToken;
            result.Success = true;

            var metadata = new TemplateSpawnMetadata
            {
                SpawnedAt = DateTime.UtcNow,
                EntityCount = spawned.Count,
                TemplateName = prefabToken,
                OriginPosition = new float3(zone.CenterX, centerY, zone.CenterZ),
                Rotation = quaternion.identity
            };

            if (!ZoneTemplateRegistry.RegisterEntities(zoneId, TemplateType, spawned, metadata))
            {
                foreach (var entity in spawned)
                {
                    if (em.Exists(entity))
                    {
                    em.DestroyEntity(entity);
                    }
                }

                result.Success = false;
                result.Error = "Glow tile spawn would exceed registry limits";
                result.Entities.Clear();
            }

            return result;
        }

        public static TemplateSpawnResult TryAutoSpawnGlowTiles(ZoneDefinition zone, EntityManager em)
        {
            if (zone == null)
            {
                return new TemplateSpawnResult { Success = false, Error = "Zone definition missing" };
            }

            if (!zone.GlowTileEnabled)
            {
                return new TemplateSpawnResult { Success = false, Error = $"Glow tiles disabled for zone '{zone.Id}'" };
            }

            if (!zone.GlowTileAutoSpawnOnEnter)
            {
                return new TemplateSpawnResult { Success = false, Error = $"Auto spawn disabled for zone '{zone.Id}'" };
            }

            return SpawnGlowTiles(zone.Id, em);
        }

        public static int ClearGlowTiles(string zoneId, EntityManager em)
        {
            if (em == default || em.World == null || !em.World.IsCreated)
            {
                return 0;
            }

            if (!ZoneTemplateRegistry.TryGetEntities(zoneId, TemplateType, out var entities))
            {
                return 0;
            }

            var toDestroy = entities.ToList();
            var destroyed = 0;
            foreach (var entity in toDestroy)
            {
                if (em.Exists(entity))
                {
                    em.DestroyEntity(entity);
                    destroyed++;
                }
            }

            ZoneTemplateRegistry.ClearZoneType(zoneId, TemplateType);
            return destroyed;
        }

        public static void PrepareForZoneActivation(string zoneId, EntityManager em)
        {
            if (string.IsNullOrWhiteSpace(zoneId))
            {
                return;
            }

            ClearGlowTiles(zoneId, em);
        }

        public static void ClearZoneGlow(string zoneId, EntityManager em)
        {
            ClearGlowTiles(zoneId, em);
        }

        public static bool IsGlowTilesSpawned(string zoneId) => ZoneTemplateRegistry.IsSpawned(zoneId, TemplateType);

        public static int GetGlowTileCount(string zoneId) => ZoneTemplateRegistry.GetEntityCount(zoneId, TemplateType);

        private static bool TryResolveGlowPrefab(ZoneDefinition zone, out Entity prefabEntity, out string prefabToken)
        {
            prefabEntity = Entity.Null;
            prefabToken = string.Empty;

            if (TryResolveByGuid(zone.GlowTilePrefabId, out prefabEntity, out prefabToken))
            {
                return true;
            }

            if (TryResolveByName(zone.GlowTilePrefab, out prefabEntity, out prefabToken))
            {
                return true;
            }

            if (TryResolveByGuid(zone.GlowPrefabId, out prefabEntity, out prefabToken))
            {
                return true;
            }

            if (TryResolveByName(zone.GlowPrefab, out prefabEntity, out prefabToken))
            {
                return true;
            }

            // Final fallback: attach glow tiles to effective zone border prefab configuration.
            var effectiveBorder = ZoneConfigService.GetEffectiveBorderConfig(zone);
            if (effectiveBorder != null)
            {
                if (TryResolveByGuid(effectiveBorder.PrefabGuid, out prefabEntity, out prefabToken))
                {
                    prefabToken = "borderGuid:" + effectiveBorder.PrefabGuid;
                    return true;
                }

                if (TryResolveByName(effectiveBorder.PrefabName, out prefabEntity, out prefabToken))
                {
                    prefabToken = "borderName:" + effectiveBorder.PrefabName;
                    return true;
                }
            }

            return false;
        }

        private static bool TryResolveByGuid(int guidHash, out Entity prefabEntity, out string prefabToken)
        {
            prefabEntity = Entity.Null;
            prefabToken = string.Empty;
            if (guidHash == 0)
            {
                return false;
            }

            var guid = new PrefabGUID(guidHash);
            if (!ZoneCore.TryGetPrefabEntity(guid, out var resolved) || resolved == Entity.Null)
            {
                return false;
            }

            prefabEntity = resolved;
            prefabToken = "GUID:" + guidHash;
            return true;
        }

        private static bool TryResolveByName(string prefabName, out Entity prefabEntity, out string prefabToken)
        {
            prefabEntity = Entity.Null;
            prefabToken = string.Empty;
            if (string.IsNullOrWhiteSpace(prefabName))
            {
                return false;
            }

            var token = prefabName.Trim();
            if (!ZoneCore.TryResolvePrefabEntity(token, out var resolvedGuid, out var resolvedEntity) || resolvedEntity == Entity.Null)
            {
                return false;
            }

            prefabEntity = resolvedEntity;
            prefabToken = token;
            return true;
        }

        public static List<(float x, float y, float z)> GenerateGlowTilePositions(ZoneDefinition zone)
        {
            if (zone == null)
            {
                return new List<(float x, float y, float z)>();
            }

            var centerY = zone.CenterY + zone.GlowTileHeightOffset;
            var positions = GlowTileGeometry.GeneratePoints(
                zone.CenterX,
                centerY,
                zone.CenterZ,
                zone.Radius,
                zone.GlowTileSpacing,
                zone.GlowTileRotationDegrees);

            if (positions.Count > MaxTileEntities)
            {
                positions = positions.Take(MaxTileEntities).ToList();
            }

            return positions;
        }

        private static void TrySetTranslation(EntityManager em, Entity entity, float3 position)
        {
            if (entity == Entity.Null || em == default || !em.Exists(entity))
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
    }
}
