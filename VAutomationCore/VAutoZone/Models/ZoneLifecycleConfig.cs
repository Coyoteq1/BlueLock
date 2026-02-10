using System;
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
        /// Interval in milliseconds between zone checks.
        /// </summary>
        public int CheckIntervalMs { get; set; } = 1000;

        /// <summary>
        /// Interval in seconds between "is in zone" trigger updates.
        /// </summary>
        public int IsInZoneIntervalSeconds { get; set; } = 30;

        /// <summary>
        /// List of zone IDs that trigger lifecycle events.
        /// </summary>
        public List<string> TriggerZones { get; set; } = new List<string>();

        /// <summary>
        /// Zone mappings for custom zone configurations.
        /// </summary>
        public Dictionary<string, ZoneMapping> Mappings { get; set; } = new Dictionary<string, ZoneMapping>();

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
        /// Gets the lifecycle stages for a specific zone.
        /// </summary>
        public List<string> GetStagesForZone(string zoneId)
        {
            if (Mappings.TryGetValue(zoneId, out var mapping))
            {
                return mapping.Stages ?? new List<string>();
            }
            return new List<string>();
        }

        /// <summary>
        /// Builds a stage name for a zone and stage type.
        /// </summary>
        public string BuildStageName(string zoneId, string stageType)
        {
            return $"{zoneId}_{stageType}";
        }

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
            CheckIntervalMs = 1000,
            IsInZoneIntervalSeconds = 30,
            TriggerZones = new List<string> { "arena_main", "arena_pvp", "arena_event" },
            Mappings = new Dictionary<string, ZoneMapping>
            {
                ["arena_main"] = new ZoneMapping
                {
                    Stages = new List<string> { "enter", "active", "exit" }
                },
                ["arena_pvp"] = new ZoneMapping
                {
                    Stages = new List<string> { "combat_start", "pvp_active", "combat_end" }
                },
                ["arena_event"] = new ZoneMapping
                {
                    Stages = new List<string> { "event_start", "event_mid", "event_end" }
                }
            },
            DefaultZone = "arena_main",
            SavePlayerState = true,
            RestorePlayerState = true,
            SendNotifications = true,
            IntegrateWithLifecycle = true
        };
    }

    /// <summary>
    /// Represents a zone mapping with custom configuration.
    /// </summary>
    public class ZoneMapping
    {
        /// <summary>
        /// List of lifecycle stages for this zone.
        /// </summary>
        public List<string> Stages { get; set; } = new List<string>();

        /// <summary>
        /// Custom metadata for the zone.
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }
}
