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
using VAuto.Core;
using VAuto.Core.Services;
using VAutoTraps;
#if INCLUDE_KILLSTREAK_ECS
using VAuto.Core.Components;
#endif

namespace VAuto.Commands.Core
{
    [CommandGroup("trap", "Trap system commands")]
    public static class TrapCommands
    {
        [Command("help", shortHand: "h", description: "Show trap commands", adminOnly: false)]
        public static void Help(ChatCommandContext ctx)
        {
            ctx.Reply("[Trap] Commands:");
            ctx.Reply("  .trap status | config | reload | debug | test");
            ctx.Reply("  .trap set/remove/list/arm/trigger/clear");
            ctx.Reply("  .trap zone create/delete/list/arm/check/clear");
            ctx.Reply("  .trap chest spawn/list/remove/clear");
            ctx.Reply("  .trap streak status/reset/config/toggle/test/stats");
        }

        [Command("status", shortHand: "s", description: "Show trap system status")]
        public static void Status(ChatCommandContext ctx)
        {
            ctx.Reply("[Trap] System Status");
            ctx.Reply("Service: TrapSpawnRules (static)");
            ctx.Reply($"Kill Threshold: {TrapSpawnRules.Config.KillThreshold}");
            ctx.Reply($"Chests Per Spawn: {TrapSpawnRules.Config.ChestsPerSpawn}");
            ctx.Reply($"Container Traps: {ContainerTrapService.GetTrapCount()}");
            ctx.Reply($"Trap Zones: {TrapZoneService.GetZoneCount()}");
            ctx.Reply($"Spawned Chests: {ChestSpawnService.GetChestCount()}");
        }

        [Command("config", shortHand: "c", description: "Show trap configuration")]
        public static void Config(ChatCommandContext ctx)
        {
            ctx.Reply("[Trap] Configuration");
            ctx.Reply($"Kill Threshold: {TrapSpawnRules.Config.KillThreshold}");
            ctx.Reply($"Chests Per Spawn: {TrapSpawnRules.Config.ChestsPerSpawn}");
            ctx.Reply($"Container Glow Radius: {TrapSpawnRules.Config.ContainerGlowRadius}");
            ctx.Reply($"Waypoint Trap Threshold: {TrapSpawnRules.Config.WaypointTrapThreshold}");
            ctx.Reply($"Waypoint Trap Glow Radius: {TrapSpawnRules.Config.WaypointTrapGlowRadius}");
            ctx.Reply($"Notification Enabled: {TrapSpawnRules.Config.NotificationEnabled}");
            ctx.Reply($"Trap Damage Amount: {TrapSpawnRules.Config.TrapDamageAmount}");
            ctx.Reply($"Trap Duration: {TrapSpawnRules.Config.TrapDuration}");
        }

        [Command("reload", shortHand: "r", description: "Reload trap configuration")]
        public static void Reload(ChatCommandContext ctx)
        {
            // Note: TrapSpawnRules.Initialize(log) should be called from Plugin.Load()
            // This command just confirms the config is active
            ctx.Reply($"[Trap] Configuration active - Kill Threshold: {TrapSpawnRules.Config.KillThreshold}");
        }

        [Command("debug", shortHand: "d", description: "Diagnostics and counts")]
        public static void Debug(ChatCommandContext ctx)
        {
            ctx.Reply("[Trap] Diagnostics");
            ctx.Reply($"Container traps: {ContainerTrapService.GetTrapCount()}");
            ctx.Reply($"Trap zones: {TrapZoneService.GetZoneCount()}");
            ctx.Reply($"Spawned chests: {ChestSpawnService.GetChestCount()}");
            ctx.Reply($"Tracked streaks: {TrapSpawnRules.GetAllStreaks().Count}");
        }

        [Command("test", shortHand: "t", description: "Basic tests for trap systems")]
        public static void Test(ChatCommandContext ctx)
        {
            ctx.Reply("[Trap] Test");
            ctx.Reply("  Config load OK");
            ctx.Reply("  Services reachable");
            ctx.Reply("  Use: .trap trigger, .trap streak test, .trap chest spawn");
        }

