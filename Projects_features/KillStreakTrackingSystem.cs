using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;
using VAuto.Core;
using VAuto.Core.Components;
using VAuto.Core.Components.Lifecycle;

namespace VAuto.Core.Systems
{
    /// <summary>
    /// Tracks player kill streaks and triggers announcements.
    /// 3 consecutive kills triggers first announcement.
    /// Color intensity increases with streak level.
    /// </summary>
    public partial class KillStreakTrackingSystem : SystemBase
    {
        private EntityQuery _deathQuery;
        private EntityQuery _killEventQuery;
        private EntityQuery _configQuery;

        public override void OnCreate()
        {
            _deathQuery = GetEntityQuery(
                ComponentType.ReadOnly<DeathEvent>(),
                ComponentType.ReadOnly<PlayerCharacter>()
            );

            _killEventQuery = GetEntityQuery(
                ComponentType.ReadOnly<DamageEvent>(),
                ComponentType.ReadOnly<PlayerCharacter>()
            );

            _configQuery = GetEntityQuery(
                ComponentType.ReadOnly<KillStreakConfig>()
            );

            // Create default config if it doesn't exist
            if (_configQuery.CalculateEntityCount() == 0)
            {
                var configEntity = CreateEntity();
                AddComponent(configEntity, new KillStreakConfig
                {
                    ChestThreshold = 5,
                    WaypointThreshold = 10,
                    TimeoutSeconds = 120.0,
                    AnnouncementsEnabled = true,
                    AnnouncementThreshold = 3
                });
            }
        }

        public override void OnUpdate()
        {
            double currentTime = SystemAPI.Time.ElapsedTime;
            var ecb = World.GetExistingSystemManaged<EndSimulationEntityCommandBufferSystem>().CreateCommandBuffer();

            // Get config
            KillStreakConfig config = default;
            if (_configQuery.TryGetSingleton<KillStreakConfig>(out var singletonConfig))
            {
                config = singletonConfig;
            }

            // Process kill events
            var damageEvents = _killEventQuery.ToComponentDataArray<DamageEvent>(Allocator.Temp);
            var killers = _killEventQuery.ToComponentDataArray<PlayerCharacter>(Allocator.Temp);
            var killEntities = _killEventQuery.ToEntityArray(Allocator.Temp);

            try
            {
                for (int i = 0; i < damageEvents.Length; i++)
                {
                    var damageEvent = damageEvents[i];
                    var killer = killers[i];
                    var killEntity = killEntities[i];

                    // Check if this was a killing blow (target died)
                    if (damageEvent.Damage > 0 && damageEvent.TargetHealth <= 0)
                    {
                        ProcessKill(killer.UserEntity, damageEvent.Target, currentTime, config, ecb);
                    }
                }
            }
            finally
            {
                damageEvents.Dispose();
                killers.Dispose();
                killEntities.Dispose();
            }

            // Process death events - reset streaks
            var deathEvents = _deathQuery.ToComponentDataArray<DeathEvent>(Allocator.Temp);
            var deathEntities = _deathQuery.ToEntityArray(Allocator.Temp);

            try
            {
                for (int i = 0; i < deathEvents.Length; i++)
                {
                    var deathEvent = deathEvents[i];
                    var deathEntity = deathEntities[i];

                    ProcessDeath(deathEvent.Killer, deathEntity, currentTime, ecb);
                }
            }
            finally
            {
                deathEvents.Dispose();
                deathEntities.Dispose();
            }

            // Check for streak timeouts
            CheckStreakTimeouts(currentTime, config.TimeoutSeconds, ecb);
        }

