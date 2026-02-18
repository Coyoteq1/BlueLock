using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using BepInEx;
using ProjectM;
using ProjectM.Network;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireCommandFramework;
using VAuto.Zone.Core;
using VAuto.Zone.Models;
using VAuto.Zone.Services;
using VAutomationCore.Core.Config;
using VAutomationCore.Core.Services;

namespace VAuto.Zone.Commands
{
    /// <summary>
    /// Admin commands for managing arena zones.
    /// Provides create/remove/list/on/off/center/radius/tp functionality.
    /// </summary>
    [CommandGroup("zone", "z")]
    public static class ZoneCommands
    {
        private static readonly string ZonesFile = ResolveZonesFilePath();

        private static string ResolveZonesFilePath()
        {
            var rootDir = System.IO.Path.Combine(Paths.ConfigPath, "Bluelock");
            Directory.CreateDirectory(rootDir);

            var rootPath = System.IO.Path.Combine(rootDir, "VAuto.Zones.json");
            var legacyPath = System.IO.Path.Combine(rootDir, "config", "VAuto.Zones.json");
            try
            {
                if (!File.Exists(rootPath) && File.Exists(legacyPath))
                {
                    File.Copy(legacyPath, rootPath, overwrite: false);
                }
            }
            catch
            {
                // Best-effort migration.
            }

            return rootPath;
        }

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
<color=#00FFFF>.z diag (.z dg)</color> - Show live runtime diagnostics for your player
<color=#00FFFF>.z default [name] (.z d)</color> - Set default zone (checked first for zone detection)
<color=#00FFFF>.z arena [name] [on/off]</color> - Toggle arena damage mode
<color=#00FFFF>.z holder [name] [playerName|clear]</color> - Set/clear holder immunity
<color=#00FFFF>.z kit verify [zoneId]</color> - Verify kit resolution for a zone (no items granted)
<color=#00FFFF>.z kit verifykit [kitId]</color> - Verify kit resolution by kit id (no items granted)";
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
                    LifecycleEnabled = true,
                    IsArenaZone = true,
                    HolderName = TryGetSenderCharacterName(ctx)
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
                    var arena = zone.IsArenaZone ? "<color=#FF6A00>ArenaDamage</color>" : "<color=#808080>NoArenaDamage</color>";
                    var holder = string.IsNullOrWhiteSpace(zone.HolderName) ? "none" : zone.HolderName;
                    var marker = !string.IsNullOrWhiteSpace(defaultZoneId) &&
                                 string.Equals(zone.Name, defaultZoneId, StringComparison.OrdinalIgnoreCase)
                        ? " <color=#00BFFF>[DEFAULT]</color>"
                        : string.Empty;
                    message += $"{zone.Name}{marker}: {zone.Shape} at ({zone.Center.x:F0}, {zone.Center.y:F0}, {zone.Center.z:F0}) r={zone.Radius}m [{status}] [{arena}] Holder={holder}\n";
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
                             $"ArenaDamage: {(zone.IsArenaZone ? "<color=#FF6A00>On</color>" : "<color=#808080>Off</color>")}\n" +
                             $"Holder: {(string.IsNullOrWhiteSpace(zone.HolderName) ? "none" : zone.HolderName)}\n" +
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
        public static void ArenaDefault(ChatCommandContext ctx, string name = "")
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