        [Command("set", shortHand: "ts", description: "Set a container trap at your location", adminOnly: true)]
        public static void TrapSet(ChatCommandContext ctx)
        {
            if (!TryGetPlayerPosition(ctx, out var pos))
            {
                ctx.Reply("[Trap] Error: Could not determine your position");
                return;
            }

            var ownerId = GetPlayerPlatformId(ctx);
            ContainerTrapService.SetTrap(pos, ownerId, "container");

            ctx.Reply("[Trap] Trap set at your location.");
            ctx.Reply($"  Position: ({pos.x:F0}, {pos.y:F0}, {pos.z:F0})");
            ctx.Reply($"  Damage: {TrapSpawnRules.Config.TrapDamageAmount}");
            ctx.Reply($"  Duration: {TrapSpawnRules.Config.TrapDuration}s");
        }

        [Command("remove", shortHand: "tr", description: "Remove trap at your location", adminOnly: true)]
        public static void TrapRemove(ChatCommandContext ctx)
        {
            if (!TryGetPlayerPosition(ctx, out var pos))
            {
                ctx.Reply("[Trap] Error: Could not determine your position");
                return;
            }

            var nearest = ContainerTrapService.FindNearestTrap(pos, 10f);
            if (nearest == null)
            {
                ctx.Reply("[Trap] No traps found nearby (within 10m)");
                return;
            }

            var trapPos = nearest.Value.Position;
            if (ContainerTrapService.RemoveTrap(trapPos))
            {
                ctx.Reply("[Trap] Trap removed.");
            }
            else
            {
                ctx.Reply("[Trap] Failed to remove trap");
            }
        }

        [Command("list", shortHand: "tl", description: "List all trapped containers", adminOnly: true)]
        public static void TrapList(ChatCommandContext ctx)
        {
            var traps = ContainerTrapService.GetAllTraps();
            ctx.Reply($"[Trap] Trapped Locations ({traps.Count})");

            if (traps.Count == 0)
            {
                ctx.Reply("  No traps set");
                return;
            }

            int i = 0;
            foreach (var kvp in traps)
            {
                i++;
                var pos = kvp.Key;
                var trap = kvp.Value;
                var status = trap.IsArmed ? "ARMED" : "DISARMED";
                var triggered = trap.Triggered ? " (TRIGGERED)" : "";
                ctx.Reply($"  {i}. {status}{triggered}");
                ctx.Reply($"     at ({pos.x:F0}, {pos.y:F0}, {pos.z:F0})");
                ctx.Reply($"     Owner: {trap.OwnerPlatformId}");
            }
        }

        [Command("arm", shortHand: "ta", description: "Arm/disarm trap at your location", adminOnly: true)]
        public static void TrapArm(ChatCommandContext ctx, string action = "toggle")
        {
            if (!TryGetPlayerPosition(ctx, out var pos))
            {
                ctx.Reply("[Trap] Error: Could not determine your position");
                return;
            }

            var nearest = ContainerTrapService.FindNearestTrap(pos, 10f);
            if (nearest == null)
            {
                ctx.Reply("[Trap] No traps found nearby");
                return;
            }

            var trapPos = nearest.Value.Position;
            var trap = nearest.Value.Trap;

            bool newArmed = action switch
            {
                "on" or "arm" => true,
                "off" or "disarm" => false,
                _ => !trap.IsArmed
            };

            if (ContainerTrapService.SetArmed(trapPos, newArmed))
            {
                var status = newArmed ? "ARMED" : "DISARMED";
                ctx.Reply($"[Trap] Trap at {trapPos} is now {status}");
            }
        }

        [Command("trigger", shortHand: "tt", description: "Test trigger a trap at your location", adminOnly: true)]
        public static void TrapTrigger(ChatCommandContext ctx)
        {
            if (!TryGetPlayerPosition(ctx, out var pos))
            {
                ctx.Reply("[Trap] Error: Could not determine your position");
                return;
            }
            var intruderId = GetPlayerPlatformId(ctx);

            var nearest = ContainerTrapService.FindNearestTrap(pos, 10f);
            if (nearest == null)
            {
                ctx.Reply("[Trap] No traps found nearby");
                return;
            }

            var trapPos = nearest.Value.Position;
            var trap = nearest.Value.Trap;

            if (ContainerTrapService.TriggerTrap(trapPos, intruderId))
            {
                ctx.Reply("[Trap] Trap triggered.");
                ctx.Reply($"  Location: ({trapPos.x:F0}, {trapPos.y:F0}, {trapPos.z:F0})");
                ctx.Reply($"  Damage: {trap.DamageAmount}");
                ctx.Reply($"  Duration: {trap.Duration}s");
            }
        }

