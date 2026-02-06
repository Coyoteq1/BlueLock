using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Stunlock.Core;

namespace VAuto.EndGameKit
{
    /// <summary>
    /// Registry for automatic PrefabGUID extraction from PrefabIndex.
    /// Provides dynamic registry generation for equipment, consumables, and jewels.
    /// 
    /// Usage:
    /// 1. Export PrefabIndex.json from the game data
    /// 2. Call Registry.LoadFromPrefabIndex(path) to populate
    /// 3. Access GUIDs via static properties or lookup methods
    /// </summary>
    public static class EndGameEquipmentRegistry
    {
        #region Private Fields
        
        private static readonly Dictionary<string, PrefabGUID> _equipmentByName = new Dictionary<string, PrefabGUID>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, PrefabGUID> _consumablesByName = new Dictionary<string, PrefabGUID>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, PrefabGUID> _jewelsByName = new Dictionary<string, PrefabGUID>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<long, string> _guidToName = new Dictionary<long, string>();
        private static bool _initialized;
        
        #endregion

        #region Equipment PrefabGUIDs
        
        // Weapon Prefabs (replace with actual GUIDs from your PrefabIndex)
        public static PrefabGUID GreatSword => new(-1234567890);
        public static PrefabGUID Axe => new(-1234567891);
        public static PrefabGUID Spear => new(-1234567892);
        public static PrefabGUID Mace => new(-1234567893);
        public static PrefabGUID Fangs => new(-1234567894);
        public static PrefabGUID DualBlades => new(-1234567895);
        
        // Armor Prefabs
        public static PrefabGUID Helm => new(-1234567900);
        public static PrefabGUID Chest => new(-1234567901);
        public static PrefabGUID Legs => new(-1234567902);
        public static PrefabGUID Feet => new(-1234567903);
        public static PrefabGUID Hands => new(-1234567904);
        
        // Jewelry Prefabs
        public static PrefabGUID Necklace => new(-1234567910);
        public static PrefabGUID Ring1 => new(-1234567911);
        public static PrefabGUID Ring2 => new(-1234567912);
        
        #endregion

        #region Consumable PrefabGUIDs
        
        // Potion Prefabs
        public static PrefabGUID PowerSurgePotion => new(-1464869972);
        public static PrefabGUID WitchPotion => new(1977859216);
        public static PrefabGUID EnchantedBrew => new(-1858380711);
        public static PrefabGUID VampiricEssence => new(-1464869973);
        public static PrefabGUID ShadowBrew => new(-1464869974);
        
        // Coating Prefabs
        public static PrefabGUID ScourgestoneCoating => new(-1446898756);
        public static PrefabGUID SilverCoating => new(-1446898757);
        public static PrefabGUID ChaosCoating => new(-1446898758);
        public static PrefabGUID BloodCoating => new(-1446898759);
        public static PrefabGUID HolyCoating => new(-1446898760);
        
        #endregion

        #region Jewel PrefabGUIDs
        
        // T4 Jewels
        public static PrefabGUID ChaosJewel_T4 => new(-987654321);
        public static PrefabGUID BloodJewel_T4 => new(123456789);
        public static PrefabGUID HolyJewel_T4 => new(123456790);
        public static PrefabGUID SilverJewel_T4 => new(123456791);
        public static PrefabGUID POJewel_T4 => new(123456792);
        
        // T3 Jewels
        public static PrefabGUID ChaosJewel_T3 => new(-987654311);
        public static PrefabGUID BloodJewel_T3 => new(123456799);
        
        #endregion

        #region Buff PrefabGUIDs
        
