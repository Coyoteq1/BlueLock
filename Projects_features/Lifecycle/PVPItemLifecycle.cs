using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Logging;
using ProjectM;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VAuto.Core;
using VAuto.Core.Patterns;
using VAuto.Services.Interfaces;
using System.Text.Json.Serialization;
using System.Text.Json;
using VAuto;

namespace VAuto.Core.Lifecycle
{
    /// <summary>
    /// PVP Item Lifecycle System - Handles event-driven actions for PVP items
    /// Based on the lifecycle specification from pvp_item.json
    /// </summary>
    public partial class PVPItemLifecycle : Singleton<PVPItemLifecycle>, IService
    {
        private static readonly string _logPrefix = "[PVPItemLifecycle]";
        
        // Configuration paths
        protected static readonly string CONFIG_PATH = Path.Combine(BepInEx.Paths.ConfigPath, MyPluginInfo.Lifecycle.Name);
        protected static readonly string LIFECYCLE_CONFIG_PATH = Path.Combine(CONFIG_PATH, "lifecycle.json");
        
        public new bool IsInitialized { get; private set; }
        public ManualLogSource Log { get; private set; }

        // Lifecycle action handlers
        private readonly Dictionary<string, LifecycleActionHandler> _actionHandlers;
        private readonly Dictionary<string, LifecycleStage> _lifecycleStages;

        public PVPItemLifecycle()
        {
            Log = Plugin.Log;
            _actionHandlers = new Dictionary<string, LifecycleActionHandler>();
            _lifecycleStages = new Dictionary<string, LifecycleStage>();
            
            InitializeActionHandlers();
        }

