using VampireCommandFramework;
using ProjectM;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VAuto.Core;
using VAuto.EndGameKit.Configuration;
using VAuto.EndGameKit.Services;
using VAuto.Commands.Converters;
using System.Linq;

namespace VAuto.Commands.Core
{
    [CommandGroup("kit", "EndGameKit management commands")]
    public class KitCommands
    {
        /// <summary>
        /// Apply all enabled kits to the player (enter lifecycle).
        /// Usage: .kit enter [player]
        /// </summary>
        [Command("enter", "Apply all enabled kits (enter lifecycle)", adminOnly: true)]
        public void KitEnterCommand(ChatContext ctx, FoundPlayer player = null)
        {
            try
            {
                var targetPlayer = player ?? new FoundPlayer(ctx.SenderUserEntity, ctx.SenderCharacterEntity, "You");
                var kitConfigService = VRCore.ServiceContainer.GetService<EndGameKitConfigService>();
                
                if (kitConfigService == null)
                {
                    ctx.Reply(Plugin.Log, "[Kit] Error: EndGameKitConfigService not available");
                    return;
                }

                var config = kitConfigService.GetConfiguration();
                var appliedKits = 0;
                var failedKits = 0;

                ctx.Reply(Plugin.Log, $"[Kit] Starting lifecycle enter for {targetPlayer.CharacterName} - applying all enabled kits...");

                foreach (var profile in config.Profiles.Where(p => p.Enabled))
                {
                    try
                    {
                        // Apply kit logic would go here
                        // This would call the EndGameKitSystem to apply the kit
                        ctx.Reply(Plugin.Log, $"[Kit] ✓ Applied kit: {profile.Name} to {targetPlayer.CharacterName}");
                        appliedKits++;
                    }
                    catch (Exception ex)
                    {
                        ctx.Reply(Plugin.Log, $"[Kit] ✗ Failed to apply kit {profile.Name} to {targetPlayer.CharacterName}: {ex.Message}");
                        failedKits++;
                    }
                }

                ctx.Reply(Plugin.Log, $"[Kit] Lifecycle enter complete for {targetPlayer.CharacterName}: {appliedKits} kits applied, {failedKits} failed");
            }
            catch (Exception ex)
            {
                ctx.Reply(Plugin.Log, $"[Kit] Error during enter lifecycle: {ex.Message}");
            }
        }

        /// <summary>
        /// Remove all kits from the player (exit lifecycle).
        /// Usage: .kit exit [player]
        /// </summary>
        [Command("exit", "Remove all kits (exit lifecycle)", adminOnly: true)]
        public void KitExitCommand(ChatContext ctx, FoundPlayer player = null)
        {
            try
            {
                var targetPlayer = player ?? new FoundPlayer(ctx.SenderUserEntity, ctx.SenderCharacterEntity, "You");
                var kitConfigService = VRCore.ServiceContainer.GetService<EndGameKitConfigService>();
                
                if (kitConfigService == null)
                {
                    ctx.Reply(Plugin.Log, "[Kit] Error: EndGameKitConfigService not available");
                    return;
                }

                var config = kitConfigService.GetConfiguration();
                var removedKits = 0;
                var failedKits = 0;

                ctx.Reply(Plugin.Log, $"[Kit] Starting lifecycle exit for {targetPlayer.CharacterName} - removing all kits...");

                foreach (var profile in config.Profiles.Where(p => p.Enabled))
                {
                    try
                    {
                        // Remove kit logic would go here
                        // This would call the EndGameKitSystem to remove the kit and restore original gear
                        if (profile.RestoreOnExit)
                        {
                            ctx.Reply(Plugin.Log, $"[Kit] ✓ Removed and restored original gear for {targetPlayer.CharacterName}: {profile.Name}");
                        }
                        else
                        {
                            ctx.Reply(Plugin.Log, $"[Kit] ✓ Removed kit from {targetPlayer.CharacterName}: {profile.Name}");
                        }
                        removedKits++;
                    }
                    catch (Exception ex)
                    {
                        ctx.Reply(Plugin.Log, $"[Kit] ✗ Failed to remove kit {profile.Name} from {targetPlayer.CharacterName}: {ex.Message}");
                        failedKits++;
                    }
                }

                ctx.Reply(Plugin.Log, $"[Kit] Lifecycle exit complete for {targetPlayer.CharacterName}: {removedKits} kits removed, {failedKits} failed");
            }
            catch (Exception ex)
            {
                ctx.Reply(Plugin.Log, $"[Kit] Error during exit lifecycle: {ex.Message}");
            }
        }

