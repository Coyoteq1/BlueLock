using VampireCommandFramework;
using ProjectM.Network;
using Unity.Entities;
using Unity.Collections;
using System;
using System.Linq;

namespace VAuto.Commands.Core
{
    /// <summary>
    /// Commands for managing kill streak configurations and announcements.
    /// These commands work standalone without ECS dependencies.
    /// </summary>
    public static class KillStreakCommands
    {
        // Static tracking for kill streaks (in-memory, resets on server restart)
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<ulong, int> _playerStreaks = new();
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<ulong, DateTime> _lastKillTime = new();
        private static bool _announcementsEnabled = true;
        private const int STREAK_THRESHOLD = 3;
        private const int CHEST_THRESHOLD = 5;
        private const int WAYPOINT_THRESHOLD = 10;
        private const int STREAK_TIMEOUT_SECONDS = 120;

        [Command("streak", description: "Manage kill streak settings", adminOnly: false)]
        public static void StreakCommand(ChatCommandContext ctx, string action = "status")
        {
            try
            {
                switch (action.ToLower())
                {
                    case "status":
                        ShowStreakStatus(ctx);
                        break;
                    case "reset":
                        ResetStreak(ctx);
                        break;
                    case "config":
                        ShowConfig(ctx);
                        break;
                    case "toggle":
                        ToggleAnnouncements(ctx);
                        break;
                    default:
                        ctx.Reply($"Unknown action: {action}. Use: status, reset, config, toggle");
                        break;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[KillStreakCommands] Error: {ex.Message}");
                ctx.Reply("An error occurred processing your command.");
            }
        }

        [Command("streakreset", description: "Reset your kill streak", adminOnly: false)]
        public static void StreakResetCommand(ChatCommandContext ctx)
        {
            ResetStreak(ctx);
        }

        [Command("streakannounce", description: "Toggle kill streak announcements", adminOnly: true)]
        public static void StreakAnnounceCommand(ChatCommandContext ctx, bool enabled = true)
        {
            _announcementsEnabled = enabled;
            Plugin.Log.LogInfo($"[KillStreakCommands] Announcements set to: {enabled}");
            ctx.Reply($"Kill streak announcements {(enabled ? "enabled" : "disabled")}");
        }

        [Command("streaktest", description: "Test streak notification", adminOnly: true)]
        public static void StreakTestCommand(ChatCommandContext ctx)
        {
            var user = ctx.User;
            var playerName = user.CharacterName.ToString();
            Plugin.Log.LogInfo($"[KillStreak] Test streak notification triggered by {playerName}");
            ctx.Reply("[KillStreak] Test notification - check server logs for colored message.");
            
            LogStreakMessage(playerName, 3, "Test streak: 3 kills - On Fire!");
            LogStreakMessage(playerName, 5, "Test streak: 5 kills - Dominating!");
            LogStreakMessage(playerName, 10, "Test streak: 10 kills - Unstoppable!");
        }

        [Command("streakstats", description: "Show your current kill streak", adminOnly: false)]
        public static void StreakStatsCommand(ChatCommandContext ctx)
        {
            var user = ctx.User;
            var platformId = user.PlatformId;
            
            if (_playerStreaks.TryGetValue(platformId, out var streak))
            {
                ctx.Reply($"[KillStreak] Your current streak: {streak} kills");
                if (_lastKillTime.TryGetValue(platformId, out var lastKill))
                {
                    var elapsed = DateTime.UtcNow - lastKill;
                    var remaining = TimeSpan.FromSeconds(STREAK_TIMEOUT_SECONDS) - elapsed;
                    if (remaining.TotalSeconds > 0)
                    {
                        ctx.Reply($"[KillStreak] Time remaining: {(int)remaining.TotalSeconds}s");
                    }
                    else
                    {
                        ctx.Reply("[KillStreak] Your streak has expired.");
                    }
                }
            }
            else
            {
                ctx.Reply("[KillStreak] No active streak. Start killing!");
            }
            
            Plugin.Log.LogInfo($"[KillStreak] Stats requested by {user.CharacterName}: streak={streak}");
        }

        private static void ShowStreakStatus(ChatCommandContext ctx)
        {
            var user = ctx.User;
            var platformId = user.PlatformId;
            
            if (_playerStreaks.TryGetValue(platformId, out var streak))
            {
                ctx.Reply($"[KillStreak] Your current streak: {streak} kills");
                if (streak >= CHEST_THRESHOLD)
                {
                    ctx.Reply($"[KillStreak] 🎁 Chest reward available at {CHEST_THRESHOLD} kills!");
                }
                if (streak >= WAYPOINT_THRESHOLD)
                {
                    ctx.Reply($"[KillStreak] 📍 Waypoint reward available at {WAYPOINT_THRESHOLD} kills!");
                }
            }
            else
            {
                ctx.Reply("[KillStreak] No active streak. Start killing!");
            }
            
            Plugin.Log.LogInfo($"[KillStreak] Status requested by {user.CharacterName}");
        }

        private static void ResetStreak(ChatCommandContext ctx)
        {
            var user = ctx.User;
            var platformId = user.PlatformId;
            
            try
            {
                if (_playerStreaks.TryRemove(platformId, out _))
                {
                    _lastKillTime.TryRemove(platformId, out _);
                    Plugin.Log.LogInfo($"[KillStreak] Streak reset for {user.CharacterName}");
                    ctx.Reply("[Kill Streak] Your streak has been reset.");
                }
                else
                {
                    ctx.Reply("[KillStreak] You had no active streak to reset.");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[KillStreakCommands] Reset failed: {ex.Message}");
                ctx.Reply("[KillStreak] Failed to reset streak. Please try again.");
            }
        }

        private static void ShowConfig(ChatCommandContext ctx)
        {
            ctx.Reply("[Kill Streak Configuration]");
            ctx.Reply($"- Announcement Threshold: {STREAK_THRESHOLD} kills");
            ctx.Reply($"- Timeout: {STREAK_TIMEOUT_SECONDS} seconds");
            ctx.Reply($"- Announcements: {(_announcementsEnabled ? "Enabled" : "Disabled")}");
            ctx.Reply($"- Chest Threshold: {CHEST_THRESHOLD} kills");
            ctx.Reply($"- Waypoint Threshold: {WAYPOINT_THRESHOLD} kills");
            
            Plugin.Log.LogInfo($"[KillStreak] Config requested by {ctx.User.CharacterName}");
        }

        private static void ToggleAnnouncements(ChatCommandContext ctx)
        {
            _announcementsEnabled = !_announcementsEnabled;
            ctx.Reply($"Kill streak announcements are now {(_announcementsEnabled ? "enabled" : "disabled")}");
            Plugin.Log.LogInfo($"[KillStreak] Announcements toggled to {_announcementsEnabled} by {ctx.User.CharacterName}");
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
            
            Plugin.Log.LogInfo($"[KillStreak] Player {platformId} now has {newStreak} streak");
            
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
                Plugin.Log.LogInfo($"[KillStreak] Player {platformId} died, streak reset from {oldStreak}");
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
                <= 3 => "White",
                <= 5 => "Yellow", 
                <= 10 => "Orange",
                <= 15 => "DarkOrange",
                _ => "Red"
            };
            
            Plugin.Log.LogInfo($"[KillStreak] 🎯 {playerName} {message} (Streak: {streak}) [Color: {color}]");
        }

        private static void ctxReply(string message)
        {
            // Fallback for typo - will be caught by compiler if used
        }
    }
}
