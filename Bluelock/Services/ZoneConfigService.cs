using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using BepInEx;
using Unity.Mathematics;
using VAutomationCore.Core.Config;
using VAutomationCore.Core;
using VAutomationCore.Core.Data;
using VAuto.Zone.Core;
using VAuto.Zone.Models;

namespace VAuto.Zone.Services
{
    /// <summary>
    /// Service for managing zone configurations from JSON.
    /// </summary>
    public static class ZoneConfigService
    {
        private static readonly string[] NumericZoneIds = { "1", "2", "3" };
        private const float DefaultZoneRadius = 50f;
        private static readonly float3[] DefaultZoneCenters =
        {
            new float3(-1000f, 0f, -500f),
            new float3(-800f, 0f, -500f),
            new float3(-700f, 0f, -500f)
        };
        private const int DefaultBorderCarpetPrefabId = 4345235;
        private const int DefaultGlowBuffGuidA = 4345235;
        private const int DefaultGlowBuffGuidB = 54252435;
        private const int DefaultGlowBuffGuidC = 65245252;
        private const int DefaultBorderMarkerPrefabId = 230163020;   // TM_Castle_ObjectDecor_TargetDummy_Vampire01
        private const string DefaultBorderCarpetPrefabName = "4345235";
        private const string DefaultBorderMarkerPrefabName = "TM_Castle_ObjectDecor_TargetDummy_Vampire01";
        private const float DefaultBorderSpacing = 3f;
        private const float DefaultBorderHeightOffset = 0f;
        private const float DefaultGlowTileSpacing = 3f;
        private const float DefaultGlowTileHeightOffset = 0.3f;
        private const float SameLocationCenterTolerance = 2f;
        private const float SameLocationRadiusTolerance = 2f;
        private static ZonesConfig _zonesConfig;
        private static readonly string ConfigPath = ResolveZonesConfigPath();
        private static bool _initialized;

        private static string ResolveZonesConfigPath()
        {
            var rootDir = Path.Combine(Paths.ConfigPath, "Bluelock");
            Directory.CreateDirectory(rootDir);
            return Path.Combine(rootDir, "VAuto.Zones.json");
        }

        private static JsonSerializerOptions CreateZoneSerializerOptions(bool writeIndented = false)
        {
            return new JsonSerializerOptions(ZoneJsonOptions.WithUnityMathConverters)
            {
                WriteIndented = writeIndented
            };
        }

        /// <summary>
        /// Initialize the zone service and load configuration.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;
            
            LoadZonesConfig();
            _initialized = true;
            ZoneCore.LogInfo($"[ZoneConfigService] Initialized with {GetZoneCount()} zones");
        }

        /// <summary>
        /// Load zones configuration from JSON file.
        /// </summary>
        private static void LoadZonesConfig()
        {
            try
            {
                TypedJsonConfigManager.TryLoadOrCreate(
                    ConfigPath,
                    CreateDefaultZonesConfigModel,
                    out _zonesConfig,
                    out var createdDefault,
                    CreateZoneSerializerOptions(writeIndented: true),
                    ValidateZonesConfig,
                    message => ZoneCore.LogInfo($"[ZoneConfigService] {message}"),
                    message => ZoneCore.LogWarning($"[ZoneConfigService] {message}"),
                    message => ZoneCore.LogError($"[ZoneConfigService] {message}"));

                _zonesConfig ??= new ZonesConfig();

                var count = _zonesConfig.Zones?.Count ?? 0;
                if (createdDefault)
                {
                    ZoneCore.LogInfo($"[ZoneConfigService] Created/sealed default zones config at {ConfigPath}.");
                }

                ZoneCore.LogInfo($"[ZoneConfigService] Loaded {count} zones from {ConfigPath}");
                if (!string.IsNullOrEmpty(_zonesConfig.Description))
                {
                    ZoneCore.LogInfo($"[ZoneConfigService] Description: {_zonesConfig.Description}");
                }

                EnsureDefaultZones();
                LogLoadedZones();
                ValidateAndNormalizePrefabReferences();
            }
            catch (Exception ex)
            {
                ZoneCore.LogError($"[ZoneConfigService] Failed to load zones: {ex.Message}");
                _zonesConfig = new ZonesConfig();
                EnsureDefaultZones();
            }
        }

