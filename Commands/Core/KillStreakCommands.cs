using VampireCommandFramework;
using ProjectM.Network;
using Unity.Entities;
using Unity.Collections;
using System;
using System.Linq;
using VAutomationCore.Core.Commands;
using VAutomationCore.Core.Logging;

namespace VAuto.Commands.Core
{
    /// <summary>
    /// Commands for managing kill streak configurations and announcements.
    /// Uses CommandBase for VCF integration, logging, and feedback.
    /// </summary>
    public static class KillStreakCommands : CommandBase
    {
        private const string CommandName = "streak";
        
        // Static tracking for kill streaks (in-memory, resets on server restart)
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<ulong, int> _playerStreaks = new();
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<ulong, DateTime> _lastKillTime = new();
        private static bool _announcementsEnabled = true;
        private const int STREAK_THRESHOLD = 3;
        private const int CHEST_THRESHOLD = 5;
        private const int WAYPOINT_THRESHOLD = 10;
        private const int STREAK_TIMEOUT_SECONDS = 120;
        private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(5);

        [Command("streak", description: "Manage kill streak settings", adminOnly: false)]
        public static void StreakCommand(ChatCommandContext ctx, string action = "status")
        {
            ExecuteSafely(ctx, CommandName, () =>
            {
                RequirePermission(ctx, PermissionLevel.Anyone);
                
                var playerInfo = GetPlayerInfo(ctx);
                Log.Debug($"Streak command '{action}' by {playerInfo.Name}");
                
                switch (action.ToLower())
                {
                    case "status":
                        ShowStreakStatus(ctx);
                        break;
                    case "reset":
                        RequireCooldown($"{CommandName}_reset", playerInfo.PlatformId, Cooldown);
                        ResetStreak(ctx);
                        break;
                    case "config":
                        ShowConfig(ctx);
                        break;
                    case "toggle":
                        RequirePermission(ctx, PermissionLevel.Admin);
                        ToggleAnnouncements(ctx);
                        break;
                    default:
                        SendError(ctx, $"Unknown action: {action}", "Use: status, reset, config, toggle");
                        break;
                }
            });
        }

        [Command("streakreset", description: "Reset your kill streak", adminOnly: false)]
        public static void StreakResetCommand(ChatCommandContext ctx)
        {
            ExecuteSafely(ctx, "streakreset", () =>
            {
                var playerInfo = GetPlayerInfo(ctx);
                RequireCooldown($"{CommandName}_reset", playerInfo.PlatformId, Cooldown);
                ResetStreak(ctx);
            });
        }

        [Command("streakannounce", description: "Toggle kill streak announcements", adminOnly: true)]
        public static void StreakAnnounceCommand(ChatCommandContext ctx, bool enabled = true)
        {
            ExecuteSafely(ctx, "streakannounce", () =>
            {
                RequirePermission(ctx, PermissionLevel.Admin);
                
                _announcementsEnabled = enabled;
                Log.Info($"Announcements set to: {enabled} by {GetPlayerInfo(ctx).Name}");
                
                SendSuccess(ctx, $"Kill streak announcements {(_announcementsEnabled ? "enabled" : "disabled")}");
            });
        }

        [Command("streaktest", description: "Test streak notification", adminOnly: true)]
        public static void StreakTestCommand(ChatCommandContext ctx)
        {
            ExecuteSafely(ctx, "streaktest", () =>
            {
                RequirePermission(ctx, PermissionLevel.Admin);
                
                var playerInfo = GetPlayerInfo(ctx);
                Log.Info($"Test streak notification triggered by {playerInfo.Name}");
                
                SendInfo(ctx, "Test notification - check server logs for colored message.");
                
                LogStreakMessage(playerInfo.Name, 3, "Test streak: 3 kills - On Fire!");
                LogStreakMessage(playerInfo.Name, 5, "Test streak: 5 kills - Dominating!");
                LogStreakMessage(playerInfo.Name, 10, "Test streak: 10 kills - Unstoppable!");
            });
        }

        [Command("streakstats", description: "Show your current kill streak", adminOnly: false)]
        public static void StreakStatsCommand(ChatCommandContext ctx)
        {
            ExecuteSafely(ctx, "streakstats", () =>
            {
                RequirePermission(ctx, PermissionLevel.Anyone);
                
                var playerInfo = GetPlayerInfo(ctx);
                var platformId = playerInfo.PlatformId;
                
                if (_playerStreaks.TryGetValue(platformId, out var streak))
                {
                    SendSuccess(ctx, $"Your current streak", $"{streak} kills");
                    
                    if (_lastKillTime.TryGetValue(platformId, out var lastKill))
                    {
                        var elapsed = DateTime.UtcNow - lastKill;
                        var remaining = TimeSpan.FromSeconds(STREAK_TIMEOUT_SECONDS) - elapsed;
                        if (remaining.TotalSeconds > 0)
                        {
                            SendInfo(ctx, $"Time remaining: {(int)remaining.TotalSeconds}s");
                        }
                        else
                        {
                            SendInfo(ctx, "Your streak has expired.");
                        }
                    }
                    
                    if (streak >= CHEST_THRESHOLD)
                    {
                        SendFeedback(ctx, FeedbackType.Count, $"🎁 Chest reward available at {CHEST_THRESHOLD} kills!");
                    }
                    if (streak >= WAYPOINT_THRESHOLD)
                    {
                        SendFeedback(ctx, FeedbackType.Location, $"📍 Waypoint reward available at {WAYPOINT_THRESHOLD} kills!");
                    }
                }
                else
                {
                    SendInfo(ctx, "No active streak. Start killing!");
                }
                
                Log.Debug($"Streak stats requested by {playerInfo.Name}: streak={streak}");
            });
        }

