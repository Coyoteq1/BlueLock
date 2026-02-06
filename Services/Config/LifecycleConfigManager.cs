using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;
using ProjectM;
using VAuto.Core.Components.Lifecycle;
using VAuto.Services.World;

namespace VAuto.Core.Services
{
    public class LifecycleConfigManager
    {
        private readonly World _world;

        public LifecycleConfigManager(World world)
        {
            _world = world;
        }

        public void Load(string path)
        {
            try
            {
                Apply(Deserialize(path));
                VAuto.Plugin.Log.LogInfo("[Config] Loaded ECS lifecycle config");
            }
            catch (Exception ex)
            {
                VAuto.Plugin.Log.LogError($"[Config] Failed to load lifecycle config: {ex.Message}");
            }
        }

        public void Reload(string path)
        {
            try
            {
                ClearEntities();
                Apply(Deserialize(path));
                VAuto.Plugin.Log.LogInfo("[Config] Reloaded ECS zones at runtime");
            }
            catch (Exception ex)
            {
                VAuto.Plugin.Log.LogError($"[Config] Failed to reload lifecycle config: {ex.Message}");
            }
        }

        private LifecycleConfigModel Deserialize(string path)
        {
            var json = File.ReadAllText(path);
            var model = JsonSerializer.Deserialize<LifecycleConfigModel>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });
            return model ?? new LifecycleConfigModel();
        }

        private void ClearEntities()
        {
            var em = _world.EntityManager;
            var q = em.CreateEntityQuery(typeof(LifecycleZone));
            em.DestroyEntity(q);
            // Also clear existing glow borders associated with zones
            GlowZoneService.ClearAll();
        }

        private void Apply(LifecycleConfigModel cfg)
        {
            if (cfg?.Lifecycle == null || cfg.Lifecycle.Zones == null) return;
            var em = _world.EntityManager;
            int zoneIndex = 0;
            foreach (var z in cfg.Lifecycle.Zones)
            {
                var e = em.CreateEntity(typeof(LifecycleZone), typeof(LocalTransform));
                em.AddComponentData(e, new LifecycleZone
                {
                    Type = ParseZoneType(z.Type),
                    AllowAutoEnter = z.Automation?.AutoEnter ?? false,
                    GearLoadout = new FixedString32Bytes(z.Automation?.Kit?.Profile ?? string.Empty),
                    AutoRepairOnEntry = z.Automation?.Repair?.OnEntry ?? false,
                    AutoRepairOnExit = z.Automation?.Repair?.OnExit ?? false,
                    RepairThreshold = z.Automation?.Repair?.Threshold ?? 0,
                    UnlockVBloods = z.Automation?.VBlood?.Enabled ?? false,
                    GrantSpellbooks = z.Automation?.Spellbook?.Enabled ?? false,
                    Radius = z.Radius,
                    Center = new float3(z.Center?.X ?? 0, z.Center?.Y ?? 0, z.Center?.Z ?? 0)
                });

                var center = new float3(z.Center?.X ?? 0, z.Center?.Y ?? 0, z.Center?.Z ?? 0);
                em.SetComponentData(e, LocalTransform.FromPositionRotationScale(
                    center,
                    quaternion.identity,
                    1f));

                // Build visual glow border for this zone
                var glowName = $"ZoneGlow_{z.Type}_{zoneIndex}";
                GlowZoneService.BuildCircleZone(glowName, center, z.Radius, 3.0f);
                zoneIndex++;

                if (z.Automation?.Spellbook?.ZoneAbilities != null && z.Automation.Spellbook.ZoneAbilities.Length > 0)
                {
                    var b = em.AddBuffer<ZoneSpellbookElement>(e);
                    foreach (var id in z.Automation.Spellbook.ZoneAbilities)
                    {
                        b.Add(new ZoneSpellbookElement { AbilityGroupGuid = new PrefabGUID(id), GrantType = GrantType.AbilityGroup });
                    }
                }
            }
        }

        private static ZoneType ParseZoneType(string? type)
        {
            if (string.IsNullOrWhiteSpace(type)) return ZoneType.Custom;
            if (Enum.TryParse<ZoneType>(type, true, out var t)) return t;
            return ZoneType.Custom;
        }
    }

    // Model compatible with plan; mapped from VAuto-ECS-Config.json (extendable)
    public class LifecycleConfigModel
    {
        public LifecycleSection Lifecycle { get; set; } = new();
    }

    public class LifecycleSection
    {
        public ZoneModel[] Zones { get; set; } = Array.Empty<ZoneModel>();
    }

    public class ZoneModel
    {
        public string Type { get; set; } = "Custom";
        public float Radius { get; set; }
        public CenterModel? Center { get; set; }
        public AutomationModel? Automation { get; set; }
    }

    public class CenterModel { public float X { get; set; } public float Y { get; set; } public float Z { get; set; } }

    public class AutomationModel
    {
        public bool AutoEnter { get; set; }
        public KitModel? Kit { get; set; }
        public RepairModel? Repair { get; set; }
        public VBloodModel? VBlood { get; set; }
        public SpellbookModel? Spellbook { get; set; }
    }

    public class KitModel { public string Profile { get; set; } = string.Empty; }
    public class RepairModel { public bool OnEntry { get; set; } public bool OnExit { get; set; } public int Threshold { get; set; } }
    public class VBloodModel { public bool Enabled { get; set; } }
    public class SpellbookModel { public bool Enabled { get; set; } public int[] ZoneAbilities { get; set; } = Array.Empty<int>(); }
}