        /// <summary>
        /// Validates and normalizes prefab references in all loaded zones.
        /// Logs warnings for missing or ambiguous references and updates in-memory config.
        /// </summary>
        private static void ValidateAndNormalizePrefabReferences()
        {
            if (_zonesConfig?.Zones == null) return;
            var changed = false;

            // Global default border config (used for zones missing Border or missing prefab tokens).
            _zonesConfig.DefaultBorder ??= new ZoneBorderConfig();
            if (NormalizeDefaultBorderConfig(_zonesConfig.DefaultBorder))
            {
                changed = true;
            }

            var defaultBorder = _zonesConfig.DefaultBorder;
            foreach (var zone in _zonesConfig.Zones)
            {
                zone.Tags ??= new List<string>();

                // Current policy: numeric zones (1/2/3) are sandbox.
                if (Array.Exists(NumericZoneIds, id => string.Equals(id, zone.Id, StringComparison.OrdinalIgnoreCase)) &&
                    !zone.Tags.Exists(t => string.Equals(t, "sandbox", StringComparison.OrdinalIgnoreCase)))
                {
                    zone.Tags.Add("sandbox");
                    changed = true;
                }

                // Validate GlowPrefab
                if (!string.IsNullOrWhiteSpace(zone.GlowPrefab))
                {
                    if (!PrefabResolver.TryResolve(zone.GlowPrefab, out var resolvedGuid))
                    {
                        ZoneCore.LogWarning($"[ZoneConfigService] Zone '{zone.Id}' has invalid GlowPrefab name '{zone.GlowPrefab}'.");
                    }
                    else if (zone.GlowPrefabId != resolvedGuid.GuidHash)
                    {
                        ZoneCore.LogInfo($"[ZoneConfigService] Zone '{zone.Id}' normalized GlowPrefabId from {zone.GlowPrefabId} to {resolvedGuid.GuidHash}.");
                        zone.GlowPrefabId = resolvedGuid.GuidHash;
                        changed = true;
                    }
                }

                // Validate GlowTile prefab
                if (!string.IsNullOrWhiteSpace(zone.GlowTilePrefab))
                {
                    if (!PrefabResolver.TryResolve(zone.GlowTilePrefab, out var resolvedGuid))
                    {
                        ZoneCore.LogWarning($"[ZoneConfigService] Zone '{zone.Id}' has invalid GlowTilePrefab name '{zone.GlowTilePrefab}'.");
                    }
                    else if (zone.GlowTilePrefabId != resolvedGuid.GuidHash)
                    {
                        ZoneCore.LogInfo($"[ZoneConfigService] Zone '{zone.Id}' normalized GlowTilePrefabId from {zone.GlowTilePrefabId} to {resolvedGuid.GuidHash}.");
                        zone.GlowTilePrefabId = resolvedGuid.GuidHash;
                        changed = true;
                    }
                }

                if (zone.GlowTileSpacing < GlowTileGeometry.MinSpacing)
                {
                    zone.GlowTileSpacing = GlowTileGeometry.MinSpacing;
                    changed = true;
                }

                // Normalize legacy kit field -> KitId
                if (string.IsNullOrWhiteSpace(zone.KitId) && !string.IsNullOrWhiteSpace(zone.KitToApplyId))
                {
                    zone.KitId = zone.KitToApplyId;
                    changed = true;
                }

                // Normalize ability preset slot tokens (older configs used Spell_* names which do not exist in PrefabsAll).
                if (zone.AbilityPresetSlots != null && zone.AbilityPresetSlots.Length > 0)
                {
                    var slots = (string[])zone.AbilityPresetSlots.Clone();
                    var slotChanged = false;
                    for (var i = 0; i < slots.Length; i++)
                    {
                        var token = (slots[i] ?? string.Empty).Trim();
                        if (string.IsNullOrWhiteSpace(token))
                        {
                            continue;
                        }

                        // Fix common mistaken token for Blood Rite.
                        if (token.Equals("AB_BloodRite_AbilityGroup", StringComparison.OrdinalIgnoreCase))
                        {
                            slots[i] = "AB_Blood_BloodRite_AbilityGroup";
                            slotChanged = true;
                            continue;
                        }

                        // Remap Spell_* veil identifiers to the correct ability-group prefabs.
                        if (token.Equals("Spell_VeilOfBlood", StringComparison.OrdinalIgnoreCase))
                        {
                            slots[i] = "AB_Vampire_VeilOfBlood_Group";
                            slotChanged = true;
                            continue;
                        }

                        if (token.Equals("Spell_VeilOfChaos", StringComparison.OrdinalIgnoreCase))
                        {
                            slots[i] = "AB_Vampire_VeilOfChaos_Group";
                            slotChanged = true;
                            continue;
                        }

                        if (token.Equals("Spell_VeilOfFrost", StringComparison.OrdinalIgnoreCase))
                        {
                            slots[i] = "AB_Vampire_VeilOfFrost_Group";
                            slotChanged = true;
                            continue;
                        }

                        if (token.Equals("Spell_VeilOfBones", StringComparison.OrdinalIgnoreCase))
                        {
                            slots[i] = "AB_Vampire_VeilOfBones_AbilityGroup";
                            slotChanged = true;
                        }
                    }

                    if (slotChanged)
                    {
                        zone.AbilityPresetSlots = slots;
                        changed = true;
                    }
                }

                // Border config migration/normalization:
                // - Treat ZonesConfig.DefaultBorder as the source of defaults.
                // - Only persist per-zone values when the zone explicitly configures them.
                if (zone.Border != null)
                {
                    // Migrate legacy border fields into Border if Border tokens are empty.
                    if (string.IsNullOrWhiteSpace(zone.Border.PrefabName) && !string.IsNullOrWhiteSpace(zone.BorderGlowPrefab))
                    {
                        zone.Border.PrefabName = zone.BorderGlowPrefab;
                        changed = true;
                    }
                    if (zone.Border.PrefabGuid == 0 && zone.BorderGlowPrefabId != 0)
                    {
                        zone.Border.PrefabGuid = zone.BorderGlowPrefabId;
                        changed = true;
                    }

                    if (zone.Border.Spacing < 1f)
                    {
                        zone.Border.Spacing = Math.Max(1f, defaultBorder?.Spacing > 0f ? defaultBorder.Spacing : DefaultBorderSpacing);
                        changed = true;
                    }

                    // Name -> GUID normalization using PrefabResolver (world-independent).
                    if (zone.Border.PrefabGuid == 0 && !string.IsNullOrWhiteSpace(zone.Border.PrefabName) &&
                        PrefabResolver.TryResolve(zone.Border.PrefabName, out var resolvedBorder))
                    {
                        zone.Border.PrefabGuid = resolvedBorder.GuidHash;
                        changed = true;
                    }

                    // Backfill legacy fields only when the zone explicitly configures a border marker.
                    if (zone.Border.PrefabGuid != 0 || !string.IsNullOrWhiteSpace(zone.Border.PrefabName))
                    {
                        if (zone.BorderGlowPrefabId != zone.Border.PrefabGuid)
                        {
                            zone.BorderGlowPrefabId = zone.Border.PrefabGuid;
                            changed = true;
                        }

                        if (!string.Equals(zone.BorderGlowPrefab, zone.Border.PrefabName, StringComparison.OrdinalIgnoreCase))
                        {
                            zone.BorderGlowPrefab = zone.Border.PrefabName;
                            changed = true;
                        }
                    }
                }
            }

            if (changed)
            {
                try
                {
                    SaveZonesConfig();
                }
                catch
                {
                    // ignore persistence failures; runtime will still use in-memory values.
                }
            }
        }

