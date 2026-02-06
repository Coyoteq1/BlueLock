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
        private static readonly string _defaultConfigBase = Path.Combine(BepInEx.Paths.ConfigPath, "VAuto", "pvp_item");
        
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
                EnsureDefaultConfig();
                LoadConfig(_defaultConfigBase);
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
                // TOML is strict default. If a matching .toml exists, prefer it.
                var resolvedPath = ResolvePreferredConfigPath(configPath);
                if (resolvedPath.EndsWith(".toml", StringComparison.OrdinalIgnoreCase))
                {
                    return LoadTomlConfig(resolvedPath);
                }

                if (!File.Exists(resolvedPath))
                {
                    Log?.LogError($"{_logPrefix} Configuration file not found: {resolvedPath}");
                    return false;
                }

                string jsonContent = File.ReadAllText(resolvedPath);
                var config = JsonSerializer.Deserialize<PVPItemConfig>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = false,
                    ReadCommentHandling = JsonCommentHandling.Disallow,
                    AllowTrailingCommas = false
                });

                if (config == null)
                {
                    Log?.LogError($"{_logPrefix} Failed to deserialize configuration from: {configPath}");
                    return false;
                }

                // Validate the configuration
                if (!ValidateConfig(config))
                {
                    Log?.LogError($"{_logPrefix} Configuration validation failed for: {resolvedPath}");
                    return false;
                }

                string configKey = Path.GetFileNameWithoutExtension(resolvedPath);
                _loadedConfigs[configKey] = config;

                Log?.LogInfo($"{_logPrefix} Successfully loaded configuration: {configKey}");

                // One-time migration: if we loaded JSON and TOML doesn't exist, write TOML next to it.
                TryMigrateJsonToToml(resolvedPath);
                return true;
            }
            catch (Exception ex)
            {
                Log?.LogError($"{_logPrefix} Failed to load configuration from {configPath}: {ex.Message}");
                return false;
            }
        }

        private string ResolvePreferredConfigPath(string configPath)
        {
            if (configPath.EndsWith(".toml", StringComparison.OrdinalIgnoreCase))
                return configPath;

            if (configPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                var candidate = Path.ChangeExtension(configPath, ".toml");
                if (File.Exists(candidate)) return candidate;
                return configPath;
            }

            // If caller provides no extension, check TOML then JSON.
            var toml = configPath + ".toml";
            if (File.Exists(toml)) return toml;
            var json = configPath + ".json";
            if (File.Exists(json)) return json;

            return configPath;
        }

        private bool LoadTomlConfig(string tomlPath)
        {
            try
            {
                if (!File.Exists(tomlPath))
                {
                    Log?.LogError($"{_logPrefix} TOML config not found: {tomlPath}");
                    return false;
                }

                var toml = File.ReadAllText(tomlPath);
                var parsed = SimpleToml.Parse(toml);
                var config = ConvertTomlToConfig(parsed);

                if (config == null)
                {
                    Log?.LogError($"{_logPrefix} Failed to parse TOML: {tomlPath}");
                    return false;
                }

                if (!ValidateConfig(config))
                {
                    Log?.LogError($"{_logPrefix} Configuration validation failed for: {tomlPath}");
                    return false;
                }

                string configKey = Path.GetFileNameWithoutExtension(tomlPath);
                _loadedConfigs[configKey] = config;

                Log?.LogInfo($"{_logPrefix} Successfully loaded TOML configuration: {configKey}");
                return true;
            }
            catch (Exception ex)
            {
                Log?.LogError($"{_logPrefix} Failed to load TOML configuration from {tomlPath}: {ex.Message}");
                return false;
            }
        }

        private static PVPItemConfig ConvertTomlToConfig(Dictionary<string, object> parsed)
        {
            var cfg = new PVPItemConfig();
            cfg.OnUse = TryReadStage(parsed, "onUse");
            cfg.OnEnterArenaZone = TryReadStage(parsed, "onEnterArenaZone");
            cfg.OnExitArenaZone = TryReadStage(parsed, "onExitArenaZone");
            return cfg;
        }

        private static LifecycleStageConfig TryReadStage(Dictionary<string, object> root, string stageKey)
        {
            if (!root.TryGetValue(stageKey, out var stageObj) || stageObj is not Dictionary<string, object> stageTable)
                return null;

            if (!stageTable.TryGetValue("actions", out var actionsObj) || actionsObj is not List<Dictionary<string, object>> actionsList)
                return new LifecycleStageConfig { Actions = new List<LifecycleAction>() };

            var actions = new List<LifecycleAction>();
            foreach (var a in actionsList)
            {
                if (!a.TryGetValue("type", out var tObj) || tObj is not string t || string.IsNullOrWhiteSpace(t))
                    continue;

                var act = new LifecycleAction { Type = t };
                if (a.TryGetValue("message", out var msg)) act.Message = msg as string;
                if (a.TryGetValue("storeKey", out var storeKey)) act.StoreKey = storeKey as string;
                if (a.TryGetValue("zoneKey", out var zoneKey)) act.ZoneKey = zoneKey as string;
                if (a.TryGetValue("bloodType", out var bloodType)) act.BloodType = bloodType as string;
                if (a.TryGetValue("commandId", out var commandId)) act.CommandId = commandId as string;
                if (a.TryGetValue("prefix", out var prefix)) act.Prefix = prefix as string;
                if (a.TryGetValue("quality", out var quality) && quality != null) act.Quality = Convert.ToInt32(quality);

                actions.Add(act);
            }

            return new LifecycleStageConfig { Actions = actions };
        }

        private void TryMigrateJsonToToml(string jsonPath)
        {
            try
            {
                if (!jsonPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    return;

                var tomlPath = Path.ChangeExtension(jsonPath, ".toml");
                if (File.Exists(tomlPath))
                    return;

                var configKey = Path.GetFileNameWithoutExtension(jsonPath);
                if (!_loadedConfigs.TryGetValue(configKey, out var cfg) || cfg == null)
                    return;

                var model = new Dictionary<string, object>(StringComparer.Ordinal);
                AddStage(model, "onUse", cfg.OnUse);
                AddStage(model, "onEnterArenaZone", cfg.OnEnterArenaZone);
                AddStage(model, "onExitArenaZone", cfg.OnExitArenaZone);

                var toml = SimpleToml.SerializePvpItem(model);
                File.WriteAllText(tomlPath, toml);
                Log?.LogInfo($"{_logPrefix} Migrated JSON to TOML: {tomlPath}");
            }
            catch
            {
                // ignore migration failures
            }
        }

        private static void AddStage(Dictionary<string, object> root, string stageKey, LifecycleStageConfig stage)
        {
            if (stage == null) return;
            var stageTable = new Dictionary<string, object>(StringComparer.Ordinal);
            var actionsList = new List<Dictionary<string, object>>();

            if (stage.Actions != null)
            {
                foreach (var a in stage.Actions)
                {
                    if (a == null || string.IsNullOrWhiteSpace(a.Type)) continue;
                    var dict = new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["type"] = a.Type
                    };

                    if (a.Message != null) dict["message"] = a.Message;
                    if (a.StoreKey != null) dict["storeKey"] = a.StoreKey;
                    if (a.ZoneKey != null) dict["zoneKey"] = a.ZoneKey;
                    if (a.BloodType != null) dict["bloodType"] = a.BloodType;
                    if (a.CommandId != null) dict["commandId"] = a.CommandId;
                    if (a.Prefix != null) dict["prefix"] = a.Prefix;
                    if (a.Quality != 0) dict["quality"] = a.Quality;

                    actionsList.Add(dict);
                }
            }

            stageTable["actions"] = actionsList;
            root[stageKey] = stageTable;
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

        private void EnsureDefaultConfig()
        {
            try
            {
                var tomlPath = _defaultConfigBase + ".toml";
                var jsonPath = _defaultConfigBase + ".json";
                if (File.Exists(tomlPath) || File.Exists(jsonPath))
                    return;

                Directory.CreateDirectory(Path.GetDirectoryName(_defaultConfigBase)!);
                CreateSampleConfig(jsonPath);
            }
            catch
            {
                // ignore
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
