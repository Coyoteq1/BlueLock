using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VAutomationCore.Configuration
{
    /// <summary>
    /// Unified Zone-Lifecycle configuration model for V Rising mods.
    /// 
    /// This configuration combines zone detection, lifecycle stage mapping, and zone definition
    /// into a single unified model for easier management and consistency across the VAuto ecosystem.
    /// 
    /// Architecture:
    /// VAutoZone monitors player positions → Determines which stages to trigger → 
    /// Passes stage names to Vlifecycle → Vlifecycle executes stage actions
    /// 
    /// Three-Stage Lifecycle Pattern:
    /// - onEnter: One-time effects when crossing INTO a zone (store inventory, apply zone buffs, send messages)
    /// - isInZone: Repeated effects while player remains INSIDE zone (reassert invariants, buff enforcement)
    /// - onExit: One-time effects when crossing OUT of zone (restore inventory, remove buffs, cleanup)
    /// 
    /// This design follows V Rising's eventually consistent ECS architecture where systems
    /// may invalidate state and mods must reassert intent rather than assuming permanence.
    /// </summary>
    public class UnifiedZoneLifecycleConfig
    {
        /// <summary>
        /// Enable zone-to-lifecycle event wiring
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// How often to check player positions for zone transitions (milliseconds)
        /// </summary>
        public int CheckIntervalMs { get; set; } = 100;

        /// <summary>
        /// Distance threshold to consider a position change significant enough to check zones
        /// </summary>
        public float PositionChangeThreshold { get; set; } = 1.0f;

        /// <summary>
        /// Interval in seconds for isInZone stage reassertion
        /// Lower values = more responsive but higher server load
        /// </summary>
        public float IsInZoneIntervalSeconds { get; set; } = 5.0f;

        /// <summary>
        /// Zone ID to lifecycle stage mappings
        /// </summary>
        public Dictionary<string, UnifiedLifecycleMapping> Mappings { get; set; } = new();

        /// <summary>
        /// Global configuration options for zone detection and tracking
        /// </summary>
        public GlobalZoneConfig GlobalConfig { get; set; } = new();

        /// <summary>
        /// Get zone definition by ID.
        /// </summary>
        public UnifiedZoneDefinition GetZoneById(string zoneId)
        {
            // This is a placeholder implementation - actual implementation may vary
            return new UnifiedZoneDefinition
            {
                Id = zoneId,
                DisplayName = zoneId,
                ZoneType = GlobalConfig.DefaultZoneType
            };
        }

        /// <summary>
        /// Get primary zone at a specific position.
        /// This is a placeholder implementation - actual implementation would query zone definitions.
        /// </summary>
        public UnifiedZoneDefinition GetPrimaryZoneAtPosition(float3 position)
        {
            // This should be implemented by querying the actual zone definitions
            return null;
        }
    }

    /// <summary>
    /// Represents a unified zone definition with properties and metadata
    /// </summary>
    public class UnifiedZoneDefinition
    {
        /// <summary>
        /// Unique identifier for the zone
        /// </summary>
        public string Id { get; set; } = "";

        /// <summary>
        /// Display name of the zone
        /// </summary>
        public string DisplayName { get; set; } = "";

        /// <summary>
        /// Description of the zone's purpose
        /// </summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// Type of zone (e.g., "Arena", "SafeZone", "PvPZone", "ResourceZone")
        /// </summary>
        public string ZoneType { get; set; } = "General";

        /// <summary>
        /// Priority of this zone for detection (higher values take precedence)
        /// </summary>
        public int Priority { get; set; } = 1;

        /// <summary>
        /// Whether this zone is enabled for detection
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Optional configuration bundle specific to this zone
        /// </summary>
        public string ConfigBundle { get; set; } = "";

        /// <summary>
        /// Custom properties for this zone
        /// </summary>
        public Dictionary<string, object> Properties { get; set; } = new();

        /// <summary>
        /// Lifecycle configuration for this zone
        /// </summary>
        public UnifiedLifecycleMapping Lifecycle { get; set; } = new();

        /// <summary>
        /// Settings for this zone
        /// </summary>
        public ZoneSettings Settings { get; set; } = new();
    }

    /// <summary>
    /// Zone settings
    /// </summary>
    public class ZoneSettings
    {
        /// <summary>
        /// Message to display when entering the zone
        /// </summary>
        public string EnterMessage { get; set; } = "";

        /// <summary>
        /// Message to display when exiting the zone
        /// </summary>
        public string ExitMessage { get; set; } = "";

        /// <summary>
        /// Whether the zone is enabled
        /// </summary>
        public bool Enabled { get; set; } = true;
    }

    /// <summary>
    /// Defines which lifecycle stages should fire for a specific zone.
    /// 
    /// Design Philosophy:
    /// - onEnter: One-time setup (store state, apply markers, send messages)
    /// - isInZone: Continuous enforcement (reapply buffs, enforce blood type, validate config)
    /// - onExit: Cleanup (restore state, remove markers, log departure)
    /// 
    /// Each stage maps to a named stage in Vlifecycle's stage registry.
    /// Vlifecycle owns the actual action execution; VAutoZone only selects stages.
    /// </summary>
    public class UnifiedLifecycleMapping
    {
        /// <summary>
        /// Actions to trigger when player enters this zone.
        /// </summary>
        public List<string> OnEnter { get; set; } = new();

        /// <summary>
        /// Actions to trigger repeatedly while player remains in zone.
        /// </summary>
        public List<string> OnExit { get; set; } = new();

        /// <summary>
        /// If true, use WildcardMapping when no explicit mapping exists for a zone.
        /// If false, no stages will fire for unmapped zones.
        /// </summary>
        public bool UseGlobalDefaults { get; set; } = true;

        /// <summary>
        /// Stage name to trigger when player enters this zone.
        /// Use "{ZoneId}" placeholder for zone-specific stages: "zone.{ZoneId}.onEnter"
        /// Set to empty string to disable onEnter for this zone.
        /// </summary>
        public string OnEnterStage { get; set; } = "";

        /// <summary>
        /// Stage name to trigger repeatedly while player remains in zone.
        /// Use "{ZoneId}" placeholder for zone-specific stages: "zone.{ZoneId}.isInZone"
        /// Set to empty string to disable isInZone for this zone.
        /// </summary>
        public string IsInZoneStage { get; set; } = "";

        /// <summary>
        /// Stage name to trigger when player exits this zone.
        /// Use "{ZoneId}" placeholder for zone-specific stages: "zone.{ZoneId}.onExit"
        /// Set to empty string to disable onExit for this zone.
        /// </summary>
        public string OnExitStage { get; set; } = "";

        /// <summary>
        /// Optional zone-specific configuration bundle to load.
        /// Referenced in Vlifecycle config as a ConfigAction.
        /// </summary>
        public string ConfigBundle { get; set; } = "";

        /// <summary>
        /// Custom configuration for this mapping
        /// </summary>
        public Dictionary<string, object> CustomConfig { get; set; } = new();

        /// <summary>
        /// Whether to enable spellbook menu integration for this zone
        /// </summary>
        public bool EnableSpellbookMenu { get; set; } = false;

        /// <summary>
        /// Whether to enable VBlood progress tracking for this zone
        /// </summary>
        public bool EnableVBloodProgress { get; set; } = false;

        /// <summary>
        /// Whether to enable legacy action handling
        /// </summary>
        public bool EnableLegacyActions { get; set; } = false;
    }

    /// <summary>
    /// Global configuration options for zone detection and tracking
    /// </summary>
    public class GlobalZoneConfig
    {
        /// <summary>
        /// Enable debug logging for zone detection
        /// </summary>
        public bool DebugLogging { get; set; } = false;

        /// <summary>
        /// Maximum number of active player zone states to track
        /// </summary>
        public int MaxTrackedPlayers { get; set; } = 100;

        /// <summary>
        /// Timeout in seconds for inactive player zone states
        /// </summary>
        public int InactivePlayerTimeoutSeconds { get; set; } = 300;

        /// <summary>
        /// Whether to track offline players' zone states
        /// </summary>
        public bool TrackOfflinePlayers { get; set; } = false;

        /// <summary>
        /// Default zone type to use when no type is specified
        /// </summary>
        public string DefaultZoneType { get; set; } = "General";

        /// <summary>
        /// Global properties applied to all zones
        /// </summary>
        public Dictionary<string, object> GlobalProperties { get; set; } = new();
    }

    /// <summary>
    /// Enhanced player zone tracking state maintained by ZoneEventBridge.
    /// Used to detect transitions and track isInZone timing.
    /// </summary>
    public class PlayerZoneState
    {
        public ulong SteamId { get; set; }
        public string CurrentZoneId { get; set; } = "";
        public string PreviousZoneId { get; set; } = "";
        public float LastPositionX { get; set; }
        public float LastPositionY { get; set; }
        public float LastPositionZ { get; set; }
        public DateTime LastZoneEnterTime { get; set; }
        public DateTime LastIsInZoneTrigger { get; set; }
        public DateTime LastUpdate { get; set; }
        public bool WasInZone { get; set; }
        public bool NeedsReassertion { get; set; }
        public int TransitionCount { get; set; } = 0;
        public Dictionary<string, object> CustomState { get; set; } = new();
    }

    /// <summary>
    /// Helper methods for unified zone lifecycle configuration
    /// </summary>
    public static class UnifiedZoneLifecycleConfigExtensions
    {
        /// <summary>
        /// Get the stage configuration for a given zone ID
        /// </summary>
        public static UnifiedLifecycleMapping GetLifecycleMapping(
            this UnifiedZoneLifecycleConfig config, 
            string zoneId,
            out bool usedWildcard)
        {
            usedWildcard = false;
            
            if (config.Mappings.TryGetValue(zoneId, out var mapping))
            {
                return mapping;
            }
            
            if (config.Mappings.TryGetValue("*", out var wildcardMapping) && wildcardMapping.UseGlobalDefaults)
            {
                usedWildcard = true;
                return wildcardMapping;
            }
            
            return new UnifiedLifecycleMapping();
        }

        /// <summary>
        /// Get the zone definition for a given zone ID
        /// </summary>
        public static UnifiedZoneDefinition GetZoneDefinition(
            this UnifiedZoneLifecycleConfig config, 
            string zoneId)
        {
            if (config == null)
                return new UnifiedZoneDefinition { Id = zoneId };
                
            return config.GetZoneById(zoneId);
        }

        /// <summary>
        /// Check if isInZone stage should fire based on timing
        /// </summary>
        public static bool ShouldTriggerIsInZone(
            this UnifiedLifecycleMapping mapping,
            PlayerZoneState state,
            float intervalSeconds)
        {
            if (string.IsNullOrEmpty(mapping.IsInZoneStage))
                return false;
                
            if (!state.WasInZone)
                return false;
                
            var now = DateTime.UtcNow;
            var elapsed = (now - state.LastIsInZoneTrigger).TotalSeconds;
            
            // Always trigger on first entry to zone
            if (state.LastIsInZoneTrigger == default)
                return true;
                
            return elapsed >= intervalSeconds;
        }

        /// <summary>
        /// Build stage name with zone ID interpolation
        /// </summary>
        public static string BuildStageName(this UnifiedLifecycleMapping mapping, string zoneId, string phase)
        {
            var baseStage = phase switch
            {
                "onEnter" => mapping.OnEnterStage,
                "isInZone" => mapping.IsInZoneStage,
                "onExit" => mapping.OnExitStage,
                _ => ""
            };
            
            if (string.IsNullOrEmpty(baseStage))
                return "";
                
            // Interpolate {ZoneId} placeholder
            return baseStage.Replace("{ZoneId}", zoneId);
        }

        /// <summary>
        /// Detect zone transition based on position
        /// </summary>
        public static ZoneTransition DetectZoneTransition(
            this PlayerZoneState state,
            string newZoneId,
            float newPosX,
            float newPosY,
            float newPosZ,
            float threshold)
        {
            var transition = new ZoneTransition
            {
                FromZone = state.PreviousZoneId,
                ToZone = newZoneId,
                Position = (newPosX, newPosY, newPosZ),
                IsReentry = state.CurrentZoneId == newZoneId && state.WasInZone,
                IsReconnection = !state.WasInZone && !string.IsNullOrEmpty(newZoneId),
                PositionChanged = Math.Abs(state.LastPositionX - newPosX) > threshold ||
                                 Math.Abs(state.LastPositionY - newPosY) > threshold ||
                                 Math.Abs(state.LastPositionZ - newPosZ) > threshold
            };
            
            return transition;
        }

        /// <summary>
        /// Check if a zone is currently enabled for detection
        /// </summary>
        public static bool IsZoneEnabled(this UnifiedZoneLifecycleConfig config, string zoneId)
        {
            if (!config.Enabled)
                return false;
                
            var definition = config.GetZoneDefinition(zoneId);
            return definition.Enabled;
        }

        /// <summary>
        /// Get all enabled zones
        /// </summary>
        public static IEnumerable<string> GetEnabledZoneIds(this UnifiedZoneLifecycleConfig config)
        {
            // This is a placeholder implementation
            return config.Mappings.Keys.Where(k => k != "*");
        }

        /// <summary>
        /// Get zones by type
        /// </summary>
        public static IEnumerable<string> GetZoneIdsByType(this UnifiedZoneLifecycleConfig config, string zoneType)
        {
            // This is a placeholder implementation
            return Enumerable.Empty<string>();
        }

        /// <summary>
        /// Validate the configuration for consistency
        /// </summary>
        public static bool Validate(this UnifiedZoneLifecycleConfig config, out List<string> validationErrors)
        {
            validationErrors = new List<string>();

            if (config.CheckIntervalMs <= 0)
                validationErrors.Add("CheckIntervalMs must be greater than 0");
                
            if (config.PositionChangeThreshold <= 0)
                validationErrors.Add("PositionChangeThreshold must be greater than 0");
                
            if (config.IsInZoneIntervalSeconds <= 0)
                validationErrors.Add("IsInZoneIntervalSeconds must be greater than 0");
                
            if (config.GlobalConfig.MaxTrackedPlayers <= 0)
                validationErrors.Add("MaxTrackedPlayers must be greater than 0");
                
            if (config.GlobalConfig.InactivePlayerTimeoutSeconds <= 0)
                validationErrors.Add("InactivePlayerTimeoutSeconds must be greater than 0");

            return validationErrors.Count == 0;
        }
    }

    /// <summary>
    /// Represents a zone transition event with additional context
    /// </summary>
    public struct ZoneTransition
    {
        public string FromZone;
        public string ToZone;
        public (float x, float y, float z) Position;
        public bool IsReentry;
        public bool IsReconnection;
        public bool PositionChanged;
        public DateTime Timestamp;

        public ZoneTransition()
        {
            FromZone = "";
            ToZone = "";
            Position = (0, 0, 0);
            IsReentry = false;
            IsReconnection = false;
            PositionChanged = false;
            Timestamp = DateTime.UtcNow;
        }
    }
}