        private static void ShowStreakStatus(ChatCommandContext ctx)
        {
            var playerInfo = GetPlayerInfo(ctx);
            var platformId = playerInfo.PlatformId;
            
            if (_playerStreaks.TryGetValue(platformId, out var streak))
            {
                SendSuccess(ctx, "Your current streak", $"{streak} kills");
                
                if (streak >= CHEST_THRESHOLD)
                {
                    SendFeedback(ctx, FeedbackType.Count, $"🎁 Chest reward available at {CHEST_THRESHOLD} kills!");
                }
                if (streak >= WAYPOINT_THRESHOLD)
                {
                    SendFeedback(ctx, FeedbackType.Location, $"📍 Waypoint reward available at {WAYPOINT_THRESHOLD} kills!");
                }
            }
            else
            {
                SendInfo(ctx, "No active streak. Start killing!");
            }
            
            Log.Debug($"Streak status requested by {playerInfo.Name}");
        }

        private static void ResetStreak(ChatCommandContext ctx)
        {
            var playerInfo = GetPlayerInfo(ctx);
            var platformId = playerInfo.PlatformId;
            
            if (_playerStreaks.TryRemove(platformId, out _))
            {
                _lastKillTime.TryRemove(platformId, out _);
                Log.Info($"Streak reset for {playerInfo.Name}");
                SendSuccess(ctx, "Your streak has been reset.");
            }
            else
            {
                SendInfo(ctx, "You had no active streak to reset.");
            }
        }

        private static void ShowConfig(ChatCommandContext ctx)
        {
            SendInfo(ctx, "[Kill Streak Configuration]");
            SendCount(ctx, "Announcement Threshold", STREAK_THRESHOLD);
            SendCount(ctx, "Timeout", STREAK_TIMEOUT_SECONDS);
            SendFeedback(ctx, FeedbackType.Info, $"Announcements: {(_announcementsEnabled ? "Enabled" : "Disabled")}");
            SendCount(ctx, "Chest Threshold", CHEST_THRESHOLD);
            SendCount(ctx, "Waypoint Threshold", WAYPOINT_THRESHOLD);
            
            Log.Debug($"Config requested by {GetPlayerInfo(ctx).Name}");
        }

        private static void ToggleAnnouncements(ChatCommandContext ctx)
        {
            _announcementsEnabled = !_announcementsEnabled;
            var status = _announcementsEnabled ? "enabled" : "disabled";
            SendSuccess(ctx, $"Kill streak announcements are now {status}");
            
            Log.Info($"Announcements toggled to {_announcementsEnabled} by {GetPlayerInfo(ctx).Name}");
        }

        /// <summary>
        /// Records a kill for a player and returns the new streak count.
        /// Returns -1 if the streak expired.
        /// </summary>
        public static int RecordKill(ulong platformId)
        {
            if (_lastKillTime.TryGetValue(platformId, out var lastKill))
            {
                var elapsed = DateTime.UtcNow - lastKill;
                if (elapsed.TotalSeconds > STREAK_TIMEOUT_SECONDS)
                {
                    // Streak expired
                    _playerStreaks.TryRemove(platformId, out _);
                    _lastKillTime.TryRemove(platformId, out _);
                    return 1; // Start fresh
                }
            }
            
            var newStreak = _playerStreaks.AddOrUpdate(platformId, 1, (_, current) => current + 1);
            _lastKillTime.AddOrUpdate(platformId, DateTime.UtcNow, (_, _) => DateTime.UtcNow);
            
            Log.Debug($"Player {platformId} now has {newStreak} streak");
            
            return newStreak;
        }

        /// <summary>
        /// Resets a player's streak (when they die).
        /// </summary>
        public static void RecordDeath(ulong platformId)
        {
            if (_playerStreaks.TryRemove(platformId, out var oldStreak))
            {
                _lastKillTime.TryRemove(platformId, out _);
                Log.Debug($"Player {platformId} died, streak reset from {oldStreak}");
            }
        }

        /// <summary>
        /// Gets the current streak for a player (0 if none).
        /// </summary>
        public static int GetStreak(ulong platformId)
        {
            return _playerStreaks.TryGetValue(platformId, out var streak) ? streak : 0;
        }

        /// <summary>
        /// Checks if a streak announcement should be made and logs it.
        /// </summary>
        public static void CheckAndAnnounceStreak(string playerName, ulong platformId)
        {
            var streak = GetStreak(platformId);
            if (streak >= STREAK_THRESHOLD && streak % STREAK_THRESHOLD == 0)
            {
                LogStreakMessage(playerName, streak, GetStreakMessage(streak));
            }
        }

        private static string GetStreakMessage(int streak)
        {
            return streak switch
            {
                3 => "is ON FIRE!",
                5 => "is DOMINATING!",
                10 => "is UNSTOPPABLE!",
                15 => "is LEGENDARY!",
                20 => "is a GOD!",
                _ => $"has {streak} kills!"
            };
        }

        private static void LogStreakMessage(string playerName, int streak, string message)
        {
            // Log with color-coded message based on streak level
            var color = streak switch
            {
                <= 3 => ChatColor.White,
                <= 5 => ChatColor.Yellow,
                <= 10 => ChatColor.Orange,
                <= 15 => "DarkOrange",
                _ => ChatColor.Red
            };
            
            Log.Info($"🎯 {playerName} {message} (Streak: {streak}) [Color: {color}]");
        }
    }
}