        [Command("diag", shortHand: "dg", description: "Show live runtime diagnostics for your player", adminOnly: true)]
        public static void Diag(ChatCommandContext ctx)
        {
            try
            {
                var em = ZoneCore.EntityManager;
                var characterEntity = ctx.Event?.SenderCharacterEntity ?? Entity.Null;
                if (characterEntity == Entity.Null || !em.Exists(characterEntity))
                {
                    ctx.Reply("<color=#FF0000>Error: Could not resolve your character entity.</color>");
                    return;
                }

                var platformId = ResolvePlatformId(em, characterEntity);
                var zoneState = VAutomationCore.Services.ZoneEventBridge.GetPlayerZoneState(characterEntity);
                var trackedZone = zoneState?.CurrentZoneId ?? string.Empty;

                var pos = float3.zero;
                if (!TryGetBestPosition(em, characterEntity, out pos))
                {
                    ctx.Reply("<color=#FF0000>Error: Could not resolve your world position.</color>");
                    return;
                }

                var detectedZone = ZoneConfigService.GetZoneAtPosition(pos.x, pos.z)?.Id ?? string.Empty;
                var effectiveZone = string.IsNullOrWhiteSpace(trackedZone) ? detectedZone : trackedZone;
                var configuredKit = string.IsNullOrWhiteSpace(effectiveZone) ? string.Empty : ZoneConfigService.GetKitIdForZone(effectiveZone);
                var activeKit = KitService.GetActiveKitId(platformId);
                var snapshotCaptured = KitService.HasSnapshotCaptured(platformId);
                var restoreOnExit = KitService.GetRestoreOnExitFlag(platformId);
                var hasReturnPos = Plugin.HasStoredReturnPosition(characterEntity);
                var enterActionCount = GameActionService.GetRegisteredEventActionCount(GameActionService.EventPlayerEnter);
                var exitActionCount = GameActionService.GetRegisteredEventActionCount(GameActionService.EventPlayerExit);

                var msg = $"<color=#FFD700>[Zone Runtime Diag]</color>\n" +
                          $"Entity: {characterEntity.Index}:{characterEntity.Version}\n" +
                          $"PlatformId: {platformId}\n" +
                          $"Pos: ({pos.x:F1}, {pos.y:F1}, {pos.z:F1})\n" +
                          $"TrackedZone: {(string.IsNullOrWhiteSpace(trackedZone) ? "none" : trackedZone)}\n" +
                          $"DetectedZone: {(string.IsNullOrWhiteSpace(detectedZone) ? "none" : detectedZone)}\n" +
                          $"ConfiguredKit: {(string.IsNullOrWhiteSpace(configuredKit) ? "none" : configuredKit)}\n" +
                          $"ActiveKit: {(string.IsNullOrWhiteSpace(activeKit) ? "none" : activeKit)}\n" +
                          $"SnapshotCaptured: {snapshotCaptured}\n" +
                          $"RestoreOnExitFlag: {restoreOnExit}\n" +
                          $"ReturnPosStored: {hasReturnPos}\n" +
                          $"GlowEnabled: {Plugin.GlowSystemEnabledValue} (ActiveEntities={Plugin.ActiveGlowEntityCount})\n" +
                          $"LifecycleIntegration: {Plugin.IntegrationLifecycleEnabledValue}\n" +
                          $"CoreLifecycleBindings: Enter={enterActionCount}, Exit={exitActionCount}";

                ctx.Reply(msg);
            }
            catch (Exception ex)
            {
                ZoneCore.LogError($"[ArenaAdmin] diag failed: {ex.Message}");
                ctx.Reply("<color=#FF0000>Error running diagnostics.</color>");
            }
        }

