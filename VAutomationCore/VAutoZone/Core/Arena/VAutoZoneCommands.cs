using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using ProjectM;
using ProjectM.Network;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireCommandFramework;
using VAuto.Zone.Core;
using VAuto.Zone.Services;
using VAutomationCore;
using VAutomationCore.Core.Logging;

namespace VAuto.Zone.Commands
{
    /// <summary>
    /// Zone management commands for VAutoZone.
    /// Provides commands for managing arena zones, glow borders, and player tracking.
    /// </summary>
    [CommandGroup("zone", "z")]
    public static class VAutoZoneCommands
    {
        /// <summary>
        /// Display help for zone commands.
        /// </summary>
        [Command("help", shortHand: "h", description: "Show zone command help", adminOnly: false)]
        public static void Help(ChatCommandContext ctx)
        {
            var message = @"<color=#FFD700>[Zone Commands]</color>
<color=#00FFFF>.zone status (.z s)</color> - Show current zone status
<color=#00FFFF>.zone border (.z b)</color> - Show border info or toggle glow
<color=#00FFFF>.zone glow (.z g) [spawn|clear|count]</color> - Manage glow borders
<color=#00FFFF>.zone reload (.z r)</color> - Reload configuration [Admin]
<color=#00FFFF>.zone config (.z c)</color> - Show arena configuration [Admin]
<color=#00FFFF>.zone glowlist (.z gl)</color> - List glow prefabs [Admin]
<color=#00FFFF>.zone glowpref (.z gp) [name]</color> - Set glow prefab [Admin]
<color=#00FFFF>.zone spacing (.z sp) [meters]</color> - Set glow spacing [Admin]
<color=#00FFFF>.zone corners (.z co) [on|off]</color> - Toggle corners [Admin]";
            ctx.Reply(message);
        }

        /// <summary>
        /// Display information about the current zone status.
        /// </summary>
        [Command("status", shortHand: "s", description: "Show current zone status", adminOnly: false)]
        public static void ZoneStatus(ChatCommandContext ctx)
        {
            try
            {
                if (!TryGetPlayerPosition(ctx, out var playerPos))
                {
                    ctx.Reply("[Zone] Error: Could not determine your position");
                    return;
                }

                var isInArena = ArenaTerritory.IsInArenaTerritory(playerPos);
                var zoneId = ArenaTerritory.GetArenaZoneId(playerPos);
                
                var blocks = ArenaTerritory.GetArenaBlocks();
                var borderPoints = ArenaTerritory.GetBorderPoints(ArenaTerritory.GlowSpacingMeters);
                
                var message = $"<color=#FFD700>[Zone Status]</color>\n" +
                              $"Position: ({playerPos.x:F0}, {playerPos.y:F0}, {playerPos.z:F0})\n" +
                              $"In Arena: {(isInArena ? "<color=#00FF00>Yes</color>" : "<color=#FF0000>No</color>")}\n" +
                              $"Zone ID: {zoneId ?? "None"}\n" +
                              $"Arena Blocks: {blocks.Count}\n" +
                              $"Border Points: {borderPoints.Count}\n" +
                              $"Glow Spacing: {ArenaTerritory.GlowSpacingMeters}m";
                
                ctx.Reply(message);
            }
            catch (Exception ex)
            {
                ZoneCore.LogError($"ZoneStatus error: {ex.Message}");
                ctx.Reply("<color=#FF0000>Error retrieving zone status.</color>");
            }
        }

        /// <summary>
        /// Show arena borders and glow status, or toggle glow border.
        /// </summary>
        [Command("border", shortHand: "b", description: "Show border info or toggle glow", adminOnly: true)]
        public static void ZoneBorder(ChatCommandContext ctx, bool? enable = null)
        {
            try
            {
                if (enable.HasValue)
                {
                    ArenaTerritory.EnableGlowBorder = enable.Value;
                    ZoneCore.LogInfo($"Glow border {(enable.Value ? "enabled" : "disabled")} by {ctx.User.CharacterName}");
                    ctx.Reply($"<color=#00FF00>Glow border {(enable.Value ? "enabled" : "disabled")}.</color>");
                }
                else
                {
                    var borderPoints = ArenaTerritory.GetBorderPoints(ArenaTerritory.GlowSpacingMeters);
                    var cornerPoints = ArenaTerritory.GetCornerPoints();
                    
                    var message = $"<color=#FFD700>[Arena Border]</color>\n" +
                                  $"Border Points: {borderPoints.Count}\n" +
                                  $"Corner Points: {cornerPoints.Count}\n" +
                                  $"Spacing: {ArenaTerritory.GlowSpacingMeters}m\n" +
                                  $"Glow Prefab: {ArenaTerritory.GlowPrefab}\n" +
                                  $"Glow Enabled: {(ArenaTerritory.EnableGlowBorder ? "Yes" : "No")}\n" +
                                  $"Corners: {(ArenaTerritory.SpawnGlowInCorners ? "Yes" : "No")}";
                    
                    ctx.Reply(message);
                }
            }
            catch (Exception ex)
            {
                ZoneCore.LogError($"ZoneBorder error: {ex.Message}");
                ctx.Reply("<color=#FF0000>Error managing border.</color>");
            }
        }

