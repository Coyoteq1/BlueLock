using System;
using Unity.Entities;
using Unity.Mathematics;

namespace VAuto.Zone.Core
{
    /// <summary>
    /// Core constants and static data for the VAutoZone plugin.
    /// </summary>
    public static class ZoneCoreConstants
    {
        /// <summary>
        /// Default arena zone identifier.
        /// </summary>
        public const string DefaultArenaZone = "arena_main";

        /// <summary>
        /// Maximum number of glow zones supported.
        /// </summary>
        public const int MaxGlowZones = 50;

        /// <summary>
        /// Default glow radius for zones.
        /// </summary>
        public const float DefaultGlowRadius = 15.0f;

        /// <summary>
        /// Default player check interval in seconds.
        /// </summary>
        public const float DefaultPlayerCheckInterval = 0.5f;

        /// <summary>
        /// Zone enter event name.
        /// </summary>
        public const string EventZoneEnter = "zone_enter";

        /// <summary>
        /// Zone exit event name.
        /// </summary>
        public const string EventZoneExit = "zone_exit";
    }

    /// <summary>
    /// Represents a zone definition with position and radius.
    /// </summary>
    public class ZoneDefinition
    {
        /// <summary>
        /// The zone ID.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// The zone name for display.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The zone center position.
        /// </summary>
        public float3 Position { get; set; }

        /// <summary>
        /// The zone radius.
        /// </summary>
        public float Radius { get; set; }

        /// <summary>
        /// Whether this is an arena zone.
        /// </summary>
        public bool IsArena { get; set; }

        /// <summary>
        /// Creates a new ZoneDefinition with default values.
        /// </summary>
        public ZoneDefinition()
        {
            Id = string.Empty;
            Name = string.Empty;
            Position = float3.zero;
            Radius = ZoneCoreConstants.DefaultGlowRadius;
            IsArena = false;
        }

        /// <summary>
        /// Checks if a position is within this zone.
        /// </summary>
        public bool ContainsPosition(float3 position)
        {
            return math.distance(position, Position) <= Radius;
        }
    }
}
