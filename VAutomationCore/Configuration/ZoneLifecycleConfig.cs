using System;
using System.Collections.Generic;

namespace VAutomationCore.Configuration
{
    /// <summary>
    /// Zone-Lifecycle wiring configuration model for V Rising mods.
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
    public class ZoneLifecycleConfig
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
        public Dictionary<string, ZoneLifecycleStages> Mappings { get; set; } = new();

        /// <summary>
        /// Wildcard zone mapping applies to any zone without explicit configuration
        /// </summary>
        public ZoneLifecycleStages WildcardMapping { get; set; } = new();
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
    public class ZoneLifecycleStages
    {
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
        /// If true, use WildcardMapping when no explicit mapping exists for a zone.
        /// If false, no stages will fire for unmapped zones.
        /// </summary>
        public bool UseWildcardDefaults { get; set; } = true;

        /// <summary>
        /// Optional zone-specific configuration bundle to load.
        /// Referenced in Vlifecycle config as a ConfigAction.
        /// </summary>
        public string ConfigBundle { get; set; } = "";
    }

        /// <summary>
        /// Player zone tracking state maintained by ZoneEventBridge.
        /// Used to detect transitions and track isInZone timing.
        /// Uses DateTime for reliable server-side timing.
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
        }

        /// <summary>
        /// Helper methods for zone lifecycle configuration
        /// </summary>
        public static class ZoneLifecycleConfigExtensions
        {
            /// <summary>
            /// Get the stage configuration for a given zone ID
            /// </summary>
            public static ZoneLifecycleStages GetStagesForZone(
                this ZoneLifecycleConfig config, 
                string zoneId,
                out bool usedWildcard)
            {
                usedWildcard = false;
                
                if (config.Mappings.TryGetValue(zoneId, out var stages))
                {
                    return stages;
                }
                
                if (config.WildcardMapping != null && config.WildcardMapping.UseWildcardDefaults)
                {
                    usedWildcard = true;
                    return config.WildcardMapping;
                }
                
                return new ZoneLifecycleStages();
            }

            /// <summary>
            /// Check if isInZone stage should fire based on timing.
            /// Uses DateTime for reliable server-side timing.
            /// </summary>
            public static bool ShouldTriggerIsInZone(
                this ZoneLifecycleStages stages,
                PlayerZoneState state,
                float intervalSeconds)
            {
                if (string.IsNullOrEmpty(stages.IsInZoneStage))
                    return false;

                if (!state.WasInZone)
                    return false;

                var now = DateTime.UtcNow;
                var elapsed = state.LastIsInZoneTrigger == default(DateTime)
                    ? TimeSpan.MaxValue
                    : now - state.LastIsInZoneTrigger;

                // Always trigger on first entry to zone
                if (state.LastIsInZoneTrigger == default(DateTime))
                    return true;

                return elapsed.TotalSeconds >= intervalSeconds;
            }

        /// <summary>
        /// Build stage name with zone ID interpolation
        /// </summary>
        public static string BuildStageName(this ZoneLifecycleStages stages, string zoneId, string phase)
        {
            var baseStage = phase switch
            {
                "onEnter" => stages.OnEnterStage,
                "isInZone" => stages.IsInZoneStage,
                "onExit" => stages.OnExitStage,
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
    }

    /// <summary>
    /// Represents a zone transition event
    /// </summary>
    public struct ZoneTransition
    {
        public string FromZone;
        public string ToZone;
        public (float x, float y, float z) Position;
        public bool IsReentry;
        public bool IsReconnection;
        public bool PositionChanged;
    }
}