        [Command("clear", shortHand: "tc", description: "Clear all traps", adminOnly: true)]
        public static void TrapClear(ChatCommandContext ctx)
        {
            var count = ContainerTrapService.GetTrapCount();
            ContainerTrapService.ClearAll();
            ctx.Reply($"[Trap] Cleared {count} traps");
        }

        [Command("zone create", shortHand: "tcz", description: "Create a trap zone at your location", adminOnly: true)]
        public static void ZoneCreate(ChatCommandContext ctx, string type = "container")
        {
            if (!TryGetPlayerPosition(ctx, out var position))
            {
                ctx.Reply("[Trap] Error: Could not get your position.");
                return;
            }
            var ownerId = GetPlayerPlatformId(ctx);

            type = type.ToLower();
            if (type != "container" && type != "waypoint" && type != "border")
            {
                ctx.Reply($"[Trap] Invalid type '{type}'. Use: container, waypoint, or border");
                return;
            }

            TrapZoneService.CreateZone(position, ownerId, 2f, type);

            ctx.Reply($"[Trap] Created {type} trap zone.");
            ctx.Reply($"  Position: ({position.x:F0}, {position.y:F0}, {position.z:F0})");
            ctx.Reply("  Radius: 2m");
        }

        [Command("zone delete", shortHand: "tdz", description: "Delete nearest trap zone", adminOnly: true)]
        public static void ZoneDelete(ChatCommandContext ctx)
        {
            if (!TryGetPlayerPosition(ctx, out var position))
            {
                ctx.Reply("[Trap] Error: Could not get your position.");
                return;
            }

            if (TrapZoneService.RemoveNearestZone(position, 5f))
            {
                ctx.Reply("[Trap] Nearest trap zone removed");
            }
            else
            {
                ctx.Reply("[Trap] No trap zones found nearby (within 5m)");
            }
        }

        [Command("zone list", shortHand: "tlz", description: "List all trap zones", adminOnly: true)]
        public static void ZoneList(ChatCommandContext ctx)
        {
            var zones = TrapZoneService.GetAllZones();

            ctx.Reply($"[Trap] Trap Zones ({zones.Count})");

            if (zones.Count == 0)
            {
                ctx.Reply("  No trap zones created");
                return;
            }

            int i = 0;
            foreach (var kvp in zones)
            {
                i++;
                var pos = kvp.Key;
                var zone = kvp.Value;
                var status = zone.IsArmed ? "ARMED" : "DISARMED";
                var triggered = zone.Triggered ? " (TRIGGERED)" : "";
                ctx.Reply($"  {i}. {status}{triggered} [{zone.TrapType}]");
                ctx.Reply($"     at ({pos.x:F0}, {pos.y:F0}, {pos.z:F0})");
                ctx.Reply($"     Radius: {zone.Radius}m | Owner: {zone.OwnerPlatformId}");
            }
        }

        [Command("zone arm", shortHand: "taz", description: "Arm/disarm nearest trap zone", adminOnly: true)]
        public static void ZoneArm(ChatCommandContext ctx, string action = "toggle")
        {
            if (!TryGetPlayerPosition(ctx, out var position))
            {
                ctx.Reply("[Trap] Error: Could not get your position.");
                return;
            }
            var zones = TrapZoneService.GetAllZones();

            float3? nearestPos = null;
            foreach (var kvp in zones)
            {
                if (math.distance(position, kvp.Key) <= 5f)
                {
                    nearestPos = kvp.Key;
                    break;
                }
            }

            if (nearestPos == null)
            {
                ctx.Reply("[Trap] No zones found nearby (within 5m)");
                return;
            }

            bool newArmed = action switch
            {
                "on" or "arm" => true,
                "off" or "disarm" => false,
                _ => !zones[nearestPos.Value].IsArmed
            };

            if (TrapZoneService.SetArmed(nearestPos.Value, newArmed))
            {
                var status = newArmed ? "ARMED" : "DISARMED";
                ctx.Reply($"[Trap] Zone is now {status}");
            }
        }

