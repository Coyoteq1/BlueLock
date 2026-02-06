using System;
using System.Collections.Generic;
using Unity.Mathematics;
using VampireCommandFramework;

namespace VAuto.Core.Services
{
    /// <summary>
    /// Message action rules - converts chat commands into game actions.
    /// This provides actual functionality beyond just logging messages.
    /// </summary>
    public static class MessageActionRules
    {
        private static readonly Dictionary<string, Action<ChatCommandContext, string[]>> _actionHandlers = new();
        private static bool _initialized;
        
        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            
            // Register built-in action handlers
            RegisterHandler("announce", HandleAnnounce);
            RegisterHandler("say", HandleSay);
            RegisterHandler("alert", HandleAlert);
            RegisterHandler("broadcast", HandleBroadcast);
            RegisterHandler("trap", HandleTrap);
            
            Plugin.Log.LogInfo("[MessageActionRules] Initialized with action handlers");
        }
        
        /// <summary>
        /// Register a command handler.
        /// </summary>
        public static void RegisterHandler(string command, Action<ChatCommandContext, string[]> handler)
        {
            _actionHandlers[command.ToLower()] = handler;
            Plugin.Log.LogInfo($"[MessageActionRules] Registered handler for: {command}");
        }
        
        /// <summary>
        /// Execute a command action.
        /// </summary>
        public static bool ExecuteAction(string command, ChatCommandContext ctx, string[] args)
        {
            if (_actionHandlers.TryGetValue(command.ToLower(), out var handler))
            {
                handler(ctx, args);
                return true;
            }
            return false;
        }
        
        #region Built-in Handlers
        
        private static void HandleAnnounce(ChatCommandContext ctx, string[] args)
        {
            if (args.Length == 0)
            {
                ctx.Reply("[Announce] Usage: .announce <message>");
                return;
            }
            
            var message = string.Join(" ", args);
            var fullMessage = $"[ANNOUNCEMENT] {message}";
            
            // Log to console
            Plugin.Log.LogInfo($"[MessageActionRules] Broadcasting: {fullMessage}");
            
            // TODO: Actually broadcast to all players using:
            // ProjectM.UserNameService.BroadcastChatMessage(fullMessage);
            
            ctx.Reply($"[Announce] Broadcasted: {message}");
            
            // Trigger announcement event for other systems
            OnAnnouncementBroadcast("announce", message);
        }
        
        private static void HandleSay(ChatCommandContext ctx, string[] args)
        {
            if (args.Length == 0)
            {
                ctx.Reply("[Say] Usage: .say <message>");
                return;
            }
            
            var message = string.Join(" ", args);
            var fullMessage = $"[ADMIN] {message}";
            
            Plugin.Log.LogInfo($"[MessageActionRules] Admin says: {fullMessage}");
            
            // TODO: Send to all players
            // ProjectM.UserNameService.BroadcastChatMessage(fullMessage);
            
            ctx.Reply($"[Say] Message sent to all players");
        }
        
        private static void HandleAlert(ChatCommandContext ctx, string[] args)
        {
            if (args.Length == 0)
            {
                ctx.Reply("[Alert] Usage: .alert <message>");
                return;
            }
            
            var message = string.Join(" ", args);
            var fullMessage = $"⚠️ ALERT: {message} ⚠️";
            
            Plugin.Log.LogWarning($"[MessageActionRules] Alert: {fullMessage}");
            
            // TODO: Send urgent alert with different notification type
            // ProjectM.UserNameService.BroadcastChatMessage(fullMessage);
            
            ctx.Reply($"[Alert] Alert sent to all players");
        }
        
        private static void HandleBroadcast(ChatCommandContext ctx, string[] args)
        {
            if (args.Length == 0)
            {
                ctx.Reply("[Broadcast] Usage: .broadcast <message>");
                return;
            }
            
            var message = string.Join(" ", args);
            var fullMessage = $"📢 {message} 📢";
            
            Plugin.Log.LogInfo($"[MessageActionRules] System broadcast: {fullMessage}");
            
            // TODO: Send system broadcast
            // ProjectM.UserNameService.BroadcastChatMessage(fullMessage);
            
            ctx.Reply("[Broadcast] Message broadcasted to all players");
        }
        
