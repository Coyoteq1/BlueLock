using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace VAuto.Zone
{
    /// <summary>
    /// Zone lifecycle configuration for VAutoZone.
    /// Maps zones to lifecycle stages for enter/exit events.
    /// </summary>
    public class ZoneLifecycleConfig
    {
        public bool Enabled { get; set; } = true;
        public int CheckIntervalMs { get; set; } = 100;
        public float PositionChangeThreshold { get; set; } = 1.0f;
        public float IsInZoneIntervalSeconds { get; set; } = 5.0f;
        public float MapIconSpawnRefreshIntervalSeconds { get; set; } = 10.0f;
        
        /// <summary>
        /// Zone ID to lifecycle stage mappings
        /// </summary>
        public Dictionary<string, ZoneLifecycleStages> Mappings { get; set; } = new();
        
        /// <summary>
        /// Wildcard mapping for any zone without explicit config
        /// </summary>
        public ZoneLifecycleStages WildcardMapping { get; set; } = new();
        
        /// <summary>
        /// Get zone by ID
        /// </summary>
        public UnifiedZoneDefinition GetZoneById(string zoneId)
        {
            var stages = GetStagesForZone(zoneId, out _);
            return new UnifiedZoneDefinition
            {
                Id = zoneId,
                OnEnterStage = stages.OnEnterStage,
                IsInZoneStage = stages.IsInZoneStage,
                OnExitStage = stages.OnExitStage,
                UseWildcardDefaults = stages.UseWildcardDefaults,
                ConfigBundle = stages.ConfigBundle,
                EnableSpellbookMenu = stages.EnableSpellbookMenu,
                EnableVBloodProgress = stages.EnableVBloodProgress,
                Settings = stages.Settings
            };
        }
        
        /// <summary>
        /// Get primary zone at position (simplified - override with actual zone detection)
        /// Returns null if position is not in any known zone
        /// </summary>
        public UnifiedZoneDefinition GetPrimaryZoneAtPosition(float3 position)
        {
            // This should be overridden with actual zone detection logic
            // For now, return null and let the caller use ArenaTerritory.IsInArenaTerritory
            return null;
        }
    }

    /// <summary>
    /// Defines which lifecycle stages should fire for a specific zone.
    /// </summary>
    public class ZoneLifecycleStages
    {
        /// <summary>
        /// Stage name to trigger when player enters this zone
        /// </summary>
        public string OnEnterStage { get; set; } = "";

        /// <summary>
        /// Stage name to trigger while player remains in zone
        /// </summary>
        public string IsInZoneStage { get; set; } = "";

        /// <summary>
        /// Stage name to trigger when player exits this zone
        /// </summary>
        public string OnExitStage { get; set; } = "";

        /// <summary>
        /// If true, use wildcard mapping when no explicit mapping exists
        /// </summary>
        public bool UseWildcardDefaults { get; set; } = true;

        /// <summary>
        /// Optional zone-specific config bundle
        /// </summary>
        public string ConfigBundle { get; set; } = "";
        
        /// <summary>
        /// Enable spellbook menu for this zone
        /// </summary>
        public bool EnableSpellbookMenu { get; set; }
        
        /// <summary>
        /// Enable VBlood progress tracking for this zone
        /// </summary>
        public bool EnableVBloodProgress { get; set; }
        
        /// <summary>
        /// Zone settings (messages, etc.)
        /// </summary>
        public ZoneSettings Settings { get; set; } = new();
        
        /// <summary>
        /// Prefab GUID to spawn/change map icon to when player is in zone
        /// </summary>
        public string MapIconChangePrefab { get; set; } = "";
    }

    /// <summary>
    /// Zone settings
    /// </summary>
    public class ZoneSettings
    {
        public bool Enabled { get; set; } = true;
        public string EnterMessage { get; set; } = "";
        public string ExitMessage { get; set; } = "";
    }

    /// <summary>
    /// Wildcard zone mapping applies to any zone without explicit configuration
    /// </summary>
    public class WildcardMapping
    {
        public string OnEnterStage { get; set; } = "";
        public string IsInZoneStage { get; set; } = "";
        public string OnExitStage { get; set; } = "";
        public bool UseWildcardDefaults { get; set; } = true;
    }

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
            
            // Check for wildcard
            if (config.Mappings.TryGetValue("*", out var wildcard))
            {
                if (wildcard.UseWildcardDefaults)
                {
                    usedWildcard = true;
                    return wildcard;
                }
            }
            
            return new ZoneLifecycleStages();
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
                
            return baseStage.Replace("{ZoneId}", zoneId);
        }
    }

    /// <summary>
    /// Alias for ZoneLifecycleStages (for compatibility with Vlifecycle)
    /// </summary>
    public class UnifiedLifecycleMapping : ZoneLifecycleStages { }

    /// <summary>
    /// Alias for ZoneLifecycleStages with zone ID
    /// </summary>
    public class UnifiedZoneDefinition : ZoneLifecycleStages 
    { 
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string TerritoryId { get; set; } = "";
    }
}