        private void ProcessKill(Entity killer, Entity victim, double currentTime, KillStreakConfig config, EntityCommandBuffer ecb)
        {
            if (killer == Entity.Null)
                return;

            // Get or create kill streak component
            if (!SystemAPI.HasComponent<KillStreak>(killer))
            {
                ecb.AddComponent(killer, new KillStreak
                {
                    Current = 0,
                    LastKillTime = currentTime,
                    MaxStreak = 0
                });
            }

            var streak = SystemAPI.GetComponent<KillStreak>(killer);
            streak.Current++;
            streak.LastKillTime = currentTime;
            streak.LastVictim = victim;

            if (streak.Current > streak.MaxStreak)
            {
                streak.MaxStreak = streak.Current;
            }

            ecb.SetComponent(killer, streak);

            // Add to kill feed
            if (SystemAPI.HasBuffer<KillFeedEntry>(killer))
            {
                var buffer = SystemAPI.GetBuffer<KillFeedEntry>(killer);
                buffer.Add(new KillFeedEntry
                {
                    Killer = killer,
                    Victim = victim,
                    Timestamp = currentTime,
                    KillerStreakBefore = streak.Current - 1,
                    KillMethod = new FixedString32Bytes("Combat")
                });
            }

            // Check for announcement threshold
            if (config.AnnouncementsEnabled && streak.Current >= config.AnnouncementThreshold)
            {
                int level = CalculateStreakLevel(streak.Current);
                float4 color = GetStreakColor(level);

                var announcement = new KillStreakAnnouncement
                {
                    Killer = killer,
                    Victim = victim,
                    StreakCount = streak.Current,
                    Timestamp = currentTime
                };

                var announcementEntity = ecb.CreateEntity();
                ecb.AddComponent(announcementEntity, announcement);
                ecb.AddComponent(announcementEntity, new ChatNotification
                {
                    Message = new FixedString128Bytes($"Kill Streak: {streak.Current}!"),
                    Color = color,
                    IsImportant = level > 2
                });
            }

            Plugin.Log.LogInfo($"[KillStreak] Player streak: {streak.Current} kills");
        }

        private void ProcessDeath(Entity killer, Entity victim, double currentTime, EntityCommandBuffer ecb)
        {
            if (victim == Entity.Null)
                return;

            // Reset victim's streak
            if (SystemAPI.HasComponent<KillStreak>(victim))
            {
                var streak = SystemAPI.GetComponent<KillStreak>(victim);
                streak.Current = 0;
                streak.LastDeathTime = currentTime;
                ecb.SetComponent(victim, streak);
            }

            // Update killer's streak if there was a killer
            if (killer != Entity.Null && SystemAPI.HasComponent<KillStreak>(killer))
            {
                var streak = SystemAPI.GetComponent<KillStreak>(killer);
                streak.Current++;
                streak.LastKillTime = currentTime;
                ecb.SetComponent(killer, streak);
            }
        }

        private void CheckStreakTimeouts(double currentTime, double timeoutSeconds, EntityCommandBuffer ecb)
        {
            foreach (var (streak, entity) in SystemAPI.Query<RefRW<KillStreak>>().WithEntityAccess())
            {
                if (streak.ValueRO.Current > 0)
                {
                    double timeSinceLastKill = currentTime - streak.ValueRO.LastKillTime;
                    if (timeSinceLastKill > timeoutSeconds)
                    {
                        streak.ValueRW.Current = 0;
                        Plugin.Log.LogInfo("[KillStreak] Streak timed out");
                    }
                }
            }
        }

        private int CalculateStreakLevel(int streak)
        {
            if (streak >= 20) return 5;      // Red - Legendary
            if (streak >= 15) return 4;      // Dark Orange - Godlike
            if (streak >= 10) return 3;      // Orange - Dominating
            if (streak >= 5) return 2;       // Yellow - On Fire
            return 1;                         // White - Normal
        }

        private float4 GetStreakColor(int level)
        {
            switch (level)
            {
                case 1: return new float4(1f, 1f, 1f, 1f);      // White
                case 2: return new float4(1f, 1f, 0f, 1f);      // Yellow
                case 3: return new float4(1f, 0.5f, 0f, 1f);    // Orange
                case 4: return new float4(1f, 0.25f, 0f, 1f);   // Dark Orange
                case 5: return new float4(1f, 0f, 0f, 1f);      // Red
                default: return new float4(1f, 1f, 1f, 1f);
            }
        }

        public override void OnDestroy()
        {
            // Cleanup if needed
        }
    }
}
