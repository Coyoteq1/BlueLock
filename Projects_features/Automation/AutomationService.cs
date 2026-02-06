using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using VAuto.Core.Components;

namespace VAuto.Core.Services
{
    /// <summary>
    /// Service for managing automation rules loaded from JSON config.
    /// </summary>
    public class AutomationService
    {
        private static AutomationService _instance;
        private static readonly object _lock = new object();
        
        private readonly string _configPath;
        private readonly Dictionary<string, AutomationRuleConfig> _rulesById = new();
        private readonly Dictionary<int, List<AutomationRuleConfig>> _rulesByContainerPrefab = new();
        private bool _initialized = false;
        
        /// <summary>
        /// Gets the singleton instance.
        /// </summary>
        public static AutomationService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new AutomationService();
                        }
                    }
                }
                return _instance;
            }
        }
        
        private AutomationService()
        {
            _configPath = Path.Combine(BepInEx.Paths.ConfigPath, "VAuto", "automation_rules.json");
        }
        
        /// <summary>
        /// Initializes the automation service by loading rules from JSON.
        /// </summary>
        public bool Initialize()
        {
            try
            {
                if (_initialized)
                {
                    Plugin.Log.LogInfo("[AutomationService] Already initialized");
                    return true;
                }
                
                Plugin.Log.LogInfo($"[AutomationService] Initializing from {_configPath}");
                
                // Ensure config directory exists
                var configDir = Path.GetDirectoryName(_configPath);
                if (!string.IsNullOrEmpty(configDir) && !Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }
                
                // Load existing rules or create default
                if (File.Exists(_configPath))
                {
                    LoadRules();
                }
                else
                {
                    CreateDefaultConfig();
                }
                
                _initialized = true;
                Plugin.Log.LogInfo($"[AutomationService] Loaded {_rulesById.Count} automation rules");
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[AutomationService] Initialization failed: {ex.Message}");
                return false;
            }
        }
        
        private void LoadRules()
        {
            try
            {
                var json = File.ReadAllText(_configPath);
                var config = JsonSerializer.Deserialize<AutomationConfig>(json);
                
                if (config?.rules == null)
                {
                    Plugin.Log.LogWarning("[AutomationService] No rules found in config");
                    return;
                }
                
                foreach (var rule in config.rules)
                {
                    if (string.IsNullOrEmpty(rule.id))
                    {
                        rule.id = Guid.NewGuid().ToString("N")[..8];
                    }
                    
                    _rulesById[rule.id] = rule;
                    
                    // Index by container prefab ID
                    if (!_rulesByContainerPrefab.ContainsKey(rule.containerPrefabId))
                    {
                        _rulesByContainerPrefab[rule.containerPrefabId] = new List<AutomationRuleConfig>();
                    }
                    _rulesByContainerPrefab[rule.containerPrefabId].Add(rule);
                }
                
                Plugin.Log.LogInfo($"[AutomationService] Loaded {_rulesById.Count} rules");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[AutomationService] Failed to load rules: {ex.Message}");
            }
        }
        
        private void CreateDefaultConfig()
        {
            var defaultRules = new AutomationConfig
            {
                version = "1.0",
                rules = new List<AutomationRuleConfig>
                {
                    new AutomationRuleConfig
                    {
                        id = "arena_chest_boss",
                        name = "Arena Chest Boss Spawn",
                        containerPrefabId = 1001, // Example chest prefab ID
                        enabled = true,
                        maxUses = 1,
                        cooldownSeconds = 300f,
                        arenaOnly = true,
                        actions = new List<AutomationActionConfig>
                        {
                            new AutomationActionConfig
                            {
                                type = "SpawnBoss",
                                intParam = 1, // Boss prefab ID
                                stringParam = "AlphaWolf",
                                positionOffset = new float[] { 0, 0, 5 }
                            },
                            new AutomationActionConfig
                            {
                                type = "VisualEffect",
                                intParam = 100,
                                positionOffset = new float[] { 0, 0, 0 }
                            }
                        }
                    }
                }
            };
            
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            var json = JsonSerializer.Serialize(defaultRules, options);
            File.WriteAllText(_configPath, json);
            
            Plugin.Log.LogInfo($"[AutomationService] Created default config at {_configPath}");
            
            // Reload the config
            LoadRules();
        }
        
        /// <summary>
        /// Gets all rules for a specific container prefab ID.
        /// </summary>
        public List<AutomationRuleConfig> GetRulesForContainer(int containerPrefabId)
        {
            return _rulesByContainerPrefab.TryGetValue(containerPrefabId, out var rules) 
                ? rules 
                : new List<AutomationRuleConfig>();
        }
        
        /// <summary>
        /// Gets a specific rule by ID.
        /// </summary>
        public AutomationRuleConfig GetRule(string ruleId)
        {
            return _rulesById.TryGetValue(ruleId, out var rule) ? rule : null;
        }
        
        /// <summary>
        /// Checks if any rules exist for the given container prefab.
        /// </summary>
        public bool HasRules(int containerPrefabId)
        {
            return _rulesByContainerPrefab.ContainsKey(containerPrefabId);
        }
        
        /// <summary>
        /// Reloads rules from disk.
        /// </summary>
        public void Reload()
        {
            _rulesById.Clear();
            _rulesByContainerPrefab.Clear();
            _initialized = false;
            Initialize();
        }
    }
    
    /// <summary>
    /// Root configuration for automation rules.
    /// </summary>
    [System.Serializable]
    public class AutomationConfig
    {
        public string version;
        public List<AutomationRuleConfig> rules;
    }
}