        [Command("kit verify", shortHand: "kv", description: "Verify kit resolution for a zone (no items granted)", adminOnly: true)]
        public static void KitVerify(ChatCommandContext ctx, string zoneId = "")
        {
            try
            {
                var em = ZoneCore.EntityManager;
                var characterEntity = ctx.Event?.SenderCharacterEntity ?? Entity.Null;
                if (characterEntity == Entity.Null || !em.Exists(characterEntity))
                {
                    ctx.Reply("<color=#FF0000>Error: Could not resolve your character entity.</color>");
                    return;
                }

                var effectiveZone = zoneId;
                if (string.IsNullOrWhiteSpace(effectiveZone))
                {
                    var zoneState = VAutomationCore.Services.ZoneEventBridge.GetPlayerZoneState(characterEntity);
                    var trackedZone = zoneState?.CurrentZoneId ?? string.Empty;

                    if (!TryGetPlayerPosition(ctx, out var position))
                    {
                        ctx.Reply("<color=#FF0000>Error: Could not resolve your position.</color>");
                        return;
                    }

                    var detectedZone = ZoneConfigService.GetZoneAtPosition(position.x, position.z)?.Id ?? string.Empty;
                    effectiveZone = string.IsNullOrWhiteSpace(trackedZone) ? detectedZone : trackedZone;
                }

                if (string.IsNullOrWhiteSpace(effectiveZone))
                {
                    ctx.Reply("<color=#FF0000>Error: Could not resolve a zone. Pass a zone id: .z kit verify <zoneId></color>");
                    return;
                }

                var ok = KitService.TryBuildKitVerifyReportForZone(effectiveZone, out var report);
                ZoneCore.LogInfo(report);

                var lines = (report ?? string.Empty).Split('\n');
                var preview = lines.Length <= 12 ? report : string.Join("\n", lines.Take(12)) + "\n...(see server log for full report)";
                ctx.Reply(ok ? $"<color=#00FF00>{preview}</color>" : $"<color=#FF5555>{preview}</color>");
            }
            catch (Exception ex)
            {
                ZoneCore.LogError($"[ArenaAdmin] kit verify failed: {ex.Message}");
                ctx.Reply("<color=#FF0000>Error verifying kit.</color>");
            }
        }

        [Command("kit verifykit", shortHand: "kvk", description: "Verify kit resolution by kit id (no items granted)", adminOnly: true)]
        public static void KitVerifyKit(ChatCommandContext ctx, string kitId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(kitId))
                {
                    ctx.Reply("<color=#FF0000>Usage: .z kit verifykit <kitId></color>");
                    return;
                }

                var ok = KitService.TryBuildKitVerifyReport(kitId, out var report);
                ZoneCore.LogInfo(report);

