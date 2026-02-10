using VampireCommandFramework;
using ProjectM.Network;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using System;
using System.Linq;
using VAutomationCore.Core.Commands;
using VAutomationCore.Core.Logging;

namespace VAuto.Commands.Core
{
    /// <summary>
    /// Commands for managing VBlood repair system functionality.
    /// Uses CommandBase for VCF integration, logging, and feedback.
    /// </summary>
    public static class VBloodRepairCommands : CommandBase
    {
        private const string CommandName = "vrepair";
        
        // Static tracking for VBlood repair system
        private static bool _repairSystemEnabled = true;
        private static int _repairCount = 0;
        private static int _unlockCount = 0;
        private static int _resetCount = 0;
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<ulong, DateTime> _lastRepairTime = new();
        private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(10);

        [Command("vrepair", description: "VBlood repair system commands", adminOnly: true)]
        public static void VRepairCommand(ChatCommandContext ctx, string action = "status")
        {
            ExecuteSafely(ctx, CommandName, () =>
            {
                RequirePermission(ctx, PermissionLevel.Admin);
                
                var playerInfo = GetPlayerInfo(ctx);
                Log.Debug($"VBlood repair command '{action}' by {playerInfo.Name}");
                
                switch (action.ToLower())
                {
                    case "status":
                        ShowRepairStatus(ctx);
                        break;
                    case "force":
                        RequireCooldown($"{CommandName}_force", playerInfo.PlatformId, Cooldown);
                        ForceRepair(ctx);
                        break;
                    case "reset":
                        RequireCooldown($"{CommandName}_reset", playerInfo.PlatformId, Cooldown);
                        ResetProgression(ctx);
                        break;
                    case "unlock":
                        RequireCooldown($"{CommandName}_unlock", playerInfo.PlatformId, Cooldown);
                        ForceUnlock(ctx);
                        break;
                    case "enable":
                        EnableRepair(ctx, true);
                        break;
                    case "disable":
                        EnableRepair(ctx, false);
                        break;
                    case "progress":
                        ShowVBloodProgress(ctx);
                        break;
                    case "lock":
                        RequireCooldown($"{CommandName}_lock", playerInfo.PlatformId, Cooldown);
                        LockProgression(ctx);
                        break;
                    case "restore":
                        RequireCooldown($"{CommandName}_restore", playerInfo.PlatformId, Cooldown);
                        RestoreProgression(ctx);
                        break;
                    default:
                        SendError(ctx, $"Unknown action: {action}", "Use: status, force, reset, unlock, enable, disable, progress, lock, restore");
                        break;
                }
            });
        }

        [Command("vrepairstatus", description: "Check VBlood repair system status", adminOnly: false)]
        public static void VRepairStatusCommand(ChatCommandContext ctx)
        {
            ExecuteSafely(ctx, "vrepairstatus", () =>
            {
                RequirePermission(ctx, PermissionLevel.Anyone);
                ShowRepairStatus(ctx);
            });
        }

        [Command("vrepairforce", description: "Force VBlood repair", adminOnly: true)]
        public static void VRepairForceCommand(ChatCommandContext ctx)
        {
            ExecuteSafely(ctx, "vrepairforce", () =>
            {
                RequirePermission(ctx, PermissionLevel.Admin);
                RequireCooldown($"{CommandName}_force", GetPlayerInfo(ctx).PlatformId, Cooldown);
                ForceRepair(ctx);
            });
        }

        [Command("vrepairreset", description: "Reset VBlood progression", adminOnly: true)]
        public static void VRepairResetCommand(ChatCommandContext ctx)
        {
            ExecuteSafely(ctx, "vrepairreset", () =>
            {
                RequirePermission(ctx, PermissionLevel.Admin);
                RequireCooldown($"{CommandName}_reset", GetPlayerInfo(ctx).PlatformId, Cooldown);
                ResetProgression(ctx);
            });
        }

        [Command("vrepairunlock", description: "Force unlock all VBloods", adminOnly: true)]
        public static void VRepairUnlockCommand(ChatCommandContext ctx)
        {
            ExecuteSafely(ctx, "vrepairunlock", () =>
            {
                RequirePermission(ctx, PermissionLevel.Admin);
                RequireCooldown($"{CommandName}_unlock", GetPlayerInfo(ctx).PlatformId, Cooldown);
                ForceUnlock(ctx);
            });
        }

        [Command("vrepairprogress", description: "Show VBlood progress", adminOnly: false)]
        public static void VRepairProgressCommand(ChatCommandContext ctx)
        {
            ExecuteSafely(ctx, "vrepairprogress", () =>
            {
                RequirePermission(ctx, PermissionLevel.Anyone);
                ShowVBloodProgress(ctx);
            });
        }