        /// <summary>
        /// Spawn, clear, or count arena glow borders.
        /// </summary>
        [Command("glow", shortHand: "g", description: "Manage glow borders (spawn/clear/count)", adminOnly: true)]
        public static void ZoneGlow(ChatCommandContext ctx, string action = "spawn")
        {
            try
            {
                switch (action.ToLower())
                {
                    case "spawn":
                    case "show":
                        if (ArenaGlowBorderService.SpawnBorderGlows(
                            ArenaTerritory.GetPreferredConfigPath(),
                            ArenaTerritory.GlowPrefab,
                            ArenaTerritory.GlowSpacingMeters,
                            out var spawnError))
                        {
                            var spawnedCount = ArenaGlowBorderService.GetSpawnedCount();
                            ZoneCore.LogInfo($"Spawned {spawnedCount} glow borders");
                            ctx.Reply($"<color=#00FF00>Spawned {spawnedCount} glow borders.</color>");
                        }
                        else
                        {
                            ctx.Reply($"<color=#FF0000>Failed to spawn glows: {spawnError}</color>");
                        }
                        break;
                        
                    case "clear":
                    case "hide":
                        ArenaGlowBorderService.ClearAll();
                        ZoneCore.LogInfo("Cleared all glow borders");
                        ctx.Reply("<color=#00FF00>Cleared all glow borders.</color>");
                        break;
                        
                    case "count":
                        var count = ArenaGlowBorderService.GetSpawnedCount();
                        ctx.Reply($"Spawned glow count: {count}");
                        break;
                        
                    default:
                        ctx.Reply($"Unknown action: {action}. Use 'spawn', 'clear', or 'count'.");
                        break;
                }
            }
            catch (Exception ex)
            {
                ZoneCore.LogError($"ZoneGlow error: {ex.Message}");
                ctx.Reply("<color=#FF0000>Error managing glows.</color>");
            }
        }

        /// <summary>
        /// Reload arena configuration.
        /// </summary>
        [Command("reload", shortHand: "r", description: "Reload arena configuration", adminOnly: true)]
        public static void ZoneReload(ChatCommandContext ctx)
        {
            try
            {
                ArenaTerritory.Reload();
                ZoneCore.LogInfo($"Arena configuration reloaded by {ctx.User.CharacterName}");
                ctx.Reply("<color=#00FF00>Arena configuration reloaded.</color>");
            }
            catch (Exception ex)
            {
                ZoneCore.LogError($"ZoneReload error: {ex.Message}");
                ctx.Reply("<color=#FF0000>Error reloading configuration.</color>");
            }
        }

        /// <summary>
        /// Show arena configuration details.
        /// </summary>
        [Command("config", shortHand: "c", description: "Show arena configuration", adminOnly: true)]
        public static void ZoneConfig(ChatCommandContext ctx)
        {
            try
            {
                var message = $"<color=#FFD700>[Arena Configuration]</color>\n" +
                              $"Zone ID: {ArenaTerritory.ZoneId}\n" +
                              $"Center: ({ArenaTerritory.ArenaGridCenter.x:F0}, {ArenaTerritory.ArenaGridCenter.y:F0}, {ArenaTerritory.ArenaGridCenter.z:F0})\n" +
                              $"Radius: {ArenaTerritory.ArenaGridRadius}m\n" +
                              $"Block Size: {ArenaTerritory.BlockSize}m\n" +
                              $"Region Type: {ArenaTerritory.ArenaRegionType}\n" +
                              $"Glow Border: {(ArenaTerritory.EnableGlowBorder ? "Enabled" : "Disabled")}\n" +
                              $"Glow Prefab: {ArenaTerritory.GlowPrefab}\n" +
                              $"Glow Spacing: {ArenaTerritory.GlowSpacingMeters}m\n" +
                              $"Corner Radius: {ArenaTerritory.GlowCornerRadius}m\n" +
                              $"Spawn Corners: {(ArenaTerritory.SpawnGlowInCorners ? "Yes" : "No")}";
                
                ctx.Reply(message);
            }
            catch (Exception ex)
            {
                ZoneCore.LogError($"ZoneConfig error: {ex.Message}");
                ctx.Reply("<color=#FF0000>Error retrieving configuration.</color>");
            }
        }

