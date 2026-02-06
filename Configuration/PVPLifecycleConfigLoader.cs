using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using BepInEx.Logging;
using VAuto.Core.Lifecycle;
using VAuto.Services.Interfaces;

namespace VAuto.Core.Configuration
{
    /// <summary>
    /// PVP Item Configuration Loader - Handles loading and validation of pvp_item.json
    /// Based on the JSON schema specification from the PowerPoint documentation
    /// </summary>
    public class PVPLifecycleConfigLoader : IService
    {
        private static readonly string _logPrefix = "[PVPLifecycleConfigLoader]";
        
        public bool IsInitialized { get; private set; }
        public ManualLogSource Log { get; private set; }

        private readonly Dictionary<string, PVPItemConfig> _loadedConfigs;

        public PVPLifecycleConfigLoader()
        {
            Log = Plugin.Log;
            _loadedConfigs = new Dictionary<string, PVPItemConfig>();
        }

        public void Initialize()
        {
            try
            {
                if (IsInitialized) return;
                
                Log?.LogInfo($"{_logPrefix} PVP Lifecycle Config Loader initialized");
                IsInitialized = true;
            }
            catch (Exception ex)
            {
                Log?.LogError($"{_logPrefix} Failed to initialize: {ex.Message}");
            }
        }

        public void Shutdown()
        {
            if (!IsInitialized) return;
            
            _loadedConfigs.Clear();
            Log?.LogInfo($"{_logPrefix} PVP Lifecycle Config Loader shutdown");
            IsInitialized = false;
        }

        public void Cleanup()
        {
            if (!IsInitialized) return;
            
            Shutdown();
        }

        /// <summary>
        /// Loads a PVP item configuration from JSON file
        /// </summary>
        /// <param name="configPath">Path to pvp_item.json file</param>
        /// <returns>True if loaded successfully, false otherwise</returns>
        public bool LoadConfig(string configPath)
        {
            try
            {
                if (!File.Exists(configPath))
                {
                    Log?.LogError($"{_logPrefix} Configuration file not found: {configPath}");
                    return false;
                }

                string jsonContent = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<PVPItemConfig>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                });

                if (config == null)
                {
                    Log?.LogError($"{_logPrefix} Failed to deserialize configuration from: {configPath}");
                    return false;
                }

                // Validate the configuration
                if (!ValidateConfig(config))
                {
                    Log?.LogError($"{_logPrefix} Configuration validation failed for: {configPath}");
                    return false;
                }

                string configKey = Path.GetFileNameWithoutExtension(configPath);
                _loadedConfigs[configKey] = config;

