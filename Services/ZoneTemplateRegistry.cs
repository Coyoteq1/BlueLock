using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using Unity.Mathematics;

namespace VAuto.Zone.Services
{
    public static class ZoneTemplateRegistry
    {
        private const int MaxEntitiesPerZone = 1000;
        private const int MaxEntitiesPerTemplate = 500;
        private static readonly Dictionary<string, Dictionary<string, List<Entity>>> _zoneTemplateEntities =
            new(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<Entity, (string ZoneId, string TemplateType)> _entityToZoneType =
            new(EntityEqualityComparer.Instance);

        private static readonly Dictionary<string, Dictionary<string, TemplateSpawnMetadata>> _zoneTemplateMetadata =
            new(StringComparer.OrdinalIgnoreCase);

        public static bool RegisterEntities(string zoneId, string templateType, List<Entity> entities, TemplateSpawnMetadata metadata = null)
        {
            if (string.IsNullOrWhiteSpace(zoneId) || string.IsNullOrWhiteSpace(templateType) || entities == null || entities.Count == 0)
            {
                return false;
            }

            var zoneMap = GetOrCreateZone(zoneId);
            var existingZoneTotal = zoneMap.Values.Sum(list => list.Count);
            if (existingZoneTotal + entities.Count > MaxEntitiesPerZone)
            {
                return false;
            }

            var templateList = GetOrCreateTemplate(zoneMap, templateType);
            if (templateList.Count + entities.Count > MaxEntitiesPerTemplate)
            {
                return false;
            }

            templateList.AddRange(entities);
            foreach (var entity in entities)
            {
                _entityToZoneType[entity] = (zoneId, templateType);
            }

            if (metadata != null)
            {
                var metaMap = GetOrCreateMetadata(zoneId);
                metaMap[templateType] = metadata;
            }

            return true;
        }

        public static bool TryGetEntities(string zoneId, string templateType, out List<Entity> entities)
        {
            entities = null;
            if (!_zoneTemplateEntities.TryGetValue(zoneId, out var map))
            {
                return false;
            }

            if (!map.TryGetValue(templateType, out var list) || list.Count == 0)
            {
                return false;
            }

            entities = list;
            return true;
        }

        public static void ClearZoneType(string zoneId, string templateType)
        {
            if (!_zoneTemplateEntities.TryGetValue(zoneId, out var map))
            {
                return;
            }

            if (!map.TryGetValue(templateType, out var entities))
            {
                return;
            }

            foreach (var entity in entities)
            {
                _entityToZoneType.Remove(entity);
            }

            map.Remove(templateType);
            if (map.Count == 0)
            {
                _zoneTemplateEntities.Remove(zoneId);
            }

            if (_zoneTemplateMetadata.TryGetValue(zoneId, out var metaMap))
            {
                metaMap.Remove(templateType);
                if (metaMap.Count == 0)
                {
                    _zoneTemplateMetadata.Remove(zoneId);
                }
            }
        }

        public static void ClearZone(string zoneId)
        {
            if (!_zoneTemplateEntities.TryGetValue(zoneId, out var map))
            {
                return;
            }

            foreach (var templateType in map.Keys.ToList())
            {
                ClearZoneType(zoneId, templateType);
            }
        }

        public static void RemoveEntity(Entity entity)
        {
            if (!_entityToZoneType.TryGetValue(entity, out var mapping))
            {
                return;
            }

            if (_zoneTemplateEntities.TryGetValue(mapping.ZoneId, out var map) &&
                map.TryGetValue(mapping.TemplateType, out var list))
            {
                list.Remove(entity);
                if (list.Count == 0)
                {
                    map.Remove(mapping.TemplateType);
                }

                if (map.Count == 0)
                {
                    _zoneTemplateEntities.Remove(mapping.ZoneId);
                }
            }

            _entityToZoneType.Remove(entity);
        }

        public static IReadOnlyCollection<string> GetSpawnedZones() => _zoneTemplateEntities.Keys.ToList();

        public static IReadOnlyCollection<string> GetSpawnedTypes(string zoneId)
        {
            if (!_zoneTemplateEntities.TryGetValue(zoneId, out var map))
            {
                return Array.Empty<string>();
            }

            return map.Keys.ToList();
        }

        public static int GetEntityCount(string zoneId, string templateType)
        {
            if (!_zoneTemplateEntities.TryGetValue(zoneId, out var map))
            {
                return 0;
            }

            return map.TryGetValue(templateType, out var list) ? list.Count : 0;
        }

        public static bool IsSpawned(string zoneId, string templateType)
            => GetEntityCount(zoneId, templateType) > 0;

        public static TemplateSpawnMetadata GetMetadata(string zoneId, string templateType)
        {
            if (!_zoneTemplateMetadata.TryGetValue(zoneId, out var metaMap))
            {
                return null;
            }

            return metaMap.TryGetValue(templateType, out var metadata) ? metadata : null;
        }

        private static Dictionary<string, List<Entity>> GetOrCreateZone(string zoneId)
        {
            if (!_zoneTemplateEntities.TryGetValue(zoneId, out var map))
            {
                map = new Dictionary<string, List<Entity>>(StringComparer.OrdinalIgnoreCase);
                _zoneTemplateEntities[zoneId] = map;
            }

            return map;
        }

        private static Dictionary<string, TemplateSpawnMetadata> GetOrCreateMetadata(string zoneId)
        {
            if (!_zoneTemplateMetadata.TryGetValue(zoneId, out var map))
            {
                map = new Dictionary<string, TemplateSpawnMetadata>(StringComparer.OrdinalIgnoreCase);
                _zoneTemplateMetadata[zoneId] = map;
            }

            return map;
        }

        private static List<Entity> GetOrCreateTemplate(Dictionary<string, List<Entity>> zoneMap, string templateType)
        {
            if (!zoneMap.TryGetValue(templateType, out var list))
            {
                list = new List<Entity>();
                zoneMap[templateType] = list;
            }

            return list;
        }

        private sealed class EntityEqualityComparer : IEqualityComparer<Entity>
        {
            public static readonly EntityEqualityComparer Instance = new();
            public bool Equals(Entity x, Entity y) => x.Equals(y);
            public int GetHashCode(Entity obj) => obj.GetHashCode();
        }
    }

    public class TemplateSpawnMetadata
    {
        public DateTime SpawnedAt { get; set; }
        public int EntityCount { get; set; }
        public string TemplateName { get; set; }
        public float3 OriginPosition { get; set; }
        public quaternion Rotation { get; set; }
    }
}