        private static bool NormalizeDefaultBorderConfig(ZoneBorderConfig border)
        {
            if (border == null)
            {
                return false;
            }

            var changed = false;

            if (border.Spacing < 1f)
            {
                border.Spacing = DefaultBorderSpacing;
                changed = true;
            }

            // Safe fallback marker if none configured.
            if (border.PrefabGuid == 0 && string.IsNullOrWhiteSpace(border.PrefabName))
            {
                border.Enabled = true;
                border.PrefabGuid = DefaultBorderMarkerPrefabId;
                border.PrefabName = DefaultBorderMarkerPrefabName;
                border.HeightOffset = DefaultBorderHeightOffset;
                changed = true;
            }

            // Name -> GUID normalization using PrefabResolver (world-independent).
            if (border.PrefabGuid == 0 && !string.IsNullOrWhiteSpace(border.PrefabName) &&
                PrefabResolver.TryResolve(border.PrefabName, out var resolved))
            {
                border.PrefabGuid = resolved.GuidHash;
                changed = true;
            }

            return changed;
        }

        /// <summary>
        /// Create default zones configuration if file doesn't exist.
        /// </summary>
        private static ZonesConfig CreateDefaultZonesConfigModel()
        {
            return new ZonesConfig
            {
                Description = "Default zones generated by VAutoZone",
                DefaultKitId = "Kit1",
                DefaultZoneId = NumericZoneIds[0],
                DefaultTeleport = DefaultZoneCenters[0],
                DefaultBorder = new ZoneBorderConfig
                {
                    Enabled = true,
                    PrefabGuid = DefaultBorderMarkerPrefabId,
                    PrefabName = DefaultBorderMarkerPrefabName,
                    Spacing = DefaultBorderSpacing,
                    HeightOffset = DefaultBorderHeightOffset
                },
                Zones = new List<ZoneDefinition>(BuildDefaultZones())
            };
        }

        private static (bool IsValid, string Error) ValidateZonesConfig(ZonesConfig config)
        {
            if (config == null)
            {
                return (false, "Zones config is null");
            }

            if (config.Zones == null)
            {
                return (false, "Zones collection is null");
            }

            return (true, string.Empty);
        }