                var lines = (report ?? string.Empty).Split('\n');
                var preview = lines.Length <= 12 ? report : string.Join("\n", lines.Take(12)) + "\n...(see server log for full report)";
                ctx.Reply(ok ? $"<color=#00FF00>{preview}</color>" : $"<color=#FF5555>{preview}</color>");
            }
            catch (Exception ex)
            {
                ZoneCore.LogError($"[ArenaAdmin] kit verifykit failed: {ex.Message}");
                ctx.Reply("<color=#FF0000>Error verifying kit.</color>");
            }
        }

        [Command("arena", description: "Set arena damage mode for a zone: on/off", adminOnly: true)]
        public static void ArenaMode(ChatCommandContext ctx, string name, string mode = "status")
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
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

                var value = (mode ?? "status").Trim().ToLowerInvariant();
                if (value is "status" or "s")
                {
                    ctx.Reply($"<color=#00BFFF>Zone '{zone.Name}' ArenaDamage={(zone.IsArenaZone ? "On" : "Off")}</color>");
                    return;
                }

                if (value is not ("on" or "off"))
                {
                    ctx.Reply("<color=#FF0000>Usage: .z arena [name] [on/off]</color>");
                    return;
                }

                zone.IsArenaZone = value == "on";
                if (!SaveZones(zones))
                {
                    ctx.Reply("<color=#FF0000>Error: Failed to save zone configuration.</color>");
                    return;
                }

                ctx.Reply($"<color=#00FF00>Zone '{zone.Name}' ArenaDamage set to {(zone.IsArenaZone ? "On" : "Off")}.</color>");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"ArenaMode error: {ex.Message}");
                ctx.Reply("<color=#FF0000>Error updating arena mode.</color>");
            }
        }

        [Command("holder", description: "Set or clear holder name for arena immunity", adminOnly: true)]
        public static void ArenaHolder(ChatCommandContext ctx, string name, string holderName = "")
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
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

                if (string.IsNullOrWhiteSpace(holderName))
                {
                    var current = string.IsNullOrWhiteSpace(zone.HolderName) ? "none" : zone.HolderName;
                    ctx.Reply($"<color=#00BFFF>Zone '{zone.Name}' holder: {current}</color>");
                    return;
                }

                var token = holderName.Trim();
                zone.HolderName = token.Equals("clear", StringComparison.OrdinalIgnoreCase) || token.Equals("none", StringComparison.OrdinalIgnoreCase)
                    ? string.Empty
                    : token;

                if (!SaveZones(zones))
                {
                    ctx.Reply("<color=#FF0000>Error: Failed to save zone configuration.</color>");
                    return;
                }

                var result = string.IsNullOrWhiteSpace(zone.HolderName) ? "none" : zone.HolderName;
                ctx.Reply($"<color=#00FF00>Zone '{zone.Name}' holder set to {result}.</color>");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"ArenaHolder error: {ex.Message}");
                ctx.Reply("<color=#FF0000>Error updating holder.</color>");
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
                            LifecycleEnabled = true,
                            IsArenaZone = false,
                            HolderName = string.Empty
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

                        if (zoneEl.TryGetProperty("IsArenaZone", out var arenaEl) &&
                            (arenaEl.ValueKind == JsonValueKind.True || arenaEl.ValueKind == JsonValueKind.False))
                        {
                            zone.IsArenaZone = arenaEl.GetBoolean();
                        }

                        if (zoneEl.TryGetProperty("HolderName", out var holderEl) && holderEl.ValueKind == JsonValueKind.String)
                        {
                            zone.HolderName = holderEl.GetString() ?? string.Empty;
                        }
                        if (TryGetBooleanProperty(zoneEl, out var lifecycleEnabled, "LifecycleEnabled", "lifecycleEnabled"))
                        {
                            zone.LifecycleEnabled = lifecycleEnabled;
                        }

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

                var options = new JsonSerializerOptions(ZoneJsonOptions.WithUnityMathConverters)
                {
                    WriteIndented = true
                };
                var mapped = new ZonesConfig
                {
                    Description = "Zones managed by VAutoZone admin commands",
                    DefaultZoneId = ZoneConfigService.GetDefaultZoneId(),
                    DefaultKitId = ZoneConfigService.GetDefaultKitId(),
                    Zones = zones.Select(z =>
                    {
                        var autoGlow = ZoneConfigService.IsAutoGlowEnabledForZone(z.Name);
                        var zoneDef = new ZoneDefinition
                        {
                            Id = z.Name,
                            DisplayName = z.Name,
                            Shape = z.Shape == ArenaZoneShape.Square ? "Rectangle" : "Circle",
                            CenterX = z.Center.x,
                            CenterZ = z.Center.z,
                            Radius = z.Radius > 0 ? z.Radius : 50f,
                            AutoGlowWithZone = autoGlow,
                            IsArenaZone = z.IsArenaZone,
                            HolderName = z.HolderName ?? string.Empty
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

            if (zoneEl.TryGetProperty("isArenaZone", out var arenaEl) &&
                (arenaEl.ValueKind == JsonValueKind.True || arenaEl.ValueKind == JsonValueKind.False))
            {
                zone.IsArenaZone = arenaEl.GetBoolean();
            }

            if (zoneEl.TryGetProperty("holderName", out var holderEl) && holderEl.ValueKind == JsonValueKind.String)
            {
                zone.HolderName = holderEl.GetString() ?? string.Empty;
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

                return GameActionService.TryTeleport(characterEntity, targetPosition);
            }
            catch (Exception ex)
            {
                ZoneCore.LogError($"[ArenaAdmin] Teleport failed: {ex.Message}");
                return false;
            }
        }

        private static bool TryGetBestPosition(EntityManager em, Entity entity, out float3 pos)
        {
            pos = default;
            try
            {
                if (em.HasComponent<LocalTransform>(entity))
                {
                    pos = em.GetComponentData<LocalTransform>(entity).Position;
                    return true;
                }

                if (em.HasComponent<Translation>(entity))
                {
                    pos = em.GetComponentData<Translation>(entity).Value;
                    return true;
                }

                if (em.HasComponent<LastTranslation>(entity))
                {
                    pos = em.GetComponentData<LastTranslation>(entity).Value;
                    return true;
                }

                if (em.HasComponent<SpawnTransform>(entity))
                {
                    pos = em.GetComponentData<SpawnTransform>(entity).Position;
                    return true;
                }
            }
            catch
            {
                // ignored
            }

            return false;
        }

        private static string TryGetSenderCharacterName(ChatCommandContext ctx)
        {
            try
            {
                var em = ZoneCore.EntityManager;
                var characterEntity = ctx.Event?.SenderCharacterEntity ?? Entity.Null;
                if (characterEntity == Entity.Null || !em.Exists(characterEntity) || !em.HasComponent<PlayerCharacter>(characterEntity))
                {
                    return string.Empty;
                }

                var userEntity = em.GetComponentData<PlayerCharacter>(characterEntity).UserEntity;
                if (userEntity == Entity.Null || !em.Exists(userEntity) || !em.HasComponent<User>(userEntity))
                {
                    return string.Empty;
                }

                return em.GetComponentData<User>(userEntity).CharacterName.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        private static ulong ResolvePlatformId(EntityManager em, Entity characterEntity)
        {
            try
            {
                if (!em.Exists(characterEntity) || !em.HasComponent<PlayerCharacter>(characterEntity))
                {
                    return 0;
                }

                var userEntity = em.GetComponentData<PlayerCharacter>(characterEntity).UserEntity;
                if (userEntity == Entity.Null || !em.Exists(userEntity) || !em.HasComponent<User>(userEntity))
                {
                    return 0;
                }

                return em.GetComponentData<User>(userEntity).PlatformId;
            }
            catch
            {
                return 0;
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

            if (action is "spawn" or "sp")
            {
                Plugin.ForceGlowRebuild();
                ZoneCore.LogInfo("[ArenaAdmin] Glow spawn requested via command.");
                ctx.Reply($"<color=#00FF00>Glow spawn/update triggered. Active entities: {Plugin.ActiveGlowEntityCount}</color>");
                return;
            }

            if (action is "auto")
            {
                var autoEnabled = Plugin.GlowSystemAutoRotateEnabledValue;
                var interval = Plugin.GlowSystemAutoRotateIntervalMinutesValue;
                ctx.Reply($"<color=#00BFFF>Glow auto-rotate: {(autoEnabled ? "On" : "Off")} (interval={interval}m)</color>");
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

            ctx.Reply("<color=#FFFF00>Usage: .z glow [status|spawn|rebuild|clear|on|off|auto|help]</color>");
        }

        [Command("glow help", shortHand: "gh", description: "Show glow commands", adminOnly: true)]
        public static void GlowHelp(ChatCommandContext ctx)
        {
            ctx.Reply("<color=#FFD700>[Glow Commands]</color>");
            ctx.Reply("<color=#00FFFF>.z glow status</color> - Show glow status");
            ctx.Reply("<color=#00FFFF>.z glow add <glowName|guid></color> - Apply glow buff to closest zone marker near your aim");
            ctx.Reply("<color=#00FFFF>.z glow remove <glowName|guid></color> - Remove glow buff from closest zone marker near your aim");
            ctx.Reply("<color=#00FFFF>.z glow spawn</color> - Force spawn/update all glow borders now");
            ctx.Reply("<color=#00FFFF>.z glow rebuild</color> - Rebuild all glow borders");
            ctx.Reply("<color=#00FFFF>.z glow clear</color> - Clear all glow borders");
            ctx.Reply("<color=#00FFFF>.z glow on|off</color> - Enable/disable global glow system");
            ctx.Reply("<color=#00FFFF>.z glow auto</color> - Show global auto-rotate status");
            ctx.Reply("<color=#00FFFF>.z glow auto on|off</color> - Enable/disable global auto-rotate");
            ctx.Reply("<color=#00FFFF>.z glow choices reload</color> - Reload glowChoices.txt and rebuild");
            ctx.Reply("<color=#00FFFF>.z glow rotate now</color> - Rotate rainbow glows now and rebuild");
            ctx.Reply("<color=#00FFFF>.z glow zoneon <zoneId></color> - Enable auto glow for zone and save");
            ctx.Reply("<color=#00FFFF>.z glow zoneoff <zoneId></color> - Disable auto glow for zone and save");
            ctx.Reply("<color=#00FFFF>.z glow zonelist</color> - List zone auto glow flags");
        }

        [Command("glow auto", shortHand: "ga", description: "Show or set global glow auto-rotate: [on|off]", adminOnly: true)]
        public static void GlowAuto(ChatCommandContext ctx, string mode = "status")
        {
            var value = (mode ?? "status").Trim().ToLowerInvariant();
            if (value is "status" or "s")
            {
                ctx.Reply($"<color=#00BFFF>Glow auto-rotate: {(Plugin.GlowSystemAutoRotateEnabledValue ? "On" : "Off")} (interval={Plugin.GlowSystemAutoRotateIntervalMinutesValue}m)</color>");
                return;
            }

            if (value is not ("on" or "off"))
            {
                ctx.Reply("<color=#FF0000>Usage: .z glow auto [on|off|status]</color>");
                return;
            }

            var enable = value == "on";
            Plugin.SetGlowAutoRotateEnabled(enable);
            ZoneCore.LogInfo($"[ArenaAdmin] Glow auto-rotate updated: {(enable ? "On" : "Off")}");
            ctx.Reply($"<color=#00FF00>Glow auto-rotate set to {(enable ? "On" : "Off")}.</color>");
        }

        [Command("glow add", shortHand: "gadd", description: "Apply glow to closest zone marker near your aim", adminOnly: true)]
        public static void GlowAdd(ChatCommandContext ctx, string glowToken)
        {
            if (string.IsNullOrWhiteSpace(glowToken))
            {
                ctx.Reply("<color=#FF0000>Usage: .z glow add <glowName|guid></color>");
                return;
            }

            if (!TryResolveClosestZoneMarker(ctx, out var zoneId, out var marker))
            {
                var label = string.IsNullOrWhiteSpace(zoneId) ? "current position" : $"zone '{zoneId}'";
                ctx.Reply($"<color=#FF0000>No markers found for {label}. Run .z glow rebuild and verify marker prefab config.</color>");
                return;
            }

            if (!GlowService.TryResolve(glowToken, out var glowGuid) || glowGuid == PrefabGUID.Empty)
            {
                ctx.Reply($"<color=#FF0000>Could not resolve glow '{glowToken}'.</color>");
                return;
            }

            if (!GameActionService.TryApplyCleanBuff(marker, glowGuid, -1f))
            {
                ctx.Reply($"<color=#FF0000>Failed to apply glow '{glowToken}' ({glowGuid.GuidHash}) to marker in zone '{zoneId}'.</color>");
                return;
            }

            ctx.Reply($"<color=#00FF00>Applied glow '{glowToken}' ({glowGuid.GuidHash}) to closest marker in zone '{zoneId}'.</color>");
        }

        [Command("glow remove", shortHand: "grem", description: "Remove glow from closest zone marker near your aim", adminOnly: true)]
        public static void GlowRemove(ChatCommandContext ctx, string glowToken)
        {
            if (string.IsNullOrWhiteSpace(glowToken))
            {
                ctx.Reply("<color=#FF0000>Usage: .z glow remove <glowName|guid></color>");
                return;
            }

            if (!TryResolveClosestZoneMarker(ctx, out var zoneId, out var marker))
            {
                var label = string.IsNullOrWhiteSpace(zoneId) ? "current position" : $"zone '{zoneId}'";
                ctx.Reply($"<color=#FF0000>No markers found for {label}. Run .z glow rebuild and verify marker prefab config.</color>");
                return;
            }

            if (!GlowService.TryResolve(glowToken, out var glowGuid) || glowGuid == PrefabGUID.Empty)
            {
                ctx.Reply($"<color=#FF0000>Could not resolve glow '{glowToken}'.</color>");
                return;
            }

            if (!GameActionService.TryRemoveBuff(marker, glowGuid))
            {
                ctx.Reply($"<color=#FF0000>Glow '{glowToken}' ({glowGuid.GuidHash}) not found on closest marker in zone '{zoneId}'.</color>");
                return;
            }

            ctx.Reply($"<color=#00FF00>Removed glow '{glowToken}' ({glowGuid.GuidHash}) from closest marker in zone '{zoneId}'.</color>");
        }

        [Command("glow choices reload", shortHand: "gcr", description: "Reload glowChoices.txt and rebuild glow borders", adminOnly: true)]
        public static void GlowChoicesReload(ChatCommandContext ctx)
        {
            try
            {
                GlowService.RefreshGlowChoices();
                Plugin.ForceGlowRebuild();
                ctx.Reply("<color=#00FF00>Reloaded glow choices and rebuilt glow borders.</color>");
            }
            catch (Exception ex)
            {
                ZoneCore.LogError($"[ArenaAdmin] glow choices reload failed: {ex.Message}");
                ctx.Reply("<color=#FF0000>Error reloading glow choices.</color>");
            }
        }

        [Command("glow rotate now", shortHand: "grn", description: "Rotate glow buffs now and rebuild glow borders", adminOnly: true)]
        public static void GlowRotateNow(ChatCommandContext ctx)
        {
            try
            {
                Plugin.RotateGlowNow();
                ctx.Reply("<color=#00FF00>Rotated glow buffs and rebuilt glow borders.</color>");
            }
            catch (Exception ex)
            {
                ZoneCore.LogError($"[ArenaAdmin] glow rotate now failed: {ex.Message}");
                ctx.Reply("<color=#FF0000>Error rotating glow buffs.</color>");
            }
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

        private static bool TryResolveClosestZoneMarker(ChatCommandContext ctx, out string zoneId, out Entity marker)
        {
            zoneId = string.Empty;
            marker = Entity.Null;
            try
            {
                var em = ZoneCore.EntityManager;
                var character = ctx.Event?.SenderCharacterEntity ?? Entity.Null;
                if (character == Entity.Null || !em.Exists(character))
                {
                    return false;
                }

                float3 referencePos;
                if (em.HasComponent<EntityAimData>(character))
                {
                    referencePos = em.GetComponentData<EntityAimData>(character).AimPosition;
                }
                else if (!TryGetBestPosition(em, character, out referencePos))
                {
                    return false;
                }

                var state = VAutomationCore.Services.ZoneEventBridge.GetPlayerZoneState(character);
                zoneId = state?.CurrentZoneId ?? string.Empty;
                if (string.IsNullOrWhiteSpace(zoneId))
                {
                    zoneId = ZoneConfigService.GetZoneAtPosition(referencePos.x, referencePos.z)?.Id ?? string.Empty;
                }

                if (string.IsNullOrWhiteSpace(zoneId))
                {
                    return false;
                }

                return Plugin.TryFindClosestMarkerInZone(zoneId, referencePos, out marker);
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetBooleanProperty(JsonElement element, out bool value, params string[] names)
        {
            foreach (var name in names)
            {
                if (!element.TryGetProperty(name, out var prop))
                {
                    continue;
                }

                if (prop.ValueKind == JsonValueKind.True || prop.ValueKind == JsonValueKind.False)
                {
                    value = prop.GetBoolean();
                    return true;
                }
            }

            value = false;
            return false;
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

