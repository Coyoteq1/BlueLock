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
    /// <summary>
    /// Native schematic spawner for zone enter flow.
    /// </summary>
    public static class SchematicZoneService
    {
        public static TemplateSpawnResult ApplySchematicOnEnter(Entity player, string zoneId, string schematicName, EntityManager em)
        {
            var schematic = ZoneSchematicLoader.LoadSchematic(schematicName);
            if (schematic == null || schematic.Entities == null || schematic.Entities.Count == 0)
            {
                return new TemplateSpawnResult
                {
                    Success = false,
                    Error = $"Schematic '{schematicName}' not found or invalid.",
                    ZoneId = zoneId,
                    TemplateName = schematicName
                };
            }

            var spawnedEntities = new List<Entity>();
            var origin = GetZoneOrigin(zoneId);
            var ordered = OrderByDependencies(schematic.Entities);
            foreach (var entityDef in ordered)
            {
                var spawned = SpawnSchematicEntity(entityDef, origin, em);
                if (spawned != Entity.Null)
                {
                    spawnedEntities.Add(spawned);
                }
            }

            return new TemplateSpawnResult
            {
                Success = spawnedEntities.Count > 0,
                ZoneId = zoneId,
                TemplateName = schematicName,
                Entities = spawnedEntities
            };
        }

        private static Entity SpawnSchematicEntity(SchematicEntity entityDef, float3 zoneOrigin, EntityManager em)
        {
            var prefab = ResolvePrefab(entityDef);
            if (prefab == Entity.Null)
            {
                return Entity.Null;
            }

            Entity entity;
            try
            {
                entity = em.Instantiate(prefab);
            }
            catch
            {
                return Entity.Null;
            }

            if (entity == Entity.Null || !em.Exists(entity))
            {
                return Entity.Null;
            }

            TrySetPosition(em, entity, zoneOrigin + entityDef.Position);
            TrySetRotation(em, entity, entityDef.Rotation);
            ApplyTeamHeartOrNeutral(entityDef, entity, em);
            return entity;
        }

        private static Entity ResolvePrefab(SchematicEntity entityDef)
        {
            if (entityDef.PrefabId != 0 &&
                ZoneCore.TryGetPrefabEntity(new PrefabGUID(entityDef.PrefabId), out var byId) &&
                byId != Entity.Null)
            {
                return byId;
            }

            if (!string.IsNullOrWhiteSpace(entityDef.PrefabName) &&
                ZoneCore.TryResolvePrefabEntity(entityDef.PrefabName, out _, out var byName) &&
                byName != Entity.Null)
            {
                return byName;
            }

            return Entity.Null;
        }

        private static List<SchematicEntity> OrderByDependencies(List<SchematicEntity> entities)
        {
            var pending = new List<(int Index, SchematicEntity Entity)>();
            for (var i = 0; i < entities.Count; i++)
            {
                pending.Add((i, entities[i]));
            }

            var resolved = new HashSet<int>();
            var ordered = new List<SchematicEntity>(entities.Count);

            while (pending.Count > 0)
            {
                var progressed = false;
                for (var i = pending.Count - 1; i >= 0; i--)
                {
                    var item = pending[i];
                    var deps = NormalizeDependencies(item.Entity.Dependencies, entities.Count);
                    if (deps.All(d => resolved.Contains(d)))
                    {
                        ordered.Add(item.Entity);
                        resolved.Add(item.Index);
                        pending.RemoveAt(i);
                        progressed = true;
                    }
                }

                if (!progressed)
                {
                    ordered.AddRange(pending.Select(p => p.Entity));
                    break;
                }
            }

            return ordered;
        }

        private static List<int> NormalizeDependencies(List<int> dependencies, int count)
        {
            var result = new List<int>();
            if (dependencies == null || dependencies.Count == 0)
            {
                return result;
            }

            foreach (var dep in dependencies)
            {
                if (dep >= 0 && dep < count)
                {
                    result.Add(dep);
                    continue;
                }

                var oneBased = dep - 1;
                if (oneBased >= 0 && oneBased < count)
                {
                    result.Add(oneBased);
                }
            }

            return result;
        }

        private static void ApplyTeamHeartOrNeutral(SchematicEntity entityDef, Entity entity, EntityManager em)
        {
            if (entityDef.TeamHeart.HasValue && entityDef.TeamHeart.Value > 0)
            {
                ZoneCore.LogDebug($"[SchematicZoneService] TeamHeart '{entityDef.TeamHeart.Value}' requested; using neutral fallback.");
            }

            if (em.HasComponent<UserOwner>(entity))
            {
                em.RemoveComponent<UserOwner>(entity);
            }

            if (em.HasComponent<TeamReference>(entity))
            {
                em.RemoveComponent<TeamReference>(entity);
            }

            if (em.HasComponent<Team>(entity))
            {
                em.RemoveComponent<Team>(entity);
            }
        }

        private static void TrySetPosition(EntityManager em, Entity entity, float3 position)
        {
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

        private static void TrySetRotation(EntityManager em, Entity entity, float3 rotationDegrees)
        {
            if (math.lengthsq(rotationDegrees) <= float.Epsilon)
            {
                return;
            }

            var rotation = quaternion.Euler(math.radians(rotationDegrees));
            if (em.HasComponent<Rotation>(entity))
            {
                em.SetComponentData(entity, new Rotation { Value = rotation });
            }
        }

        private static float3 GetZoneOrigin(string zoneId)
        {
            var zone = ZoneConfigService.GetZoneById(zoneId);
            return zone != null ? new float3(zone.CenterX, zone.CenterY, zone.CenterZ) : float3.zero;
        }
    }
}
