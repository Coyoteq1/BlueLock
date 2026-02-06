using System.Collections.Generic;
using Unity.Entities;
using VAuto.Core;

namespace VAuto.Core.Configuration
{
    /// <summary>
    /// ECS System Configuration - Manages system registration and execution order
    /// </summary>
    public class ECSSystemConfiguration
    {
        public static readonly string LogPrefix = "[ECS_CONFIG]";
        
        /// <summary>
        /// Configuration for ECS system integration
        /// </summary>
        public class SystemConfig
        {
            public string SystemName { get; set; }
            public string TargetGroup { get; set; }
            public string ReferenceSystem { get; set; }
            public string UpdateType { get; set; } // Before, After, None
            public bool Enabled { get; set; } = true;
            public int Priority { get; set; } = 0;
            public string Description { get; set; }
        }

        /// <summary>
        /// All configured systems based on integration plan
        /// </summary>
        public static readonly List<SystemConfig> ConfiguredSystems = new()
        {
            // PVP/Glow Zones - Before PlayerCombatBuffSystem_InitialApplication_Aggro
            new SystemConfig
            {
                SystemName = "GlowZoneEnforcementSystem",
                TargetGroup = "ProjectM.UpdateGroup",
                ReferenceSystem = "ProjectM.Gameplay.Systems.PlayerCombatBuffSystem_InitialApplication_Aggro",
                UpdateType = "Before",
                Priority = 100,
                Description = "Enforce glow zone rules for PvP combat"
            },

            // Portals/Teleport - Before TeleportSystem
            new SystemConfig
            {
                SystemName = "PortalInterceptSystem",
                TargetGroup = "ProjectM.UpdateGroup",
                ReferenceSystem = "ProjectM.Gameplay.Systems.TeleportSystem",
                UpdateType = "Before",
                Priority = 90,
                Description = "Intercept and process portal requests"
            },

            // Chat/Commands - Before ChatMessageSystem
            new SystemConfig
            {
                SystemName = "CommandParsingSystem",
                TargetGroup = "ProjectM.UpdateGroup",
                ReferenceSystem = "ProjectM.ChatMessageSystem",
                UpdateType = "Before",
                Priority = 80,
                Description = "Process commands before chat display"
            },

            // Persistence - After TriggerPersistenceSaveSystem
            new SystemConfig
            {
                SystemName = "PersistenceHookSystem",
                TargetGroup = "ProjectM.UpdateGroup",
                ReferenceSystem = "ProjectM.TriggerPersistenceSaveSystem",
                UpdateType = "After",
                Priority = 70,
                Description = "Hook into save system for custom data"
            },

            // General Gameplay Automation - After CreateGameplayEventOnTickSystem
            new SystemConfig
            {
                SystemName = "VAutomationEventProcessingSystem",
                TargetGroup = "ProjectM.UpdateGroup",
                ReferenceSystem = "ProjectM.Gameplay.Systems.CreateGameplayEventOnTickSystem",
                UpdateType = "After",
                Priority = 60,
                Description = "Main automation event processing"
            },

            // Cooldown Processing - After CreateGameplayEventOnTickSystem
            new SystemConfig
            {
                SystemName = "VAutomationCooldownSystem",
                TargetGroup = "ProjectM.UpdateGroup",
                ReferenceSystem = "ProjectM.Gameplay.Systems.CreateGameplayEventOnTickSystem",
                UpdateType = "After",
                Priority = 50,
                Description = "Process cooldowns and timers"
            },

            // Damage Prevention - Before DealDamageSystem
            new SystemConfig
            {
                SystemName = "SafeZoneDamagePreventionSystem",
                TargetGroup = "ProjectM.UpdateGroup",
                ReferenceSystem = "ProjectM.Gameplay.Systems.DealDamageSystem",
                UpdateType = "Before",
                Priority = 40,
                Description = "Prevent damage in safe zones"
            },

            // Damage Analytics - After DealDamageSystem
            new SystemConfig
            {
                SystemName = "DamageAnalyticsSystem",
                TargetGroup = "ProjectM.UpdateGroup",
                ReferenceSystem = "ProjectM.Gameplay.Systems.DealDamageSystem",
                UpdateType = "After",
                Priority = 30,
                Description = "Track and analyze damage events"
            },

            // Spawning - After SpawnCharacterSystem
            new SystemConfig
            {
                SystemName = "SpawnCustomizationSystem",
                TargetGroup = "ProjectM.SpawnGroup",
                ReferenceSystem = "ProjectM.Gameplay.Systems.SpawnCharacterSystem",
                UpdateType = "After",
                Priority = 20,
                Description = "Customize entities on spawn"
            },

            // Lifecycle Spellbook System - After CreateGameplayEventOnTickSystem
            new SystemConfig
            {
                SystemName = "LifecycleSpellbookSystem",
                TargetGroup = "ProjectM.UpdateGroup",
                ReferenceSystem = "ProjectM.Gameplay.Systems.CreateGameplayEventOnTickSystem",
                UpdateType = "After",
                Priority = 10,
                Description = "Manage spellbooks in lifecycle zones"
            }
        };

        /// <summary>
        /// Initialize ECS system configuration
        /// </summary>
        public static void Initialize()
        {
            Plugin.Log.LogInfo($"{LogPrefix} Initializing ECS System Configuration");
            Plugin.Log.LogInfo($"{LogPrefix} {ConfiguredSystems.Count} systems configured");

            foreach (var system in ConfiguredSystems)
            {
                if (system.Enabled)
                {
                    Plugin.Log.LogInfo($"{LogPrefix} ✓ {system.SystemName} - {system.Description}");
                }
                else
                {
                    Plugin.Log.LogInfo($"{LogPrefix} ✗ {system.SystemName} - DISABLED");
                }
            }
        }

        /// <summary>
        /// Get system configuration by name
        /// </summary>
        public static SystemConfig GetSystemConfig(string systemName)
        {
            return ConfiguredSystems.Find(s => s.SystemName == systemName);
        }

        /// <summary>
        /// Get all enabled systems
        /// </summary>
        public static List<SystemConfig> GetEnabledSystems()
        {
            return ConfiguredSystems.FindAll(s => s.Enabled);
        }

        /// <summary>
        /// Get systems by target group
        /// </summary>
        public static List<SystemConfig> GetSystemsByGroup(string targetGroup)
        {
            return ConfiguredSystems.FindAll(s => s.TargetGroup == targetGroup && s.Enabled);
        }

        /// <summary>
        /// Validate system configuration
        /// </summary>
        public static bool ValidateConfiguration()
        {
            var isValid = true;
            
            foreach (var system in ConfiguredSystems)
            {
                if (string.IsNullOrEmpty(system.SystemName))
                {
                    Plugin.Log.LogError($"{LogPrefix} System missing name");
                    isValid = false;
                }

                if (string.IsNullOrEmpty(system.TargetGroup))
                {
                    Plugin.Log.LogError($"{LogPrefix} {system.SystemName} missing target group");
                    isValid = false;
                }
            }

            Plugin.Log.LogInfo($"{LogPrefix} Configuration validation: {(isValid ? "PASSED" : "FAILED")}");
            return isValid;
        }
    }
}