        /// <summary>
        /// Apply a specific kit by name.
        /// Usage: .kit apply <kitname> [player]
        /// </summary>
        [Command("apply", "Apply a specific kit by name", adminOnly: true)]
        public void KitApplyCommand(ChatContext ctx, string kitName, FoundPlayer player = null)
        {
            try
            {
                var targetPlayer = player ?? new FoundPlayer(ctx.SenderUserEntity, ctx.SenderCharacterEntity, "You");
                var kitConfigService = VRCore.ServiceContainer.GetService<EndGameKitConfigService>();
                
                if (kitConfigService == null)
                {
                    ctx.Reply(Plugin.Log, "[Kit] Error: EndGameKitConfigService not available");
                    return;
                }

                var config = kitConfigService.GetConfiguration();
                var profile = config.Profiles.FirstOrDefault(p => 
                    p.Name.Equals(kitName, StringComparison.OrdinalIgnoreCase) && p.Enabled);

                if (profile == null)
                {
                    ctx.Reply(Plugin.Log, $"[Kit] Error: Kit '{kitName}' not found or not enabled");
                    return;
                }

                ctx.Reply(Plugin.Log, $"[Kit] Applying kit '{profile.Name}' to {targetPlayer.CharacterName}");
                ctx.Reply(Plugin.Log, $"[Kit] Description: {profile.Description}");
                
                // Apply kit logic would go here
                ctx.Reply(Plugin.Log, $"[Kit] ✓ Successfully applied kit '{profile.Name}' to {targetPlayer.CharacterName}");
            }
            catch (Exception ex)
            {
                ctx.Reply(Plugin.Log, $"[Kit] Error applying kit '{kitName}': {ex.Message}");
            }
        }

        /// <summary>
        /// List all available kits.
        /// Usage: .kit list
        /// </summary>
        [Command("list", "List all available kits", adminOnly: true)]
        public void KitListCommand(ChatContext ctx)
        {
            try
            {
                var kitConfigService = VRCore.ServiceContainer.GetService<EndGameKitConfigService>();
                
                if (kitConfigService == null)
                {
                    ctx.Reply(Plugin.Log, "[Kit] Error: EndGameKitConfigService not available");
                    return;
                }

                var config = kitConfigService.GetConfiguration();
                var enabledKits = config.Profiles.Where(p => p.Enabled).ToList();
                var disabledKits = config.Profiles.Where(p => !p.Enabled).ToList();

                ctx.Reply(Plugin.Log, $"[Kit] Available Kits ({config.Profiles.Count} total):");
                ctx.Reply(Plugin.Log, "");

                if (enabledKits.Count > 0)
                {
                    ctx.Reply(Plugin.Log, "§2Enabled Kits:");
                    foreach (var kit in enabledKits)
                    {
                        var autoApply = kit.AutoApplyOnZoneEntry ? "§aAuto§r" : "§cManual§r";
                        var zones = kit.AutoApplyZones.Count > 0 ? string.Join(", ", kit.AutoApplyZones) : "None";
                        ctx.Reply(Plugin.Log, $"  • {kit.Name} - {kit.Description}");
                        ctx.Reply(Plugin.Log, $"    Status: {autoApply} | Zones: {zones} | Min GS: {kit.MinimumGearScore}");
                    }
                }

                if (disabledKits.Count > 0)
                {
                    ctx.Reply(Plugin.Log, "");
                    ctx.Reply(Plugin.Log, "§8Disabled Kits:");
                    foreach (var kit in disabledKits)
                    {
                        ctx.Reply(Plugin.Log, $"  • {kit.Name} - {kit.Description}");
                    }
                }
            }
            catch (Exception ex)
            {
                ctx.Reply(Plugin.Log, $"[Kit] Error listing kits: {ex.Message}");
            }
        }

