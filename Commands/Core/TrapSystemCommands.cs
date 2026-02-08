using VampireCommandFramework;
using VAuto.Core;
using VAuto.Core.Services;
using System.Collections.Generic;
using System.Text.Json;

namespace VAuto.Commands.Core
{
    /// <summary>
    /// Commands for managing the trap system.
    /// </summary>
    public static class TrapSystemCommands
    {
        [Command("trap", shortHand: "trap", description: "Manage trap system", adminOnly: true)]
        public static void Trap(ChatCommandContext ctx)
        {
            ctx.Reply("[TrapSystem] Commands:");
            ctx.Reply("  .trap status - Show system status");
            ctx.Reply("  .trap reload - Reload configuration");
            ctx.Reply("  .trap spawnchest <player> - Spawn chest for player");
            ctx.Reply("  .trap settrap <container> - Add trap to container");
            ctx.Reply("  .trap waypoint <index> - Toggle waypoint trap");
            ctx.Reply("  .trap config - Show current config");
            ctx.Reply("  .trap streaks - Show all player streaks");
        }
        
        [Command("trap status", shortHand: "ts", description: "Show trap system status")]
        public static void TrapStatus(ChatCommandContext ctx)
        {
            try
            {
                ctx.Reply("[TrapSystem] === System Status ===");
                ctx.Reply("Service: TrapSpawnRules (static)");
                ctx.Reply("Chest spawn: Enabled");
                ctx.Reply("Container trap: Enabled");
                ctx.Reply("Waypoint trap: Enabled");
                ctx.Reply($"Kill Threshold: {TrapSpawnRules.Config.KillThreshold}");
                ctx.Reply($"Chests Per Spawn: {TrapSpawnRules.Config.ChestsPerSpawn}");
            }
            catch (System.Exception ex)
            {
                ctx.Reply($"[TrapSystem] Error: {ex.Message}");
            }
        }
        
        [Command("trap reload", shortHand: "tr", description: "Reload trap system configuration")]
        public static void TrapReload(ChatCommandContext ctx)
        {
            try
            {
                TrapSpawnRules.Initialize();
                ctx.Reply("[TrapSystem] Configuration reloaded successfully");
            }
            catch (System.Exception ex)
            {
                ctx.Reply($"[TrapSystem] Error: {ex.Message}");
            }
        }
        
        [Command("trap spawnchest", shortHand: "tsc", description: "Spawn kill streak chest for player", adminOnly: true)]
        public static void TrapSpawnChest(ChatCommandContext ctx, string playerName)
        {
            try
            {
                // Would need to find player entity and call service
                ctx.Reply($"[TrapSystem] Would spawn chest for {playerName}");
            }
            catch (System.Exception ex)
            {
                ctx.Reply($"[TrapSystem] Error: {ex.Message}");
            }
        }
        
        [Command("trap settrap", shortHand: "tst", description: "Add trap to container", adminOnly: true)]
        public static void TrapSetTrap(ChatCommandContext ctx)
        {
            ctx.Reply("[TrapSystem] Use container targeting to set traps");
        }
        
        [Command("trap waypoint", shortHand: "twp", description: "Toggle waypoint trap", adminOnly: true)]
        public static void TrapWaypoint(ChatCommandContext ctx, int index)
        {
            try
            {
                var waypoints = TrapSpawnRules.GetWaypoints();
                if (waypoints.TryGetValue(index, out var waypoint))
                {
                    waypoint.IsActive = !waypoint.IsActive;
                    ctx.Reply($"[TrapSystem] Waypoint {index} ({waypoint.Name}): {(waypoint.IsActive ? "Active" : "Inactive")}");
                }
                else
                {
                    ctx.Reply($"[TrapSystem] Waypoint {index} not found");
                }
            }
            catch (System.Exception ex)
            {
                ctx.Reply($"[TrapSystem] Error: {ex.Message}");
            }
        }
        
        [Command("trap config", shortHand: "tc", description: "Show current trap configuration")]
        public static void TrapConfig(ChatCommandContext ctx)
        {
            try
            {
                ctx.Reply("[TrapSystem] === Configuration ===");
                ctx.Reply($"Kill Threshold: {TrapSpawnRules.Config.KillThreshold}");
                ctx.Reply($"Chests Per Spawn: {TrapSpawnRules.Config.ChestsPerSpawn}");
                ctx.Reply($"Container Glow Color: {TrapSpawnRules.Config.ContainerGlowColor}");
                ctx.Reply($"Container Glow Radius: {TrapSpawnRules.Config.ContainerGlowRadius}");
                ctx.Reply($"Waypoint Trap Threshold: {TrapSpawnRules.Config.WaypointTrapThreshold}");
                ctx.Reply($"Waypoint Trap Glow Color: {TrapSpawnRules.Config.WaypointTrapGlowColor}");
                ctx.Reply($"Waypoint Trap Glow Radius: {TrapSpawnRules.Config.WaypointTrapGlowRadius}");
                ctx.Reply($"Notification Enabled: {TrapSpawnRules.Config.NotificationEnabled}");
                ctx.Reply($"Trap Damage Amount: {TrapSpawnRules.Config.TrapDamageAmount}");
                ctx.Reply($"Trap Duration: {TrapSpawnRules.Config.TrapDuration}");
            }
            catch (System.Exception ex)
            {
                ctx.Reply($"[TrapSystem] Error: {ex.Message}");
            }
        }
        
        [Command("trap streaks", shortHand: "tss", description: "Show all player kill streaks")]
        public static void TrapStreaks(ChatCommandContext ctx)
        {
            try
            {
                var streaks = TrapSpawnRules.GetAllStreaks();
                ctx.Reply($"[TrapSystem] === Player Kill Streaks ({streaks.Count} players) ===");
                
                foreach (var kvp in streaks)
                {
                    ctx.Reply($"  Player {kvp.Key}: {kvp.Value} kills");
                }
                
                if (streaks.Count == 0)
                {
                    ctx.Reply("  No streaks recorded");
                }
            }
            catch (System.Exception ex)
            {
                ctx.Reply($"[TrapSystem] Error: {ex.Message}");
            }
        }
        
        [Command("trap clear", shortHand: "tcl", description: "Clear all trap system data", adminOnly: true)]
        public static void TrapClear(ChatCommandContext ctx)
        {
            try
            {
                TrapSpawnRules.ClearAll();
                ctx.Reply("[TrapSystem] All data cleared successfully");
            }
            catch (System.Exception ex)
            {
                ctx.Reply($"[TrapSystem] Error: {ex.Message}");
            }
        }
    }
}