        private static void EnsureDefaultZones()
        {
            try
            {
                _zonesConfig ??= new ZonesConfig();
                _zonesConfig.Zones ??= new List<ZoneDefinition>();
                _zonesConfig.DefaultKitId ??= "Kit1";
                _zonesConfig.DefaultZoneId ??= NumericZoneIds[0];

                var added = false;
                var normalized = false;

                if (_zonesConfig.DefaultTeleport.x == 0f &&
                    _zonesConfig.DefaultTeleport.y == 0f &&
                    _zonesConfig.DefaultTeleport.z == 0f)
                {
                    _zonesConfig.DefaultTeleport = DefaultZoneCenters[0];
                    normalized = true;
                }

                foreach (var zone in _zonesConfig.Zones)
                {
                    if (zone == null)
                    {
                        continue;
                    }

                    if (zone.BorderGlowPrefabId == -1408736701 ||
                        string.Equals(zone.BorderGlowPrefab, "eSwatch_Color_StrongbladeDLC_ModularCarpets", StringComparison.OrdinalIgnoreCase))
                    {
                        zone.BorderGlowPrefabId = 0;
                        zone.BorderGlowPrefab = string.Empty;
                        normalized = true;
                    }
                }

                foreach (var template in BuildDefaultZones())
                {
                    var existing = _zonesConfig.Zones.Find(z => string.Equals(z.Id, template.Id, StringComparison.OrdinalIgnoreCase));
                    if (existing != null)
                    {
                        if (string.IsNullOrWhiteSpace(existing.KitToApplyId))
                        {
                            existing.KitToApplyId = template.KitToApplyId;
                            normalized = true;
                        }

                        if (string.IsNullOrWhiteSpace(existing.KitId))
                        {
                            existing.KitId = template.KitId;
                            normalized = true;
                        }

                        existing.Tags ??= new List<string>();
                        if (!existing.Tags.Exists(t => string.Equals(t, "sandbox", StringComparison.OrdinalIgnoreCase)))
                        {
                            existing.Tags.Add("sandbox");
                            normalized = true;
                        }

                        if (existing.AbilityPresetSlots == null || existing.AbilityPresetSlots.Length == 0)
                        {
                            existing.AbilityPresetSlots = template.AbilityPresetSlots;
                            normalized = true;
                        }

                        if (existing.Templates == null || existing.Templates.Count == 0)
                        {
                            existing.Templates = new Dictionary<string, string>(template.Templates, StringComparer.OrdinalIgnoreCase);
                            normalized = true;
                        }

                        // Border config is optional; if missing, zones inherit ZonesConfig.DefaultBorder at runtime.
                        // Only materialize per-zone Border when the zone explicitly sets a legacy border prefab.
                        if (existing.Border == null &&
                            (existing.BorderGlowPrefabId != 0 || !string.IsNullOrWhiteSpace(existing.BorderGlowPrefab)))
                        {
                            existing.Border = new ZoneBorderConfig
                            {
                                Enabled = true,
                                PrefabGuid = existing.BorderGlowPrefabId,
                                PrefabName = existing.BorderGlowPrefab ?? string.Empty,
                                Spacing = DefaultBorderSpacing,
                                HeightOffset = DefaultBorderHeightOffset
                            };
                            normalized = true;
                        }

                        if (existing.Border != null)
                        {
                            if (existing.Border.PrefabGuid == 0 && existing.BorderGlowPrefabId != 0)
                            {
                                existing.Border.PrefabGuid = existing.BorderGlowPrefabId;
                                normalized = true;
                            }
                            if (string.IsNullOrWhiteSpace(existing.Border.PrefabName) && !string.IsNullOrWhiteSpace(existing.BorderGlowPrefab))
                            {
                                existing.Border.PrefabName = existing.BorderGlowPrefab;
                                normalized = true;
                            }
                            if (existing.Border.Spacing < 1f)
                            {
                                existing.Border.Spacing = DefaultBorderSpacing;
                                normalized = true;
                            }

                            // Name -> GUID normalization using PrefabResolver (world-independent).
                            if (existing.Border.PrefabGuid == 0 && !string.IsNullOrWhiteSpace(existing.Border.PrefabName) &&
                                PrefabResolver.TryResolve(existing.Border.PrefabName, out var resolvedBorder))
                            {
                                existing.Border.PrefabGuid = resolvedBorder.GuidHash;
                                normalized = true;
                            }

                            // Keep legacy fields coherent only when the zone explicitly configures a border marker.
                            if (existing.Border.PrefabGuid != 0 || !string.IsNullOrWhiteSpace(existing.Border.PrefabName))
                            {
                                if (existing.BorderGlowPrefabId == 0 && existing.Border.PrefabGuid != 0)
                                {
                                    existing.BorderGlowPrefabId = existing.Border.PrefabGuid;
                                    normalized = true;
                                }
                                if (string.IsNullOrWhiteSpace(existing.BorderGlowPrefab) && !string.IsNullOrWhiteSpace(existing.Border.PrefabName))
                                {
                                    existing.BorderGlowPrefab = existing.Border.PrefabName;
                                    normalized = true;
                                }
                            }
                        }

                if (string.IsNullOrWhiteSpace(existing.DisplayName))
                {
                    existing.DisplayName = template.DisplayName;
                    normalized = true;
                }

                if (string.IsNullOrWhiteSpace(existing.Shape))
                {
                    existing.Shape = template.Shape;
                    normalized = true;
                }

                if (existing.CenterY == 0f && template.CenterY != 0f)
                {
                    existing.CenterY = template.CenterY;
                    normalized = true;
                }

                if (string.IsNullOrWhiteSpace(existing.GlowTilePrefab))
                {
                    existing.GlowTilePrefab = template.GlowTilePrefab;
                    normalized = true;
                }

                if (existing.GlowTilePrefabId == 0 && template.GlowTilePrefabId != 0)
                {
                    existing.GlowTilePrefabId = template.GlowTilePrefabId;
                    normalized = true;
                }

                if (existing.GlowTileSpacing <= 0f)
                {
                    existing.GlowTileSpacing = template.GlowTileSpacing;
                    normalized = true;
                }

                if (existing.GlowTileHeightOffset == 0f && template.GlowTileHeightOffset != 0f)
                {
                    existing.GlowTileHeightOffset = template.GlowTileHeightOffset;
                    normalized = true;
                }

                if (existing.GlowTileRotationDegrees != template.GlowTileRotationDegrees)
                {
                    existing.GlowTileRotationDegrees = template.GlowTileRotationDegrees;
                    normalized = true;
                }

                if (existing.GlowTileEnabled != template.GlowTileEnabled)
                {
                    existing.GlowTileEnabled = template.GlowTileEnabled;
                    normalized = true;
                }

                if (existing.GlowTileAutoSpawnOnEnter != template.GlowTileAutoSpawnOnEnter)
                {
                    existing.GlowTileAutoSpawnOnEnter = template.GlowTileAutoSpawnOnEnter;
                    normalized = true;
                }

                if (existing.GlowTileAutoSpawnOnReset != template.GlowTileAutoSpawnOnReset)
                {
                    existing.GlowTileAutoSpawnOnReset = template.GlowTileAutoSpawnOnReset;
                    normalized = true;
                }

                        if (existing.Radius <= 0f)
                        {
                            existing.Radius = template.Radius;
                            normalized = true;
                        }

                        if (existing.BorderGlowPrefabId == -1408736701 ||
                            string.Equals(existing.BorderGlowPrefab, "eSwatch_Color_StrongbladeDLC_ModularCarpets", StringComparison.OrdinalIgnoreCase))
                        {
                            existing.BorderGlowPrefabId = 0;
                            existing.BorderGlowPrefab = string.Empty;
                            normalized = true;
                        }

                        if (existing.Id == template.Id && Math.Abs(existing.CenterX) < 0.001f && Math.Abs(existing.CenterZ) < 0.001f)
                        {
                            existing.CenterX = template.CenterX;
                            existing.CenterZ = template.CenterZ;
                            normalized = true;
                        }

                        continue;
                    }

                    _zonesConfig.Zones.Add(template);
                    added = true;
                }

                if (added || normalized)
                {
                    if (SaveZonesConfig())
                    {
                        ZoneCore.LogInfo(added
                            ? "[ZoneConfigService] Added missing numeric default zones."
                            : "[ZoneConfigService] Normalized numeric default zones.");
                    }
                    else
                    {
                        ZoneCore.LogWarning("[ZoneConfigService] Failed to persist normalized/default zone values.");
                    }
                }
            }
            catch (Exception ex)
            {
                ZoneCore.LogError($"[ZoneConfigService] Failed to ensure default zones: {ex.Message}");
            }
        }