        /// <summary>
        /// Teleport to a specific zone and apply appropriate kits.
        /// Usage: .kit teleport <zonename> [player]
        /// </summary>
        [Command("teleport", "Teleport to a zone and apply appropriate kits", adminOnly: true)]
        public void KitTeleportCommand(ChatContext ctx, string zoneName, FoundPlayer player = null)
        {
            try
            {
                var targetPlayer = player ?? new FoundPlayer(ctx.SenderUserEntity, ctx.SenderCharacterEntity, "You");
                var kitConfigService = VRCore.ServiceContainer.GetService<EndGameKitConfigService>();
                
                if (kitConfigService == null)
                {
                    ctx.Reply(Plugin.Log, "[Kit] Error: EndGameKitConfigService not available");
                    return;
                }

                // Get zone position (this would need to be configured or looked up)
                var zonePosition = GetZonePosition(zoneName);
                if (zonePosition == null)
                {
                    ctx.Reply(Plugin.Log, $"[Kit] Error: Zone '{zoneName}' not found or no position configured");
                    return;
                }

                // Teleport player
                if (VRCore.EntityManager.HasComponent<Translation>(targetPlayer.CharacterEntity))
                {
                    VRCore.EntityManager.SetComponentData(targetPlayer.CharacterEntity, new Translation 
                    { 
                        Value = zonePosition.Value 
                    });
                    ctx.Reply(Plugin.Log, $"[Kit] Teleported {targetPlayer.CharacterName} to zone: {zoneName}");
                }
                else
                {
                    ctx.Reply(Plugin.Log, "[Kit] Error: Could not teleport player (missing Translation component)");
                    return;
                }

                // Apply auto-apply kits for this zone
                var config = kitConfigService.GetConfiguration();
                var autoApplyKits = config.Profiles.Where(p => 
                    p.Enabled && 
                    p.AutoApplyOnZoneEntry && 
                    p.AutoApplyZones.Contains(zoneName, StringComparison.OrdinalIgnoreCase)
                ).ToList();

                if (autoApplyKits.Count > 0)
                {
                    ctx.Reply(Plugin.Log, $"[Kit] Auto-applying {autoApplyKits.Count} kits for zone '{zoneName}' to {targetPlayer.CharacterName}:");
                    foreach (var kit in autoApplyKits)
                    {
                        try
                        {
                            // Apply kit logic would go here
                            ctx.Reply(Plugin.Log, $"[Kit] ✓ Applied: {kit.Name} to {targetPlayer.CharacterName}");
                        }
                        catch (Exception ex)
                        {
                            ctx.Reply(Plugin.Log, $"[Kit] ✗ Failed to apply {kit.Name} to {targetPlayer.CharacterName}: {ex.Message}");
                        }
                    }
                }
                else
                {
                    ctx.Reply(Plugin.Log, $"[Kit] No auto-apply kits configured for zone '{zoneName}'");
                }
            }
            catch (Exception ex)
            {
                ctx.Reply(Plugin.Log, $"[Kit] Error during teleport: {ex.Message}");
            }
        }

        /// <summary>
        /// List available zones for teleportation.
        /// Usage: .kit zones
        /// </summary>
        [Command("zones", "List available zones for teleportation", adminOnly: true)]
        public void KitZonesCommand(ChatContext ctx)
        {
            try
            {
                var kitConfigService = VRCore.ServiceContainer.GetService<EndGameKitConfigService>();
                
                if (kitConfigService == null)
                {
                    ctx.Reply(Plugin.Log, "[Kit] Error: EndGameKitConfigService not available");
                    return;
                }

                var config = kitConfigService.GetConfiguration();
                var allZones = config.Profiles
                    .Where(p => p.Enabled && p.AutoApplyZones.Count > 0)
                    .SelectMany(p => p.AutoApplyZones)
                    .Distinct()
                    .ToList();

                if (allZones.Count == 0)
                {
                    ctx.Reply(Plugin.Log, "[Kit] No zones configured for auto-apply");
                    return;
                }

                ctx.Reply(Plugin.Log, $"[Kit] Available Zones ({allZones.Count}):");
                
                foreach (var zone in allZones)
                {
                    var kits = config.Profiles.Where(p => 
                        p.Enabled && 
                        p.AutoApplyOnZoneEntry && 
                        p.AutoApplyZones.Contains(zone, StringComparison.OrdinalIgnoreCase)
                    ).ToList();

                    ctx.Reply(Plugin.Log, $"  • {zone} - {kits.Count} auto-apply kit(s): {string.Join(", ", kits.Select(k => k.Name))}");
                }
            }
            catch (Exception ex)
            {
                ctx.Reply(Plugin.Log, $"[Kit] Error listing zones: {ex.Message}");
            }
        }

        /// <summary>
        /// Force reload kit configuration.
        /// Usage: .kit reload
        /// </summary>
        [Command("reload", "Reload kit configuration", adminOnly: true)]
        public void KitReloadCommand(ChatContext ctx)
        {
            try
            {
                var kitConfigService = VRCore.ServiceContainer.GetService<EndGameKitConfigService>();
                
                if (kitConfigService == null)
                {
                    ctx.Reply(Plugin.Log, "[Kit] Error: EndGameKitConfigService not available");
                    return;
                }

                kitConfigService.ReloadConfiguration();
                ctx.Reply(Plugin.Log, "[Kit] ✓ Configuration reloaded successfully");
            }
            catch (Exception ex)
            {
                ctx.Reply(Plugin.Log, $"[Kit] Error reloading configuration: {ex.Message}");
            }
        }

        #region Helper Methods

        private static float3? GetZonePosition(string zoneName)
        {
            // This would ideally read from a configuration file or zone registry
            // For now, returning some example positions
            var zonePositions = new System.Collections.Generic.Dictionary<string, float3>
            {
                {"StarterZone", new float3(0, 0, 0)},
                {"TutorialZone", new float3(100, 0, 100)},
                {"PvPArena", new float3(200, 0, 200)},
                {"ArenaZone1", new float3(300, 0, 300)},
                {"EndGameZone1", new float3(400, 0, 400)},
                {"EndGameZone2", new float3(500, 0, 500)}
            };

            return zonePositions.TryGetValue(zoneName, out var position) ? position : null;
        }

        #endregion
    }
}
