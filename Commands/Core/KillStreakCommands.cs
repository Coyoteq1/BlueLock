using VampireCommandFramework;
using ProjectM.Network;
using Unity.Entities;
using Unity.Collections;
using System;
using System.Linq;
using VAuto.Core;
#if INCLUDE_KILLSTREAK_ECS
using VAuto.Core.Components;
#endif

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
            
            if (TryGetEcsStreak(platformId, out var ecsStreak, out var ecsLastKillTime))
            {
                ctx.Reply($"[KillStreak] Your current streak: {ecsStreak} kills");
                TryReplyEcsTimeout(ctx, ecsLastKillTime);
                Plugin.Log.LogInfo($"[KillStreak] Stats requested by {user.CharacterName}: streak={ecsStreak} (ecs)");
                return;
            }

            if (TryGetLegacyStreak(platformId, out var legacyStreak, out var lastKill))
            {
                ctx.Reply($"[KillStreak] Your current streak: {legacyStreak} kills");
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

                Plugin.Log.LogInfo($"[KillStreak] Stats requested by {user.CharacterName}: streak={legacyStreak}");
                return;
            }

            ctx.Reply("[KillStreak] No active streak. Start killing!");
            Plugin.Log.LogInfo($"[KillStreak] Stats requested by {user.CharacterName}: streak=0");
        }

        private static void ShowStreakStatus(ChatCommandContext ctx)
        {
            var user = ctx.User;
            var platformId = user.PlatformId;
            
            if (TryGetEcsStreak(platformId, out var ecsStreak, out _))
            {
                ctx.Reply($"[KillStreak] Your current streak: {ecsStreak} kills");
                var config = GetEcsConfigOrDefault();
                if (ecsStreak >= config.ChestThreshold)
                {
                    ctx.Reply($"[KillStreak] 🎁 Chest reward available at {config.ChestThreshold} kills!");
                }
                if (ecsStreak >= config.WaypointThreshold)
                {
                    ctx.Reply($"[KillStreak] 📍 Waypoint reward available at {config.WaypointThreshold} kills!");
                }
                Plugin.Log.LogInfo($"[KillStreak] Status requested by {user.CharacterName} (ecs)");
                return;
            }

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
            var config = GetEcsConfigOrDefault();
            ctx.Reply("[Kill Streak Configuration]");
            ctx.Reply($"- Announcement Threshold: {config.AnnouncementThreshold} kills");
            ctx.Reply($"- Timeout: {(int)config.TimeoutSeconds} seconds");
            ctx.Reply($"- Announcements: {(config.AnnouncementsEnabled ? "Enabled" : "Disabled")}");
            ctx.Reply($"- Chest Threshold: {config.ChestThreshold} kills");
            ctx.Reply($"- Waypoint Threshold: {config.WaypointThreshold} kills");
            
            Plugin.Log.LogInfo($"[KillStreak] Config requested by {ctx.User.CharacterName} (ecs-aware)");
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
            if (TryGetEcsStreak(platformId, out var ecsStreak, out _))
            {
                return ecsStreak;
            }

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
                _ => $"{streak} kills!"
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

#if INCLUDE_KILLSTREAK_ECS
        private static bool TryGetEcsStreak(ulong platformId, out int streak, out double lastKillTime)
        {
            streak = 0;
            lastKillTime = 0;

            var em = VRCore.EntityManager;
            if (em == default)
                return false;

            if (!TryGetPlayerEntities(platformId, out var userEntity, out var characterEntity))
                return false;

            if (characterEntity != Entity.Null && em.HasComponent<KillStreak>(characterEntity))
        {
                var data = em.GetComponentData<KillStreak>(characterEntity);
                streak = data.Current;
                lastKillTime = data.LastKillTime;
                return true;
            }

            if (userEntity != Entity.Null && em.HasComponent<KillStreak>(userEntity))
        {
                var data = em.GetComponentData<KillStreak>(userEntity);
                streak = data.Current;
                lastKillTime = data.LastKillTime;
                return true;
            }

            return false;
        }

        private static bool TryGetPlayerEntities(ulong platformId, out Entity userEntity, out Entity characterEntity)
        {
            userEntity = Entity.Null;
            characterEntity = Entity.Null;

            var em = VRCore.EntityManager;
            if (em == default)
                return false;

            var query = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerCharacter>());
            var entities = query.ToEntityArray(Allocator.Temp);

            try
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    var entity = entities[i];
                    var pc = em.GetComponentData<PlayerCharacter>(entity);
                    var candidateUserEntity = pc.UserEntity;
                    if (candidateUserEntity == Entity.Null)
                        continue;

                    var user = em.GetComponentData<User>(candidateUserEntity);
                    if (user.PlatformId == platformId)
                    {
                        userEntity = candidateUserEntity;
                        characterEntity = entity;
                        return true;
                    }
                }
            }
            finally
            {
                entities.Dispose();
            }

            return false;
        }

        private static bool TryGetWorldElapsedTime(out double elapsedTime)
        {
            elapsedTime = 0;
            var world = VRCore.ServerWorld;
            if (world == null || !world.IsCreated)
                return false;

            elapsedTime = world.Time.ElapsedTime;
            return true;
        }

        private static void TryReplyEcsTimeout(ChatCommandContext ctx, double lastKillTime)
        {
            if (!TryGetWorldElapsedTime(out var elapsedTime))
                return;

            var config = GetEcsConfigOrDefault();
            var remaining = config.TimeoutSeconds - (elapsedTime - lastKillTime);
            if (remaining > 0)
            {
                ctx.Reply($"[KillStreak] Time remaining: {(int)remaining}s");
            }
            else
            {
                ctx.Reply("[KillStreak] Your streak has expired.");
            }
        }

        private static KillStreakConfig GetEcsConfigOrDefault()
        {
            if (TryGetEcsConfig(out var config))
            {
                return config;
            }

            return new KillStreakConfig
            {
                ChestThreshold = CHEST_THRESHOLD,
                WaypointThreshold = WAYPOINT_THRESHOLD,
                TimeoutSeconds = STREAK_TIMEOUT_SECONDS,
                AnnouncementsEnabled = _announcementsEnabled,

                AnnouncementThreshold = STREAK_THRESHOLD
            };
        }

        private static bool TryGetEcsConfig(out KillStreakConfig config)
        {
            config = default;
            var em = VRCore.EntityManager;
            if (em == default)
                return false;

            var query = em.CreateEntityQuery(ComponentType.ReadOnly<KillStreakConfig>());
            if (query.CalculateEntityCount() == 0)
                return false;

            config = query.GetSingleton<KillStreakConfig>();
            return true;
        }
