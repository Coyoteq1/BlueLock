using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using BepInEx;
using ProjectM.Network;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireCommandFramework;
using VAuto.Zone.Core;
using VAuto.Zone.Models;
using VAuto.Zone.Services;
using VAutomationCore.Core.Config;

namespace VAuto.Zone.Commands
{
    /// <summary>
    /// Admin commands for managing arena zones.
    /// Provides create/remove/list/on/off/center/radius/tp functionality.
    /// </summary>
    [CommandGroup("zone", "z")]
    public static class ZoneCommands
    {
        private static readonly string ZonesFile = VAutoPathMap.GetConfigFile(VAutoModule.Zone, "VAuto.Zones.json");

        /// <summary>
        /// Show help for arena admin commands.
        /// </summary>
        [Command("help", shortHand: "h", description: "Show arena admin command help", adminOnly: false)]
        public static void ArenaAdminHelp(ChatCommandContext ctx)
        {
            var message = @"<color=#FFD700>[Arena Admin Commands]</color>
<color=#00FFFF>.z create [radius] (.z c)</color> - Create new arena zone with auto numeric ID
<color=#00FFFF>.z remove [name] (.z rem)</color> - Remove arena zone by name
<color=#00FFFF>.z list (.z l)</color> - List all arena zones
<color=#00FFFF>.z on [name] (.z enable)</color> - Enable arena zone
<color=#00FFFF>.z off [name] (.z disable)</color> - Disable arena zone
<color=#00FFFF>.z center [name] (.z cen)</color> - Set zone center to your position
<color=#00FFFF>.z radius [name] [radius] (.z r)</color> - Set zone radius
<color=#00FFFF>.z tp [name] (.z teleport)</color> - Teleport to zone center
<color=#00FFFF>.z status [name] (.z s)</color> - Show zone details including lifecycle status
<color=#00FFFF>.z default [name] (.z d)</color> - Set default zone (checked first for zone detection)";
            ctx.Reply(message);
        }

        /// <summary>
        /// Create a new arena zone at the command user's position.
        /// </summary>
        [Command("create", shortHand: "c", description: "Create new arena zone at your position", adminOnly: true)]
        public static void ArenaCreate(ChatCommandContext ctx, float radius = 50f)
        {
            try
            {
                if (radius <= 0)
                {
                    ctx.Reply("<color=#FF0000>Error: Radius must be greater than 0.</color>");
                    return;
                }

                if (!TryGetPlayerPosition(ctx, out var position))
                {
                    ctx.Reply("<color=#FF0000>Error: Could not determine your position.</color>");
                    return;
                }

                var zones = LoadZones();
                var name = GetNextNumericZoneId(zones);
                
                if (zones.Any(z => string.Equals(z.Name, name, StringComparison.OrdinalIgnoreCase)))
                {
                    ctx.Reply($"<color=#FF0000>Error: Zone '{name}' already exists.</color>");
                    return;
                }

                var newZone = new ArenaZoneDef
                {
                    Name = name,
                    Center = position,
                    Radius = radius,
                    Shape = ArenaZoneShape.Circle,
                    LifecycleEnabled = true
                };

                zones.Add(newZone);

                if (SaveZones(zones))
                {
                    ctx.Reply($"<color=#00FF00>Created zone '{name}' at ({position.x:F0}, {position.y:F0}, {position.z:F0}) with radius {radius}m. Lifecycle enabled: true</color>");
                    ZoneCore.LogInfo($"[ArenaAdmin] Created zone '{name}' at {position} with radius {radius}");
                }
                else
                {
                    ctx.Reply("<color=#FF0000>Error: Failed to save zone configuration.</color>");
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"ArenaCreate error: {ex.Message}");
                ctx.Reply("<color=#FF0000>Error creating zone.</color>");
            }
        }

