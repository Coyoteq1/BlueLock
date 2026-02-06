using System.Collections.Generic;
using System.Text.Json.Serialization;
using ProjectM;
using Stunlock.Core;

namespace VAuto.EndGameKit.Configuration
{
    /// <summary>
    /// Represents a consumable item with GUID and quantity.
    /// </summary>
    public class ConsumableItem
    {
        private int _quantity = 1;

        /// <summary>
        /// The PrefabGUID of the consumable item.
        /// </summary>
        [JsonPropertyName("guid")]
        public long Guid { get; set; }

        /// <summary>
        /// The stack quantity of the consumable. Defaults to 1 if empty or zero.
        /// </summary>
        [JsonPropertyName("quantity")]
        public int Quantity 
        { 
            get => _quantity; 
            set => _quantity = value <= 0 ? 1 : value; 
        }
    }
    /// <summary>
    /// Root configuration container for the EndGameKit system.
    /// </summary>
    public class KitConfiguration
    {
        /// <summary>
        /// Configuration version for migration support.
        /// </summary>
        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0";

        /// <summary>
        /// Last modification timestamp.
        /// </summary>
        [JsonPropertyName("lastModified")]
        public string LastModified { get; set; } = string.Empty;

        /// <summary>
        /// Whether to enable hot-reload functionality.
        /// </summary>
        [JsonPropertyName("hotReloadEnabled")]
        public bool HotReloadEnabled { get; set; } = true;

        /// <summary>
        /// Interval in seconds between hot-reload checks.
        /// </summary>
        [JsonPropertyName("hotReloadCheckInterval")]
        public float HotReloadCheckInterval { get; set; } = 5.0f;

        /// <summary>
        /// List of kit profiles.
        /// </summary>
        [JsonPropertyName("profiles")]
        public List<EndGameKitProfile> Profiles { get; set; } = new List<EndGameKitProfile>();

        /// <summary>
        /// Global stat override defaults applied to all kits.
        /// </summary>
        [JsonPropertyName("globalStatDefaults")]
        public StatOverrideConfig? GlobalStatDefaults { get; set; }
    }

    /// <summary>
    /// Individual kit profile configuration.
    /// </summary>
    public class EndGameKitProfile
    {
        /// <summary>
        /// Unique name for this kit profile.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable description of this kit.
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Whether this kit is enabled.
        /// </summary>
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Whether to automatically apply this kit when entering a zone.
        /// </summary>
        [JsonPropertyName("autoApplyOnZoneEntry")]
        public bool AutoApplyOnZoneEntry { get; set; } = false;

        /// <summary>
        /// Zone names where this kit should auto-apply.
        /// </summary>
        [JsonPropertyName("autoApplyZones")]
        public List<string> AutoApplyZones { get; set; } = new List<string>();

        /// <summary>
        /// Whether to restore previous gear when exiting zone.
        /// </summary>
        [JsonPropertyName("restoreOnExit")]
        public bool RestoreOnExit { get; set; } = true;

        /// <summary>
        /// Minimum gear score required to use this kit (0 = no requirement).
        /// </summary>
        [JsonPropertyName("minimumGearScore")]
        public int MinimumGearScore { get; set; }

        /// <summary>
        /// Whether this kit can be used in PvP areas.
        /// </summary>
        [JsonPropertyName("allowInPvP")]
        public bool AllowInPvP { get; set; } = false;

        /// <summary>
        /// Equipment slot to PrefabGUID mapping.
        /// </summary>
        [JsonPropertyName("equipment")]
        public Dictionary<string, long> Equipment { get; set; } = new Dictionary<string, long>();

        /// <summary>
        /// List of consumable items with quantities (potions, coatings).
        /// </summary>
        [JsonPropertyName("consumables")]
        public List<ConsumableItem> Consumables { get; set; } = new List<ConsumableItem>();

        /// <summary>
        /// List of jewel PrefabGUIDs to socket.
        /// </summary>
        [JsonPropertyName("jewels")]
        public List<long> Jewels { get; set; } = new List<long>();

        /// <summary>
        /// Stat override values applied via buffs.
        /// </summary>
        [JsonPropertyName("statOverrides")]
        public StatOverrideConfig StatOverrides { get; set; } = new StatOverrideConfig();

        /// <summary>
        /// Converts equipment dictionary to proper PrefabGUID format.
        /// </summary>
        public Dictionary<EquipmentSlot, PrefabGUID> GetEquipmentGuidMap()
        {
            var result = new Dictionary<EquipmentSlot, PrefabGUID>();
            
            foreach (var kvp in Equipment)
            {
                if (EquipmentSlotConverter.TryParse(kvp.Key, out var slot))
                {
                    result[slot] = new PrefabGUID((int)kvp.Value);
                }
                else
                {
                    Plugin.Log.LogWarning($"[EndGameKitProfile] Unknown equipment slot: {kvp.Key}");
                }
            }
            
            return result;
        }

        /// <summary>
        /// Converts consumables to PrefabGUID list.
        /// </summary>
        public List<PrefabGUID> GetConsumableGuidList()
        {
            var result = new List<PrefabGUID>();
            foreach (var consumable in Consumables)
            {
                result.Add(new PrefabGUID((int)consumable.Guid));
            }
            return result;
        }

        /// <summary>
        /// Gets consumables with their quantities.
        /// </summary>
        public List<(PrefabGUID guid, int quantity)> GetConsumablesWithQuantities()
        {
            var result = new List<(PrefabGUID, int)>();
            foreach (var consumable in Consumables)
            {
                result.Add((new PrefabGUID((int)consumable.Guid), consumable.Quantity));
            }
            return result;
        }

        /// <summary>
        /// Converts jewels to PrefabGUID list.
        /// </summary>
        public List<PrefabGUID> GetJewelGuidList()
        {
            var result = new List<PrefabGUID>();
            foreach (var guid in Jewels)
            {
                result.Add(new PrefabGUID((int)guid));
            }
            return result;
        }
    }

    /// <summary>
    /// Stat override configuration applied via buffs.
    /// </summary>
    public class StatOverrideConfig
    {
        /// <summary>
        /// Bonus physical power percentage.
        /// </summary>
        [JsonPropertyName("bonusPower")]
        public float BonusPower { get; set; }

        /// <summary>
        /// Bonus max health flat amount.
        /// </summary>
        [JsonPropertyName("bonusMaxHealth")]
        public float BonusMaxHealth { get; set; }

        /// <summary>
        /// Bonus spell power percentage.
        /// </summary>
        [JsonPropertyName("bonusSpellPower")]
        public float BonusSpellPower { get; set; }

        /// <summary>
        /// Bonus movement speed percentage (0.05 = 5%).
        /// </summary>
        [JsonPropertyName("bonusMoveSpeed")]
        public float BonusMoveSpeed { get; set; }

        /// <summary>
        /// Bonus physical resistance percentage.
        /// </summary>
        [JsonPropertyName("bonusPhysicalResistance")]
        public float BonusPhysicalResistance { get; set; }

        /// <summary>
        /// Bonus spell resistance percentage.
        /// </summary>
        [JsonPropertyName("bonusSpellResistance")]
        public float BonusSpellResistance { get; set; }

        /// <summary>
        /// Bonus armor flat amount.
        /// </summary>
        [JsonPropertyName("bonusArmor")]
        public float BonusArmor { get; set; }

        /// <summary>
        /// Bonus max stamina flat amount.
        /// </summary>
        [JsonPropertyName("bonusMaxStamina")]
        public float BonusMaxStamina { get; set; }
    }
}
