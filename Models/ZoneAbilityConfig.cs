using System.Collections.Generic;
using Unity.Entities;
using Stunlock.Core;

namespace VAuto.Zone.Models
{
    /// <summary>
    /// Configuration for ability restrictions and behavior in a specific zone.
    /// Loaded from JSON configuration files.
    /// </summary>
    public struct ZoneAbilityConfig
    {
        /// <summary>
        /// The unique identifier for this zone.
        /// </summary>
        public string ZoneId { get; set; }

        /// <summary>
        /// Whitelist of abilities that are allowed in this zone.
        /// If empty, all abilities are allowed (unless restricted).
        /// </summary>
        public PrefabGUID[] AllowedAbilities { get; set; }

        /// <summary>
        /// Blacklist of abilities that are restricted in this zone.
        /// These abilities cannot be used while in the zone.
        /// </summary>
        public PrefabGUID[] RestrictedAbilities { get; set; }

        /// <summary>
        /// Whether to reset all ability cooldowns when entering the zone.
        /// </summary>
        public bool ResetCooldownsOnEnter { get; set; }

        /// <summary>
        /// Whether to reset all ability cooldowns when exiting the zone.
        /// </summary>
        public bool ResetCooldownsOnExit { get; set; }

        /// <summary>
        /// Whether to save the player's current ability slots on enter
        /// and restore them on exit.
        /// </summary>
        public bool SaveAndRestoreSlots { get; set; }

        /// <summary>
        /// Optional preset ability slots to apply when entering the zone.
        /// If set, these override the player's current slots.
        /// </summary>
        public PrefabGUID[] PresetSlots { get; set; }

        /// <summary>
        /// Whether to save the player's current cooldowns on enter
        /// and restore them on exit.
        /// </summary>
        public bool SaveAndRestoreCooldowns { get; set; }

        /// <summary>
        /// Creates a default zone configuration with no restrictions.
        /// </summary>
        public static ZoneAbilityConfig Default(string zoneId)
        {
            return new ZoneAbilityConfig
            {
                ZoneId = zoneId,
                AllowedAbilities = System.Array.Empty<PrefabGUID>(),
                RestrictedAbilities = System.Array.Empty<PrefabGUID>(),
                ResetCooldownsOnEnter = false,
                ResetCooldownsOnExit = false,
                SaveAndRestoreSlots = false,
                SaveAndRestoreCooldowns = false,
                PresetSlots = System.Array.Empty<PrefabGUID>()
            };
        }

        /// <summary>
        /// Checks if an ability is allowed in this zone.
        /// </summary>
        public bool IsAbilityAllowed(PrefabGUID ability)
        {
            // If there's a whitelist, check if ability is in it
            if (AllowedAbilities != null && AllowedAbilities.Length > 0)
            {
                foreach (var allowed in AllowedAbilities)
                {
                    if (allowed.GuidHash == ability.GuidHash)
                    {
                        return true;
                    }
                }
                return false;
            }

            // If there's a blacklist, check if ability is restricted
            if (RestrictedAbilities != null && RestrictedAbilities.Length > 0)
            {
                foreach (var restricted in RestrictedAbilities)
                {
                    if (restricted.GuidHash == ability.GuidHash)
                    {
                        return false;
                    }
                }
            }

            // Default: allow all abilities
            return true;
        }
    }
}