        /// <summary>
        /// Remove an arena zone by name.
        /// </summary>
        [Command("remove", shortHand: "rem", description: "Remove arena zone by name", adminOnly: true)]
        public static void ArenaRemove(ChatCommandContext ctx, string name)
        {
            try
            {
                if (string.IsNullOrEmpty(name))
                {
                    ctx.Reply("<color=#FF0000>Error: Zone name required.</color>");
                    return;
                }

                var zones = LoadZones();
                var zoneToRemove = zones.FirstOrDefault(z => string.Equals(z.Name, name, StringComparison.OrdinalIgnoreCase));

                if (zoneToRemove == null)
                {
                    ctx.Reply($"<color=#FF0000>Error: Zone '{name}' not found.</color>");
                    return;
                }

                zones.Remove(zoneToRemove);

                if (SaveZones(zones))
                {
                    ctx.Reply($"<color=#00FF00>Removed zone '{name}'.</color>");
                    ZoneCore.LogInfo($"[ArenaAdmin] Removed zone '{name}'");
                }
                else
                {
                    ctx.Reply("<color=#FF0000>Error: Failed to save zone configuration.</color>");
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"ArenaRemove error: {ex.Message}");
                ctx.Reply("<color=#FF0000>Error removing zone.</color>");
            }
        }

        /// <summary>
        /// List all arena zones.
        /// </summary>
        [Command("list", shortHand: "l", description: "List all arena zones", adminOnly: false)]
        public static void ArenaList(ChatCommandContext ctx)
        {
            try
            {
                var zones = LoadZones();

                if (zones.Count == 0)
                {
                    ctx.Reply("[Arena] No zones configured.");
                    return;
                }

                var message = $"<color=#FFD700>[Arena Zones ({zones.Count})]</color>\n";
                var defaultZoneId = ZoneConfigService.GetDefaultZoneId();
                foreach (var zone in zones)
                {
                    var status = zone.LifecycleEnabled ? "<color=#00FF00>Lifecycle</color>" : "<color=#808080>Disabled</color>";
                    var marker = !string.IsNullOrWhiteSpace(defaultZoneId) &&
                                 string.Equals(zone.Name, defaultZoneId, StringComparison.OrdinalIgnoreCase)
                        ? " <color=#00BFFF>[DEFAULT]</color>"
                        : string.Empty;
                    message += $"{zone.Name}{marker}: {zone.Shape} at ({zone.Center.x:F0}, {zone.Center.y:F0}, {zone.Center.z:F0}) r={zone.Radius}m [{status}]\n";
                }

                ctx.Reply(message);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"ArenaList error: {ex.Message}");
                ctx.Reply("<color=#FF0000>Error listing zones.</color>");
            }
        }

        /// <summary>
        /// Enable an arena zone.
        /// </summary>
        [Command("on", shortHand: "enable", description: "Enable arena zone", adminOnly: true)]
        public static void ArenaEnable(ChatCommandContext ctx, string name)
        {
            SetZoneEnabled(ctx, name, true);
        }

        /// <summary>
        /// Disable an arena zone.
        /// </summary>
        [Command("off", shortHand: "disable", description: "Disable arena zone", adminOnly: true)]
        public static void ArenaDisable(ChatCommandContext ctx, string name)
        {
            SetZoneEnabled(ctx, name, false);
        }

        private static void SetZoneEnabled(ChatCommandContext ctx, string name, bool enabled)
        {
            try
            {
                if (string.IsNullOrEmpty(name))
                {
                    ctx.Reply("<color=#FF0000>Error: Zone name required.</color>");
                    return;
                }

                var zones = LoadZones();
                var zone = zones.FirstOrDefault(z => string.Equals(z.Name, name, StringComparison.OrdinalIgnoreCase));

                if (zone == null)
                {
                    ctx.Reply($"<color=#FF0000>Error: Zone '{name}' not found.</color>");
                    return;
                }

                zone.LifecycleEnabled = enabled;

                if (SaveZones(zones))
                {
                    var status = enabled ? "enabled" : "disabled";
                    ctx.Reply($"<color=#00FF00>Zone '{name}' {status}.</color>");
                    ZoneCore.LogInfo($"[ArenaAdmin] Zone '{name}' {status}");
                }
                else
                {
                    ctx.Reply("<color=#FF0000>Error: Failed to save zone configuration.</color>");
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"SetZoneEnabled error: {ex.Message}");
                ctx.Reply("<color=#FF0000>Error updating zone.</color>");
            }
        }

