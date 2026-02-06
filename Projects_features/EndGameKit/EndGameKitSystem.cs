using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using ProjectM;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using VAuto.Core;
using VAuto.EndGameKit.Configuration;
using VAuto.EndGameKit.Helpers;
using VAuto.EndGameKit.Services;

namespace VAuto.EndGameKit
{
    /// <summary>
    /// Main orchestrator for the EndGameKit system.
    /// 
    /// Execution Order (Non-Negotiable):
    /// 1. Validate player
    /// 2. Equip gear
    /// 3. Apply consumables
    /// 4. Attach jewels
    /// 5. Apply stat extensions
    /// 6. Mark applied
    /// 
    /// Supports hot-reload of kit configurations without server restarts.
    /// </summary>
    public class EndGameKitSystem
    {
        #region Private Fields
        
        private readonly EntityManager _entityManager;
        private ServerGameManager _serverGameManager;
        
        // Services
        private EquipmentService _equipmentService;
        private ConsumableService _consumableService;
        private JewelService _jewelService;
        private StatExtensionService _statExtensionService;
        private EndGameKitConfigService _configService;
        
        // Player state tracking
        private readonly Dictionary<Entity, PlayerKitState> _playerStates = new Dictionary<Entity, PlayerKitState>();
        private readonly HashSet<Entity> _kitAppliedPlayers = new HashSet<Entity>();
        
        // Kit profiles cache
        private readonly Dictionary<string, EndGameKitProfile> _kitProfiles = new Dictionary<string, EndGameKitProfile>();
        
        // Hot-reload monitoring
        private string _configPath;
        private DateTime _lastConfigWriteTime;
        private float _configCheckInterval = 5.0f;
        private float _configCheckTimer;
        
        private bool _initialized;
        private bool _disposed;

        #endregion

        #region Properties
        
        /// <summary>
        /// Gets the number of registered kit profiles.
        /// </summary>
        public int RegisteredKitCount => _kitProfiles.Count;

        /// <summary>
        /// Gets whether the system has been initialized.
        /// </summary>
        public bool IsInitialized => _initialized;

        #endregion

        #region Constructor
        
        /// <summary>
        /// Creates a new EndGameKitSystem instance.
        /// </summary>
        /// <param name="entityManager">The entity manager for ECS operations.</param>
        public EndGameKitSystem(EntityManager entityManager)
        {
            _entityManager = entityManager ?? throw new ArgumentNullException(nameof(entityManager));
        }

        #endregion

        #region Initialization
        
        /// <summary>
        /// Initializes the EndGameKit system with default configuration path.
        /// </summary>
        public void Initialize()
        {
            Initialize(Path.Combine(Paths.ConfigPath, "EndGameKit.json"));
        }

        /// <summary>
        /// Initializes the EndGameKit system with a custom configuration path.
        /// </summary>
        /// <param name="configPath">Path to the kit configuration JSON file.</param>
        public void Initialize(string configPath)
        {
            if (_initialized)
            {
                Plugin.Log.LogWarning("[EndGameKitSystem] Already initialized, skipping...");
                return;
            }

            Plugin.Log.LogInfo("[EndGameKitSystem] Initializing EndGameKit system...");
            
            try
            {
                // Get ServerGameManager from World
                _serverGameManager = World.GetExistingSystemManaged<ServerGameManager>();
                if (_serverGameManager == null)
                {
                    Plugin.Log.LogWarning("[EndGameKitSystem] ServerGameManager not available during init, will retry on first use");
                }

                // Initialize services
                _equipmentService = new EquipmentService(_serverGameManager, _entityManager);
                _consumableService = new ConsumableService(_serverGameManager, _entityManager);
                _jewelService = new JewelService(_entityManager);
                _statExtensionService = new StatExtensionService(_serverGameManager, _entityManager);
                
                // Initialize config service
                _configPath = configPath;
                _configService = new EndGameKitConfigService(_entityManager);
                
                // Load configuration
                LoadConfiguration();
                
                _initialized = true;
                Plugin.Log.LogInfo("[EndGameKitSystem] Initialization complete. Registered kits: " + _kitProfiles.Count);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[EndGameKitSystem] Initialization failed: {ex.Message}");
                throw;
            }
        }

        #endregion

        #region Configuration Management
        
