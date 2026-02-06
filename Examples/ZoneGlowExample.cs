using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using VAuto.Services.World;
using VAuto.Services.Visual;
using VAuto.Utilities;

namespace VAuto.Examples
{
    /// <summary>
    /// Example: Complete zone and glow integration using your Zone and Schematic Integration Guide
    /// </summary>
    public static class ZoneGlowExample
    {
        private static ZoneGlowIntegration _zoneGlowIntegration;
        private static GlowManager _glowManager;
        
        /// <summary>
        /// Initialize zone glow system with your existing zone management
        /// </summary>
        public static void InitializeZoneGlowSystem()
        {
            try
            {
                // Initialize glow manager
                _glowManager = new GlowManager();
                _glowManager.Initialize();
                
                // Initialize zone glow integration
                _zoneGlowIntegration = new ZoneGlowIntegration(_glowManager);
                
                Plugin.Log?.LogInfo("[ZONE_GLOW_EXAMPLE] Zone glow system initialized");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[ZONE_GLOW_EXAMPLE] Initialization failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Example 1: Create arena zone with schematic loading and glow effects
        /// </summary>
        public static void CreateArenaZoneWithSchematicAndGlow()
        {
            try
            {
                // 1. Define zone based on your Zone and Schematic Integration Guide
                var zoneDefinition = new ZoneDefinition
                {
                    Id = "ArenaZone1",
                    Name = "Main Arena",
                    Center = new float3(-1000, 5, -500),
                    Radius = 75f,
                    AllowedPrefabs = new List<string> { "Castle_Basic_T1_C", "Arena_Spawner", "Arena_Teleporter" },
                    Properties = new Dictionary<string, object>
                    {
                        ["glowEnabled"] = true,
                        ["glowType"] = "Both",
                        ["playerGlowDuration"] = 5f,
                        ["arenaGlowColor"] = "1,0,0,0.8"
                    }
                };
                
                // 2. Load schematic (from your guide)
                var schematicData = LoadArenaSchematic();
                
                // 3. Create zone rule with glow effects
                var zoneRule = CreateZoneRuleWithGlow(zoneDefinition, schematicData);
                
                // 4. Register zone with WorldAutomationService
                WorldAutomationService.AddRule(zoneDefinition.Id, zoneRule);
                
                // 5. Register zone glow with our integration
                _zoneGlowIntegration.RegisterZoneGlow(
                    zoneDefinition.Id,
                    zoneDefinition.Center,
                    zoneDefinition.Radius
                );
                
                Plugin.Log?.LogInfo($"[ZONE_GLOW_EXAMPLE] Created arena zone with {schematicData.Objects.Count} objects and glow effects");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[ZONE_GLOW_EXAMPLE] Error creating arena zone: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Example 2: Create conditional zone with glow effects based on player state
        /// </summary>
        public static void CreateConditionalZoneWithGlow()
        {
            try
            {
                // Create conditional zone rule (from your guide)
                var conditionalRule = new AutomationRule
                {
                    TriggerName = "PlayerInConditionalZone",
                    Actions = new List<AutomationAction>
                    {
                        new AutomationAction
                        {
                            Type = AutomationActionType.TriggerEvent,
                            EventName = "OnZoneEnter",
                            Parameters = new Dictionary<string, object>
                            {
                                ["zoneId"] = "ConditionalZone1",
                                ["condition"] = "player.health < 50%"
                            }
                        },
                        new AutomationAction
                        {
                            Type = AutomationActionType.Delay,
                            DelaySeconds = 1.0f,
                            Parameters = new Dictionary<string, object>
                            {
                                ["action"] = "ApplyLowHealthGlow"
                            }
                        },
                        new AutomationAction
                        {
                            Type = AutomationActionType.SpawnObject,
                            PrefabName = "Health_Potion_Small",
                            Position = new float3(-1000, 10, -500)
                        }
                    }
                };
                
                // Register with WorldAutomationService
                WorldAutomationService.AddRule("ConditionalZone1", conditionalRule);
                
                // Configure special glow for low health
                var lowHealthGlowConfig = new ZoneGlowConfig
                {
                    ZoneId = "ConditionalZone1",
                    ZoneName = "Conditional Zone",
                    GlowType = ZoneGlowType.PlayerOnly,
                    PlayerGlowDuration = 8f, // Longer duration for low health
                    PlayerGlowColor = new float4(1.0f, 0.0f, 0.0f, 0.9f), // Bright red
                    AutoApply = true
                };
                
                _zoneGlowIntegration.UpdateZoneGlowConfig("ConditionalZone1", lowHealthGlowConfig);
                
                Plugin.Log?.LogInfo("[ZONE_GLOW_EXAMPLE] Created conditional zone with low health glow");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[ZONE_GLOW_EXAMPLE] Error creating conditional zone: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Example 3: Create PvP zone with dynamic glow based on player count
        /// </summary>
        public static void CreatePvPZoneWithDynamicGlow()
        {
            try
            {
                // Create PvP zone definition
                var pvpZone = new ZoneDefinition
                {
                    Id = "PvPZone1",
                    Name = "PvP Arena",
                    Center = new float3(-1500, 5, -1000),
                    Radius = 60f,
                    AllowedPrefabs = new List<string> { "PvP_Spawner", "PvP_Reward_Chest" },
                    Properties = new Dictionary<string, object>
                    {
                        ["glowType"] = "Both",
                        ["playerGlowDuration"] = 3f,
                        ["arenaGlowIntensity"] = 1.0f,
                        ["dynamicGlow"] = true
                    }
                };
                
                // Create zone rule with dynamic glow
                var pvpRule = new AutomationRule
                {
                    TriggerName = "PvPZoneDynamic",
                    Actions = new List<AutomationAction>
                    {
                        new AutomationAction
                        {
                            Type = AutomationActionType.TriggerEvent,
                            EventName = "OnZoneEnter",
                            Parameters = new Dictionary<string, object>
                            {
                                ["zoneId"] = "PvPZone1",
                                ["applyPvPGlow"] = true
                            }
                        },
                        new AutomationAction
                        {
                            Type = AutomationActionType.Monitor,
                            Parameters = new Dictionary<string, object>
                            {
                                ["metric"] = "playerCount",
                                ["interval"] = 1.0f
                            }
                        }
                    }
                };
                
                WorldAutomationService.AddRule("PvPZone1", pvpRule);
                
                // Register PvP zone with glow
                _zoneGlowIntegration.RegisterZoneGlow(
                    "PvPZone1",
                    pvpZone.Center,
                    pvpZone.Radius
                );
                
                Plugin.Log?.LogInfo("[ZONE_GLOW_EXAMPLE] Created PvP zone with dynamic glow effects");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[ZONE_GLOW_EXAMPLE] Error creating PvP zone: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Example 4: Load zones from settings file with glow configuration
        /// </summary>
        public static void LoadZonesFromSettingsWithGlow()
        {
            try
            {
                // Load zones from Settings.json (from your guide)
                var settings = PluginSettings.GetSettings();
                
                foreach (var zone in settings.Zones.Zones)
                {
                    // Create zone rule
                    var zoneRule = CreateZoneRuleFromSettings(zone);
                    WorldAutomationService.AddRule(zone.Id, zoneRule);
                    
                    // Configure glow based on zone properties
                    var glowConfig = CreateGlowConfigFromZoneProperties(zone);
                    _zoneGlowIntegration.UpdateZoneGlowConfig(zone.Id, glowConfig);
                    
                    // Register zone glow
                    _zoneGlowIntegration.RegisterZoneGlow(
                        zone.Id,
                        zone.Center,
                        zone.Radius,
                        glowConfig
                    );
                    
                    Plugin.Log?.LogInfo($"[ZONE_GLOW_EXAMPLE] Loaded zone with glow: {zone.Name}");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[ZONE_GLOW_EXAMPLE] Error loading zones from settings: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Handle zone state changes with glow effects
        /// </summary>
        public static void HandleZoneStateChange(string zoneId, ZoneState zoneState)
        {
            try
            {
                // Update zone state (from your guide)
                WorldAutomationService.UpdateZoneState(zoneId, zoneState);
                
                // Update glow effects based on zone state
                _zoneGlowIntegration.UpdateZoneStateWithGlow(zoneId, zoneState);
                
                // Log zone state change with glow information
                Plugin.Log?.LogInfo($"[ZONE_GLOW_EXAMPLE] Zone state updated: {zoneId} - Active: {zoneState.IsActive}, Players: {zoneState.PlayerCount}");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[ZONE_GLOW_EXAMPLE] Error handling zone state change: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Load arena schematic (from your guide)
        /// </summary>
        private static SchematicData LoadArenaSchematic()
        {
            try
            {
                // Configure JSON options with schematic converters (from your guide)
                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    Converters = 
                    {
                        new SchematicFloat3Converter(),
                        new AabbConverter()
                    }
                };
                
                // Load schematic file
                var schematicJson = File.ReadAllText("arena_schematic.json");
                var schematic = JsonSerializer.Deserialize<SchematicData>(schematicJson, jsonOptions);
                
                return schematic;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[ZONE_GLOW_EXAMPLE] Error loading arena schematic: {ex.Message}");
                return GetDefaultArenaSchematic();
            }
        }
        
        /// <summary>
        /// Get default arena schematic
        /// </summary>
        private static SchematicData GetDefaultArenaSchematic()
        {
            return new SchematicData
            {
                Name = "Default Arena Layout",
                Bounds = new Aabb 
                {
                    Min = new float3(-1100, 0, -600),
                    Max = new float3(-900, 20, -400)
                },
                Objects = new List<SchematicObject>
                {
                    new SchematicObject
                    {
                        PrefabName = "Castle_Basic_T1_C",
                        Position = new float3(-1000, 5, -500),
                        Rotation = quaternion.identity,
                        Scale = new float3(1, 1, 1),
                        Properties = new Dictionary<string, object>
                        {
                            ["teamId"] = 1,
                            ["health"] = 1000
                        }
                    },
                    new SchematicObject
                    {
                        PrefabName = "Arena_Spawner",
                        Position = new float3(-1000, 10, -500),
                        Rotation = quaternion.identity,
                        Properties = new Dictionary<string, object>
                        {
                            ["spawnType"] = "player",
                            ["respawnTime"] = 5f
                        }
                    }
                }
            };
        }
        
        /// <summary>
        /// Create zone rule with glow effects
        /// </summary>
        private static AutomationRule CreateZoneRuleWithGlow(ZoneDefinition zone, SchematicData schematic)
        {
            var rule = new AutomationRule
            {
                TriggerName = $"ZoneWithGlow_{zone.Id}",
                Actions = new List<AutomationAction>()
            };
            
            // Add schematic objects to zone actions
            foreach (var obj in schematic.Objects)
            {
                rule.Actions.Add(new AutomationAction
                {
                    Type = AutomationActionType.SpawnObject,
                    PrefabName = obj.PrefabName,
                    Position = obj.Position,
                    Rotation = obj.Rotation,
                    Scale = obj.Scale,
                    Parameters = obj.Properties
                });
            }
            
            // Add glow trigger action
            rule.Actions.Add(new AutomationAction
            {
                Type = AutomationActionType.TriggerEvent,
                EventName = "OnZoneEnter",
                Parameters = new Dictionary<string, object>
                {
                    ["zoneId"] = zone.Id,
                    ["applyGlow"] = true,
                    ["glowConfig"] = zone.Properties.GetValueOrDefault("glowType", "Both")
                }
            });
            
            return rule;
        }
        
        /// <summary>
        /// Create zone rule from settings
        /// </summary>
        private static AutomationRule CreateZoneRuleFromSettings(ZoneDefinition zone)
        {
            return new AutomationRule
            {
                TriggerName = $"ZoneFromSettings_{zone.Id}",
                Actions = new List<AutomationAction>
                {
                    new AutomationAction
                    {
                        Type = AutomationActionType.TriggerEvent,
                        EventName = "OnZoneEnter",
                        Parameters = new Dictionary<string, object>
                        {
                            ["zoneId"] = zone.Id,
                            ["applyGlow"] = zone.Properties.GetValueOrDefault("glowEnabled", false)
                        }
                    }
                }
            };
        }
        
        /// <summary>
        /// Create glow config from zone properties
        /// </summary>
        private static ZoneGlowConfig CreateGlowConfigFromZoneProperties(ZoneDefinition zone)
        {
            var glowType = zone.Properties.GetValueOrDefault("glowType", "Both");
            var duration = Convert.ToSingle(zone.Properties.GetValueOrDefault("playerGlowDuration", 5f));
            var colorString = zone.Properties.GetValueOrDefault("arenaGlowColor", "1,0,0,0.8");
            var color = ParseColor(colorString);
            
            return new ZoneGlowConfig
            {
                ZoneId = zone.Id,
                ZoneName = zone.Name,
                GlowType = Enum.Parse<ZoneGlowType>(glowType),
                PlayerGlowDuration = duration,
                PlayerGlowColor = color,
                ArenaGlowColor = color,
                AutoApply = zone.Properties.GetValueOrDefault("glowEnabled", true)
            };
        }
        
        /// <summary>
        /// Parse color string to float4
        /// </summary>
        private static float4 ParseColor(string colorString)
        {
            try
            {
                var parts = colorString.Split(',');
                if (parts.Length >= 4)
                {
                    return new float4(
                        float.Parse(parts[0]),
                        float.Parse(parts[1]),
                        float.Parse(parts[2]),
                        float.Parse(parts[3])
                    );
                }
            }
            catch
            {
                Plugin.Log?.LogWarning($"[ZONE_GLOW_EXAMPLE] Invalid color format: {colorString}, using default");
            }
            
            return new float4(1.0f, 0.0f, 0.0f, 0.8f);
        }
        
        /// <summary>
        /// Cleanup resources
        /// </summary>
        public static void Cleanup()
        {
            try
            {
                _zoneGlowIntegration?.Cleanup();
                _glowManager?.Shutdown();
                Plugin.Log?.LogInfo("[ZONE_GLOW_EXAMPLE] Zone glow example cleanup complete");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[ZONE_GLOW_EXAMPLE] Error during cleanup: {ex.Message}");
            }
        }
    }
}
