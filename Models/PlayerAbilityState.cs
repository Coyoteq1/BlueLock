using System;
using System.Collections.Generic;
using Unity.Entities;
using Stunlock.Core;

namespace VAuto.Zone.Models
{
    /// <summary>
    /// Tracks a player's ability state for zone lifecycle management.
    /// Used to save and restore abilities when entering/exiting zones.
    /// </summary>
    public struct PlayerAbilityState
    {
        /// <summary>
        /// The player's Steam ID (or unique identifier).
        /// </summary>
        public ulong SteamId { get; set; }

        /// <summary>
        /// The ID of the zone the player is currently in.
        /// </summary>
        public string CurrentZoneId { get; set; }

        /// <summary>
        /// Saved ability slots before zone enter.
        /// Used to restore player's original abilities on zone exit.
        /// </summary>
        public PrefabGUID[] SavedSlots { get; set; }

        /// <summary>
        /// Saved ability cooldowns before zone enter.
        /// Key: Ability PrefabGUID, Value: Remaining cooldown time.
        /// </summary>
        public Dictionary<PrefabGUID, float> SavedCooldowns { get; set; }

        /// <summary>
        /// Timestamp when the player entered the current zone.
        /// </summary>
        public DateTime ZoneEnterTime { get; set; }

        /// <summary>
        /// Whether the player's state is currently active (in a zone).
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Creates a new player ability state.
        /// </summary>
        public static PlayerAbilityState Create(ulong steamId, string zoneId)
        {
            return new PlayerAbilityState
            {
                SteamId = steamId,
                CurrentZoneId = zoneId,
                SavedSlots = Array.Empty<PrefabGUID>(),
                SavedCooldowns = new Dictionary<PrefabGUID, float>(),
                ZoneEnterTime = DateTime.UtcNow,
                IsActive = true
            };
        }

        /// <summary>
        /// Clears the saved state.
        /// </summary>
        public void Clear()
        {
            SavedSlots = Array.Empty<PrefabGUID>();
            SavedCooldowns?.Clear();
            CurrentZoneId = string.Empty;
            IsActive = false;
        }
    }
}
