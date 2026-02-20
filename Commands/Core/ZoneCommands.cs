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
                
                // Sandbox progression info
                var (sbActive, sbPending, sbDeltas, sbDirty) = VAuto.Core.Services.DebugEventBridge.GetSnapshotCounts();
                var (sbZoneId, sbRows, sbCaptured) = VAuto.Core.Services.DebugEventBridge.GetBaselineInfo(platformId);

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
                          $"CoreLifecycleBindings: Enter={enterActionCount}, Exit={exitActionCount}\n" +
                          $"<color=#00FFFF>--- Sandbox Progression ---</color>\n" +
                          $"ActiveBaselines: {sbActive}, Pending: {sbPending}, Deltas: {sbDeltas}, Dirty: {sbDirty}\n" +
                          $"YourBaseline: zone={sbZoneId ?? "none"}, rows={sbRows}, captured={sbCaptured?.ToString("HH:mm:ss") ?? "none"}";

                ctx.Reply(msg);
            }
            catch (Exception ex)
            {
                ZoneCore.LogError($"[ArenaAdmin] diag failed: {ex.Message}");
                ctx.Reply("<color=#FF0000>Error running diagnostics.</color>");
            }
        }

        /// <summary>
        /// Show global sandbox progression status.
        /// </summary>
        [Command("sandbox", shortHand: "sb", description: "Show sandbox progression status", adminOnly: true)]
        public static void SandboxStatus(ChatCommandContext ctx)
        {
            try
            {
                var (active, pending, deltas, dirty) = VAuto.Core.Services.DebugEventBridge.GetSnapshotCounts();
                
                var msg = $"<color=#00FFFF>[Sandbox Progression]</color>\n" +
                          $"Enabled: {Plugin.SandboxProgressionEnabledValue}\n" +
                          $"Active Baselines: {active}\n" +
                          $"Pending Contexts: {pending}\n" +
                          $"Active Deltas: {deltas}\n" +
                          $"Dirty (unsaved): {dirty}\n" +
                          $"PersistSnapshots: {Plugin.SandboxProgressionPersistSnapshotsValue}";
                
                ctx.Reply(msg);
            }
            catch (Exception ex)
            {
                ZoneCore.LogError($"[SandboxStatus] failed: {ex.Message}");
                ctx.Reply("<color=#FF0000>Error getting sandbox status.</color>");
            }
        }

        /// <summary>
        /// Force save progression snapshot (for testing).
        /// </summary>
        [Command("snap", shortHand: "sn", description: "Force save progression snapshot", adminOnly: true)]
        public static void ForceSnapshot(ChatCommandContext ctx, string zoneId = "")
        {
            try
            {
                var em = ZoneCore.EntityManager;
                var characterEntity = ctx.Event?.SenderCharacterEntity ?? Entity.Null;
                if (characterEntity == Entity.Null || !em.Exists(characterEntity))
                {
                    ctx.Reply("<color=#FF0000>Error: Could not resolve your character.</color>");
                    return;
                }
                
                var targetZone = string.IsNullOrWhiteSpace(zoneId) ? "sandbox" : zoneId;
                var enableUnlock = ZoneConfigService.IsSandboxUnlockEnabled(targetZone, Plugin.SandboxProgressionDefaultZoneUnlockEnabledValue);
                
                VAuto.Core.Services.DebugEventBridge.OnPlayerEnter(characterEntity, targetZone, enableUnlock);
                
                var (sbZoneId, sbRows, sbCaptured) = VAuto.Core.Services.DebugEventBridge.GetBaselineInfo(ctx.User.PlatformId);
                
                ctx.Reply($"<color=#00FF00>Snapshot saved!</color>\n" +
                          $"Zone: {sbZoneId ?? targetZone}\n" +
                          $"Components: {sbRows}\n" +
                          $"Time: {sbCaptured?.ToString("HH:mm:ss") ?? "now"}");
            }
            catch (Exception ex)
            {
                ZoneCore.LogError($"[ForceSnapshot] failed: {ex.Message}");
                ctx.Reply("<color=#FF0000>Error saving snapshot.</color>");
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