        /// <summary>
        /// Set zone center to command user's current position.
        /// </summary>
        [Command("center", shortHand: "cen", description: "Set zone center to your position", adminOnly: true)]
        public static void ArenaCenter(ChatCommandContext ctx, string name)
        {
            try
            {
                if (string.IsNullOrEmpty(name))
                {
                    ctx.Reply("<color=#FF0000>Error: Zone name required.</color>");
                    return;
                }

                if (!TryGetPlayerPosition(ctx, out var position))
                {
                    ctx.Reply("<color=#FF0000>Error: Could not determine your position.</color>");
                    return;
                }

                var zones = LoadZones();
                var zone = zones.FirstOrDefault(z => string.Equals(z.Name, name, StringComparison.OrdinalIgnoreCase));

                if (zone == null)
                {
                    ctx.Reply($"<color=#FF0000>Error: Zone '{name}' not found.</color>");
                    return;
                }

                var oldCenter = zone.Center;
                zone.Center = position;

                if (SaveZones(zones))
                {
                    ctx.Reply($"<color=#00FF00>Zone '{name}' center updated from ({oldCenter.x:F0}, {oldCenter.y:F0}, {oldCenter.z:F0}) to ({position.x:F0}, {position.y:F0}, {position.z:F0})</color>");
                    ZoneCore.LogInfo($"[ArenaAdmin] Zone '{name}' center updated from {oldCenter} to {position}");
                }
                else
                {
                    ctx.Reply("<color=#FF0000>Error: Failed to save zone configuration.</color>");
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"ArenaCenter error: {ex.Message}");
                ctx.Reply("<color=#FF0000>Error updating zone center.</color>");
            }
        }

        /// <summary>
        /// Set zone radius.
        /// </summary>
        [Command("radius", shortHand: "r", description: "Set zone radius", adminOnly: true)]
        public static void ArenaRadius(ChatCommandContext ctx, string name, float radius)
        {
            try
            {
                if (string.IsNullOrEmpty(name))
                {
                    ctx.Reply("<color=#FF0000>Error: Zone name required.</color>");
                    return;
                }

                if (radius <= 0)
                {
                    ctx.Reply("<color=#FF0000>Error: Radius must be greater than 0.</color>");
                    return;
                }

                var zones = LoadZones();
                var zone = zones.FirstOrDefault(z => string.Equals(z.Name, name, StringComparison.OrdinalIgnoreCase));

                if (zone == null)
                {
                    ctx.Reply($"<color=#FF0000>Error: Zone '{name}' not found.</color>");
                    return;
                }

                zone.Radius = radius;

                if (SaveZones(zones))
                {
                    ctx.Reply($"<color=#00FF00>Zone '{name}' radius updated to {radius}m.</color>");
                    ZoneCore.LogInfo($"[ArenaAdmin] Zone '{name}' radius updated to {radius}");
                }
                else
                {
                    ctx.Reply("<color=#FF0000>Error: Failed to save zone configuration.</color>");
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"ArenaRadius error: {ex.Message}");
                ctx.Reply("<color=#FF0000>Error updating zone radius.</color>");
            }
        }

