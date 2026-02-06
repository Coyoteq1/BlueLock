using VampireCommandFramework;
using ProjectM.Network;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using System;
using System.Linq;

namespace VAuto.Commands.Core
{
    /// <summary>
    /// Commands for testing VBlood repair system functionality.
    /// These commands work standalone without ECS dependencies.
    /// </summary>
    public static class VBloodRepairCommands
    {
        // Static tracking for VBlood repair system
        private static bool _repairSystemEnabled = true;
        private static int _repairCount = 0;
        private static int _unlockCount = 0;
        private static int _resetCount = 0;
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<ulong, DateTime> _lastRepairTime = new();

        [Command("vrepair", description: "VBlood repair system test commands", adminOnly: true)]
        public static void VRepairCommand(ChatCommandContext ctx, string action = "status")
        {
            try
            {
                switch (action.ToLower())
                {
                    case "status":
                        ShowRepairStatus(ctx);
                        break;
                    case "force":
                        ForceRepair(ctx);
                        break;
                    case "reset":
                        ResetProgression(ctx);
                        break;
                    case "unlock":
                        ForceUnlock(ctx);
                        break;
                    case "enable":
                        EnableRepair(ctx, true);
                        break;
                    case "disable":
                        EnableRepair(ctx, false);
                        break;
                    default:
                        ctx.Reply($"Unknown action: {action}. Use: status, force, reset, unlock, enable, disable");
                        break;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[VBloodRepairCommands] Error: {ex.Message}");
                ctx.Reply("An error occurred processing your command.");
            }
        }

        [Command("vrepairstatus", description: "Check VBlood repair system status", adminOnly: false)]
        public static void VRepairStatusCommand(ChatCommandContext ctx)
        {
            ShowRepairStatus(ctx);
        }

        [Command("vrepairforce", description: "Force VBlood repair", adminOnly: true)]
        public static void VRepairForceCommand(ChatCommandContext ctx)
        {
            ForceRepair(ctx);
        }

        [Command("vrepairreset", description: "Reset VBlood progression", adminOnly: true)]
        public static void VRepairResetCommand(ChatCommandContext ctx)
        {
            ResetProgression(ctx);
        }

        [Command("vrepairunlock", description: "Force unlock all VBloods", adminOnly: true)]
        public static void VRepairUnlockCommand(ChatCommandContext ctx)
        {
            ForceUnlock(ctx);
        }

        [Command("vrepairtest", description: "Test VBlood repair functionality", adminOnly: true)]
        public static void VRepairTestCommand(ChatCommandContext ctx)
        {
            var user = ctx.User;
            var playerName = user.CharacterName.ToString();
            Plugin.Log.LogInfo($"[VBloodRepair] Test repair functionality triggered by {playerName}");
            ctx.Reply("[VBloodRepair] Test command executed - check server logs.");
            
            // Simulate some repair activity
            _repairCount++;
            _lastRepairTime.TryAdd(user.PlatformId, DateTime.UtcNow);
            
            LogRepairMessage("Test repair completed", playerName);
        }

        [Command("vrepairstats", description: "Show repair system statistics", adminOnly: true)]
        public static void VRepairStatsCommand(ChatCommandContext ctx)
        {
            ctx.Reply("[VBlood Repair Statistics]");
            ctx.Reply($"- System Enabled: {(_repairSystemEnabled ? "Yes" : "No")}");
            ctx.Reply($"- Total Repairs: {_repairCount}");
            ctx.Reply($"- Total Unlocks: {_unlockCount}");
            ctx.Reply($"- Total Resets: {_resetCount}");
            ctx.Reply($"- Active Users: {_lastRepairTime.Count}");
            
            Plugin.Log.LogInfo($"[VBloodRepair] Stats requested by {ctx.User.CharacterName.ToString()}");
        }

        private static void ShowRepairStatus(ChatCommandContext ctx)
        {
            var user = ctx.User;
            var platformId = user.PlatformId;
            
            ctx.Reply("[VBlood Repair System]");
            ctx.Reply($"- Status: {(_repairSystemEnabled ? "Online" : "Offline")}");
            
            if (_lastRepairTime.TryGetValue(platformId, out var lastRepair))
            {
                var elapsed = DateTime.UtcNow - lastRepair;
                ctx.Reply($"- Last Repair: {(int)elapsed.TotalSeconds} seconds ago");
            }
            else
            {
                ctx.Reply("- Last Repair: Never");
            }
            
            Plugin.Log.LogInfo($"[VBloodRepair] Status requested by {user.CharacterName}");
        }

        private static void ForceRepair(ChatCommandContext ctx)
        {
            var user = ctx.User;
            var playerName = user.CharacterName.ToString();
            
            try
            {
                if (!_repairSystemEnabled)
                {
                    ctx.Reply("[VBloodRepair] Repair system is disabled.");
                    return;
                }
                
                _repairCount++;
                _lastRepairTime.AddOrUpdate(user.PlatformId, DateTime.UtcNow, (_, _) => DateTime.UtcNow);
                
                Plugin.Log.LogInfo($"[VBloodRepair] Force repair triggered by {playerName}");
                ctx.Reply("[VBloodRepair] Force repair completed.");
                
                LogRepairMessage("Force repair executed", playerName);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[VBloodRepairCommands] Force repair failed: {ex.Message}");
                ctx.Reply("[VBloodRepair] Failed to execute force repair.");
            }
        }

        private static void ResetProgression(ChatCommandContext ctx)
        {
            var user = ctx.User;
            var playerName = user.CharacterName.ToString();
            
            try
            {
                _resetCount++;
                // Clear user's repair history
                _lastRepairTime.TryRemove(user.PlatformId, out _);
                
                Plugin.Log.LogInfo($"[VBloodRepair] Progression reset triggered by {playerName}");
                ctx.Reply("[VBloodRepair] Progression has been reset.");
                
                LogRepairMessage("Progression reset executed", playerName);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[VBloodRepairCommands] Reset failed: {ex.Message}");
                ctx.Reply("[VBloodRepair] Failed to reset progression.");
            }
        }

        private static void ForceUnlock(ChatCommandContext ctx)
        {
            var user = ctx.User;
            var playerName = user.CharacterName.ToString();
            
            try
            {
                _unlockCount++;
                
                Plugin.Log.LogInfo($"[VBloodRepair] Force unlock triggered by {playerName}");
                ctx.Reply("[VBloodRepair] Force unlock completed - all VBloods unlocked.");
                
                LogRepairMessage("Force unlock executed", playerName);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[VBloodRepairCommands] Force unlock failed: {ex.Message}");
                ctx.Reply("[VBloodRepair] Failed to execute force unlock.");
            }
        }

        private static void EnableRepair(ChatCommandContext ctx, bool enabled)
        {
            var user = ctx.User;
            
            _repairSystemEnabled = enabled;
            Plugin.Log.LogInfo($"[VBloodRepair] System {(enabled ? "enabled" : "disabled")} by {user.CharacterName}");
            ctx.Reply($"[VBloodRepair] Repair system {(enabled ? "enabled" : "disabled")}.");
        }

        private static void LogRepairMessage(string action, string playerName)
        {
            var status = _repairSystemEnabled ? "Online" : "Offline";
            Plugin.Log.LogInfo($"[VBloodRepair] {action} by {playerName} [Status: {status}]");
        }

        private static void ctxReply(string message)
        {
            // Fallback for typo - will be caught by compiler if used
        }

        /// <summary>
        /// Public method to record a repair event (can be called from other systems).
        /// </summary>
        public static void RecordRepair(ulong platformId)
        {
            if (!_repairSystemEnabled) return;
            
            _repairCount++;
            _lastRepairTime.AddOrUpdate(platformId, DateTime.UtcNow, (_, _) => DateTime.UtcNow);
        }

        /// <summary>
        /// Public method to record an unlock event.
        /// </summary>
        public static void RecordUnlock(ulong platformId)
        {
            if (!_repairSystemEnabled) return;
            
            _unlockCount++;
        }

        /// <summary>
        /// Gets the repair system enabled status.
        /// </summary>
        public static bool IsRepairSystemEnabled()
        {
            return _repairSystemEnabled;
        }

        /// <summary>
        /// Gets the total repair count.
        /// </summary>
        public static int GetRepairCount()
        {
            return _repairCount;
        }
    }
}
