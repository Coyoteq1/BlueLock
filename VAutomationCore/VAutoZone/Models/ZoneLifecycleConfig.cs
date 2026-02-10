using System.Collections.Generic;

namespace VAuto.Zone.Models
{
    /// <summary>
    /// Configuration for zone lifecycle behavior in VAutoZone.
    /// </summary>
    public class ZoneLifecycleConfig
    {
        /// <summary>
        /// Whether the zone lifecycle system is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// List of zone IDs that trigger lifecycle events.
        /// </summary>
        public List<string> TriggerZones { get; set; } = new List<string>();

        /// <summary>
        /// Default zone to use when none is specified.
        /// </summary>
        public string DefaultZone { get; set; } = "arena_main";

        /// <summary>
        /// Whether to save player state when entering zones.
        /// </summary>
        public bool SavePlayerState { get; set; } = true;

        /// <summary>
        /// Whether to restore player state when exiting zones.
        /// </summary>
        public bool RestorePlayerState { get; set; } = true;

        /// <summary>
        /// Whether to send notifications to players on zone entry/exit.
        /// </summary>
        public bool SendNotifications { get; set; } = true;

        /// <summary>
        /// Whether to integrate with the lifecycle system.
        /// </summary>
        public bool IntegrateWithLifecycle { get; set; } = true;

        /// <summary>
        /// Creates a default ZoneLifecycleConfig.
        /// </summary>
        public static ZoneLifecycleConfig Default => new ZoneLifecycleConfig();

        /// <summary>
        /// Creates a ZoneLifecycleConfig with common arena settings.
        /// </summary>
        public static ZoneLifecycleConfig ArenaDefault => new ZoneLifecycleConfig
        {
            Enabled = true,
            TriggerZones = new List<string> { "arena_main", "arena_pvp", "arena_event" },
            DefaultZone = "arena_main",
            SavePlayerState = true,
            RestorePlayerState = true,
            SendNotifications = true,
            IntegrateWithLifecycle = true
        };
    }
}