        private static IEnumerable<ZoneDefinition> BuildDefaultZones()
        {
            var templates = new List<ZoneDefinition>();
            var kitTemplate = new[] { "Kit1", "Kit2", "Kit3" };

            for (int i = 0; i < NumericZoneIds.Length; i++)
            {
                var id = NumericZoneIds[i];
                var center = i < DefaultZoneCenters.Length ? DefaultZoneCenters[i] : DefaultZoneCenters[0];
                templates.Add(new ZoneDefinition
                {
                    Id = id,
                    Tags = new List<string> { "sandbox" },
                    DisplayName = $"Arena {id}",
                    Shape = "Circle",
                    CenterX = center.x,
                    CenterZ = center.z,
                    Radius = DefaultZoneRadius,
                    CenterY = center.y,
                    KitToApplyId = kitTemplate.Length > i ? kitTemplate[i] : $"Kit{id}",
                    KitId = kitTemplate.Length > i ? kitTemplate[i] : $"Kit{id}",
                    AbilityPresetSlots = new[]
                    {
                        "AB_Vampire_VeilOfBlood_Group",
                        "AB_Vampire_VeilOfChaos_Group",
                        "AB_Vampire_VeilOfFrost_Group",
                        "AB_Vampire_VeilOfBones_AbilityGroup"
                    },
                    GlowEffectColorHex = "#FFD700",
                    GlowPrefabId = DefaultGlowBuffGuidA,
                    GlowPrefab = DefaultGlowBuffGuidA.ToString(),
                    GlowTilePrefabId = DefaultGlowBuffGuidA,
                    GlowTilePrefab = DefaultGlowBuffGuidA.ToString(),
                    GlowTileSpacing = DefaultGlowTileSpacing,
                    GlowTileHeightOffset = DefaultGlowTileHeightOffset,
                    GlowTileRotationDegrees = 0f,
                    GlowTileEnabled = true,
                    GlowTileAutoSpawnOnEnter = true,
                    GlowTileAutoSpawnOnReset = true,
                    GlowSpawnHeight = 0.3f,
                    AutoGlowWithZone = true,
                    
                    // Border glow boundaries configuration
                    GlowBoundariesEnabled = true,
                    GlowBoundariesAutoSpawn = false,
                    GlowBoundariesSpacing = 3f,
                    GlowBoundariesSpawnHeight = 25f,
                    GlowBoundariesTeleportToLibrary = true,
                    GlowBoundariesAutoCleanup = true,
                    
                    // Zone-specific glow prefabs for borders
                    GlowBoundariesPrefab = i switch
                    {
                        0 => "AB_Vampire_VeilOfBlood_Group",
                        1 => "AB_Vampire_VeilOfChaos_Group", 
                        2 => "AB_Vampire_VeilOfFrost_Group",
                        _ => "AB_Militia_LightArrow_SpawnMinions_Summon"
                    },
                    GlowBoundariesPrefabId = i switch
                    {
                        0 => 4345235,
                        1 => 54252435,
                        2 => 65245252,
                        _ => 4345235
                    },
                    EnterMessage = $"Entered arena zone {id}.",
                    ExitMessage = $"Exited arena zone {id}.",
                    TeleportOnEnter = true,
                    TeleportX = center.x,
                    TeleportY = center.y,
                    TeleportZ = center.z,
                    ReturnOnExit = true,
                    Templates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["arena"] = "arena_default"
                    }
                });
            }

