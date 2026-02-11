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
using VAutomationCore.Core.Services;

namespace VAuto.Zone.Services
{
    public static class ZoneGlowBorderService
    {
        public const string ServiceName = "ZoneGlowBorderService";

        private static readonly Dictionary<string, ZoneRuntime> _zones = new();
        private static GlowZonesConfig _config = new();
        private const string ConfigFileName = "glow_zones.json";

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
            ZoneCore.LogInfo("Initializing ZoneGlowBorderService");
            LoadConfig();
            ZoneCore.LogInfo($"ZoneGlowBorderService initialized with {_config.Zones.Count} zones");
        }

        private static bool Validate()
        {
            return _config != null && _config.Zones != null;
        }

        private static void LoadConfig()
        {
            var configPath = Path.Combine(ZoneCore.ConfigPath, ConfigFileName);
            if (File.Exists(configPath))
            {
                try
                {
                    var json = File.ReadAllText(configPath);
                    _config = JsonSerializer.Deserialize<GlowZonesConfig>(json) ?? new GlowZonesConfig();
                }
                catch (Exception ex)
                {
                    ZoneCore.LogException("Failed to load glow zones config", ex);
                }
            }
            else
            {
                ZoneCore.LogInfo($"Creating default glow zones config at {configPath}");
                _config = GenerateDefaultConfig();
                SaveConfig();
            }
        }

        private static void SaveConfig()
        {
            var configPath = Path.Combine(ZoneCore.ConfigPath, ConfigFileName);
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(_config, options);
            File.WriteAllText(configPath, json);
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
                        GlowPrefabs = new List<string> { "CarpetPrefab" },
                        Rotation = new GlowRotationConfig { IntervalSeconds = 60 }
                    }
                }
            };
        }

        #endregion

        #region Rotation Methods

        public static void RotateDueZones()
        {
            foreach (var zone in _zones.Values)
            {
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
                RotateZone(zone);
            }
        }

        private static void RotateZone(ZoneRuntime zone)
        {
            if (zone.ResolvedPrefabs.Length == 0) return;
            
            zone.ActivePrefabIndex = (zone.ActivePrefabIndex + 1) % zone.ResolvedPrefabs.Length;
            zone.NextRotationUtc = DateTime.UtcNow.AddSeconds(zone.Entry.Rotation.IntervalSeconds);
            
            ZoneCore.LogDebug($"Rotated zone {zone.Entry.Id} to prefab index {zone.ActivePrefabIndex}");
        }

        public static void BuildAll()
        {
            foreach (var zone in _zones.Values)
            {
                BuildZone(zone);
            }
        }

        private static void BuildZone(ZoneRuntime zone)
        {
            ZoneCore.LogInfo($"Building zone: {zone.Entry.Id}");
            
            if (!zone.Entry.Enabled)
            {
                ZoneCore.LogInfo($"Skipping disabled zone: {zone.Entry.Id}");
                return;
            }
            
            // Clear existing entities first
            if (zone.SpawnedEntities.IsCreated)
            {
                var em = ZoneCore.EntityManager;
                if (em != null && em.IsCreated)
                {
                    em.DestroyEntity(zone.SpawnedEntities.AsArray());
                }
            }
            
            // Resolve prefabs from config
            zone.ResolvedPrefabs = ResolvePrefabs(zone.Entry.GlowPrefabs);
            ZoneCore.LogInfo($"Zone {zone.Entry.Id}: Resolved {zone.ResolvedPrefabs.Length} prefabs");
            
            // Build border entities around the zone perimeter
            var borderEntities = BuildZoneBorder(zone);
            
            ZoneCore.LogInfo($"Zone {zone.Entry.Id} built with {borderEntities} border entities");
        }

        private static int BuildZoneBorder(ZoneRuntime zone)
        {
            try
            {
                var em = ZoneCore.EntityManager;
                if (em == null || !em.IsCreated) return 0;
                
                var count = 0;
                var center = zone.Entry.Center;
                var radius = zone.Entry.Radius;
                var borderSpacing = zone.Entry.BorderSpacing;
                var entityCount = (int)(Math.PI * 2 * radius / borderSpacing);
                
                for (int i = 0; i < entityCount; i++)
                {
                    var angle = (i / (float)entityCount) * Math.PI * 2;
                    var position = new float3(
                        center.x + (float)Math.Cos(angle) * radius,
                        center.y,
                        center.z + (float)Math.Sin(angle) * radius
                    );
                    
                    if (zone.ActivePrefabIndex < zone.ResolvedPrefabs.Length)
                    {
                        var entity = CreateGlowEntity(em, position, zone.ResolvedPrefabs[zone.ActivePrefabIndex]);
                        if (entity != Entity.Null)
                        {
                            zone.SpawnedEntities.Add(entity);
                            count++;
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

        private static Entity CreateGlowEntity(EntityManager em, float3 position, PrefabGUID prefabGuid)
        {
            try
            {
                var entity = em.CreateEntity();
                em.SetComponentData(entity, new LocalTransform
                {
                    Position = position,
                    Rotation = quaternion.identity,
                    Scale = 1f
                });
                return entity;
            }
            catch (Exception ex)
            {
                ZoneCore.LogException($"Failed to create glow entity: {ex.Message}", ex);
                return Entity.Null;
            }
        }

        private static PrefabGUID[] ResolvePrefabs(List<string> prefabNames)
        {
            // Placeholder - resolve prefab names to GUIDs
            // In production, use PrefabIndex.json
            return prefabNames.Select(name => new PrefabGUID(0)).ToArray();
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
                try
                {
                    var em = ZoneCore.EntityManager;
                    if (em != null && em.IsCreated && zone.SpawnedEntities.IsCreated)
                    {
                        em.DestroyEntity(zone.SpawnedEntities.AsArray());
                        zone.SpawnedEntities.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    ZoneCore.LogException($"Failed to dispose zone {zone.Entry.Id}: {ex.Message}", ex);
                }
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

        #endregion
    }
}
