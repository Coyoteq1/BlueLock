using System;
using System.Collections.Generic;
using Unity.Entities;

namespace VAuto.Zone.Services
{
    /// <summary>
    /// Service managing arena-caused death registry for loot suppression.
    /// Uses TTL-based tracking to determine if a death should suppress loot drops.
    /// </summary>
    public static class ArenaDeathTracker
    {
        private struct DeathEntry
        {
            public DateTime DeathTime;
            public string ZoneId;
        }

        private static readonly Dictionary<int, DeathEntry> ArenaDeaths = new Dictionary<int, DeathEntry>();
        private const double DeathTTLSeconds = 5.0;  // How long to remember a death for loot suppression

        /// <summary>
        /// Register an arena-caused death for loot suppression.
        /// Marks this entity's death as arena-caused, suppressing normal loot drops for TTL window.
        /// </summary>
        /// <param name="victimEntity">The player entity that died in the arena</param>
        /// <param name="zoneId">Zone ID where the death occurred</param>
        public static void RegisterArenaDeath(Entity victimEntity, string zoneId)
        {
            try
            {
                ArenaDeaths[victimEntity.Index] = new DeathEntry
                {
                    DeathTime = DateTime.UtcNow,
                    ZoneId = zoneId
                };
            }
            catch
            {
                // Silently fail on registration
            }
        }

        /// <summary>
        /// Check if a death was arena-caused and retrieve the zone where it occurred.
        /// Automatically removes expired entries (older than TTL).
        /// </summary>
        /// <param name="entity">The entity that died</param>
        /// <param name="zoneId">Output: zone ID where death occurred (if arena death)</param>
        /// <returns>True if this was an arena-caused death within TTL window</returns>
        public static bool IsArenaDeath(Entity entity, out string zoneId)
        {
            zoneId = string.Empty;

            try
            {
                if (!ArenaDeaths.TryGetValue(entity.Index, out var entry))
                    return false;

                var now = DateTime.UtcNow;
                var age = now - entry.DeathTime;

                // Check if still within TTL window
                if (age.TotalSeconds > DeathTTLSeconds)
                {
                    ArenaDeaths.Remove(entity.Index);
                    return false;
                }

                zoneId = entry.ZoneId;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if a recent arena death is still active (haven't expired yet).
        /// Used by loot drop suppression system.
        /// </summary>
        /// <param name="entity">The entity that died</param>
        /// <param name="currentTimeSeconds">Current time in Unix seconds for comparison</param>
        /// <param name="zoneId">Output: zone ID where death occurred</param>
        /// <returns>True if this was a recent arena death</returns>
        public static bool TryGetRecentArenaDeath(Entity entity, double currentTimeSeconds, out string zoneId)
        {
            return IsArenaDeath(entity, out zoneId);
        }

        /// <summary>
        /// Clean up expired arena death records.
        /// Called periodically to prevent dictionary from growing unbounded.
        /// </summary>
        /// <returns>Number of entries cleaned up</returns>
        public static int CleanupExpired()
        {
            try
            {
                var now = DateTime.UtcNow;
                var toRemove = new List<int>();

                foreach (var kvp in ArenaDeaths)
                {
                    var age = now - kvp.Value.DeathTime;
                    if (age.TotalSeconds > DeathTTLSeconds)
                    {
                        toRemove.Add(kvp.Key);
                    }
                }

                foreach (var key in toRemove)
                {
                    ArenaDeaths.Remove(key);
                }

                return toRemove.Count;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Clear all tracked arena deaths (for testing or reset).
        /// </summary>
        public static void Clear()
        {
            try
            {
                ArenaDeaths.Clear();
            }
            catch
            {
                // Silently fail
            }
        }
    }
}
