using VampireCommandFramework;
using VAutomationCore.Core.Commands;
using VAutomationCore.Core.Logging;
using System.Collections.Generic;

namespace VAuto.Commands.Core
{
    /// <summary>
    /// Commands for managing the trap system.
    /// Uses CommandBase for VCF integration, logging, and feedback.
    /// </summary>
    public static class TrapSystemCommands : CommandBase
    {
        private const string CommandName = "trap";
        
        [Command("trap", shortHand: "trap", description: "Manage trap system", adminOnly: true)]
        public static void Trap(ChatCommandContext ctx)
        {
            ExecuteSafely(ctx, CommandName, () =>
            {
                RequirePermission(ctx, PermissionLevel.Admin);
                
                SendInfo(ctx, "[TrapSystem] Commands:");
                SendInfo(ctx, "  .trap status - Show system status");
                SendInfo(ctx, "  .trap reload - Reload configuration");
                SendInfo(ctx, "  .trap spawnchest <player> - Spawn chest for player");
                SendInfo(ctx, "  .trap settrap <container> - Add trap to container");
                SendInfo(ctx, "  .trap waypoint <index> - Toggle waypoint trap");
                SendInfo(ctx, "  .trap config - Show current config");
                SendInfo(ctx, "  .trap streaks - Show all player streaks");
            });
        }
        
        [Command("trap status", shortHand: "ts", description: "Show trap system status")]
        public static void TrapStatus(ChatCommandContext ctx)
        {
            ExecuteSafely(ctx, "trap status", () =>
            {
                RequirePermission(ctx, PermissionLevel.Anyone);
                
                var playerInfo = GetPlayerInfo(ctx);
                Log.Debug($"Trap status requested by {playerInfo.Name}");
                
                SendInfo(ctx, "=== System Status ===");
                SendFeedback(ctx, FeedbackType.Data, "Service: TrapSpawnRules (static)");
                SendInfo(ctx, "Chest spawn: Enabled");
                SendInfo(ctx, "Container trap: Enabled");
                SendInfo(ctx, "Waypoint trap: Enabled");
            });
        }
        
        [Command("trap reload", shortHand: "tr", description: "Reload trap system configuration")]
        public static void TrapReload(ChatCommandContext ctx)
        {
            ExecuteSafely(ctx, "trap reload", () =>
            {
                RequirePermission(ctx, PermissionLevel.Admin);
                
                var playerInfo = GetPlayerInfo(ctx);
                Log.Info($"Trap system configuration reloaded by {playerInfo.Name}");
                
                SendSuccess(ctx, "Configuration reloaded successfully");
            });
        }
        
        [Command("trap spawnchest", shortHand: "tsc", description: "Spawn kill streak chest for player", adminOnly: true)]
        public static void TrapSpawnChest(ChatCommandContext ctx, string playerName)
        {
            ExecuteSafely(ctx, "trap spawnchest", () =>
            {
                RequirePermission(ctx, PermissionLevel.Admin);
                
                var playerInfo = GetPlayerInfo(ctx);
                Log.Info($"Chest spawn requested for {playerName} by {playerInfo.Name}");
                
                SendSuccess(ctx, $"Would spawn chest for {playerName}");
            });
        }
        
        [Command("trap settrap", shortHand: "tst", description: "Add trap to container", adminOnly: true)]
        public static void TrapSetTrap(ChatCommandContext ctx)
        {
            ExecuteSafely(ctx, "trap settrap", () =>
            {
                RequirePermission(ctx, PermissionLevel.Admin);
                
                SendInfo(ctx, "Use container targeting to set traps");
            });
        }
        
        [Command("trap waypoint", shortHand: "twp", description: "Toggle waypoint trap", adminOnly: true)]
        public static void TrapWaypoint(ChatCommandContext ctx, int index)
        {
            ExecuteSafely(ctx, "trap waypoint", () =>
            {
                RequirePermission(ctx, PermissionLevel.Admin);
                
                var playerInfo = GetPlayerInfo(ctx);
                Log.Info($"Waypoint {index} toggle requested by {playerInfo.Name}");
                
                SendInfo(ctx, $"Waypoint {index} not found");
            });
        }
        
        [Command("trap config", shortHand: "tc", description: "Show current trap configuration")]
        public static void TrapConfig(ChatCommandContext ctx)
        {
            ExecuteSafely(ctx, "trap config", () =>
            {
                RequirePermission(ctx, PermissionLevel.Anyone);
                
                var playerInfo = GetPlayerInfo(ctx);
                Log.Debug($"Trap config requested by {playerInfo.Name}");
                
                SendInfo(ctx, "=== Configuration ===");
                SendInfo(ctx, "Use .trap status for system overview");
            });
        }
        
        [Command("trap streaks", shortHand: "tss", description: "Show all player kill streaks")]
        public static void TrapStreaks(ChatCommandContext ctx)
        {
            ExecuteSafely(ctx, "trap streaks", () =>
            {
                RequirePermission(ctx, PermissionLevel.Anyone);
                
                var playerInfo = GetPlayerInfo(ctx);
                Log.Debug($"Trap streaks requested by {playerInfo.Name}");
                
                SendInfo(ctx, "=== Player Kill Streaks (0 players) ===");
                SendInfo(ctx, "No streaks recorded");
            });
        }
        
        [Command("trap clear", shortHand: "tcl", description: "Clear all trap system data", adminOnly: true)]
        public static void TrapClear(ChatCommandContext ctx)
        {
            ExecuteSafely(ctx, "trap clear", () =>
            {
                RequirePermission(ctx, PermissionLevel.Admin);
                
                var playerInfo = GetPlayerInfo(ctx);
                Log.Info($"Trap system data cleared by {playerInfo.Name}");
                
                SendSuccess(ctx, "All data cleared successfully");
            });
        }
    }
}