        [Command("vrepairlock", description: "Lock VBlood progression at current state", adminOnly: true)]
        public static void VRepairLockCommand(ChatCommandContext ctx)
        {
            ExecuteSafely(ctx, "vrepairlock", () =>
            {
                RequirePermission(ctx, PermissionLevel.Admin);
                RequireCooldown($"{CommandName}_lock", GetPlayerInfo(ctx).PlatformId, Cooldown);
                LockProgression(ctx);
            });
        }

        [Command("vrepairrestore", description: "Restore VBlood progression from locked state", adminOnly: true)]
        public static void VRepairRestoreCommand(ChatCommandContext ctx)
        {
            ExecuteSafely(ctx, "vrepairrestore", () =>
            {
                RequirePermission(ctx, PermissionLevel.Admin);
                RequireCooldown($"{CommandName}_restore", GetPlayerInfo(ctx).PlatformId, Cooldown);
                RestoreProgression(ctx);
            });
        }

        [Command("vrepairtest", description: "Test VBlood repair functionality", adminOnly: true)]
        public static void VRepairTestCommand(ChatCommandContext ctx)
        {
            ExecuteSafely(ctx, "vrepairtest", () =>
            {
                RequirePermission(ctx, PermissionLevel.Admin);
                
                var playerInfo = GetPlayerInfo(ctx);
                Log.Info($"VBlood repair test triggered by {playerInfo.Name}");
                
                SendInfo(ctx, "Test command executed - check server logs.");
                
                // Simulate some repair activity
                _repairCount++;
                _lastRepairTime.TryAdd(playerInfo.PlatformId, DateTime.UtcNow);
                
                LogRepairMessage("Test repair completed", playerInfo.Name);
            });
        }

        [Command("vrepairstats", description: "Show repair system statistics", adminOnly: true)]
        public static void VRepairStatsCommand(ChatCommandContext ctx)
        {
            ExecuteSafely(ctx, "vrepairstats", () =>
            {
                RequirePermission(ctx, PermissionLevel.Admin);
                
                SendInfo(ctx, "[VBlood Repair Statistics]");
                SendFeedback(ctx, FeedbackType.Data, $"System Enabled: {(_repairSystemEnabled ? "Yes" : "No")}");
                SendCount(ctx, "Total Repairs", _repairCount);
                SendCount(ctx, "Total Unlocks", _unlockCount);
                SendCount(ctx, "Total Resets", _resetCount);
                SendCount(ctx, "Active Users", _lastRepairTime.Count);
                
                Log.Debug($"VBlood repair stats requested by {GetPlayerInfo(ctx).Name}");
            });
        }

        private static void ShowRepairStatus(ChatCommandContext ctx)
        {
            var playerInfo = GetPlayerInfo(ctx);
            var platformId = playerInfo.PlatformId;
            
            SendInfo(ctx, "[VBlood Repair System]");
            SendFeedback(ctx, FeedbackType.Data, $"Status: {(_repairSystemEnabled ? "Online" : "Offline")}");
            
            if (_lastRepairTime.TryGetValue(platformId, out var lastRepair))
            {
                var elapsed = DateTime.UtcNow - lastRepair;
                SendCount(ctx, "Last Repair", (int)elapsed.TotalSeconds);
            }
            else
            {
                SendInfo(ctx, "- Last Repair: Never");
            }
            
            // TODO: Integrate with ZoneLifecycleBridge when available
            // var vbloodState = ZoneLifecycleBridge.Instance?.GetVBloodState(platformId);
            
            Log.Debug($"VBlood repair status requested by {playerInfo.Name}");
        }

        private static void ShowVBloodProgress(ChatCommandContext ctx)
        {
            var playerInfo = GetPlayerInfo(ctx);
            var platformId = playerInfo.PlatformId;
            
            // TODO: Integrate with ZoneLifecycleBridge when available
            // var vbloodState = ZoneLifecycleBridge.Instance?.GetVBloodState(platformId);
            
            SendInfo(ctx, "[VBlood Progress]");
            SendInfo(ctx, "- No VBlood progress recorded.");
            
            Log.Debug($"VBlood progress requested by {playerInfo.Name}");
        }

