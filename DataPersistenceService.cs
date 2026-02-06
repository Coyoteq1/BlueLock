using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Unity.Mathematics;
using VAuto.Core;

namespace VAuto.Core.Services
{
    /// <summary>
    /// Service for persisting game data (zones, sections, regions, sessions) to JSON.
    /// </summary>
    public class DataPersistenceService
    {
        private static readonly string _dataDirectory = Path.Combine(BepInEx.Paths.ConfigPath, "VAuto");
        private static readonly string _dataFilePath = Path.Combine(_dataDirectory, "game_data.json");
        
        private static GameData _gameData = new();
        private static bool _initialized = false;

        /// <summary>
        /// Initialize the data persistence service
        /// </summary>
        public static bool Initialize()
        {
            try
            {
                // Ensure data directory exists
                if (!Directory.Exists(_dataDirectory))
                {
                    Directory.CreateDirectory(_dataDirectory);
                }

                // Load existing data or create new
                if (File.Exists(_dataFilePath))
                {
                    var json = File.ReadAllText(_dataFilePath);
                    _gameData = JsonSerializer.Deserialize<GameData>(json) ?? new GameData();
                    Plugin.Log.LogInfo($"[Data] Loaded game data from {_dataFilePath}");
                }
                else
                {
                    _gameData = new GameData();
                    SaveData();
                    Plugin.Log.LogInfo($"[Data] Created new game data file at {_dataFilePath}");
                }

                _initialized = true;
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[Data] Error initializing: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Save all game data to file
        /// </summary>
        public static void SaveData()
        {
            try
            {
                var options = new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                var json = JsonSerializer.Serialize(_gameData, options);
                File.WriteAllText(_dataFilePath, json);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[Data] Error saving: {ex.Message}");
            }
        }

        #region Region Methods

        public static RegionData GetRegion(string regionId)
        {
            if (!_gameData.Regions.TryGetValue(regionId, out var region))
            {
                region = new RegionData { Id = regionId };
                _gameData.Regions[regionId] = region;
            }
            return region;
        }

        public static void UpdateRegion(RegionData data)
        {
            _gameData.Regions[data.Id] = data;
            SaveData();
        }

        public static Dictionary<string, RegionData> GetAllRegions()
        {
            return _gameData.Regions;
        }

        #endregion

        #region Zone Methods

        public static ZoneData GetZone(string zoneId)
        {
            if (!_gameData.Zones.TryGetValue(zoneId, out var zone))
            {
                zone = new ZoneData { Id = zoneId };
                _gameData.Zones[zoneId] = zone;
            }
            return zone;
        }

        public static void UpdateZone(ZoneData data)
        {
            _gameData.Zones[data.Id] = data;
            SaveData();
        }

        public static Dictionary<string, ZoneData> GetAllZones()
        {
            return _gameData.Zones;
        }

        #endregion

        #region Section Methods

        public static SectionData GetSection(string sectionId)
        {
            if (!_gameData.Sections.TryGetValue(sectionId, out var section))
            {
                section = new SectionData { Id = sectionId };
                _gameData.Sections[sectionId] = section;
            }
            return section;
        }

        public static void UpdateSection(SectionData data)
        {
            _gameData.Sections[data.Id] = data;
            SaveData();
        }

        public static Dictionary<string, SectionData> GetAllSections()
        {
            return _gameData.Sections;
        }

        #endregion

        #region Session Methods

        public static void SaveSession(PersistedSessionData session)
        {
            var key = $"{session.UserPlatformId}_{session.SessionType}_{session.StartTime:yyyyMMdd_HHmmss}";
            _gameData.Sessions[key] = session;
            SaveData();
        }

        public static List<PersistedSessionData> GetAllSessions()
        {
            return new List<PersistedSessionData>(_gameData.Sessions.Values);
        }

        #endregion
    }

    /// <summary>
    /// Root game data container
    /// </summary>
    public class GameData
    {
        public Dictionary<string, RegionData> Regions { get; set; } = new();
        public Dictionary<string, ZoneData> Zones { get; set; } = new();
        public Dictionary<string, SectionData> Sections { get; set; } = new();
        public Dictionary<string, PersistedSessionData> Sessions { get; set; } = new();
    }

    /// <summary>
    /// Region data (e.g., Farbane Woods, Dunley Farmlands)
    /// </summary>
    public class RegionData
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public float3 Center { get; set; }
        public float Radius { get; set; }
        public List<string> Zones { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Zone data (e.g., PvP Arena, Safe Zone)
    /// </summary>
    public class ZoneData
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "Custom";
        public float3 Center { get; set; }
        public float Radius { get; set; }
        public bool PvPEnabled { get; set; }
        public bool SafeZone { get; set; }
        public string ParentRegion { get; set; } = string.Empty;
        public List<string> Sections { get; set; } = new();
        public Dictionary<string, object> Rules { get; set; } = new();
    }

    /// <summary>
    /// Section data (subdivisions within zones)
    /// </summary>
    public class SectionData
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public float3 Position { get; set; }
        public float Size { get; set; }
        public string ParentZone { get; set; } = string.Empty;
        public List<string> AllowedBuildTypes { get; set; } = new();
        public Dictionary<string, object> Settings { get; set; } = new();
    }

    /// <summary>
    /// Persisted player session data for JSON storage
    /// </summary>
    public class PersistedSessionData
    {
        public string Id { get; set; } = string.Empty;
        public ulong UserPlatformId { get; set; }
        public string CharacterName { get; set; } = string.Empty;
        public string SessionType { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        public float3 StartPosition { get; set; }
        public float3 EndPosition { get; set; }
        public List<SessionEventData> Events { get; set; } = new();
    }

    public class SessionEventData
    {
        public DateTime Timestamp { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
