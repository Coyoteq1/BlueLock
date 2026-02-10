using System;
using Unity.Entities;
using Unity.Mathematics;

namespace VAuto.Zone.Models
{
    /// <summary>
    /// Represents the zone state for a player in the arena system.
    /// </summary>
    public class PlayerZoneState
    {
        /// <summary>
        /// The entity of the player character.
        /// </summary>
        public Entity CharacterEntity { get; set; }

        /// <summary>
        /// The entity of the user (player connection).
        /// </summary>
        public Entity UserEntity { get; set; }

        /// <summary>
        /// The current zone the player is in.
        /// </summary>
        public string CurrentZone { get; set; } = "none";

        /// <summary>
        /// Whether the player is currently inside an arena zone.
        /// </summary>
        public bool IsInArena { get; set; }

        /// <summary>
        /// The player's position when they entered the arena.
        /// </summary>
        public float3 EntryPosition { get; set; }

        /// <summary>
        /// The timestamp when the player entered the arena.
        /// </summary>
        public DateTime EntryTime { get; set; }

        /// <summary>
        /// Creates a new PlayerZoneState with default values.
        /// </summary>
        public PlayerZoneState()
        {
        }

        /// <summary>
        /// Creates a new PlayerZoneState for a player entering a zone.
        /// </summary>
        public static PlayerZoneState CreateEnterState(Entity character, Entity user, string zoneId, float3 position)
        {
            return new PlayerZoneState
            {
                CharacterEntity = character,
                UserEntity = user,
                CurrentZone = zoneId,
                IsInArena = true,
                EntryPosition = position,
                EntryTime = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Creates a new PlayerZoneState for a player exiting a zone.
        /// </summary>
        public static PlayerZoneState CreateExitState()
        {
            return new PlayerZoneState
            {
                CurrentZone = "none",
                IsInArena = false
            };
        }
    }
}