        public static PrefabGUID PowerBonusBuff => new(-2000000001);
        public static PrefabGUID MaxHealthBonusBuff => new(-2000000002);
        public static PrefabGUID SpellPowerBonusBuff => new(-2000000003);
        public static PrefabGUID MoveSpeedBonusBuff => new(-2000000004);
        public static PrefabGUID PhysicalResistanceBonusBuff => new(-2000000005);
        public static PrefabGUID SpellResistanceBonusBuff => new(-2000000006);
        public static PrefabGUID ArmorBonusBuff => new(-2000000007);
        public static PrefabGUID MaxStaminaBonusBuff => new(-2000000008);
        
        #endregion

        #region Public Methods
        
        /// <summary>
        /// Initializes the registry with default GUIDs.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized)
                return;

            // Register equipment
            RegisterEquipment("GreatSword", GreatSword);
            RegisterEquipment("Axe", Axe);
            RegisterEquipment("Spear", Spear);
            RegisterEquipment("Mace", Mace);
            RegisterEquipment("Fangs", Fangs);
            RegisterEquipment("DualBlades", DualBlades);
            RegisterEquipment("Helm", Helm);
            RegisterEquipment("Chest", Chest);
            RegisterEquipment("Legs", Legs);
            RegisterEquipment("Feet", Feet);
            RegisterEquipment("Hands", Hands);
            RegisterEquipment("Necklace", Necklace);
            RegisterEquipment("Ring1", Ring1);
            RegisterEquipment("Ring2", Ring2);

            // Register consumables
            RegisterConsumable("PowerSurgePotion", PowerSurgePotion);
            RegisterConsumable("WitchPotion", WitchPotion);
            RegisterConsumable("EnchantedBrew", EnchantedBrew);
            RegisterConsumable("VampiricEssence", VampiricEssence);
            RegisterConsumable("ShadowBrew", ShadowBrew);
            RegisterConsumable("ScourgestoneCoating", ScourgestoneCoating);
            RegisterConsumable("SilverCoating", SilverCoating);
            RegisterConsumable("ChaosCoating", ChaosCoating);
            RegisterConsumable("BloodCoating", BloodCoating);
            RegisterConsumable("HolyCoating", HolyCoating);

            // Register jewels
            RegisterJewel("ChaosJewel_T4", ChaosJewel_T4);
            RegisterJewel("BloodJewel_T4", BloodJewel_T4);
            RegisterJewel("HolyJewel_T4", HolyJewel_T4);
            RegisterJewel("SilverJewel_T4", SilverJewel_T4);
            RegisterJewel("POJewel_T4", POJewel_T4);
            RegisterJewel("ChaosJewel_T3", ChaosJewel_T3);
            RegisterJewel("BloodJewel_T3", BloodJewel_T3);

            _initialized = true;
            Plugin.Log.LogInfo("[EndGameEquipmentRegistry] Initialized with default GUIDs");
        }