        /// <summary>
        /// Loads or reloads the kit configuration from JSON file.
        /// Supports hot-reload without server restart.
        /// </summary>
        public bool LoadConfiguration()
        {
            try
            {
                Plugin.Log.LogInfo($"[EndGameKitSystem] Loading configuration from: {_configPath}");
                
                // Ensure default configuration exists
                if (!File.Exists(_configPath))
                {
                    Plugin.Log.LogInfo("[EndGameKitSystem] Creating default configuration...");
                    CreateDefaultConfiguration();
                }

                // Read and parse configuration
                var json = File.ReadAllText(_configPath);
                var config = JsonSerializer.Deserialize<KitConfiguration>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    WriteIndented = true
                });

                if (config == null)
                {
                    Plugin.Log.LogError("[EndGameKitSystem] Failed to deserialize configuration");
                    return false;
                }

                // Validate configuration
                var validationErrors = ValidateConfiguration(config);
                if (validationErrors.Count > 0)
                {
                    foreach (var error in validationErrors)
                    {
                        Plugin.Log.LogWarning($"[EndGameKitConfig] Validation error: {error}");
                    }
                }

                // Clear existing profiles
                _kitProfiles.Clear();

                // Register profiles from configuration
                foreach (var profile in config.Profiles)
                {
                    if (string.IsNullOrEmpty(profile.Name))
                    {
                        Plugin.Log.LogWarning("[EndGameKitSystem] Skipping profile with empty name");
                        continue;
                    }

                    _kitProfiles[profile.Name] = profile;
                    Plugin.Log.LogInfo($"[EndGameKitSystem] Registered kit profile: {profile.Name}");
                }

