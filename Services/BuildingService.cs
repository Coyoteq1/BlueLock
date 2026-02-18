using System;
using System.Collections.Generic;
using ProjectM;
using Stunlock.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VAuto.Zone.Core;
using VAuto.Zone.Models;

namespace VAuto.Zone.Services
{
    public sealed class BuildingService
    {
        public static BuildingService Instance { get; } = new BuildingService();

        private BuildingService()
        {
        }

        public TemplateLoadResult LoadTemplate(string templateName, EntityManager em, float3 origin, float rotationDegrees = 0f)
        {
            var result = new TemplateLoadResult();
            if (em == default || em.World == null || !em.World.IsCreated)
            {
                result.Error = "EntityManager world not ready";
                return result;
            }

            if (!TemplateRepository.TryLoadTemplate(templateName, out var template))
            {
                result.Error = $"Template '{templateName}' not found";
                return result;
            }

            var spawned = new List<Entity>();
            var rotationRadians = math.radians(rotationDegrees);
            var cos = math.cos(rotationRadians);
            var sin = math.sin(rotationRadians);

            foreach (var entry in template.Entities)
            {
                var prefabEntity = ResolvePrefab(em, entry);
                if (prefabEntity == Entity.Null)
                {
                    ZoneCore.LogWarning($"[BuildingService] Template '{templateName}' prefab '{entry.PrefabName ?? string.Empty}' ({entry.PrefabGuid}) unavailable.");
                    continue;
                }

                var offset = entry.Offset;
                if (math.abs(rotationRadians) > float.Epsilon)
                {
                    offset = new float3(
                        offset.x * cos - offset.z * sin,
                        offset.y,
                        offset.x * sin + offset.z * cos);
                }

                var spawnPos = origin + offset;
                var entity = em.Instantiate(prefabEntity);
                if (entity == Entity.Null || !em.Exists(entity))
                {
                    ZoneCore.LogWarning($"[BuildingService] Template '{templateName}' instantiation failed for prefab {prefabEntity.Index}:{prefabEntity.Version}");
                    continue;
                }

                TrySetTranslation(em, entity, spawnPos);
                TrySetRotation(em, entity, math.radians(entry.RotationDegrees + rotationDegrees));

                spawned.Add(entity);
            }

            result.Success = true;
            result.Entities = spawned;
            return result;
        }

        public TemplateSpawnResult SpawnTemplateNeutral(
            string templateName,
            EntityManager em,
            float3 origin,
            float rotationDegrees = 0f)
        {
            var result = new TemplateSpawnResult
            {
                TemplateName = templateName,
                ZoneId = string.Empty,
                TemplateType = string.Empty
            };

            var loadResult = LoadTemplate(templateName, em, origin, rotationDegrees);
            if (!loadResult.Success)
            {
                result.Error = loadResult.Error;
                return result;
            }

            foreach (var entity in loadResult.Entities)
            {
                MakeNeutral(entity, em);
            }

            result.Success = true;
            result.Entities = loadResult.Entities;
            return result;
        }

        private static void MakeNeutral(Entity entity, EntityManager em)
        {
            if (entity == Entity.Null || em == default)
            {
                return;
            }

            if (!em.Exists(entity))
            {
                return;
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

        public TemplateValidationResult ValidateTemplate(string templateName, EntityManager em)
        {
            var result = new TemplateValidationResult();

            if (!TemplateRepository.TryLoadTemplate(templateName, out var template))
            {
                result.IsValid = false;
                result.MissingPrefabs.Add($"Template '{templateName}' not found");
                return result;
            }

            result.TotalEntities = template.Entities.Count;

            foreach (var entry in template.Entities)
            {
                var prefabEntity = ResolvePrefab(em, entry);
                if (prefabEntity == Entity.Null)
                {
                    result.MissingPrefabs.Add($"{entry.PrefabName} (GUID: {entry.PrefabGuid})");
                }
            }

            result.IsValid = result.MissingPrefabs.Count == 0;
            return result;
        }

        private static Entity ResolvePrefab(EntityManager em, TemplateEntityEntry entry)
        {
            if (entry.PrefabGuid != 0 && ZoneCore.TryGetPrefabEntity(new PrefabGUID(entry.PrefabGuid), out var guidEntity) && guidEntity != Entity.Null)
            {
                return guidEntity;
            }

            if (!string.IsNullOrWhiteSpace(entry.PrefabName) &&
                ZoneCore.TryResolvePrefabEntity(entry.PrefabName, out var resolvedGuid, out var resolvedEntity) &&
                resolvedEntity != Entity.Null)
            {
                return resolvedEntity;
            }

            return Entity.Null;
        }

        private static void TrySetTranslation(EntityManager em, Entity entity, float3 position)
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

        private static void TrySetRotation(EntityManager em, Entity entity, float rotationRadians)
        {
            if (!em.Exists(entity) || math.abs(rotationRadians) < float.Epsilon)
            {
                return;
            }

            var rotation = quaternion.RotateY(rotationRadians);
            if (em.HasComponent<Rotation>(entity))
            {
                em.SetComponentData(entity, new Rotation { Value = rotation });
            }
            else
            {
                em.AddComponent<Rotation>(entity);
                em.SetComponentData(entity, new Rotation { Value = rotation });
            }
        }
    }

    public sealed class TemplateLoadResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public List<Entity> Entities { get; set; } = new List<Entity>();
    }
}
