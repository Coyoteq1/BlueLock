using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using BepInEx;
using ProjectM;
using Stunlock.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VAutomationCore.Core.Data;
using VAuto.Zone.Core;
using VAuto.Zone.Models;

namespace VAuto.Zone.Services
{
    public static class TemplateSnapshotService
    {
        private static readonly string TemplatesDirectory =
            Path.Combine(Paths.ConfigPath, "Bluelock", "templates");

        private static readonly JsonSerializerOptions JsonOptions = new(ZoneJsonOptions.WithUnityMathConverters)
        {
            WriteIndented = true
        };

        private static readonly Lazy<Dictionary<int, string>> GuidToNameMap = new(() =>
            PrefabsAll.ByName.ToDictionary(kvp => kvp.Value.GuidHash, kvp => kvp.Key));

        public static SnapshotResult CreateSnapshot(
            string zoneId,
            string templateType,
            string newTemplateName,
            EntityManager em,
            SnapshotOptions options = null)
        {
            var zone = ZoneConfigService.GetZoneById(zoneId);
            if (zone == null)
            {
                return new SnapshotResult { Success = false, Error = $"Zone '{zoneId}' not found." };
            }

            if (string.IsNullOrWhiteSpace(newTemplateName))
            {
                newTemplateName = $"{zoneId}-{templateType ?? "snapshot"}-{DateTime.UtcNow:yyyyMMddHHmmss}";
            }

            options ??= new SnapshotOptions();
            if (!ZoneTemplateRegistry.TryGetEntities(zoneId, templateType, out var entities) || entities.Count == 0)
            {
                return new SnapshotResult { Success = false, Error = $"No tracked entities found for '{templateType}' in zone '{zoneId}'." };
            }

            var snapshot = BuildSnapshotFromEntities(zone, entities.ToList(), newTemplateName, em, options);
            if (snapshot.Entities.Count == 0)
            {
                return new SnapshotResult { Success = false, Error = "Failed to capture any entities for the snapshot." };
            }

            var saved = SaveTemplate(snapshot, newTemplateName);
            return new SnapshotResult
            {
                Success = saved,
                TemplateName = newTemplateName,
                EntityCount = snapshot.Entities.Count,
                FilePath = saved ? GetTemplateFilePath(newTemplateName) : string.Empty,
                Error = saved ? string.Empty : "Failed to persist template snapshot."
            };
        }

        public static SnapshotResult SnapshotTrackedEntities(
            string zoneId,
            string templateType,
            string newTemplateName,
            EntityManager em,
            SnapshotOptions options = null)
        {
            return CreateSnapshot(zoneId, templateType, newTemplateName, em, options);
        }

        public static bool SaveTemplate(TemplateSnapshot template, string templateName)
        {
            if (template == null || string.IsNullOrWhiteSpace(templateName))
            {
                return false;
            }

            try
            {
                EnsureTemplatesDirectory();
                var path = GetTemplateFilePath(templateName);
                template.Name = templateName.Trim();
                File.WriteAllText(path, JsonSerializer.Serialize(template, JsonOptions));
                return true;
            }
            catch (Exception ex)
            {
                ZoneCore.LogWarning($"[TemplateSnapshotService] Failed to save '{templateName}': {ex.Message}");
                return false;
            }
        }