                _lastConfigWriteTime = File.GetLastWriteTime(_configPath);
                Plugin.Log.LogInfo($"[EndGameKitSystem] Configuration loaded. {_kitProfiles.Count} profiles registered.");
                
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[EndGameKitSystem] Failed to load configuration: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Checks for configuration file changes and reloads if necessary.
        /// Should be called periodically (e.g., in Update).
        /// </summary>
        /// <param name="deltaTime">Time since last check.</param>
        /// <returns>True if configuration was reloaded.</returns>
        public bool CheckConfigHotReload(float deltaTime)
        {
            if (!_initialized || string.IsNullOrEmpty(_configPath))
                return false;

            _configCheckTimer += deltaTime;
            if (_configCheckTimer < _configCheckInterval)
                return false;

            _configCheckTimer = 0f;

            try
            {
                if (File.Exists(_configPath))
                {
                    var currentWriteTime = File.GetLastWriteTime(_configPath);
                    if (currentWriteTime > _lastConfigWriteTime)
                    {
                        Plugin.Log.LogInfo("[EndGameKitSystem] Detected configuration change, hot-reloading...");
                        
                        if (LoadConfiguration())
                        {
                            Plugin.Log.LogInfo("[EndGameKitSystem] Hot-reload complete");
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[EndGameKitSystem] Hot-reload check failed: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Creates a default configuration file with GS91 kit.
        /// </summary>
        private void CreateDefaultConfiguration()
        {
            var defaultConfig = new KitConfiguration
            {
                Version = "1.0",
                LastModified = DateTime.UtcNow.ToString("O"),
                Profiles = new List<EndGameKitProfile>
                {
                    new EndGameKitProfile
                    {
                        Name = "GS91_Standard",
                        Description = "Standard GS91 end-game kit with Greatsword",
                        Enabled = true,
                        AutoApplyOnZoneEntry = false,
                        RestoreOnExit = true,
                        MinimumGearScore = 0,
                        AllowInPvP = false,
                        Equipment = new Dictionary<string, long>
                        {
                            ["MainHand"] = -1234567890,
                            ["Head"] = -1234567900,
                            ["Chest"] = -1234567901,
                            ["Legs"] = -1234567902,
                            ["Feet"] = -1234567903,
                            ["Hands"] = -1234567904,
                            ["Neck"] = -1234567910,
                            ["Finger1"] = -1234567911,
                            ["Finger2"] = -1234567912
                        },
                        Consumables = new List<long>
                        {
                            -1464869972, // PowerSurgePotion
                            1977859216,  // WitchPotion
                            -1858380711, // EnchantedBrew
                            -1446898756  // ScourgestoneCoating
                        },
                        Jewels = new List<long>
                        {
                            -987654321, // ChaosJewel_T4
                            123456789   // BloodJewel_T4
                        },
                        StatOverrides = new StatOverrideConfig
                        {
                            BonusPower = 25f,
                            BonusMaxHealth = 300f,
                            BonusSpellPower = 15f,
                            BonusMoveSpeed = 0.05f
                        }
                    }
                }
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(defaultConfig, options);
            File.WriteAllText(_configPath, json);
            Plugin.Log.LogInfo($"[EndGameKitSystem] Default configuration created at: {_configPath}");
        }

        /// <summary>
        /// Validates a kit configuration for common errors.
        /// </summary>
        private List<string> ValidateConfiguration(KitConfiguration config)
        {
            var errors = new List<string>();

            if (config.Profiles == null || config.Profiles.Count == 0)
            {
                errors.Add("No profiles defined in configuration");
                return errors;
            }

            foreach (var profile in config.Profiles)
            {
                if (string.IsNullOrEmpty(profile.Name))
                {
                    errors.Add("Profile has empty name");
                }

                if (profile.Equipment != null)
                {
                    foreach (var kvp in profile.Equipment)
                    {
                        if (kvp.Value == 0)
                        {
                            errors.Add($"Profile '{profile.Name}': Invalid GUID 0 for slot {kvp.Key}");
                        }
                    }
                }

                if (profile.Consumables != null)
                {
                    foreach (var guid in profile.Consumables)
                    {
                        if (guid == 0)
                        {
                            errors.Add($"Profile '{profile.Name}': Invalid consumable GUID 0");
                        }
                    }
                }

                if (profile.Jewels != null)
                {
                    foreach (var guid in profile.Jewels)
                    {
                        if (guid == 0)
                        {
                            errors.Add($"Profile '{profile.Name}': Invalid jewel GUID 0");
                        }
                    }
                }
            }

            return errors;
        }

        #endregion

        #region Kit Application
        
        /// <summary>
        /// Applies a full end-game kit to a player (one-shot execution).
        /// Order: Equipment → Consumables → Jewels → Stats
        /// </summary>
        /// <param name="player">Player entity.</param>
        /// <param name="kitName">Name of the kit profile to apply.</param>
        /// <returns>True if kit was successfully applied.</returns>
        public bool ApplyKit(Entity player, string kitName = "GS91_Standard")
        {
            if (!_initialized)
            {
                Plugin.Log.LogWarning("[EndGameKitSystem] System not initialized");
                return false;
            }

            if (!PlayerHelper.IsValidPlayer(_entityManager, player))
            {
                Plugin.Log.LogWarning($"[EndGameKitSystem] Invalid player entity: {player.Index}");
                return false;
            }

            // One-shot check
            if (_kitAppliedPlayers.Contains(player))
            {
                Plugin.Log.LogDebug($"[EndGameKitSystem] Kit already applied to player {player.Index}, skipping");
                return false;
            }

            // Get kit profile
            if (!_kitProfiles.TryGetValue(kitName, out var kit))
            {
                Plugin.Log.LogWarning($"[EndGameKitSystem] Kit profile not found: {kitName}");
                Plugin.Log.LogInfo($"[EndGameKitSystem] Available kits: {string.Join(", ", _kitProfiles.Keys)}");
                return false;
            }

            if (!kit.Enabled)
            {
                Plugin.Log.LogWarning($"[EndGameKitSystem] Kit '{kitName}' is disabled");
                return false;
            }

            try
            {
                // Ensure ServerGameManager is available
                if (_serverGameManager == null)
                {
                    _serverGameManager = World.GetExistingSystemManaged<ServerGameManager>();
                    if (_serverGameManager == null)
                    {
                        Plugin.Log.LogError("[EndGameKitSystem] ServerGameManager not available");
                        return false;
                    }
                }

                // Mark as starting
                _kitAppliedPlayers.Add(player);

                var playerName = PlayerHelper.GetPlayerName(_entityManager, player);
                Plugin.Log.LogInfo($"[EndGameKitSystem] Applying kit '{kitName}' to {playerName}...");

                // Step 1: Equip gear (MUST be first)
                int equipmentCount = _equipmentService.EquipKit(player, kit.Equipment);
                if (equipmentCount == 0)
                {
                    Plugin.Log.LogWarning($"[EndGameKitSystem] Failed to equip gear for {playerName}");
                }

                // Step 2: Apply consumables
                int consumableCount = _consumableService.ApplyBatch(player, kit.Consumables);

                // Step 3: Attach jewels
                int jewelCount = _jewelService.AttachJewels(player, kit.Jewels);

                // Step 4: Apply stat overrides
                int statCount = _statExtensionService.ApplyStatOverrides(player, kit.StatOverrides);

                // Step 5: Track player state
                TrackPlayerState(player, kitName, equipmentCount, consumableCount, jewelCount, statCount);

                Plugin.Log.LogInfo(
                    $"[EndGameKitSystem] Kit '{kitName}' applied to {playerName}: " +
                    $"{equipmentCount} gear, {consumableCount} consumables, {jewelCount} jewels, {statCount} stats"
                );

                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[EndGameKitSystem] Error applying kit to player {player.Index}: {ex.Message}");
                _kitAppliedPlayers.Remove(player);
                return false;
            }
        }

        /// <summary>
        /// Restores kit state for a player (e.g., on reconnect or respawn).
        /// </summary>
        /// <param name="player">Player entity.</param>
        /// <returns>True if state was restored.</returns>
        public bool RestorePlayerState(Entity player)
        {
            if (!_initialized)
                return false;

            if (!PlayerHelper.IsValidPlayer(_entityManager, player))
                return false;

            if (!_playerStates.TryGetValue(player, out var state))
            {
                Plugin.Log.LogDebug($"[EndGameKitSystem] No saved state for player {player.Index}");
                return false;
            }

            // Check if kit was previously applied
            if (!state.KitApplied || string.IsNullOrEmpty(state.AppliedKitName))
            {
                return false;
            }

            Plugin.Log.LogInfo($"[EndGameKitSystem] Restoring kit state for {PlayerHelper.GetPlayerName(_entityManager, player)}...");

            // Re-apply the kit
            var success = ApplyKit(player, state.AppliedKitName);

            if (success)
            {
                Plugin.Log.LogInfo($"[EndGameKitSystem] Kit state restored for player {player.Index}");
            }

            return success;
        }

        /// <summary>
        /// Removes all kit effects from a player.
        /// </summary>
        public bool RemoveKit(Entity player)
        {
            if (!PlayerHelper.IsValidPlayer(_entityManager, player))
                return false;

            // Remove stat buffs
            _statExtensionService.RemoveStatBuffs(player);

            // Clear jewels
            _jewelService.ClearJewels(player);

            // Remove tracking state
            _kitAppliedPlayers.Remove(player);
            _playerStates.Remove(player);

            // Remove component if exists
            if (_entityManager.HasComponent<EndGameKitPlayerComponent>(player))
            {
                _entityManager.RemoveComponent<EndGameKitPlayerComponent>(player);
            }

            Plugin.Log.LogInfo($"[EndGameKitSystem] Kit removed from player {player.Index}");
            return true;
        }

        /// <summary>
        /// Apply the default GS91 end-game kit (backward compatibility).
        /// </summary>
        /// <param name="player">Player entity.</param>
        /// <returns>True if kit was successfully applied.</returns>
        public bool ApplyGS91Kit(Entity player)
        {
            return ApplyKit(player, "GS91_Standard");
        }

        #endregion

        #region Player State Tracking
        
        /// <summary>
        /// Tracks the applied kit state for a player.
        /// </summary>
        private void TrackPlayerState(Entity player, string kitName, int gear, int consumables, int jewels, int stats)
        {
            var timestamp = SystemAPI.Time.ElapsedTime;

            // Update dictionary state
            _playerStates[player] = new PlayerKitState
            {
                KitApplied = true,
                AppliedKitName = kitName,
                AppliedTimestamp = timestamp,
                LastRestoreTimestamp = timestamp,
                EquipmentApplied = gear,
                ConsumablesApplied = consumables,
                JewelsApplied = jewels,
                StatsApplied = stats
            };

            // Add/update ECS component
            if (!_entityManager.HasComponent<EndGameKitPlayerComponent>(player))
            {
                _entityManager.AddComponentData(player, new EndGameKitPlayerComponent
                {
                    KitApplied = true,
                    KitName = kitName,
                    AppliedTimestamp = timestamp
                });
            }
            else
            {
                var component = _entityManager.GetComponentData<EndGameKitPlayerComponent>(player);
                component.KitApplied = true;
                component.KitName = kitName;
                component.AppliedTimestamp = timestamp;
                _entityManager.SetComponentData(player, component);
            }
        }

        /// <summary>
        /// Checks if a player has a kit applied.
        /// </summary>
        public bool HasKitApplied(Entity player)
        {
            return _kitAppliedPlayers.Contains(player);
        }

        /// <summary>
        /// Gets the kit state for a player.
        /// </summary>
        public PlayerKitState? GetPlayerState(Entity player)
        {
            return _playerStates.TryGetValue(player, out var state) ? state : null;
        }

        /// <summary>
        /// Handles player disconnect - cleans up tracking but preserves state for potential restore.
        /// </summary>
        public void HandlePlayerDisconnect(Entity player)
        {
            if (_playerStates.TryGetValue(player, out var state))
            {
                state.LastDisconnectTimestamp = SystemAPI.Time.ElapsedTime;
                _playerStates[player] = state;
            }

            _kitAppliedPlayers.Remove(player);
            Plugin.Log.LogDebug($"[EndGameKitSystem] Player {player.Index} disconnected, state preserved for restore");
        }

        #endregion

        #region Profile Management
        
        /// <summary>
        /// Gets a list of all registered kit profile names.
        /// </summary>
        public List<string> GetRegisteredKitNames()
        {
            return _kitProfiles.Keys.ToList();
        }

        /// <summary>
        /// Gets a list of all registered kit profile names (alias for command compatibility).
        /// </summary>
        public List<string> GetKitProfileNames()
        {
            return GetRegisteredKitNames();
        }

        /// <summary>
        /// Gets a kit profile by name.
        /// </summary>
        public EndGameKitProfile? GetKitProfile(string name)
        {
            return _kitProfiles.TryGetValue(name, out var profile) ? profile : null;
        }

        /// <summary>
        /// Registers a new kit profile at runtime.
        /// </summary>
        public bool RegisterKitProfile(EndGameKitProfile profile)
        {
            if (profile == null || string.IsNullOrEmpty(profile.Name))
            {
                Plugin.Log.LogWarning("[EndGameKitSystem] Cannot register null or unnamed profile");
                return false;
            }

            _kitProfiles[profile.Name] = profile;
            Plugin.Log.LogInfo($"[EndGameKitSystem] Runtime profile registered: {profile.Name}");
            return true;
        }

        /// <summary>
        /// Unregisters a kit profile at runtime.
        /// </summary>
        public bool UnregisterKitProfile(string name)
        {
            if (_kitProfiles.Remove(name))
            {
                Plugin.Log.LogInfo($"[EndGameKitSystem] Profile unregistered: {name}");
                return true;
            }
            return false;
        }

        #endregion

        #region Update Loop
        
        /// <summary>
        /// Updates the system - call this in your plugin's Update method.
        /// Handles hot-reload checking.
        /// </summary>
        /// <param name="deltaTime">Time since last update.</param>
        public void Update(float deltaTime)
        {
            if (!_initialized)
                return;

            // Check for configuration hot-reload
            CheckConfigHotReload(deltaTime);
        }

        #endregion

        #region Shutdown
        
        /// <summary>
        /// Shuts down the system and cleans up resources.
        /// </summary>
        public void Shutdown()
        {
            if (!_initialized || _disposed)
                return;

            _disposed = true;

            // Clear all tracking
            _kitAppliedPlayers.Clear();
            _playerStates.Clear();
            _kitProfiles.Clear();

            _initialized = false;
            Plugin.Log.LogInfo("[EndGameKitSystem] Shutdown complete");
        }

        #endregion

        #region IDisposable
        
        public void Dispose()
        {
            Shutdown();
        }

        #endregion
    }

    #region Supporting Types

    /// <summary>
    /// Player kit state for tracking and restoration.
    /// </summary>
    public class PlayerKitState
    {
        public bool KitApplied;
        public string AppliedKitName = string.Empty;
        public double AppliedTimestamp;
        public double LastRestoreTimestamp;
        public double LastDisconnectTimestamp;
        public int EquipmentApplied;
        public int ConsumablesApplied;
        public int JewelsApplied;
        public int StatsApplied;
    }

    #endregion
}