                Log?.LogInfo($"{_logPrefix} Successfully loaded configuration: {configKey}");
                return true;
            }
            catch (Exception ex)
            {
                Log?.LogError($"{_logPrefix} Failed to load configuration from {configPath}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets a loaded configuration by key
        /// </summary>
        /// <param name="configKey">Configuration key</param>
        /// <returns>The configuration or null if not found</returns>
        public PVPItemConfig GetConfig(string configKey)
        {
            _loadedConfigs.TryGetValue(configKey, out var config);
            return config;
        }

        /// <summary>
        /// Validates PVP item configuration against the schema
        /// </summary>
        private bool ValidateConfig(PVPItemConfig config)
        {
            try
            {
                // Validate onUse actions
                if (config.OnUse != null)
                {
                    if (config.OnUse.Actions == null || config.OnUse.Actions.Count == 0)
                    {
                        Log?.LogWarning($"{_logPrefix} onUse stage has no actions defined");
                    }
                    else
                    {
                        foreach (var action in config.OnUse.Actions)
                        {
                            if (!ValidateAction(action))
                                return false;
                        }
                    }
                }

                // Validate onEnterArenaZone actions
                if (config.OnEnterArenaZone != null)
                {
                    if (config.OnEnterArenaZone.Actions == null || config.OnEnterArenaZone.Actions.Count == 0)
                    {
                        Log?.LogWarning($"{_logPrefix} onEnterArenaZone stage has no actions defined");
                    }
                    else
                    {
                        foreach (var action in config.OnEnterArenaZone.Actions)
                        {
                            if (!ValidateAction(action))
                                return false;
                        }
                    }
                }

                // Validate onExitArenaZone actions
                if (config.OnExitArenaZone != null)
                {
                    if (config.OnExitArenaZone.Actions == null || config.OnExitArenaZone.Actions.Count == 0)
                    {
                        Log?.LogWarning($"{_logPrefix} onExitArenaZone stage has no actions defined");
                    }
                    else
                    {
                        foreach (var action in config.OnExitArenaZone.Actions)
                        {
                            if (!ValidateAction(action))
                                return false;
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Log?.LogError($"{_logPrefix} Configuration validation failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Validates a single lifecycle action
        /// </summary>
        private bool ValidateAction(LifecycleAction action)
        {
            if (string.IsNullOrEmpty(action.Type))
            {
                Log?.LogError($"{_logPrefix} Action type is required");
                return false;
            }

            // Validate required fields based on action type
            switch (action.Type.ToLower())
            {
                case "store":
                    if (string.IsNullOrEmpty(action.StoreKey))
                    {
                        Log?.LogError($"{_logPrefix} Store action requires storeKey");
                        return false;
                    }
                    break;

                case "message":
                    if (string.IsNullOrEmpty(action.Message))
                    {
                        Log?.LogError($"{_logPrefix} Message action requires message");
                        return false;
                    }
                    break;

                case "command":
                    if (string.IsNullOrEmpty(action.CommandId))
                    {
                        Log?.LogError($"{_logPrefix} Command action requires commandId");
                        return false;
                    }
                    break;

                case "config":
                    if (string.IsNullOrEmpty(action.ConfigId))
                    {
                        Log?.LogError($"{_logPrefix} Config action requires configId");
                        return false;
                    }
                    break;

                case "zone":
                    if (string.IsNullOrEmpty(action.ZoneKey))
                    {
                        Log?.LogError($"{_logPrefix} Zone action requires zoneKey");
                        return false;
                    }
                    break;

                case "prefix":
                    if (string.IsNullOrEmpty(action.Prefix))
                    {
                        Log?.LogError($"{_logPrefix} Prefix action requires prefix");
                        return false;
                    }
                    break;

                case "blood":
                    if (string.IsNullOrEmpty(action.BloodType))
                    {
                        Log?.LogError($"{_logPrefix} Blood action requires bloodType");
                        return false;
                    }
                    break;

                case "quality":
                    // Quality is optional, defaults to 0
                    break;

                default:
                    Log?.LogWarning($"{_logPrefix} Unknown action type: {action.Type}");
                    break;
            }

            return true;
        }

        /// <summary>
        /// Creates a sample pvp_item.json configuration
        /// </summary>
        public void CreateSampleConfig(string outputPath)
        {
            try
            {
                var sampleConfig = new PVPItemConfig
                {
                    OnUse = new LifecycleStageConfig
                    {
                        Actions = new List<LifecycleAction>
                        {
                            new LifecycleAction
                            {
                                Type = "store",
                                StoreKey = "playerState",
                                Message = "active"
                            },
                            new LifecycleAction
                            {
                                Type = "message",
                                Message = "PVP Item activated!"
                            },
                            new LifecycleAction
                            {
                                Type = "zone",
                                ZoneKey = "arena_zone_1"
                            },
                            new LifecycleAction
                            {
                                Type = "blood",
                                BloodType = "warrior"
                            }
                        }
                    },
                    OnEnterArenaZone = new LifecycleStageConfig
                    {
                        Actions = new List<LifecycleAction>
                        {
                            new LifecycleAction
                            {
                                Type = "message",
                                Message = "You have entered the arena zone!"
                            },
                            new LifecycleAction
                            {
                                Type = "store",
                                StoreKey = "arenaState",
                                Message = "inArena"
                            },
                            new LifecycleAction
                            {
                                Type = "prefix",
                                Prefix = "[PVP]"
                            }
                        }
                    },
                    OnExitArenaZone = new LifecycleStageConfig
                    {
                        Actions = new List<LifecycleAction>
                        {
                            new LifecycleAction
                            {
                                Type = "message",
                                Message = "You have left the arena zone"
                            },
                            new LifecycleAction
                            {
                                Type = "store",
                                StoreKey = "arenaState",
                                Message = "outOfArena"
                            },
                            new LifecycleAction
                            {
                                Type = "command",
                                CommandId = "cleanup_arena_effects"
                            }
                        }
                    }
                };

                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                string json = JsonSerializer.Serialize(sampleConfig, jsonOptions);
                File.WriteAllText(outputPath, json);

                Log?.LogInfo($"{_logPrefix} Sample configuration created at: {outputPath}");
            }
            catch (Exception ex)
            {
                Log?.LogError($"{_logPrefix} Failed to create sample configuration: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// PVP Item Configuration structure matching to JSON schema
    /// </summary>
    public class PVPItemConfig
    {
        [JsonPropertyName("onUse")]
        public LifecycleStageConfig OnUse { get; set; }

        [JsonPropertyName("onEnterArenaZone")]
        public LifecycleStageConfig OnEnterArenaZone { get; set; }

        [JsonPropertyName("onExitArenaZone")]
        public LifecycleStageConfig OnExitArenaZone { get; set; }
    }

    /// <summary>
    /// Lifecycle stage configuration
    /// </summary>
    public class LifecycleStageConfig
    {
        [JsonPropertyName("actions")]
        public List<LifecycleAction> Actions { get; set; }
    }
}