        [Command("zone check", shortHand: "tczk", description: "Check if you're in a trap zone", adminOnly: false)]
        public static void ZoneCheck(ChatCommandContext ctx)
        {
            if (!TryGetPlayerPosition(ctx, out var position))
            {
                ctx.Reply("[Trap] Error: Could not get your position.");
                return;
            }
            var isInZone = TrapZoneService.IsInZone(position);
            ctx.Reply(isInZone ? "[Trap] You are inside a trap zone." : "[Trap] You are not in any trap zone");
        }

        [Command("zone clear", shortHand: "tcc", description: "Clear all trap zones", adminOnly: true)]
        public static void ZoneClear(ChatCommandContext ctx)
        {
            var count = TrapZoneService.GetZoneCount();
            TrapZoneService.ClearAll();
            ctx.Reply($"[Trap] Cleared {count} trap zones");
        }

        [Command("chest spawn", shortHand: "sc", description: "Spawn a reward chest at your location", adminOnly: true)]
        public static void ChestSpawn(ChatCommandContext ctx, string type = "normal")
        {
            if (!TryGetPlayerPosition(ctx, out var playerPos))
            {
                ctx.Reply("[Chest] Error: Could not get player position.");
                return;
            }
            var playerId = GetPlayerPlatformId(ctx);

            var chestType = type.ToLower() switch
            {
                "rare" or "r" => ChestRewardType.Rare,
                "epic" or "e" => ChestRewardType.Epic,
                "legendary" or "l" => ChestRewardType.Legendary,
                _ => ChestRewardType.Normal
            };

            ChestSpawnService.SpawnChest(playerPos, playerId, chestType);

            ctx.Reply($"[Chest] Spawned {chestType} chest.");
            ctx.Reply($"  Position: ({playerPos.x:F0}, {playerPos.y:F0}, {playerPos.z:F0})");
        }

        [Command("chest list", shortHand: "cl", description: "List spawned chests", adminOnly: true)]
        public static void ChestList(ChatCommandContext ctx)
        {
            var chests = ChestSpawnService.GetAllChests();
            ctx.Reply($"[Chest] Active Chests ({chests.Count})");

            if (chests.Count == 0)
            {
                ctx.Reply("  No active chests");
                return;
            }

            foreach (var chest in chests)
            {
                ctx.Reply($"  {chest.Value.ChestType} at ({chest.Value.Position.x:F0}, {chest.Value.Position.y:F0}, {chest.Value.Position.z:F0})");
            }
        }

        [Command("chest remove", shortHand: "cr", description: "Remove chest at your location", adminOnly: true)]
        public static void ChestRemove(ChatCommandContext ctx)
        {
            if (!TryGetPlayerPosition(ctx, out var playerPos))
            {
                ctx.Reply("[Chest] Error: Could not get player position.");
                return;
            }

            if (ChestSpawnService.RemoveNearestChest(playerPos, 5f))
            {
                ctx.Reply("[Chest] Nearest chest removed");
            }
            else
            {
                ctx.Reply("[Chest] No chests found nearby (within 5m)");
            }
        }

        [Command("chest clear", shortHand: "cc", description: "Remove all spawned chests", adminOnly: true)]
        public static void ChestClear(ChatCommandContext ctx)
        {
            var count = ChestSpawnService.GetChestCount();
            ChestSpawnService.ClearAll();
            ctx.Reply($"[Chest] Cleared {count} chests");
        }

        [Command("streak status", shortHand: "ss", description: "Show your current kill streak", adminOnly: false)]
        public static void StreakStatus(ChatCommandContext ctx)
        {
            var user = ctx.User;
            var platformId = user.PlatformId;

            if (TryGetEcsStreak(platformId, out var ecsStreak, out var ecsLastKillTime))
            {
                ctx.Reply($"[KillStreak] Your current streak: {ecsStreak} kills");
                TryReplyEcsTimeout(ctx, ecsLastKillTime);
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

                return;
            }

            ctx.Reply("[KillStreak] No active streak. Start killing.");
        }