        /// <summary>
        /// Get list of available glow prefabs.
        /// </summary>
        [Command("glowlist", shortHand: "gl", description: "List available glow prefabs", adminOnly: true)]
        public static void ZoneGlowList(ChatCommandContext ctx)
        {
            try
            {
                var glowService = new GlowService();
                var choices = glowService.ListGlowChoices().ToList();
                
                if (choices.Count == 0)
                {
                    ctx.Reply("No glow prefabs available.");
                    return;
                }
                
                var message = $"<color=#FFD700>[Glow Prefabs ({choices.Count})]</color>\n";
                foreach (var (name, prefab) in choices.Take(10))
                {
                    message += $"{name}: {prefab.GuidHash}\n";
                }
                
                if (choices.Count > 10)
                {
                    message += $"... and {choices.Count - 10} more.";
                }
                
                ctx.Reply(message);
            }
            catch (Exception ex)
            {
                ZoneCore.LogError($"ZoneGlowList error: {ex.Message}");
                ctx.Reply("<color=#FF0000>Error listing glow prefabs.</color>");
            }
        }

        /// <summary>
        /// Set the glow prefab for arena borders.
        /// </summary>
        [Command("glowpref", shortHand: "gp", description: "Set glow prefab by name", adminOnly: true)]
        public static void ZoneGlowPrefab(ChatCommandContext ctx, string prefabName)
        {
            try
            {
                var glowService = new GlowService();
                var prefab = glowService.GetGlowPrefab(prefabName);
                
                if (prefab.IsEmpty())
                {
                    ctx.Reply($"<color=#FF0000>Unknown glow prefab: {prefabName}. Use '.zone glowlist' to see options.</color>");
                    return;
                }
                
                ArenaTerritory.GlowPrefab = prefabName;
                ZoneCore.LogInfo($"Glow prefab set to {prefabName} by {ctx.User.CharacterName}");
                ctx.Reply($"<color=#00FF00>Glow prefab set to: {prefabName}</color>");
            }
            catch (Exception ex)
            {
                ZoneCore.LogError($"ZoneGlowPrefab error: {ex.Message}");
                ctx.Reply("<color=#FF0000>Error setting glow prefab.</color>");
            }
        }

        /// <summary>
        /// Set glow border spacing in meters.
        /// </summary>
        [Command("spacing", shortHand: "sp", description: "Set glow spacing in meters", adminOnly: true)]
        public static void ZoneSpacing(ChatCommandContext ctx, float spacing)
        {
            try
            {
                if (spacing < 1f || spacing > 20f)
                {
                    ctx.Reply("<color=#FF0000>Spacing must be between 1 and 20 meters.</color>");
                    return;
                }
                
                ArenaTerritory.GlowSpacingMeters = spacing;
                ZoneCore.LogInfo($"Glow spacing set to {spacing}m by {ctx.User.CharacterName}");
                ctx.Reply($"<color=#00FF00>Glow spacing set to: {spacing}m</color>");
            }
            catch (Exception ex)
            {
                ZoneCore.LogError($"ZoneSpacing error: {ex.Message}");
                ctx.Reply("<color=#FF0000>Error setting spacing.</color>");
            }
        }

        /// <summary>
        /// Toggle corner glow spawning.
        /// </summary>
        [Command("corners", shortHand: "co", description: "Toggle corner glow spawning", adminOnly: true)]
        public static void ZoneCorners(ChatCommandContext ctx, bool? enable = null)
        {
            try
            {
                var newValue = enable ?? !ArenaTerritory.SpawnGlowInCorners;
                ArenaTerritory.SpawnGlowInCorners = newValue;
                ZoneCore.LogInfo($"Corner spawning {(newValue ? "enabled" : "disabled")} by {ctx.User.CharacterName}");
                ctx.Reply($"<color=#00FF00>Corner spawning {(newValue ? "enabled" : "disabled")}.</color>");
            }
            catch (Exception ex)
            {
                ZoneCore.LogError($"ZoneCorners error: {ex.Message}");
                ctx.Reply("<color=#FF0000>Error managing corners.</color>");
            }
        }

        #region Helper Methods

        private static bool TryGetPlayerPosition(ChatCommandContext ctx, out float3 position)
        {
            position = float3.zero;
            try
            {
                // FIX: Use ZoneCore.Server instead of non-existent ArenaVRCore
                var serverWorld = ZoneCore.Server;
                if (serverWorld == null || !serverWorld.IsCreated)
                {
                    ZoneCore.LogWarning("[Zone] Server world not available");
                    return false;
                }

                var entityManager = serverWorld.EntityManager;
                var characterEntity = ctx.Event?.SenderCharacterEntity ?? Entity.Null;
                if (characterEntity == Entity.Null || !entityManager.Exists(characterEntity))
                {
                    ZoneCore.LogWarning("[Zone] Sender character entity not found");
                    return false;
                }

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
            catch (Exception ex)
            {
                ZoneCore.LogError($"[Zone] GetPlayerPosition failed: {ex.Message}");
                return false;
            }
        }

        #endregion
    }
}