#else
        private struct KillStreakConfig
        {
            public int ChestThreshold;
            public int WaypointThreshold;
            public double TimeoutSeconds;
            public bool AnnouncementsEnabled;
            public int AnnouncementThreshold;
        }

        private static bool TryGetEcsStreak(ulong platformId, out int streak, out double lastKillTime)
        {
            streak = 0;
            lastKillTime = 0;
            return false;
        }

        private static bool TryGetEcsConfig(out KillStreakConfig config)
        {
            config = default;
            return false;
        }

        private static void TryReplyEcsTimeout(ChatCommandContext ctx, double lastKillTime)
        {            
        }

        private static KillStreakConfig GetEcsConfigOrDefault()
        {
            if (TryGetEcsConfig(out var config))
            {
                return config;
            }

            return new KillStreakConfig
            {
                ChestThreshold = CHEST_THRESHOLD,
                WaypointThreshold = WAYPOINT_THRESHOLD,
                TimeoutSeconds = STREAK_TIMEOUT_SECONDS,
                AnnouncementsEnabled = _announcementsEnabled,

                AnnouncementThreshold = STREAK_THRESHOLD
            };
        }

        private static bool TryGetLegacyStreak(ulong platformId, out int streak, out DateTime lastKill)
        {
            if (_playerStreaks.TryGetValue(platformId, out streak) &&
                _lastKillTime.TryGetValue(platformId, out lastKill))
            {
                return true;
            }

            lastKill = default;
            streak = 0;
            return false;
        }

        private static bool TryGetWorldElapsedTime(out double elapsedTime)
        {
            elapsedTime = 0;
            var world = VRCore.ServerWorld;
            if (world == null || !world.IsCreated)
                return false;

            elapsedTime = world.Time.ElapsedTime;
            return true;
        } 
#endif

        private static void ctxReply(string message)
        {
            // Fallback for typo - will be caught by compiler if used
        }
    }
}