        /// <summary>
        /// Teleport to zone center.
        /// </summary>
        [Command("tp", shortHand: "teleport", description: "Teleport to zone center", adminOnly: true)]
        public static void ArenaTeleport(ChatCommandContext ctx, string name)
        {
            try
            {
                if (string.IsNullOrEmpty(name))
                {
                    ctx.Reply("<color=#FF0000>Error: Zone name required.</color>");
                    return;
                }

                var zones = LoadZones();
                var zone = zones.FirstOrDefault(z => string.Equals(z.Name, name, StringComparison.OrdinalIgnoreCase));

                if (zone == null)
                {
                    ctx.Reply($"<color=#FF0000>Error: Zone '{name}' not found.</color>");
                    return;
                }

                if (!TryTeleportPlayer(ctx, zone.Center))
                {
                    ctx.Reply("<color=#FF0000>Error: Failed to teleport.</color>");
                    return;
                }

                ctx.Reply($"<color=#00FF00>Teleported to zone '{name}' center at ({zone.Center.x:F0}, {zone.Center.y:F0}, {zone.Center.z:F0})</color>");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"ArenaTeleport error: {ex.Message}");
                ctx.Reply("<color=#FF0000>Error teleporting.</color>");
            }
        }

        /// <summary>
        /// Show zone status including lifecycle details.
        /// </summary>
        [Command("status", shortHand: "s", description: "Show zone details including lifecycle status", adminOnly: false)]
        public static void ArenaStatus(ChatCommandContext ctx, string name)
        {
            try
            {
                if (string.IsNullOrEmpty(name))
                {
                    ctx.Reply("<color=#FF0000>Error: Zone name required.</color>");
                    return;
                }

                var zones = LoadZones();
                var zone = zones.FirstOrDefault(z => string.Equals(z.Name, name, StringComparison.OrdinalIgnoreCase));

                if (zone == null)
                {
                    ctx.Reply($"<color=#FF0000>Error: Zone '{name}' not found.</color>");
                    return;
                }

                var lifecycleStatus = zone.LifecycleEnabled ? "<color=#00FF00>Enabled</color>" : "<color=#FF0000>Disabled</color>";
                var isDefault = string.Equals(zone.Name, ZoneConfigService.GetDefaultZoneId(), StringComparison.OrdinalIgnoreCase)
                    ? "<color=#00BFFF>Yes</color>"
                    : "<color=#808080>No</color>";
                var message = $"<color=#FFD700>[Zone: {zone.Name}]</color>\n" +
                             $"Shape: {zone.Shape}\n" +
                             $"Center: ({zone.Center.x:F0}, {zone.Center.y:F0}, {zone.Center.z:F0})\n" +
                             $"Radius: {zone.Radius}m\n" +
                             $"Lifecycle: {lifecycleStatus}\n" +
                             $"Default: {isDefault}";
                
                ctx.Reply(message);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"ArenaStatus error: {ex.Message}");
                ctx.Reply("<color=#FF0000>Error retrieving zone status.</color>");
            }
        }

        #region Helper Methods

        [Command("default", shortHand: "d", description: "Set default zone (priority zone checked first)", adminOnly: true)]
        public static void ArenaDefault(ChatCommandContext ctx, string name)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    var current = ZoneConfigService.GetDefaultZoneId();
                    if (string.IsNullOrWhiteSpace(current))
                    {
                        ctx.Reply("<color=#FFFF00>No default zone is set.</color>");
                    }
                    else
                    {
                        ctx.Reply($"<color=#00BFFF>Current default zone: {current}</color>");
                    }
                    return;
                }

                if (!ZoneConfigService.SetDefaultZoneId(name))
                {
                    ctx.Reply($"<color=#FF0000>Error: Zone '{name}' not found or failed to set default.</color>");
                    return;
                }

                ctx.Reply($"<color=#00FF00>Default zone set to '{name}'. It will be checked first.</color>");
                ZoneCore.LogInfo($"[ArenaAdmin] Default zone set to '{name}'");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"ArenaDefault error: {ex.Message}");
                ctx.Reply("<color=#FF0000>Error setting default zone.</color>");
            }
        }

        private static List<ArenaZoneDef> LoadZones()
        {
            var zones = new List<ArenaZoneDef>();
            
            if (!File.Exists(ZonesFile))
            {
                ZoneCore.LogWarning($"[ArenaAdmin] Zones file not found: {ZonesFile}");
                return zones;
            }

            try
            {
                var json = File.ReadAllText(ZonesFile);
                using var doc = JsonDocument.Parse(json);

                // Primary schema: VAuto.Zones.json (capitalized "Zones")
                if (doc.RootElement.TryGetProperty("Zones", out var zonesEl) && zonesEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var zoneEl in zonesEl.EnumerateArray())
                    {
                        if (!zoneEl.TryGetProperty("Id", out var idEl) || idEl.ValueKind != JsonValueKind.String)
                        {
                            continue;
                        }

                        var zone = new ArenaZoneDef
                        {
                            Name = idEl.GetString() ?? string.Empty,
                            Shape = ArenaZoneShape.Circle,
                            Radius = 50f,
                            LifecycleEnabled = true
                        };

                        if (zoneEl.TryGetProperty("Shape", out var shapeEl) && shapeEl.ValueKind == JsonValueKind.String)
                        {
                            var shape = shapeEl.GetString() ?? "Circle";
                            zone.Shape = shape.Equals("Rectangle", StringComparison.OrdinalIgnoreCase)
                                ? ArenaZoneShape.Square
                                : ArenaZoneShape.Circle;
                        }

                        var cx = 0f;
                        var cz = 0f;
                        if (zoneEl.TryGetProperty("CenterX", out var cxEl) && cxEl.ValueKind == JsonValueKind.Number) cx = cxEl.GetSingle();
                        if (zoneEl.TryGetProperty("CenterZ", out var czEl) && czEl.ValueKind == JsonValueKind.Number) cz = czEl.GetSingle();
                        zone.Center = new float3(cx, 0f, cz);

                        if (zone.Shape == ArenaZoneShape.Circle)
                        {
                            if (zoneEl.TryGetProperty("Radius", out var radiusEl) && radiusEl.ValueKind == JsonValueKind.Number)
                            {
                                zone.Radius = radiusEl.GetSingle();
                            }
                        }
                        else
                        {
                            var minX = cx;
                            var maxX = cx;
                            var minZ = cz;
                            var maxZ = cz;
                            if (zoneEl.TryGetProperty("MinX", out var minXEl) && minXEl.ValueKind == JsonValueKind.Number) minX = minXEl.GetSingle();
                            if (zoneEl.TryGetProperty("MaxX", out var maxXEl) && maxXEl.ValueKind == JsonValueKind.Number) maxX = maxXEl.GetSingle();
                            if (zoneEl.TryGetProperty("MinZ", out var minZEl) && minZEl.ValueKind == JsonValueKind.Number) minZ = minZEl.GetSingle();
                            if (zoneEl.TryGetProperty("MaxZ", out var maxZEl) && maxZEl.ValueKind == JsonValueKind.Number) maxZ = maxZEl.GetSingle();
                            zone.Size = new float2(Math.Abs(maxX - minX), Math.Abs(maxZ - minZ));
                        }

                        zones.Add(zone);
                    }
                    return zones;
                }

                // Legacy schema fallback: arena_zones style ("zones")
                if (doc.RootElement.TryGetProperty("zones", out var legacyZonesEl) && legacyZonesEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var zoneEl in legacyZonesEl.EnumerateArray())
                    {
                        if (TryParseZone(zoneEl, out var zone, out _))
                        {
                            zones.Add(zone);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ZoneCore.LogError($"[ArenaAdmin] Failed to load zones: {ex.Message}");
            }

            return zones;
        }

        private static bool SaveZones(List<ArenaZoneDef> zones)
        {
            try
            {
                var configPath = Path.GetDirectoryName(ZonesFile);
                if (!string.IsNullOrEmpty(configPath) && !Directory.Exists(configPath))
                {
                    Directory.CreateDirectory(configPath);
                }

                var options = new JsonSerializerOptions { WriteIndented = true };
                var mapped = new ZonesConfig
                {
                    Description = "Zones managed by VAutoZone admin commands",
                    DefaultZoneId = ZoneConfigService.GetDefaultZoneId(),
                    DefaultKitId = ZoneConfigService.GetDefaultKitId(),
                    Zones = zones.Select(z =>
                    {
                        var zoneDef = new ZoneDefinition
                        {
                            Id = z.Name,
                            DisplayName = z.Name,
                            Shape = z.Shape == ArenaZoneShape.Square ? "Rectangle" : "Circle",
                            CenterX = z.Center.x,
                            CenterZ = z.Center.z,
                            Radius = z.Radius > 0 ? z.Radius : 50f,
                            AutoGlowWithZone = true
                        };

                        if (z.Shape == ArenaZoneShape.Square)
                        {
                            var halfX = Math.Max(1f, z.Size.x) * 0.5f;
                            var halfZ = Math.Max(1f, z.Size.y) * 0.5f;
                            zoneDef.MinX = z.Center.x - halfX;
                            zoneDef.MaxX = z.Center.x + halfX;
                            zoneDef.MinZ = z.Center.z - halfZ;
                            zoneDef.MaxZ = z.Center.z + halfZ;
                        }

                        return zoneDef;
                    }).ToList()
                };

                var json = JsonSerializer.Serialize(mapped, options);
                File.WriteAllText(ZonesFile, json);
                ZoneConfigService.Reload();
                return true;
            }
            catch (Exception ex)
            {
                ZoneCore.LogError($"[ArenaAdmin] Failed to save zones: {ex.Message}");
                return false;
            }
        }

        private static bool TryParseZone(JsonElement zoneEl, out ArenaZoneDef zone, out string error)
        {
            zone = new ArenaZoneDef();
            error = string.Empty;

            if (zoneEl.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String)
            {
                zone.Name = nameEl.GetString() ?? "";
            }

            if (!TryGetFloat3(zoneEl, "center", out var center))
            {
                error = "Zone missing valid 'center' [x,y,z].";
                return false;
            }
            zone.Center = center;

            if (zoneEl.TryGetProperty("radius", out var radiusEl) && radiusEl.ValueKind == JsonValueKind.Number)
            {
                zone.Shape = ArenaZoneShape.Circle;
                zone.Radius = radiusEl.GetSingle();
            }
            else if (zoneEl.TryGetProperty("size", out var sizeEl) && sizeEl.ValueKind == JsonValueKind.Array)
            {
                zone.Shape = ArenaZoneShape.Square;
                if (!TryGetFloat2(sizeEl, out var size))
                {
                    error = "Zone size must be [x,z].";
                    return false;
                }
                zone.Size = size;
            }

            // Parse lifecycleEnabled field
            if (zoneEl.TryGetProperty("lifecycleEnabled", out var lifecycleEl))
            {
                if (lifecycleEl.ValueKind == JsonValueKind.True || lifecycleEl.ValueKind == JsonValueKind.False)
                {
                    zone.LifecycleEnabled = lifecycleEl.GetBoolean();
                }
            }

            return true;
        }

        private static bool TryGetFloat3(JsonElement parent, string property, out float3 value)
        {
            value = float3.zero;
            if (!parent.TryGetProperty(property, out var el) || el.ValueKind != JsonValueKind.Array)
                return false;

            var arr = new List<float>(3);
            foreach (var item in el.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Number) return false;
                arr.Add(item.GetSingle());
            }
            if (arr.Count != 3) return false;

            value = new float3(arr[0], arr[1], arr[2]);
            return true;
        }

        private static bool TryGetFloat2(JsonElement el, out float2 value)
        {
            value = float2.zero;
            var arr = new List<float>(2);
            foreach (var item in el.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Number) return false;
                arr.Add(item.GetSingle());
            }
            if (arr.Count != 2) return false;

            value = new float2(arr[0], arr[1]);
            return true;
        }

        private static bool TryGetPlayerPosition(ChatCommandContext ctx, out float3 position)
        {
            position = float3.zero;
            try
            {
                var serverWorld = ZoneCore.Server;
                if (serverWorld == null) return false;

                var entityManager = serverWorld.EntityManager;
                var characterEntity = ctx.Event?.SenderCharacterEntity ?? Entity.Null;
                if (characterEntity == Entity.Null || !entityManager.Exists(characterEntity)) return false;

                if (entityManager.HasComponent<LocalTransform>(characterEntity))
                {
                    position = entityManager.GetComponentData<LocalTransform>(characterEntity).Position;
                    return true;
                }

                if (entityManager.HasComponent<Translation>(characterEntity))
                {
                    position = entityManager.GetComponentData<Translation>(characterEntity).Value;
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryTeleportPlayer(ChatCommandContext ctx, float3 targetPosition)
        {
            try
            {
                var characterEntity = ctx.Event?.SenderCharacterEntity ?? Entity.Null;
                if (characterEntity == Entity.Null) return false;

                var em = ZoneCore.EntityManager;
                if (!em.Exists(characterEntity)) return false;

                // Set position using LocalTransform
                if (em.HasComponent<LocalTransform>(characterEntity))
                {
                    var transform = LocalTransform.FromPositionRotation(targetPosition, quaternion.identity);
                    em.SetComponentData(characterEntity, transform);
                    return true;
                }

                // Fallback to Translation component
                if (em.HasComponent<Translation>(characterEntity))
                {
                    em.SetComponentData(characterEntity, new Translation { Value = targetPosition });
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                ZoneCore.LogError($"[ArenaAdmin] Teleport failed: {ex.Message}");
                return false;
            }
        }

        [Command("glowimport", shortHand: "gimp", description: "Import glow library from Kindred glowChoices.txt", adminOnly: true)]
        public static void GlowImport(ChatCommandContext ctx, string sourcePath = "")
        {
            try
            {
                var candidates = new List<string>();
                if (!string.IsNullOrWhiteSpace(sourcePath))
                {
                    candidates.Add(sourcePath);
                }

                var configRoot = Paths.ConfigPath;
                candidates.Add(Path.Combine(configRoot, "KindredSchematics", "glowChoices.txt"));
                candidates.Add(Path.Combine(configRoot, "KindredCommands", "glowChoices.txt"));
                candidates.Add(Path.Combine(configRoot, "KindredSchematics", "Config", "glowChoices.txt"));

                var path = candidates.FirstOrDefault(File.Exists);
                if (string.IsNullOrWhiteSpace(path))
                {
                    ctx.Reply("<color=#FF0000>Kindred glowChoices.txt not found. Pass explicit path: .z glowimport \"C:\\path\\glowChoices.txt\"</color>");
                    return;
                }

                var targetDir = Path.Combine(Paths.ConfigPath, "VAuto.Arena");
                var targetPath = Path.Combine(targetDir, "glowChoices.txt");
                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                var existing = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                if (File.Exists(targetPath))
                {
                    foreach (var line in File.ReadAllLines(targetPath))
                    {
                        var p = line.Split('=', 2);
                        if (p.Length == 2 && int.TryParse(p[1].Trim(), out var g))
                        {
                            existing[p[0].Trim()] = g;
                        }
                    }
                }

                var imported = 0;
                var skipped = 0;
                foreach (var line in File.ReadAllLines(path))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var p = line.Split('=', 2);
                    if (p.Length != 2 || !int.TryParse(p[1].Trim(), out var g))
                    {
                        skipped++;
                        continue;
                    }

                    var key = p[0].Trim();
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        skipped++;
                        continue;
                    }

                    if (existing.ContainsKey(key))
                    {
                        skipped++;
                        continue;
                    }

                    existing[key] = g;
                    imported++;
                }

                File.WriteAllLines(targetPath, existing.Select(kvp => $"{kvp.Key}={kvp.Value}"));
                ctx.Reply($"<color=#00FF00>Glow library imported from '{path}'. Imported: {imported}, Skipped: {skipped}. Target: '{targetPath}'</color>");
            }
            catch (Exception ex)
            {
                ZoneCore.LogError($"[ArenaAdmin] glowimport failed: {ex.Message}");
                ctx.Reply("<color=#FF0000>Error importing glow library.</color>");
            }
        }

        [Command("glow", shortHand: "g", description: "Glow status (use .z glow help)", adminOnly: true)]
        public static void Glow(ChatCommandContext ctx, string action = "status")
        {
            action = (action ?? "status").Trim().ToLowerInvariant();
            if (action is "status" or "s")
            {
                var zones = ZoneConfigService.GetAllZones();
                var autoGlowZones = zones.Count(z => z != null && z.AutoGlowWithZone);
                ctx.Reply($"<color=#FFD700>[Glow]</color> Enabled={Plugin.GlowSystemEnabledValue} ZonesAutoGlow={autoGlowZones}/{zones.Count} ActiveEntities={Plugin.ActiveGlowEntityCount}");
                return;
            }

            if (action is "rebuild" or "rb")
            {
                Plugin.ForceGlowRebuild();
                ctx.Reply($"<color=#00FF00>Glow borders rebuilt. Active entities: {Plugin.ActiveGlowEntityCount}</color>");
                return;
            }

            if (action is "clear" or "c")
            {
                Plugin.ClearGlowBordersNow();
                ctx.Reply("<color=#00FF00>Glow borders cleared.</color>");
                return;
            }

            if (action is "on" or "enable")
            {
                Plugin.SetGlowSystemEnabled(true);
                Plugin.ForceGlowRebuild();
                ctx.Reply($"<color=#00FF00>Glow system enabled. Active entities: {Plugin.ActiveGlowEntityCount}</color>");
                return;
            }

            if (action is "off" or "disable")
            {
                Plugin.SetGlowSystemEnabled(false);
                Plugin.ClearGlowBordersNow();
                ctx.Reply("<color=#00FF00>Glow system disabled and borders cleared.</color>");
                return;
            }

            ctx.Reply("<color=#FFFF00>Usage: .z glow [status|rebuild|clear|on|off|help]</color>");
        }

        [Command("glow help", shortHand: "gh", description: "Show glow commands", adminOnly: true)]
        public static void GlowHelp(ChatCommandContext ctx)
        {
            ctx.Reply("<color=#FFD700>[Glow Commands]</color>");
            ctx.Reply("<color=#00FFFF>.z glow status</color> - Show glow status");
            ctx.Reply("<color=#00FFFF>.z glow rebuild</color> - Rebuild all glow borders");
            ctx.Reply("<color=#00FFFF>.z glow clear</color> - Clear all glow borders");
            ctx.Reply("<color=#00FFFF>.z glow on|off</color> - Enable/disable global glow system");
            ctx.Reply("<color=#00FFFF>.z glow zoneon <zoneId></color> - Enable auto glow for zone and save");
            ctx.Reply("<color=#00FFFF>.z glow zoneoff <zoneId></color> - Disable auto glow for zone and save");
            ctx.Reply("<color=#00FFFF>.z glow zonelist</color> - List zone auto glow flags");
        }

        [Command("glow zoneon", shortHand: "gzon", description: "Enable auto glow for a zone", adminOnly: true)]
        public static void GlowZoneOn(ChatCommandContext ctx, string zoneId)
        {
            SetZoneAutoGlow(ctx, zoneId, true);
        }

        [Command("glow zoneoff", shortHand: "gzoff", description: "Disable auto glow for a zone", adminOnly: true)]
        public static void GlowZoneOff(ChatCommandContext ctx, string zoneId)
        {
            SetZoneAutoGlow(ctx, zoneId, false);
        }

        [Command("glow zonelist", shortHand: "gzl", description: "List zones and auto glow flag", adminOnly: true)]
        public static void GlowZoneList(ChatCommandContext ctx)
        {
            var zones = ZoneConfigService.GetAllZones();
            if (zones == null || zones.Count == 0)
            {
                ctx.Reply("<color=#FF0000>No zones configured.</color>");
                return;
            }

            ctx.Reply($"<color=#FFD700>[Glow Zones: {zones.Count}]</color>");
            foreach (var zone in zones)
            {
                var flag = zone.AutoGlowWithZone ? "<color=#00FF00>ON</color>" : "<color=#FF5555>OFF</color>";
                ctx.Reply($"{zone.Id}: {flag}");
            }
        }

        private static void SetZoneAutoGlow(ChatCommandContext ctx, string zoneId, bool enabled)
        {
            if (string.IsNullOrWhiteSpace(zoneId))
            {
                ctx.Reply("<color=#FF0000>Zone ID is required.</color>");
                return;
            }

            var ok = ZoneConfigService.SetAutoGlowForZone(zoneId, enabled);
            if (!ok)
            {
                ctx.Reply($"<color=#FF0000>Zone '{zoneId}' not found or save failed.</color>");
                return;
            }

            ZoneConfigService.Reload();
            Plugin.ForceGlowRebuild();
            var status = enabled ? "enabled" : "disabled";
            ctx.Reply($"<color=#00FF00>Auto glow {status} for zone '{zoneId}'.</color>");
        }

        private static string GetNextNumericZoneId(List<ArenaZoneDef> zones)
        {
            var max = 0;
            foreach (var zone in zones)
            {
                if (zone == null || string.IsNullOrWhiteSpace(zone.Name))
                {
                    continue;
                }

                if (int.TryParse(zone.Name, out var n) && n > max)
                {
                    max = n;
                }
            }

            return (max + 1).ToString();
        }

        #endregion
    }
}