        /// <summary>
        /// Loads registry data from a PrefabIndex.json file.
        /// </summary>
        public static bool LoadFromPrefabIndex(string prefabIndexPath)
        {
            try
            {
                if (!File.Exists(prefabIndexPath))
                {
                    Plugin.Log.LogWarning($"[EndGameEquipmentRegistry] PrefabIndex not found: {prefabIndexPath}");
                    return false;
                }

                var json = File.ReadAllText(prefabIndexPath);
                var prefabs = JsonSerializer.Deserialize<List<PrefabEntry>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (prefabs == null)
                {
                    Plugin.Log.LogWarning("[EndGameEquipmentRegistry] Failed to parse PrefabIndex");
                    return false;
                }

                foreach (var entry in prefabs)
                {
                    if (string.IsNullOrEmpty(entry.Name))
                        continue;

                    var guid = new PrefabGUID((int)entry.Guid);
                    RegisterPrefab(entry.Name, guid);
                }

                Plugin.Log.LogInfo($"[EndGameEquipmentRegistry] Loaded {prefabs.Count} prefabs from {prefabIndexPath}");
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[EndGameEquipmentRegistry] Failed to load PrefabIndex: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets an equipment GUID by name.
        /// </summary>
        public static PrefabGUID? GetEquipmentByName(string name)
        {
            if (_equipmentByName.TryGetValue(name, out var guid))
                return guid;
            return null;
        }

        /// <summary>
        /// Gets a consumable GUID by name.
        /// </summary>
        public static PrefabGUID? GetConsumableByName(string name)
        {
            if (_consumablesByName.TryGetValue(name, out var guid))
                return guid;
            return null;
        }

        /// <summary>
        /// Gets a jewel GUID by name.
        /// </summary>
        public static PrefabGUID? GetJewelByName(string name)
        {
            if (_jewelsByName.TryGetValue(name, out var guid))
                return guid;
            return null;
        }

        /// <summary>
        /// Gets a name for a GUID.
        /// </summary>
        public static string? GetNameByGuid(long guid)
        {
            if (_guidToName.TryGetValue(guid, out var name))
                return name;
            return null;
        }

        /// <summary>
        /// Gets all registered equipment names.
        /// </summary>
        public static IEnumerable<string> GetEquipmentNames() => _equipmentByName.Keys;

        /// <summary>
        /// Gets all registered consumable names.
        /// </summary>
        public static IEnumerable<string> GetConsumableNames() => _consumablesByName.Keys;

        /// <summary>
        /// Gets all registered jewel names.
        /// </summary>
        public static IEnumerable<string> GetJewelNames() => _jewelsByName.Keys;

        /// <summary>
        /// Clears the registry (useful for hot-reload).
        /// </summary>
        public static void Clear()
        {
            _equipmentByName.Clear();
            _consumablesByName.Clear();
            _jewelsByName.Clear();
            _guidToName.Clear();
            _initialized = false;
        }

        #endregion

        #region Private Methods
        
        private static void RegisterEquipment(string name, PrefabGUID guid)
        {
            _equipmentByName[name] = guid;
            _guidToName[guid.Value] = name;
        }

        private static void RegisterConsumable(string name, PrefabGUID guid)
        {
            _consumablesByName[name] = guid;
            _guidToName[guid.Value] = name;
        }

        private static void RegisterJewel(string name, PrefabGUID guid)
        {
            _jewelsByName[name] = guid;
            _guidToName[guid.Value] = name;
        }

        private static void RegisterPrefab(string name, PrefabGUID guid)
        {
            _guidToName[guid.Value] = name;

            // Auto-categorize based on name patterns
            var lowerName = name.ToLowerInvariant();

            if (lowerName.Contains("sword") || lowerName.Contains("axe") || lowerName.Contains("spear") ||
                lowerName.Contains("mace") || lowerName.Contains("fang") || lowerName.Contains("blade"))
            {
                _equipmentByName[name] = guid;
            }
            else if (lowerName.Contains("helm") || lowerName.Contains("chest") || lowerName.Contains("leg") ||
                     lowerName.Contains("boot") || lowerName.Contains("glove"))
            {
                _equipmentByName[name] = guid;
            }
            else if (lowerName.Contains("ring") || lowerName.Contains("necklace") || lowerName.Contains("amulet"))
            {
                _equipmentByName[name] = guid;
            }
            else if (lowerName.Contains("potion") || lowerName.Contains("brew") || lowerName.Contains("elixir"))
            {
                _consumablesByName[name] = guid;
            }
            else if (lowerName.Contains("coating") || lowerName.Contains("oil"))
            {
                _consumablesByName[name] = guid;
            }
            else if (lowerName.Contains("jewel") || lowerName.Contains("gem"))
            {
                _jewelsByName[name] = guid;
            }
        }

        #endregion

        #region Helper Classes
        
        /// <summary>
        /// Represents an entry in the PrefabIndex.
        /// </summary>
        private class PrefabEntry
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;

            [JsonPropertyName("guid")]
            public long Guid { get; set; }

            [JsonPropertyName("type")]
            public string? Type { get; set; }
        }

        #endregion
    }
}