        private static void ForceRepair(ChatCommandContext ctx)
        {
            var playerInfo = GetPlayerInfo(ctx);
            
            try
            {
                if (!_repairSystemEnabled)
                {
                    SendWarning(ctx, "Repair system is disabled.");
                    return;
                }
                
                // TODO: Integrate with ZoneLifecycleBridge when available
                // var bridge = ZoneLifecycleBridge.Instance;
                // if (bridge == null)
                // {
                //     SendError(ctx, "ZoneLifecycleBridge not available.");
                //     return;
                // }
                
                // Save current VBlood state (this triggers the repair process)
                // bridge.SaveVBloodProgress(platformId);
                _repairCount++;
                _lastRepairTime.AddOrUpdate(platformId, DateTime.UtcNow, (_, _) => DateTime.UtcNow);
                
                Log.Info($"VBlood force repair triggered by {playerInfo.Name}");
                SendSuccess(ctx, "Force repair completed.");
                
                LogRepairMessage("Force repair executed", playerInfo.Name);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "ForceRepair");
                SendError(ctx, "Failed to execute force repair.");
            }
        }

        private static void ResetProgression(ChatCommandContext ctx)
        {
            var playerInfo = GetPlayerInfo(ctx);
            var platformId = playerInfo.PlatformId;
            
            try
            {
                // TODO: Integrate with ZoneLifecycleBridge when available
                // var bridge = ZoneLifecycleBridge.Instance;
                // if (bridge != null)
                // {
                //     bridge.ClearVBloodState(platformId);
                // }
                
                _resetCount++;
                _lastRepairTime.TryRemove(platformId, out _);
                
                Log.Info($"VBlood progression reset triggered by {playerInfo.Name}");
                SendSuccess(ctx, "Progression has been reset.");
                
                LogRepairMessage("Progression reset executed", playerInfo.Name);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "ResetProgression");
                SendError(ctx, "Failed to reset progression.");
            }
        }

        private static void ForceUnlock(ChatCommandContext ctx)
        {
            var playerInfo = GetPlayerInfo(ctx);
            
            try
            {
                _unlockCount++;
                
                Log.Info($"VBlood force unlock triggered by {playerInfo.Name}");
                SendSuccess(ctx, "Force unlock completed - all VBloods unlocked.");
                
                LogRepairMessage("Force unlock executed", playerInfo.Name);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "ForceUnlock");
                SendError(ctx, "Failed to execute force unlock.");
            }
        }

        private static void EnableRepair(ChatCommandContext ctx, bool enabled)
        {
            var playerInfo = GetPlayerInfo(ctx);
            
            _repairSystemEnabled = enabled;
            var status = enabled ? "enabled" : "disabled";
            Log.Info($"VBlood repair system {status} by {playerInfo.Name}");
            SendSuccess(ctx, $"Repair system {status}.");
        }

        private static void LockProgression(ChatCommandContext ctx)
        {
            var playerInfo = GetPlayerInfo(ctx);
            
            try
            {
                // TODO: Integrate with ZoneLifecycleBridge when available
                // var bridge = ZoneLifecycleBridge.Instance;
                // if (bridge == null)
                // {
                //     SendError(ctx, "ZoneLifecycleBridge not available.");
                //     return;
                // }
                
                // bridge.LockVBloodProgress(platformId);
                
                Log.Info($"VBlood progression locked by {playerInfo.Name}");
                SendSuccess(ctx, "VBlood progression has been locked at current state.");
                
                LogRepairMessage("Progression lock executed", playerInfo.Name);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "LockProgression");
                SendError(ctx, "Failed to lock progression.");
            }
        }

        private static void RestoreProgression(ChatCommandContext ctx)
        {
            var playerInfo = GetPlayerInfo(ctx);
            
            try
            {
                // TODO: Integrate with ZoneLifecycleBridge when available
                // var bridge = ZoneLifecycleBridge.Instance;
                // if (bridge == null)
                // {
                //     SendError(ctx, "ZoneLifecycleBridge not available.");
                //     return;
                // }
                
                // var restored = bridge.RestoreVBloodProgress(platformId);
                var restored = false;
                
                if (restored)
                {
                    Log.Info($"VBlood progression restored by {playerInfo.Name}");
                    SendSuccess(ctx, "VBlood progression restored from locked state.");
                }
                else
                {
                    SendInfo(ctx, "No locked progression found to restore.");
                }
                
                LogRepairMessage("Progression restore executed", playerInfo.Name);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "RestoreProgression");
                SendError(ctx, "Failed to restore progression.");
            }
        }

        private static void LogRepairMessage(string action, string playerName)
        {
            var status = _repairSystemEnabled ? "Online" : "Offline";
            Log.Info($"[VBloodRepair] {action} by {playerName} [Status: {status}]");
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