        public static List<string> ListTemplates()
        {
            if (!Directory.Exists(TemplatesDirectory))
            {
                return new List<string>();
            }

            return Directory.GetFiles(TemplatesDirectory, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static TemplateSnapshot BuildSnapshotFromEntities(
            ZoneDefinition zone,
            List<Entity> entities,
            string templateName,
            EntityManager em,
            SnapshotOptions options)
        {
            var snapshot = new TemplateSnapshot
            {
                Name = templateName
            };

            var center = new float3(zone.CenterX, zone.CenterY, zone.CenterZ);
            foreach (var entity in entities)
            {
                if (snapshot.Entities.Count >= options.MaxEntities)
                {
                    break;
                }

                if (!TryCreateEntry(em, entity, center, out var entry))
                {
                    continue;
                }

                snapshot.Entities.Add(entry);
            }

            return snapshot;
        }

        private static bool TryCreateEntry(EntityManager em, Entity entity, float3 center, out TemplateEntityEntry entry)
        {
            entry = null;
            if (entity == Entity.Null || !em.Exists(entity))
            {
                return false;
            }

            if (!TryGetEntityPosition(em, entity, out var position))
            {
                return false;
            }

            if (!TryGetPrefabGuid(em, entity, out var guidHash))
            {
                return false;
            }

            entry = new TemplateEntityEntry
            {
                PrefabGuid = guidHash,
                PrefabName = ResolvePrefabName(guidHash),
                Offset = position - center,
                RotationDegrees = DetermineRotationDegrees(em, entity)
            };

            return true;
        }

        private static bool TryGetEntityPosition(EntityManager em, Entity entity, out float3 position)
        {
            position = float3.zero;
            if (em.HasComponent<LocalTransform>(entity))
            {
                position = em.GetComponentData<LocalTransform>(entity).Position;
                return true;
            }

            if (em.HasComponent<Translation>(entity))
            {
                position = em.GetComponentData<Translation>(entity).Value;
                return true;
            }

            return false;
        }

        private static bool TryGetPrefabGuid(EntityManager em, Entity entity, out int guidHash)
        {
            guidHash = 0;
            if (!em.HasComponent<PrefabGUID>(entity))
            {
                return false;
            }

            var prefab = em.GetComponentData<PrefabGUID>(entity);
            guidHash = prefab.GuidHash;
            return guidHash != 0;
        }

        private static float DetermineRotationDegrees(EntityManager em, Entity entity)
        {
            if (em.HasComponent<LocalTransform>(entity))
            {
                var rotation = em.GetComponentData<LocalTransform>(entity).Rotation;
                return ExtractYawDegrees(rotation);
            }

            return 0f;
        }

        private static float ExtractYawDegrees(quaternion rotation)
        {
            var q = rotation.value;
            var sinyCosp = 2f * (q.w * q.y + q.x * q.z);
            var cosyCosp = 1f - 2f * (q.y * q.y + q.x * q.x);
            return math.degrees(math.atan2(sinyCosp, cosyCosp));
        }

        private static string ResolvePrefabName(int guidHash)
        {
            if (guidHash == 0)
            {
                return "unknown";
            }

            return GuidToNameMap.Value.TryGetValue(guidHash, out var name) ? name : $"GUID:{guidHash}";
        }

        private static void EnsureTemplatesDirectory()
        {
            if (!Directory.Exists(TemplatesDirectory))
            {
                Directory.CreateDirectory(TemplatesDirectory);
            }
        }

        private static string GetTemplateFilePath(string templateName)
        {
            var cleanName = SanitizeTemplateName(templateName);
            return Path.Combine(TemplatesDirectory, $"{cleanName}.json");
        }

        private static string SanitizeTemplateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return $"template_{DateTime.UtcNow:yyyyMMddHHmmss}";
            }

            var invalid = Path.GetInvalidFileNameChars();
            var cleaned = new string(name.Where(ch => !invalid.Contains(ch)).ToArray());
            return string.IsNullOrWhiteSpace(cleaned) ? $"template_{DateTime.UtcNow:yyyyMMddHHmmss}" : cleaned;
        }
    }

    public class SnapshotOptions
    {
        public List<string> IncludeCategories { get; set; }
        public List<string> ExcludeCategories { get; set; } = new() { "Debris", "Effect" };
        public int MaxEntities { get; set; } = 500;
        public bool BuildablesOnly { get; set; } = true;
    }

    public class SnapshotResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public string TemplateName { get; set; }
        public int EntityCount { get; set; }
        public string FilePath { get; set; }
    }
}