        [Command("streak stats", shortHand: "sst", description: "Show your current kill streak", adminOnly: false)]
        public static void StreakStats(ChatCommandContext ctx)
        {
            StreakStatus(ctx);
        }

        [Command("streak reset", shortHand: "sr", description: "Reset your kill streak", adminOnly: false)]
        public static void StreakReset(ChatCommandContext ctx)
        {
            var user = ctx.User;
            var platformId = user.PlatformId;

            if (_playerStreaks.TryRemove(platformId, out _))
            {
                _lastKillTime.TryRemove(platformId, out _);
                ctx.Reply("[KillStreak] Your streak has been reset.");
            }
            else
            {
                ctx.Reply("[KillStreak] You had no active streak to reset.");
            }
        }

        [Command("streak config", shortHand: "scf", description: "Show kill streak config", adminOnly: false)]
        public static void StreakConfig(ChatCommandContext ctx)
        {
            var config = GetEcsConfigOrDefault();
            ctx.Reply("[Kill Streak Configuration]");
            ctx.Reply($"- Announcement Threshold: {config.AnnouncementThreshold} kills");
            ctx.Reply($"- Timeout: {(int)config.TimeoutSeconds} seconds");
            ctx.Reply($"- Announcements: {(config.AnnouncementsEnabled ? "Enabled" : "Disabled")}");
            ctx.Reply($"- Chest Threshold: {config.ChestThreshold} kills");
            ctx.Reply($"- Waypoint Threshold: {config.WaypointThreshold} kills");
        }

        [Command("streak toggle", shortHand: "stg", description: "Toggle kill streak announcements", adminOnly: true)]
        public static void StreakToggle(ChatCommandContext ctx)
        {
            _announcementsEnabled = !_announcementsEnabled;
            ctx.Reply($"Kill streak announcements are now {(_announcementsEnabled ? "enabled" : "disabled")}");
        }

        [Command("streak test", shortHand: "stt", description: "Test streak notification", adminOnly: true)]
        public static void StreakTest(ChatCommandContext ctx)
        {
            var user = ctx.User;
            var playerName = user.CharacterName.ToString();
            ctx.Reply("[KillStreak] Test notification - check server logs.");

            LogStreakMessage(playerName, 3, "Test streak: 3 kills - On Fire!");
            LogStreakMessage(playerName, 5, "Test streak: 5 kills - Dominating!");
            LogStreakMessage(playerName, 10, "Test streak: 10 kills - Unstoppable!");
        }

        private static bool TryGetPlayerPosition(ChatCommandContext ctx, out float3 position)
        {
            position = float3.zero;
            try
            {
                var serverWorld = VRCore.ServerWorld;
                if (serverWorld == null)
                {
                    Plugin.Log.LogWarning("[Trap] Server world not available");
                    return false;
                }

                var entityManager = serverWorld.EntityManager;
                var characterEntity = ctx.Event?.SenderCharacterEntity ?? Entity.Null;
                if (characterEntity == Entity.Null || !entityManager.Exists(characterEntity))
                {
                    Plugin.Log.LogWarning("[Trap] Sender character entity not found");
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
                Plugin.Log.LogError($"[Trap] GetPlayerPosition failed: {ex.Message}");
                return false;
            }
        }

        private static ulong GetPlayerPlatformId(ChatCommandContext ctx)
        {
            try
            {
                return ctx.User.PlatformId;
            }
            catch
            {
                return 0;
            }
        }

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<ulong, int> _playerStreaks = new();
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<ulong, DateTime> _lastKillTime = new();
        private static bool _announcementsEnabled = true;
        private const int STREAK_THRESHOLD = 3;
        private const int CHEST_THRESHOLD = 5;
        private const int WAYPOINT_THRESHOLD = 10;
        private const int STREAK_TIMEOUT_SECONDS = 120;

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
            var color = streak switch
            {
                <= 3 => "White",
                <= 5 => "Yellow",
                <= 10 => "Orange",
                <= 15 => "DarkOrange",
                _ => "Red"
            };

            Plugin.Log.LogInfo($"[KillStreak] {playerName} {message} (Streak: {streak}) [Color: {color}]");
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
    }
}