            return templates;
        }

        private static void LogLoadedZones()
        {
            foreach (var zone in GetAllZones())
            {
                var templateCount = GetBuildTemplatesForZone(zone.Id).Count;
                ZoneCore.LogInfo($"[ZoneConfigService]   Zone '{zone.Id}' ({zone.DisplayName}): {zone.Shape} at ({zone.CenterX}, {zone.CenterZ}) r={zone.Radius}, Kit={zone.KitToApplyId ?? "none"}, Templates={templateCount}, ArenaDamage={zone.IsArenaZone}");

                if (zone.IsArenaZone && templateCount == 0)
                {
                    ZoneCore.LogWarning($"[ZoneConfigService]   Zone '{zone.Id}' is ArenaDamage-enabled but has zero templates. Only PvP-style behavior will be present until templates are configured.");
                }
            }
        }

        /// <summary>
        /// Get all zones.
        /// </summary>
        public static List<ZoneDefinition> GetAllZones()
        {
            return _zonesConfig?.Zones ?? new List<ZoneDefinition>();
        }

        /// <summary>
        /// Get zone by ID.
        /// </summary>
        public static ZoneDefinition GetZoneById(string zoneId)
        {
            if (_zonesConfig?.Zones == null) return null;
            
            foreach (var zone in _zonesConfig.Zones)
            {
                if (zone.Id.Equals(zoneId, StringComparison.OrdinalIgnoreCase))
                {
                    return zone;
                }
            }
            
            return null;
        }

        /// <summary>
        /// Find zone at position.
        /// </summary>
        public static ZoneDefinition GetZoneAtPosition(float x, float z)
        {
            var matches = GetZoneMatchesAtPosition(x, z);
            return matches.Count > 0 ? matches[0].Zone : null;
        }

        /// <summary>
        /// Returns all matching zones at a position in deterministic priority order.
        /// Priority:
        /// 1) default zone when matched
        /// 2) smaller footprint (more specific zone)
        /// 3) closer center distance
        /// 4) lexical zone id (stable tie-break)
        /// </summary>
        public static List<ZoneMatchInfo> GetZoneMatchesAtPosition(float x, float z)
        {
            var matches = new List<ZoneMatchInfo>();
            if (_zonesConfig?.Zones == null)
            {
                return matches;
            }

            var defaultZoneId = GetDefaultZoneId();
            foreach (var zone in _zonesConfig.Zones)
            {
                if (zone == null || !zone.IsInside(x, z))
                {
                    continue;
                }

                var centerDx = x - zone.CenterX;
                var centerDz = z - zone.CenterZ;
                var distanceSq = centerDx * centerDx + centerDz * centerDz;
                var footprintRadius = GetZoneFootprintRadius(zone);
                var footprintArea = math.PI * footprintRadius * footprintRadius;
                var isDefault = !string.IsNullOrWhiteSpace(defaultZoneId) &&
                                string.Equals(zone.Id, defaultZoneId, StringComparison.OrdinalIgnoreCase);

                matches.Add(new ZoneMatchInfo
                {
                    Zone = zone,
                    IsDefault = isDefault,
                    DistanceSq = distanceSq,
                    FootprintRadius = footprintRadius,
                    FootprintArea = footprintArea
                });
            }

            matches.Sort((a, b) =>
            {
                if (a.IsDefault != b.IsDefault)
                {
                    return a.IsDefault ? -1 : 1;
                }

                var areaCompare = a.FootprintArea.CompareTo(b.FootprintArea);
                if (areaCompare != 0)
                {
                    return areaCompare;
                }

                var distanceCompare = a.DistanceSq.CompareTo(b.DistanceSq);
                if (distanceCompare != 0)
                {
                    return distanceCompare;
                }

                return string.Compare(a.Zone?.Id ?? string.Empty, b.Zone?.Id ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            });

            return matches;
        }

        /// <summary>
        /// Checks whether a candidate zone location matches an existing zone location.
        /// Used to prevent duplicate template-zone footprints.
        /// </summary>
        public static bool TryFindSameLocationZone(
            string candidateZoneId,
            float centerX,
            float centerZ,
            float referenceRadius,
            out ZoneDefinition conflictingZone)
        {
            conflictingZone = null;
            if (_zonesConfig?.Zones == null)
            {
                return false;
            }

            var candidateId = candidateZoneId ?? string.Empty;
            var normalizedRadius = math.max(0.1f, referenceRadius);
            foreach (var zone in _zonesConfig.Zones)
            {
                if (zone == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(candidateId) &&
                    string.Equals(zone.Id, candidateId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var dx = centerX - zone.CenterX;
                var dz = centerZ - zone.CenterZ;
                var distance = math.sqrt(dx * dx + dz * dz);
                if (distance > SameLocationCenterTolerance)
                {
                    continue;
                }

                var zoneRadius = GetZoneFootprintRadius(zone);
                if (math.abs(zoneRadius - normalizedRadius) > SameLocationRadiusTolerance)
                {
                    continue;
                }

                conflictingZone = zone;
                return true;
            }

            return false;
        }

        public static bool AreZonesSameLocation(ZoneDefinition left, ZoneDefinition right)
        {
            if (left == null || right == null)
            {
                return false;
            }

            var dx = left.CenterX - right.CenterX;
            var dz = left.CenterZ - right.CenterZ;
            var centerDistance = math.sqrt(dx * dx + dz * dz);
            if (centerDistance > SameLocationCenterTolerance)
            {
                return false;
            }

            var leftRadius = GetZoneFootprintRadius(left);
            var rightRadius = GetZoneFootprintRadius(right);
            return math.abs(leftRadius - rightRadius) <= SameLocationRadiusTolerance;
        }

        public static float GetZoneFootprintRadius(ZoneDefinition zone)
        {
            if (zone == null)
            {
                return 0f;
            }

            if (zone.ShapeType == ZoneShapeType.Rectangle)
            {
                var width = math.max(0f, zone.MaxX - zone.MinX);
                var height = math.max(0f, zone.MaxZ - zone.MinZ);
                if (width > 0f || height > 0f)
                {
                    return math.sqrt(width * width + height * height) * 0.5f;
                }
            }

            return math.max(0.1f, zone.Radius);
        }

        /// <summary>
        /// Get number of loaded zones.
        /// </summary>
        public static int GetZoneCount()
        {
            return _zonesConfig?.Zones?.Count ?? 0;
        }

        /// <summary>
        /// Get kit ID for a zone.
        /// </summary>
        public static string GetKitIdForZone(string zoneId)
        {
            var zone = GetZoneById(zoneId);
            if (!string.IsNullOrWhiteSpace(zone?.KitId))
            {
                return zone.KitId;
            }

            if (!string.IsNullOrWhiteSpace(zone?.KitToApplyId))
            {
                return zone.KitToApplyId;
            }

            if (!string.IsNullOrWhiteSpace(_zonesConfig?.DefaultKitId))
            {
                return _zonesConfig.DefaultKitId;
            }

            return string.Empty;
        }

        public static string GetDefaultKitId()
        {
            return _zonesConfig?.DefaultKitId;
        }

        public static string GetDefaultZoneId()
        {
            return _zonesConfig?.DefaultZoneId ?? string.Empty;
        }

        public static ZoneDefinition GetDefaultZone()
        {
            var id = GetDefaultZoneId();
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            return GetZoneById(id);
        }

        public static bool HasDefaultZone()
        {
            return GetDefaultZone() != null;
        }

        public static bool SetDefaultZoneId(string zoneId)
        {
            if (string.IsNullOrWhiteSpace(zoneId))
            {
                return false;
            }

            var zone = GetZoneById(zoneId);
            if (zone == null)
            {
                return false;
            }

            _zonesConfig ??= new ZonesConfig();
            _zonesConfig.DefaultZoneId = zone.Id;
            return SaveZonesConfig();
        }

        public static List<string> GetSchematicsForZone(string zoneId)
        {
            var zone = GetZoneById(zoneId);
            return zone?.Schematics ?? new List<string>();
        }

        public static List<string> GetBuildTemplatesForZone(string zoneId)
        {
            var zone = GetZoneById(zoneId);
            if (zone == null)
            {
                return new List<string>();
            }

            if (zone.Templates != null && zone.Templates.Count > 0)
            {
                return new List<string>(zone.Templates.Values);
            }

            // Backward compatibility for older config naming.
            if (zone.Schematics != null && zone.Schematics.Count > 0)
            {
                return zone.Schematics;
            }

            if (zone.LegacyTemplates != null && zone.LegacyTemplates.Count > 0)
            {
                return zone.LegacyTemplates;
            }

            return new List<string>();
        }

        /// <summary>
        /// Registers or updates a zone template mapping and persists the zones config.
        /// </summary>
        public static bool SetTemplateForZone(string zoneId, string templateType, string templateName, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(zoneId))
            {
                error = "Zone ID is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(templateType))
            {
                error = "Template type is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(templateName))
            {
                error = "Template name is required.";
                return false;
            }

            var zone = GetZoneById(zoneId);
            if (zone == null)
            {
                error = $"Zone '{zoneId}' not found.";
                return false;
            }

            zone.Templates ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            zone.Templates[templateType.Trim()] = templateName.Trim();

            if (!SaveZonesConfig())
            {
                error = "Failed to save zones configuration.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Removes a zone template mapping and persists the zones config.
        /// </summary>
        public static bool RemoveTemplateForZone(string zoneId, string templateType, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(zoneId))
            {
                error = "Zone ID is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(templateType))
            {
                error = "Template type is required.";
                return false;
            }

            var zone = GetZoneById(zoneId);
            if (zone == null)
            {
                error = $"Zone '{zoneId}' not found.";
                return false;
            }

            if (zone.Templates == null || zone.Templates.Count == 0)
            {
                error = $"Zone '{zoneId}' has no registered templates.";
                return false;
            }

            if (!zone.Templates.Remove(templateType.Trim()))
            {
                error = $"Template type '{templateType}' not found in zone '{zoneId}'.";
                return false;
            }

            if (!SaveZonesConfig())
            {
                error = "Failed to save zones configuration.";
                return false;
            }

            return true;
        }

        public static bool TryGetTeleportPointForZone(string zoneId, out float x, out float y, out float z)
        {
            x = y = z = 0f;
            var zone = GetZoneById(zoneId);
            if (zone == null)
            {
                return false;
            }

            if (!zone.TeleportOnEnter)
            {
                return false;
            }

            // If zone teleport position is left empty, use full global default vector.
            if (zone.TeleportX == 0f && zone.TeleportY == 0f && zone.TeleportZ == 0f)
            {
                var fallback = _zonesConfig?.DefaultTeleport ?? new float3(-2657.5f, 10f, -987.5f);
                x = fallback.x;
                y = fallback.y;
                z = fallback.z;
                return true;
            }

                x = zone.TeleportX;
                y = zone.TeleportY;
                z = zone.TeleportZ;
                return true;
            }

        public static bool ShouldReturnOnExit(string zoneId)
        {
            var zone = GetZoneById(zoneId);
            return zone?.ReturnOnExit ?? false;
        }

        public static bool HasTag(string zoneId, string tag)
        {
            if (string.IsNullOrWhiteSpace(zoneId) || string.IsNullOrWhiteSpace(tag))
            {
                return false;
            }

            var zone = GetZoneById(zoneId);
            if (zone?.Tags == null || zone.Tags.Count == 0)
            {
                return false;
            }

            return zone.Tags.Exists(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Resolves sandbox progression unlock behavior for a zone.
        /// Uses per-zone override when present, otherwise falls back to global default.
        /// </summary>
        public static bool IsSandboxUnlockEnabled(string zoneId, bool globalDefault)
        {
            var zone = GetZoneById(zoneId);
            if (zone == null)
            {
                return false;
            }

            if (zone.Tags == null || !zone.Tags.Exists(tag => string.Equals(tag, "sandbox", StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            if (zone.SandboxUnlockEnabled.HasValue)
            {
                return zone.SandboxUnlockEnabled.Value;
            }

            return globalDefault;
        }

        public static string[] GetAbilityPresetSlotsForZone(string zoneId)
        {
            var zone = GetZoneById(zoneId);
            return zone?.AbilityPresetSlots ?? Array.Empty<string>();
        }

        public static ZoneBorderConfig GetBorderConfigForZone(string zoneId)
        {
            var zone = GetZoneById(zoneId);
            return zone?.Border;
        }

        /// <summary>
        /// Gets the effective border config for a zone (per-zone override merged with ZonesConfig.DefaultBorder).
        /// Never returns null.
        /// </summary>
        public static ZoneBorderConfig GetEffectiveBorderConfigForZone(string zoneId)
        {
            var zone = GetZoneById(zoneId);
            return GetEffectiveBorderConfig(zone);
        }

        /// <summary>
        /// Gets the effective border config for a zone definition (per-zone override merged with ZonesConfig.DefaultBorder).
        /// Never returns null.
        /// </summary>
        public static ZoneBorderConfig GetEffectiveBorderConfig(ZoneDefinition zone)
        {
            var defaults = _zonesConfig?.DefaultBorder ?? new ZoneBorderConfig
            {
                Enabled = true,
                PrefabGuid = DefaultBorderMarkerPrefabId,
                PrefabName = DefaultBorderMarkerPrefabName,
                Spacing = DefaultBorderSpacing,
                HeightOffset = DefaultBorderHeightOffset
            };

            // Defensive: config deserialization may leave DefaultBorder null for older files.
            defaults ??= new ZoneBorderConfig
            {
                Enabled = true,
                PrefabGuid = DefaultBorderMarkerPrefabId,
                PrefabName = DefaultBorderMarkerPrefabName,
                Spacing = DefaultBorderSpacing,
                HeightOffset = DefaultBorderHeightOffset
            };

            var perZone = zone?.Border;

            var effective = new ZoneBorderConfig
            {
                Enabled = perZone?.Enabled ?? defaults.Enabled,
                PrefabName = perZone != null && !string.IsNullOrWhiteSpace(perZone.PrefabName) ? perZone.PrefabName : (defaults.PrefabName ?? string.Empty),
                PrefabGuid = perZone != null && perZone.PrefabGuid != 0 ? perZone.PrefabGuid : defaults.PrefabGuid,
                Spacing = perZone != null && perZone.Spacing >= 1f ? perZone.Spacing : Math.Max(1f, defaults.Spacing),
                HeightOffset = perZone?.HeightOffset ?? defaults.HeightOffset
            };

            if (effective.PrefabGuid == 0 && !string.IsNullOrWhiteSpace(effective.PrefabName) &&
                PrefabResolver.TryResolve(effective.PrefabName, out var resolved))
            {
                effective.PrefabGuid = resolved.GuidHash;
            }

            if (effective.PrefabGuid == 0 && string.IsNullOrWhiteSpace(effective.PrefabName))
            {
                effective.PrefabGuid = DefaultBorderMarkerPrefabId;
                effective.PrefabName = DefaultBorderMarkerPrefabName;
            }

            return effective;
        }

        /// <summary>
        /// Get glow color for a zone.
        /// </summary>
        public static string GetGlowColorForZone(string zoneId)
        {
            var zone = GetZoneById(zoneId);
            return zone?.GlowEffectColorHex;
        }

        /// <summary>
        /// Get glow prefab ID for a zone.
        /// </summary>
        public static int GetGlowPrefabIdForZone(string zoneId)
        {
            var zone = GetZoneById(zoneId);
            return zone?.GlowPrefabId ?? 0;
        }

        /// <summary>
        /// Get glow spawn height for a zone.
        /// </summary>
        public static float GetGlowSpawnHeightForZone(string zoneId)
        {
            var zone = GetZoneById(zoneId);
            return zone?.GlowSpawnHeight ?? 0f;
        }

        /// <summary>
        /// Should glow tiles auto spawn when the zone first activates.
        /// </summary>
        public static bool ShouldAutoSpawnGlowOnEnter(string zoneId)
        {
            var zone = GetZoneById(zoneId);
            return zone?.GlowTileEnabled == true && zone.GlowTileAutoSpawnOnEnter;
        }

        /// <summary>
        /// Should glow tiles clear/reset when the zone empties.
        /// </summary>
        public static bool ShouldAutoSpawnGlowOnReset(string zoneId)
        {
            var zone = GetZoneById(zoneId);
            return zone?.GlowTileEnabled == true && zone.GlowTileAutoSpawnOnReset;
        }

        public static bool IsAutoGlowEnabledForZone(string zoneId)
        {
            var zone = GetZoneById(zoneId);
            return zone?.AutoGlowWithZone ?? false;
        }

        public static bool SetAutoGlowForZone(string zoneId, bool enabled)
        {
            var zone = GetZoneById(zoneId);
            if (zone == null)
            {
                return false;
            }

            zone.AutoGlowWithZone = enabled;
            return SaveZonesConfig();
        }

        public static bool SaveZonesConfig()
        {
            return TypedJsonConfigManager.TrySave(
                ConfigPath,
                _zonesConfig ?? new ZonesConfig(),
                CreateZoneSerializerOptions(writeIndented: true),
                message => ZoneCore.LogDebug($"[ZoneConfigService] {message}"),
                message => ZoneCore.LogError($"[ZoneConfigService] {message}"));
        }

        /// <summary>
        /// Get enter message for a zone.
        /// </summary>
        public static string GetEnterMessageForZone(string zoneId)
        {
            var zone = GetZoneById(zoneId);
            return zone?.EnterMessage;
        }

        /// <summary>
        /// Get exit message for a zone.
        /// </summary>
        public static string GetExitMessageForZone(string zoneId)
        {
            var zone = GetZoneById(zoneId);
            return zone?.ExitMessage;
        }

        /// <summary>
        /// Reload zones configuration.
        /// </summary>
        public static void Reload()
        {
            _initialized = false;
            Initialize();
            ZoneCore.LogInfo("[ZoneConfigService] Configuration reloaded");
        }
    }

    public sealed class ZoneMatchInfo
    {
        public ZoneDefinition Zone { get; set; }
        public bool IsDefault { get; set; }
        public float DistanceSq { get; set; }
        public float FootprintRadius { get; set; }
        public float FootprintArea { get; set; }
    }
}