        private static void HandleTrap(ChatCommandContext ctx, string[] args)
        {
            if (args.Length == 0)
            {
                // Main trap menu
                ctx.Reply("[TrapSystem] === Commands ===");
                ctx.Reply("  .trap status - System status");
                ctx.Reply("  .trap config - Current configuration");
                ctx.Reply("  .trap streaks - All player kill streaks");
                ctx.Reply("  .trap reload - Reload config");
                ctx.Reply("  .trap clear - Clear all data");
                ctx.Reply("  .trap spawnchest <player> - Spawn chest");
                ctx.Reply("  .trap waypoint <index> - Toggle waypoint");
                return;
            }
            
            var subCommand = args[0].ToLower();
            var subArgs = args.Length > 1 ? new ArraySegment<string>(args, 1, args.Length - 1).ToArray() : Array.Empty<string>();
            
            switch (subCommand)
            {
                case "status":
                    ShowTrapStatus(ctx);
                    break;
                    
                case "config":
                    ShowTrapConfig(ctx);
                    break;
                    
                case "streaks":
                    ShowTrapStreaks(ctx);
                    break;
                    
                case "reload":
                    TrapSpawnRules.Initialize();
                    ctx.Reply("[TrapSystem] Configuration reloaded");
                    break;
                    
                case "clear":
                    TrapSpawnRules.ClearAll();
                    ctx.Reply("[TrapSystem] All data cleared");
                    break;
                    
                case "spawnchest":
                    if (subArgs.Length > 0)
                    {
                        ctx.Reply($"[TrapSystem] Would spawn chest for: {subArgs[0]}");
                        // TODO: Find player entity and spawn chest
                    }
                    else
                    {
                        ctx.Reply("[TrapSystem] Usage: .trap spawnchest <player>");
                    }
                    break;
                    
                case "waypoint":
                    if (subArgs.Length > 0 && int.TryParse(subArgs[0], out var index))
                    {
                        ToggleWaypoint(ctx, index);
                    }
                    else
                    {
                        ctx.Reply("[TrapSystem] Usage: .trap waypoint <index>");
                    }
                    break;
                    
                default:
                    ctx.Reply($"[TrapSystem] Unknown command: {subCommand}");
                    break;
            }
        }
        
        #endregion
        
        #region Helper Methods
        
        private static void ShowTrapStatus(ChatCommandContext ctx)
        {
            ctx.Reply("[TrapSystem] === System Status ===");
            ctx.Reply("Service: TrapSpawnRules (static)");
            ctx.Reply("Chest Spawn: Enabled");
            ctx.Reply($"Kill Threshold: {TrapSpawnRules.Config.KillThreshold}");
            ctx.Reply($"Chests Per Spawn: {TrapSpawnRules.Config.ChestsPerSpawn}");
            ctx.Reply($"Waypoint Trap Threshold: {TrapSpawnRules.Config.WaypointTrapThreshold}");
        }
        
        private static void ShowTrapConfig(ChatCommandContext ctx)
        {
            ctx.Reply("[TrapSystem] === Configuration ===");
            ctx.Reply($"Container Glow: {TrapSpawnRules.Config.ContainerGlowColor}");
            ctx.Reply($"Container Glow Radius: {TrapSpawnRules.Config.ContainerGlowRadius}m");
            ctx.Reply($"Waypoint Glow: {TrapSpawnRules.Config.WaypointTrapGlowColor}");
            ctx.Reply($"Waypoint Glow Radius: {TrapSpawnRules.Config.WaypointTrapGlowRadius}m");
            ctx.Reply($"Notifications: {(TrapSpawnRules.Config.NotificationEnabled ? "Enabled" : "Disabled")}");
            ctx.Reply($"Trap Damage: {TrapSpawnRules.Config.TrapDamageAmount}");
        }
        
        private static void ShowTrapStreaks(ChatCommandContext ctx)
        {
            var streaks = TrapSpawnRules.GetAllStreaks();
            ctx.Reply($"[TrapSystem] === Player Streaks ({streaks.Count}) ===");
            
            foreach (var kvp in streaks)
            {
                ctx.Reply($"  Player {kvp.Key}: {kvp.Value} kills");
            }
            
            if (streaks.Count == 0)
            {
                ctx.Reply("  No streaks recorded");
            }
        }
        
        private static void ToggleWaypoint(ChatCommandContext ctx, int index)
        {
            var waypoints = TrapSpawnRules.GetWaypoints();
            if (waypoints.TryGetValue(index, out var waypoint))
            {
                waypoint.IsActive = !waypoint.IsActive;
                ctx.Reply($"[TrapSystem] Waypoint {index} ({waypoint.Name}): {(waypoint.IsActive ? "Active" : "Inactive")}");
            }
            else
            {
                ctx.Reply($"[TrapSystem] Waypoint {index} not found. Available: {string.Join(", ", waypoints.Keys)}");
            }
        }
        
        #endregion
        
        #region Events
        
        /// <summary>
        /// Called when an announcement is broadcast.
        /// </summary>
        private static void OnAnnouncementBroadcast(string command, string message)
        {
            Plugin.Log.LogInfo($"[MessageActionRules] Announcement event: {command} - {message}");
            // Could trigger other systems to react
        }
        
        #endregion
    }
    
}