        public void Initialize()
        {
            try
            {
                if (IsInitialized) return;
                
                RegisterLifecycleStages();
                Log?.LogInfo($"{_logPrefix} PVP Item Lifecycle system initialized");
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
            
            _actionHandlers.Clear();
            _lifecycleStages.Clear();
            Log?.LogInfo($"{_logPrefix} PVP Item Lifecycle system shutdown");
            IsInitialized = false;
        }

        public void Cleanup()
        {
            if (!IsInitialized) return;
            
            Shutdown();
        }

        /// <summary>
        /// Triggers a lifecycle stage with context data
        /// </summary>
        /// <param name="stageName">Name of the lifecycle stage</param>
        /// <param name="context">Context data for the action execution</param>
        /// <returns>True if all actions executed successfully</returns>
        public bool TriggerLifecycleStage(string stageName, LifecycleContext context)
        {
            try
            {
                if (!_lifecycleStages.TryGetValue(stageName, out var stage))
                {
                    Log?.LogWarning($"{_logPrefix} Unknown lifecycle stage: {stageName}");
                    return false;
                }

                Log?.LogInfo($"{_logPrefix} Triggering lifecycle stage: {stageName}");
                
                bool allSuccessful = true;
                foreach (var action in stage.Actions)
                {
                    if (!ExecuteAction(action, context))
                    {
                        allSuccessful = false;
                        Log?.LogWarning($"{_logPrefix} Action failed in stage {stageName}: {action.Type}");
                    }
                }

                return allSuccessful;
            }
            catch (Exception ex)
            {
                Log?.LogError($"{_logPrefix} Failed to trigger lifecycle stage {stageName}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Executes a single lifecycle action
        /// </summary>
        private bool ExecuteAction(LifecycleAction action, LifecycleContext context)
        {
            try
            {
                if (!_actionHandlers.TryGetValue(action.Type, out var handler))
                {
                    Log?.LogError($"{_logPrefix} No handler found for action type: {action.Type}");
                    return false;
                }

                return handler.Execute(action, context);
            }
            catch (Exception ex)
            {
                Log?.LogError($"{_logPrefix} Failed to execute action {action.Type}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Registers the default lifecycle stages
        /// </summary>
        private void RegisterLifecycleStages()
        {
            // Register onUse lifecycle stage
            _lifecycleStages["onUse"] = new LifecycleStage
            {
                Name = "onUse",
                Description = "Triggered when a player uses the PVP item",
                Actions = new List<LifecycleAction>()
            };

            // Register onEnterArenaZone lifecycle stage
            _lifecycleStages["onEnterArenaZone"] = new LifecycleStage
            {
                Name = "onEnterArenaZone",
                Description = "Triggered when a player enters an arena zone",
                Actions = new List<LifecycleAction>()
            };

            // Register onExitArenaZone lifecycle stage
            _lifecycleStages["onExitArenaZone"] = new LifecycleStage
            {
                Name = "onExitArenaZone",
                Description = "Triggered when a player leaves an arena zone",
                Actions = new List<LifecycleAction>()
            };
        }

        /// <summary>
        /// Initializes the action handlers
        /// </summary>
        private void InitializeActionHandlers()
        {
            // Register action handlers
            _actionHandlers["store"] = new StoreActionHandler();
            _actionHandlers["message"] = new MessageActionHandler();
            _actionHandlers["command"] = new CommandActionHandler();
            _actionHandlers["config"] = new ConfigActionHandler();
            _actionHandlers["zone"] = new ZoneActionHandler();
            _actionHandlers["prefix"] = new PrefixActionHandler();
            _actionHandlers["blood"] = new BloodActionHandler();
            _actionHandlers["quality"] = new QualityActionHandler();
        }

        /// <summary>
        /// Loads lifecycle configuration from JSON
        /// </summary>
        public bool LoadConfiguration(string jsonPath)
        {
            try
            {
                // TODO: Implement JSON loading logic
                Log?.LogInfo($"{_logPrefix} Configuration loading not yet implemented for: {jsonPath}");
                return true;
            }
            catch (Exception ex)
            {
                Log?.LogError($"{_logPrefix} Failed to load configuration: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Loads actions for a specific lifecycle stage
        /// </summary>
        /// <param name="stageName">Name of the lifecycle stage</param>
        /// <param name="actions">List of actions to load</param>
        public void LoadLifecycleStage(string stageName, List<LifecycleAction> actions)
        {
            try
            {
                if (!_lifecycleStages.TryGetValue(stageName, out var stage))
                {
                    Log?.LogWarning($"{_logPrefix} Unknown lifecycle stage: {stageName}");
                    return;
                }

                stage.Actions.Clear();
                stage.Actions.AddRange(actions);

                Log?.LogInfo($"{_logPrefix} Loaded {actions.Count} actions for lifecycle stage: {stageName}");
            }
            catch (Exception ex)
            {
                Log?.LogError($"{_logPrefix} Failed to load lifecycle stage {stageName}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Represents a lifecycle stage with its actions
    /// </summary>
    public class LifecycleStage
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public List<LifecycleAction> Actions { get; set; }
    }

    /// <summary>
    /// Represents a single lifecycle action
    /// </summary>
    public class LifecycleAction
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("storeKey")]
        public string StoreKey { get; set; }

        [JsonPropertyName("prefix")]
        public string Prefix { get; set; }

        [JsonPropertyName("bloodType")]
        public string BloodType { get; set; }

        [JsonPropertyName("quality")]
        public int Quality { get; set; }

        [JsonPropertyName("zoneKey")]
        public string ZoneKey { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }

        [JsonPropertyName("configId")]
        public string ConfigId { get; set; }

        [JsonPropertyName("commandId")]
        public string CommandId { get; set; }
    }

    /// <summary>
    /// Context data passed to lifecycle actions
    /// </summary>
    public class LifecycleContext
    {
        public Entity UserEntity { get; set; }
        public Entity CharacterEntity { get; set; }
        public Entity ItemEntity { get; set; }
        public string ZoneId { get; set; }
        public Dictionary<string, object> StoredData { get; set; }
        public float3 Position { get; set; }

        public LifecycleContext()
        {
            StoredData = new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Configuration methods for PVP Item Lifecycle
    /// </summary>
    public partial class PVPItemLifecycle
    {
        /// <summary>
        /// Load lifecycle configuration from JSON file
        /// </summary>
        /// <returns>True if loaded successfully, false otherwise</returns>
        public bool LoadLifecycleConfig()
        {
            try
            {
                if (!File.Exists(LIFECYCLE_CONFIG_PATH))
                {
                    Log?.LogWarning($"{_logPrefix} Lifecycle config file not found: {LIFECYCLE_CONFIG_PATH}");
                    return false;
                }

                string jsonContent = File.ReadAllText(LIFECYCLE_CONFIG_PATH);
                var config = JsonSerializer.Deserialize<PVPLifecycleConfig>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                });

                if (config?.Stages != null)
                {
                    foreach (var stage in config.Stages)
                    {
                        _lifecycleStages[stage.Name] = stage;
                    }
                    Log?.LogInfo($"{_logPrefix} Loaded {config.Stages.Count} lifecycle stages from config");
                    return true;
                }

                Log?.LogError($"{_logPrefix} Failed to deserialize lifecycle configuration");
                return false;
            }
            catch (Exception ex)
            {
                Log?.LogError($"{_logPrefix} Error loading lifecycle config: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Save current lifecycle configuration to JSON file
        /// </summary>
        /// <returns>True if saved successfully, false otherwise</returns>
        public bool SaveLifecycleConfig()
        {
            try
            {
                var config = new PVPLifecycleConfig
                {
                    Stages = _lifecycleStages.Values.ToList()
                };

                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true
                };

                string jsonContent = JsonSerializer.Serialize(config, options);
                
                // Ensure directory exists
                var directory = Path.GetDirectoryName(LIFECYCLE_CONFIG_PATH);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(LIFECYCLE_CONFIG_PATH, jsonContent);
                Log?.LogInfo($"{_logPrefix} Saved lifecycle configuration to {LIFECYCLE_CONFIG_PATH}");
                return true;
            }
            catch (Exception ex)
            {
                Log?.LogError($"{_logPrefix} Error saving lifecycle config: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get configuration file path
        /// </summary>
        /// <returns>Path to lifecycle configuration file</returns>
        public string GetConfigPath()
        {
            return LIFECYCLE_CONFIG_PATH;
        }
    }

    /// <summary>
    /// PVP Lifecycle configuration structure
    /// </summary>
    public class PVPLifecycleConfig
    {
        [JsonPropertyName("stages")]
        public List<LifecycleStage> Stages { get; set; } = new List<LifecycleStage>();

        [JsonPropertyName("defaultSettings")]
        public Dictionary<string, object> DefaultSettings { get; set; } = new Dictionary<string, object>();
    }
}
