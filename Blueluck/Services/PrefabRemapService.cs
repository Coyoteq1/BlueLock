using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using Stunlock.Core;
using System.Text.Json;
using System.Linq;
using System.Text.Json.Serialization;
using VAuto.Services.Interfaces;

namespace Blueluck.Services
{
    /// <summary>
    /// ECS-based service for prefab name to GUID mapping and remapping.
    /// </summary>
    public class PrefabRemapService : IService
    {
        private static readonly ManualLogSource _log = Logger.CreateLogSource("Blueluck.PrefabRemap");
        
        public bool IsInitialized { get; private set; }
        public ManualLogSource Log => _log;

        private readonly Dictionary<string, PrefabGUID> _nameToGuid = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, string> _guidToName = new();
        private readonly Dictionary<string, string> _aliases = new(StringComparer.OrdinalIgnoreCase);
        private string _configPath;

        public void Initialize()
        {
            _configPath = Path.Combine(Paths.ConfigPath, "Blueluck", "prefab_remap.json");
            Directory.CreateDirectory(Path.GetDirectoryName(_configPath) ?? Paths.ConfigPath);

            LoadRemapConfig();
            RegisterCorePrefabs();
            
            IsInitialized = true;
            _log.LogInfo("[PrefabRemap] Initialized with core prefabs and custom remaps");
        }

        public void Cleanup()
        {
            _nameToGuid.Clear();
            _guidToName.Clear();
            _aliases.Clear();
            IsInitialized = false;
            _log.LogInfo("[PrefabRemap] Cleaned up");
        }

        /// <summary>
        /// Gets PrefabGUID by name or alias.
        /// </summary>
        public bool TryGetGuid(string name, out PrefabGUID guid)
        {
            guid = default;
            
            // Check direct name
            if (_nameToGuid.TryGetValue(name, out guid))
                return true;
            
            // Check aliases
            if (_aliases.TryGetValue(name, out var aliasName) && _nameToGuid.TryGetValue(aliasName, out guid))
                return true;
            
            return false;
        }

        /// <summary>
        /// Gets prefab name by GUID hash.
        /// </summary>
        public bool TryGetName(int guidHash, out string name)
        {
            return _guidToName.TryGetValue(guidHash, out name);
        }

        /// <summary>
        /// Gets prefab name by PrefabGUID.
        /// </summary>
        public bool TryGetName(PrefabGUID guid, out string name)
        {
            return TryGetName(guid.GetHashCode(), out name);
        }

        /// <summary>
        /// Adds or updates a prefab mapping.
        /// </summary>
        public void AddMapping(string name, PrefabGUID guid)
        {
            _nameToGuid[name] = guid;
            _guidToName[guid.GetHashCode()] = name;
        }

        /// <summary>
        /// Adds an alias for an existing prefab name.
        /// </summary>
        public void AddAlias(string alias, string targetName)
        {
            _aliases[alias] = targetName;
        }

        /// <summary>
        /// Registers core V Rising prefabs.
        /// </summary>
        private void RegisterCorePrefabs()
        {
            // Boss prefabs
            AddMapping("CHAR_Gloomrot_Purifier_VBlood", new PrefabGUID(2075390218));
            AddMapping("CHAR_Bandit_Bomber_VBlood", new PrefabGUID(-2048180340));
            AddMapping("CHAR_ArchMage_VBlood", new PrefabGUID(-88630604));
            
            // Ability prefabs
            AddMapping("AB_Fireball_Cast", new PrefabGUID(1106195644));
            AddMapping("AB_IceBolt_Cast", new PrefabGUID(-1423243724));
            AddMapping("AB_Lightning_Cast", new PrefabGUID(826214455));
            
            // Buff prefabs
            AddMapping("Buff_PvP_Enabled", new PrefabGUID(123456789));
            AddMapping("Buff_Glow_Red", new PrefabGUID(-123456789));
            AddMapping("Buff_Glow_Purple", new PrefabGUID(-987654321));
            
            // Common aliases
            AddAlias("dracula", "CHAR_Gloomrot_Purifier_VBlood");
            AddAlias("fireball", "AB_Fireball_Cast");
            AddAlias("icebolt", "AB_IceBolt_Cast");
            AddAlias("lightning", "AB_Lightning_Cast");
            AddAlias("pvp", "Buff_PvP_Enabled");
            AddAlias("redglow", "Buff_Glow_Red");
            AddAlias("purpleglow", "Buff_Glow_Purple");
        }

        /// <summary>
        /// Loads remap configuration from JSON file.
        /// </summary>
        private void LoadRemapConfig()
        {
            try
            {
                if (!File.Exists(_configPath))
                {
                    CreateDefaultRemapConfig();
                    return;
                }

                var json = File.ReadAllText(_configPath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                };

                var config = JsonSerializer.Deserialize<PrefabRemapConfig>(json, options);
                if (config != null)
                {
                    // Apply custom mappings
                    foreach (var mapping in config.Mappings)
                    {
                        if (int.TryParse(mapping.GuidHash, out var guidHash))
                        {
                            AddMapping(mapping.Name, new PrefabGUID(guidHash));
                        }
                    }

                    // Apply aliases
                    foreach (var alias in config.Aliases)
                    {
                        AddAlias(alias.Alias, alias.TargetName);
                    }
                }

                _log.LogInfo($"[PrefabRemap] Loaded {config?.Mappings.Count ?? 0} custom mappings and {config?.Aliases.Count ?? 0} aliases");
            }
            catch (Exception ex)
            {
                _log.LogError($"[PrefabRemap] Failed to load remap config: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates default remap configuration file.
        /// </summary>
        private void CreateDefaultRemapConfig()
        {
            var defaultConfig = new PrefabRemapConfig
            {
                Mappings = new List<PrefabMapping>
                {
                    new() { Name = "Custom_Boss", GuidHash = "1234567890" },
                    new() { Name = "Custom_Ability", GuidHash = "987654321" }
                },
                Aliases = new List<PrefabAlias>
                {
                    new() { Alias = "customboss", TargetName = "Custom_Boss" },
                    new() { Alias = "customability", TargetName = "Custom_Ability" }
                }
            };

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(defaultConfig, options);
            File.WriteAllText(_configPath, json);

            _log.LogInfo($"[PrefabRemap] Created default remap config at {_configPath}");
        }
    }

    /// <summary>
    /// Prefab remap configuration model.
    /// </summary>
    public class PrefabRemapConfig
    {
        [JsonPropertyName("mappings")]
        public List<PrefabMapping> Mappings { get; set; } = new();

        [JsonPropertyName("aliases")]
        public List<PrefabAlias> Aliases { get; set; } = new();
    }

    /// <summary>
    /// Single prefab mapping.
    /// </summary>
    public class PrefabMapping
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("guidHash")]
        public string GuidHash { get; set; } = string.Empty;
    }

    /// <summary>
    /// Prefab name alias.
    /// </summary>
    public class PrefabAlias
    {
        [JsonPropertyName("alias")]
        public string Alias { get; set; } = string.Empty;

        [JsonPropertyName("targetName")]
        public string TargetName { get; set; } = string.Empty;
    }
}
