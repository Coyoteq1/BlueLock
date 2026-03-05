using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using BepInEx;
using BepInEx.Logging;
using VAuto.Services.Interfaces;
using Blueluck.Models;

namespace Blueluck.Services
{
    /// <summary>
    /// Service for loading and managing zone configurations.
    /// Implements IService from VAutomationCore.
    /// </summary>
    public class ZoneConfigService : IService
    {
        private static readonly ManualLogSource _log = Logger.CreateLogSource("Blueluck.ZoneConfig");
        
        public bool IsInitialized { get; private set; }
        public ManualLogSource Log => _log;

        private ZonesConfig _config = new();
        private readonly Dictionary<int, ZoneDefinition> _zonesByHash = new();
        private string _configPath;
        private DateTime _lastConfigCheck = DateTime.MinValue;

        public void Initialize()
        {
            _configPath = Path.Combine(Paths.ConfigPath, "Blueluck", "zones.json");
            Directory.CreateDirectory(Path.GetDirectoryName(_configPath) ?? Paths.ConfigPath);
            
            LoadConfig();
            IsInitialized = true;
            _log.LogInfo($"[ZoneConfig] Initialized with {_zonesByHash.Count} zones.");
        }

        public void Cleanup()
        {
            _zonesByHash.Clear();
            IsInitialized = false;
            _log.LogInfo("[ZoneConfig] Cleaned up.");
        }

        /// <summary>
        /// Gets all configured zones.
        /// </summary>
        public IReadOnlyList<ZoneDefinition> GetZones()
        {
            var zones = new List<ZoneDefinition>();
            foreach (var zone in _zonesByHash.Values)
            {
                if (zone.Enabled)
                    zones.Add(zone);
            }
            return zones;
        }

        /// <summary>
        /// Gets a zone by its hash.
        /// </summary>
        public bool TryGetZoneByHash(int hash, out ZoneDefinition zone)
        {
            return _zonesByHash.TryGetValue(hash, out zone);
        }

        /// <summary>
        /// Gets detection configuration.
        /// </summary>
        public ZoneDetectionConfig GetDetectionConfig()
        {
            return _config.Detection ?? new ZoneDetectionConfig();
        }

        /// <summary>
        /// Reloads configuration from disk.
        /// </summary>
        public void Reload()
        {
            LoadConfig();
            _log.LogInfo("[ZoneConfig] Configuration reloaded.");
        }

        /// <summary>
        /// Checks if config file has been modified and reloads if needed.
        /// </summary>
        public void CheckForChanges()
        {
            if (!File.Exists(_configPath))
                return;

            var lastWrite = File.GetLastWriteTime(_configPath);
            if (lastWrite > _lastConfigCheck)
            {
                Reload();
                _lastConfigCheck = lastWrite;
            }
        }

        private void LoadConfig()
        {
            _zonesByHash.Clear();

            try
            {
                if (!File.Exists(_configPath))
                {
                    CreateDefaultConfig();
                    return;
                }

                var json = File.ReadAllText(_configPath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve,
                    MaxDepth = 128
                };
                options.Converters.Add(new ZoneDefinitionJsonConverter());

                _config = JsonSerializer.Deserialize<ZonesConfig>(json, options) ?? new ZonesConfig();
                
                foreach (var zone in _config.Zones ?? Array.Empty<ZoneDefinition>())
                {
                    if (zone.Enabled && zone.Hash != 0)
                    {
                        _zonesByHash[zone.Hash] = zone;
                    }
                }

                _log.LogInfo($"[ZoneConfig] Loaded {_zonesByHash.Count} zones from config.");
            }
            catch (Exception ex)
            {
                _log.LogError($"[ZoneConfig] Failed to load config: {ex.Message}");
                _config = new ZonesConfig();
            }
        }

        private void CreateDefaultConfig()
        {
            _config = new ZonesConfig
            {
                Detection = new ZoneDetectionConfig
                {
                    CheckIntervalMs = 500,
                    PositionThreshold = 1.0f
                },
                Zones = new ZoneDefinition[]
                {
                    new BossZoneConfig
                    {
                        Type = "BossZone",
                        Name = "Example Boss Zone",
                        Hash = 1001,
                        Center = new float[] { 500, 0, -300 },
                        EntryRadius = 80,
                        ExitRadius = 100,
                        Enabled = false,
                        Priority = 1,
                        FlowOnEnter = "boss_enter",
                        FlowOnExit = "boss_exit",
                        AbilitySet = "boss",
                        NoProgress = true,
                        BossPrefab = "CHAR_Gloomrot_Purifier_VBlood",
                        BossQuantity = 1,
                        RandomSpawn = true,
                        BorderVisual = new BorderVisualConfig
                        {
                            Effect = "dracula_visual",
                            Range = 10f,
                            IntensityMax = 3,
                            RemoveOnExit = true
                        }
                    },
                    new ArenaZoneConfig
                    {
                        Type = "ArenaZone",
                        Name = "Example Arena",
                        Hash = 2001,
                        Center = new float[] { 1000, 0, -500 },
                        EntryRadius = 150,
                        ExitRadius = 160,
                        Enabled = false,
                        Priority = 0,
                        KitOnEnter = "arena_enter",
                        KitOnExit = "arena_exit",
                        FlowOnEnter = "arena_enter",
                        FlowOnExit = "arena_exit",
                        AbilitySet = "arena",
                        SaveProgress = true,
                        RestoreOnExit = true,
                        PvpEnabled = true,
                        BorderVisual = new BorderVisualConfig
                        {
                            Effect = "solarus_visual",
                            Range = 12f,
                            IntensityMax = 3,
                            RemoveOnExit = true
                        }
                    }
                }
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            };
            options.Converters.Add(new ZoneDefinitionJsonConverter());

            var json = JsonSerializer.Serialize(_config, options);
            File.WriteAllText(_configPath, json);
            _log.LogInfo($"[ZoneConfig] Created default config at {_configPath}");

            foreach (var zone in _config.Zones)
            {
                if (zone.Enabled && zone.Hash != 0)
                {
                    _zonesByHash[zone.Hash] = zone;
                }
            }
        }
    }
}
