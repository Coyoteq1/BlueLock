using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Unity.Mathematics;

namespace VAuto.Core.Services
{
    #region Trap Data
    
    /// <summary>
    /// Trap data model - stores trap configuration and state.
    /// </summary>
    public class TrapData
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public float3 Position { get; set; }
        public float Radius { get; set; } = 2f;
        public ulong OwnerId { get; set; }
        public TrapType Type { get; set; } = TrapType.Container;
        public bool IsArmed { get; set; } = true;
        public bool IsTriggered { get; set; }
        public float3 GlowColor { get; set; } = new float3(1f, 0.5f, 0f);
        public float GlowRadius { get; set; } = 5f;
        public float DamageAmount { get; set; } = 50f;
        public float Duration { get; set; } = 30f;
        public ulong? LastTriggeredBy { get; set; }
        public DateTime? LastTriggeredTime { get; set; }
        public DateTime CreatedTime { get; set; } = DateTime.UtcNow;
        public string Name { get; set; } = "Trap";
        public int PrefabId { get; set; } // PrefabGuid for trap entity
        public int GlowPrefabId { get; set; } // PrefabGuid for glow effect
    }
    
    public enum TrapType
    {
        Container,
        Waypoint,
        Border,
        Custom
    }
    
    /// <summary>
    /// Collection of all traps.
    /// </summary>
    public class TrapDataCollection
    {
        public List<TrapData> Traps { get; set; } = new();
        public DateTime LastModified { get; set; } = DateTime.UtcNow;
    }
    
    #endregion
    
    #region Glow Data
    
    /// <summary>
    /// Glow zone data model.
    /// </summary>
    public class GlowData
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public float3 Position { get; set; }
        public float Radius { get; set; } = 10f;
        public float3 Color { get; set; } = new float3(0f, 1f, 1f);
        public float Intensity { get; set; } = 1f;
        public GlowType Type { get; set; } = GlowType.Arena;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedTime { get; set; } = DateTime.UtcNow;
        public string Name { get; set; } = "Glow Zone";
        public string Description { get; set; } = "";
        public int PrefabId { get; set; } // PrefabGuid for glow entity
    }
    
    public enum GlowType
    {
        Arena,
        SafeZone,
        PvPZone,
        QuestArea,
        Custom
    }
    
    /// <summary>
    /// Collection of all glow zones.
    /// </summary>
    public class GlowDataCollection
    {
        public List<GlowData> GlowZones { get; set; } = new();
        public DateTime LastModified { get; set; } = DateTime.UtcNow;
    }
    
    #endregion
    
    #region Zone Data
    
    /// <summary>
    /// Zone data model - defines gameplay zones.
    /// </summary>
    public class GameZoneData
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "Zone";
        public ZoneType Type { get; set; } = ZoneType.Custom;
        public List<ZoneBoundary> Boundaries { get; set; } = new();
        public float3 Center { get; set; }
        public float Radius { get; set; } = 50f;
        public bool IsActive { get; set; } = true;
        public List<ZoneEffect> Effects { get; set; } = new();
        public List<string> AllowedPlayers { get; set; } = new();
        public List<string> DeniedPlayers { get; set; } = new();
        public DateTime CreatedTime { get; set; } = DateTime.UtcNow;
        public string Description { get; set; } = "";
        public int PrefabId { get; set; } // PrefabGuid for zone marker
        public int EnterPrefabId { get; set; } // PrefabGuid for enter effect
        public int ExitPrefabId { get; set; } // PrefabGuid for exit effect
    }
    
    public enum ZoneType
    {
        MainArena,
        PvPArena,
        SafeZone,
        GlowZone,
        Custom
    }
    
    /// <summary>
    /// Zone boundary definition.
    /// </summary>
    public class ZoneBoundary
    {
        public List<float3> Points { get; set; } = new();
        public float MinY { get; set; }
        public float MaxY { get; set; }
    }
    
    /// <summary>
    /// Zone effect application.
    /// </summary>
    public class ZoneEffect
    {
        public EffectType Type { get; set; }
        public float Value { get; set; }
        public float Duration { get; set; }
        public string BuffId { get; set; } = "";
    }
    
    public enum EffectType
    {
        Buff,
        Debuff,
        Damage,
        Heal,
        Speed,
        Teleport
    }
    
    /// <summary>
    /// Collection of all zones.
    /// </summary>
    public class ZoneDataCollection
    {
        public List<GameZoneData> Zones { get; set; } = new();
        public DateTime LastModified { get; set; } = DateTime.UtcNow;
    }
    
    #endregion
    
    #region Kit Data
    
    /// <summary>
    /// EndGameKit data model - player loadout profiles.
    /// </summary>
    public class KitData
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "Kit";
        public string Description { get; set; } = "";
        public List<KitEquipmentSlot> Equipment { get; set; } = new();
        public List<KitConsumable> Consumables { get; set; } = new();
        public List<KitJewel> Jewels { get; set; } = new();
        public List<KitStatModifier> StatModifiers { get; set; } = new();
        public int RequiredKillStreak { get; set; } = 0;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedTime { get; set; } = DateTime.UtcNow;
        public int PrefabId { get; set; } // PrefabGuid for kit reward chest
        public int VBloodPrefabId { get; set; } // PrefabGuid for VBlood unlock
    }
    
    public class KitEquipmentSlot
    {
        public string Slot { get; set; } = "";
        public string ItemId { get; set; } = "";
        public int Amount { get; set; } = 1;
        public int Level { get; set; } = 1;
    }
    
    public class KitConsumable
    {
        public string ItemId { get; set; } = "";
        public int Amount { get; set; } = 1;
    }
    
    public class KitJewel
    {
        public string ItemId { get; set; } = "";
        public int Socket { get; set; }
    }
    
    public class KitStatModifier
    {
        public string Stat { get; set; } = "";
        public float Value { get; set; }
        public ModifierType Type { get; set; } = ModifierType.Add;
    }
    
    public enum ModifierType
    {
        Add,
        Multiply,
        Override
    }
    
    /// <summary>
    /// Collection of all kits.
    /// </summary>
    public class KitDataCollection
    {
        public List<KitData> Kits { get; set; } = new();
        public DateTime LastModified { get; set; } = DateTime.UtcNow;
    }
    
    #endregion
    
    #region Unified Game Data Manager
    
    /// <summary>
    /// Central manager for all game data persistence.
    /// </summary>
    public static class GameDataManager
    {
        private static readonly object _lock = new object();
        private static TrapDataCollection _traps = new();
        private static GlowDataCollection _glows = new();
        private static ZoneDataCollection _zones = new();
        private static KitDataCollection _kits = new();
        private static bool _initialized;
        
        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            
            LoadAllData();
            Plugin.Log.LogInfo("[GameDataManager] Initialized");
        }
        
        #region Trap Data Methods
        
        public static void AddTrap(TrapData trap)
        {
            lock (_lock)
            {
                _traps.Traps.Add(trap);
                _traps.LastModified = DateTime.UtcNow;
            }
        }
        
        public static bool RemoveTrap(string id)
        {
            lock (_lock)
            {
                var removed = _traps.Traps.RemoveAll(t => t.Id == id);
                if (removed > 0) _traps.LastModified = DateTime.UtcNow;
                return removed > 0;
            }
        }
        
        public static List<TrapData> GetAllTraps() => new(_traps.Traps);
        
        public static void SaveTraps()
        {
            var path = Path.Combine(BepInEx.Paths.ConfigPath, "VAuto", "trap_data.json");
            SaveJson(path, _traps);
        }
        
        private static void LoadTraps()
        {
            var path = Path.Combine(BepInEx.Paths.ConfigPath, "VAuto", "trap_data.json");
            if (File.Exists(path))
            {
                _traps = LoadJson<TrapDataCollection>(path) ?? new TrapDataCollection();
            }
        }
        
        #endregion
        
        #region Glow Data Methods
        
        public static void AddGlow(GlowData glow)
        {
            lock (_lock)
            {
                _glows.GlowZones.Add(glow);
                _glows.LastModified = DateTime.UtcNow;
            }
        }
        
        public static bool RemoveGlow(string id)
        {
            lock (_lock)
            {
                var removed = _glows.GlowZones.RemoveAll(g => g.Id == id);
                if (removed > 0) _glows.LastModified = DateTime.UtcNow;
                return removed > 0;
            }
        }
        
        public static List<GlowData> GetAllGlows() => new(_glows.GlowZones);
        
        public static void SaveGlows()
        {
            var path = Path.Combine(BepInEx.Paths.ConfigPath, "VAuto", "glow_data.json");
            SaveJson(path, _glows);
        }
        
        private static void LoadGlows()
        {
            var path = Path.Combine(BepInEx.Paths.ConfigPath, "VAuto", "glow_data.json");
            if (File.Exists(path))
            {
                _glows = LoadJson<GlowDataCollection>(path) ?? new GlowDataCollection();
            }
        }
        
        #endregion
        
        #region Zone Data Methods
        
        public static void AddZone(GameZoneData zone)
        {
            lock (_lock)
            {
                _zones.Zones.Add(zone);
                _zones.LastModified = DateTime.UtcNow;
            }
        }
        
        public static bool RemoveZone(string id)
        {
            lock (_lock)
            {
                var removed = _zones.Zones.RemoveAll(z => z.Id == id);
                if (removed > 0) _zones.LastModified = DateTime.UtcNow;
                return removed > 0;
            }
        }
        
        public static List<GameZoneData> GetAllZones() => new(_zones.Zones);
        
        public static void SaveZones()
        {
            var path = Path.Combine(BepInEx.Paths.ConfigPath, "VAuto", "zone_data.json");
            SaveJson(path, _zones);
        }
        
        private static void LoadZones()
        {
            var path = Path.Combine(BepInEx.Paths.ConfigPath, "VAuto", "zone_data.json");
            if (File.Exists(path))
            {
                _zones = LoadJson<ZoneDataCollection>(path) ?? new ZoneDataCollection();
            }
        }
        
        #endregion
        
        #region Kit Data Methods
        
        public static void AddKit(KitData kit)
        {
            lock (_lock)
            {
                _kits.Kits.Add(kit);
                _kits.LastModified = DateTime.UtcNow;
            }
        }
        
        public static bool RemoveKit(string id)
        {
            lock (_lock)
            {
                var removed = _kits.Kits.RemoveAll(k => k.Id == id);
                if (removed > 0) _kits.LastModified = DateTime.UtcNow;
                return removed > 0;
            }
        }
        
        public static List<KitData> GetAllKits() => new(_kits.Kits);
        
        public static KitData GetKitByName(string name)
        {
            return _kits.Kits.Find(k => k.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
        
        public static void SaveKits()
        {
            var path = Path.Combine(BepInEx.Paths.ConfigPath, "VAuto", "kit_data.json");
            SaveJson(path, _kits);
        }
        
        private static void LoadKits()
        {
            var path = Path.Combine(BepInEx.Paths.ConfigPath, "VAuto", "kit_data.json");
            if (File.Exists(path))
            {
                _kits = LoadJson<KitDataCollection>(path) ?? new KitDataCollection();
            }
        }
        
        #endregion
        
        #region General Methods
        
        public static void LoadAllData()
        {
            LoadTraps();
            LoadGlows();
            LoadZones();
            LoadKits();
            Plugin.Log.LogInfo($"[GameDataManager] Loaded: {_traps.Traps.Count} traps, {_glows.GlowZones.Count} glows, {_zones.Zones.Count} zones, {_kits.Kits.Count} kits");
        }
        
        public static void SaveAllData()
        {
            SaveTraps();
            SaveGlows();
            SaveZones();
            SaveKits();
        }
        
        private static void SaveJson<T>(string path, T data)
        {
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                
                var options = new System.Text.Json.JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                };
                File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(data, options));
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[GameDataManager] Save failed: {ex.Message}");
            }
        }
        
        private static T LoadJson<T>(string path) where T : new()
        {
            try
            {
                var json = File.ReadAllText(path);
                return System.Text.Json.JsonSerializer.Deserialize<T>(json) ?? new T();
            }
            catch
            {
                return new T();
            }
        }
        
        #endregion
    }
    
    #endregion
}
