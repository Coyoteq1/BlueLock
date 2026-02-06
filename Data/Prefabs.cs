using System;
using System.Collections.Generic;
using Stunlock.Core;

#pragma warning disable IDE0090 // Use 'new(...)'

namespace VAuto.Data
{
    /// <summary>
    /// Minimal Prefab Registry for VAutoLifecycle
    /// Contains essential prefabs for glow zones, arena management, and EndGameKit
    /// </summary>
    public static class Prefabs
    {
        #region Prefab GUIDs

        // Arena Glow Prefabs
        public static readonly PrefabGUID ArenaGlow_Default = new PrefabGUID(1234567890);
        public static readonly PrefabGUID ArenaGlowPrefabs_Default = new PrefabGUID(1234567891);

        // Zone Glow Prefabs (11 per zone as requested)
        public static readonly PrefabGUID ZoneGlow_01 = new PrefabGUID(1234567892);
        public static readonly PrefabGUID ZoneGlow_02 = new PrefabGUID(1234567893);
        public static readonly PrefabGUID ZoneGlow_03 = new PrefabGUID(1234567894);
        public static readonly PrefabGUID ZoneGlow_04 = new PrefabGUID(1234567895);
        public static readonly PrefabGUID ZoneGlow_05 = new PrefabGUID(1234567896);
        public static readonly PrefabGUID ZoneGlow_06 = new PrefabGUID(1234567897);
        public static readonly PrefabGUID ZoneGlow_07 = new PrefabGUID(1234567898);
        public static readonly PrefabGUID ZoneGlow_08 = new PrefabGUID(1234567899);
        public static readonly PrefabGUID ZoneGlow_09 = new PrefabGUID(1234567900);
        public static readonly PrefabGUID ZoneGlow_10 = new PrefabGUID(1234567901);
        public static readonly PrefabGUID ZoneGlow_11 = new PrefabGUID(1234567902);

        // Buff Prefabs
        public static readonly PrefabGUID Buff_General_Vampire_Wounded_Buff = new PrefabGUID(1234567903);
        public static readonly PrefabGUID ImprisonedBuff = new PrefabGUID(1234567904);

        // Inventory Prefabs
        public static readonly PrefabGUID External_Inventory = new PrefabGUID(1234567905);

        // Emote Prefabs (for BuildService)
        public static readonly PrefabGUID AB_Emote_Vampire_Beckon_AbilityGroup = new PrefabGUID(1234567906);
        public static readonly PrefabGUID AB_Emote_Vampire_Clap_AbilityGroup = new PrefabGUID(1234567907);
        public static readonly PrefabGUID AB_Emote_Vampire_Bow_AbilityGroup = new PrefabGUID(1234567908);
        public static readonly PrefabGUID AB_Emote_Vampire_Yes_AbilityGroup = new PrefabGUID(1234567909);
        public static readonly PrefabGUID AB_Emote_Vampire_Shrug_AbilityGroup = new PrefabGUID(1234567910);
        public static readonly PrefabGUID AB_Emote_Vampire_Salute_AbilityGroup = new PrefabGUID(1234567911);
        public static readonly PrefabGUID AB_Emote_Vampire_No_AbilityGroup = new PrefabGUID(1234567912);

        // Weapons (MainHand/OffHand)
        public static readonly PrefabGUID Weapon_Greatsword_GS91 = new PrefabGUID(-1234567890);
        public static readonly PrefabGUID Weapon_Fangs_Vampire = new PrefabGUID(-1234567894);
        public static readonly PrefabGUID Weapon_Sword_PvP = new PrefabGUID(-1234567895);
        public static readonly PrefabGUID Shield_PvP = new PrefabGUID(-1234567896);
        public static readonly PrefabGUID Weapon_Staff_Healer = new PrefabGUID(-1234567897);
        public static readonly PrefabGUID Weapon_Mace_Tank = new PrefabGUID(-1234567898);
        public static readonly PrefabGUID Shield_Tank = new PrefabGUID(-1234567899);
        public static readonly PrefabGUID Weapon_Dagger_Speed = new PrefabGUID(-1234568000);
        public static readonly PrefabGUID Weapon_Sword_Starter = new PrefabGUID(-1234568100);

        // Armor - Head
        public static readonly PrefabGUID Head_Plate_GS91 = new PrefabGUID(-1234567900);
        public static readonly PrefabGUID Head_Plate_PvP = new PrefabGUID(-1234567905);
        public static readonly PrefabGUID Head_Cloth_Healer = new PrefabGUID(-1234567916);
        public static readonly PrefabGUID Head_Plate_Tank = new PrefabGUID(-1234567924);
        public static readonly PrefabGUID Head_Leather_Speed = new PrefabGUID(-1234568001);
        public static readonly PrefabGUID Head_Cloth_Starter = new PrefabGUID(-1234568101);

        // Armor - Chest
        public static readonly PrefabGUID Chest_Plate_GS91 = new PrefabGUID(-1234567901);
        public static readonly PrefabGUID Chest_Plate_PvP = new PrefabGUID(-1234567906);
        public static readonly PrefabGUID Chest_Cloth_Healer = new PrefabGUID(-1234567917);
        public static readonly PrefabGUID Chest_Plate_Tank = new PrefabGUID(-1234567925);
        public static readonly PrefabGUID Chest_Leather_Speed = new PrefabGUID(-1234568002);
        public static readonly PrefabGUID Chest_Cloth_Starter = new PrefabGUID(-1234568102);

        // Armor - Legs
        public static readonly PrefabGUID Legs_Plate_GS91 = new PrefabGUID(-1234567902);
        public static readonly PrefabGUID Legs_Plate_PvP = new PrefabGUID(-1234567907);
        public static readonly PrefabGUID Legs_Cloth_Healer = new PrefabGUID(-1234567918);
        public static readonly PrefabGUID Legs_Plate_Tank = new PrefabGUID(-1234567926);
        public static readonly PrefabGUID Legs_Leather_Speed = new PrefabGUID(-1234568003);
        public static readonly PrefabGUID Legs_Cloth_Starter = new PrefabGUID(-1234568103);

        // Armor - Feet
        public static readonly PrefabGUID Feet_Plate_GS91 = new PrefabGUID(-1234567903);
        public static readonly PrefabGUID Feet_Plate_PvP = new PrefabGUID(-1234567908);
        public static readonly PrefabGUID Feet_Cloth_Healer = new PrefabGUID(-1234567919);
        public static readonly PrefabGUID Feet_Plate_Tank = new PrefabGUID(-1234567927);
        public static readonly PrefabGUID Feet_Leather_Speed = new PrefabGUID(-1234568004);
        public static readonly PrefabGUID Feet_Cloth_Starter = new PrefabGUID(-1234568104);

        // Armor - Hands
        public static readonly PrefabGUID Hands_Plate_GS91 = new PrefabGUID(-1234567904);
        public static readonly PrefabGUID Hands_Plate_PvP = new PrefabGUID(-1234567909);
        public static readonly PrefabGUID Hands_Cloth_Healer = new PrefabGUID(-1234567920);
        public static readonly PrefabGUID Hands_Plate_Tank = new PrefabGUID(-1234567928);
        public static readonly PrefabGUID Hands_Leather_Speed = new PrefabGUID(-1234568005);
        public static readonly PrefabGUID Hands_Cloth_Starter = new PrefabGUID(-1234568105);

        // Jewelry - Neck
        public static readonly PrefabGUID Neck_GS91 = new PrefabGUID(-1234567910);
        public static readonly PrefabGUID Neck_PvP = new PrefabGUID(-1234567913);
        public static readonly PrefabGUID Neck_Healer = new PrefabGUID(-1234567921);
        public static readonly PrefabGUID Neck_Tank = new PrefabGUID(-1234567929);
        public static readonly PrefabGUID Neck_Speed = new PrefabGUID(-1234568006);
        public static readonly PrefabGUID Neck_Starter = new PrefabGUID(-1234568106);

        // Jewelry - Finger
        public static readonly PrefabGUID Finger1_GS91 = new PrefabGUID(-1234567911);
        public static readonly PrefabGUID Finger2_GS91 = new PrefabGUID(-1234567912);
        public static readonly PrefabGUID Finger1_PvP = new PrefabGUID(-1234567914);
        public static readonly PrefabGUID Finger2_PvP = new PrefabGUID(-1234567915);
        public static readonly PrefabGUID Finger1_Healer = new PrefabGUID(-1234567922);
        public static readonly PrefabGUID Finger2_Healer = new PrefabGUID(-1234567923);
        public static readonly PrefabGUID Finger1_Tank = new PrefabGUID(-1234567930);
        public static readonly PrefabGUID Finger2_Tank = new PrefabGUID(-1234567931);
        public static readonly PrefabGUID Finger1_Speed = new PrefabGUID(-1234568007);
        public static readonly PrefabGUID Finger2_Speed = new PrefabGUID(-1234568008);
        public static readonly PrefabGUID Finger1_Starter = new PrefabGUID(-1234568107);
        public static readonly PrefabGUID Finger2_Starter = new PrefabGUID(-1234568108);

        // Consumables
        public static readonly PrefabGUID Consumable_HealthPotion = new PrefabGUID(-1464869972);
        public static readonly PrefabGUID Consumable_BloodPotion = new PrefabGUID(1977859216);
        public static readonly PrefabGUID Consumable_VerminSalve = new PrefabGUID(-1858380711);
        public static readonly PrefabGUID Consumable_BloodEssence = new PrefabGUID(-1446898756);
        public static readonly PrefabGUID Consumable_BloodPotion_2 = new PrefabGUID(-1234569999);
        public static readonly PrefabGUID Consumable_BloodPotion_3 = new PrefabGUID(-1234569998);
        public static readonly PrefabGUID Consumable_HealthPotion_2 = new PrefabGUID(-1464869973);
        public static readonly PrefabGUID Consumable_HealthPotion_3 = new PrefabGUID(-1464869974);
        public static readonly PrefabGUID Consumable_BloodPotion_4 = new PrefabGUID(-1464869975);
        public static readonly PrefabGUID Consumable_HealthPotion_4 = new PrefabGUID(-1464869976);
        public static readonly PrefabGUID Consumable_HealthPotion_5 = new PrefabGUID(-1464869977);
        public static readonly PrefabGUID Consumable_HealthPotion_6 = new PrefabGUID(-1464869978);
        public static readonly PrefabGUID Consumable_HealthPotion_7 = new PrefabGUID(-1464869979);
        public static readonly PrefabGUID Consumable_HealthPotion_8 = new PrefabGUID(-1464869980);

        // Jewels
        public static readonly PrefabGUID Jewel_Power_1 = new PrefabGUID(-987654321);
        public static readonly PrefabGUID Jewel_Power_2 = new PrefabGUID(123456789);
        public static readonly PrefabGUID Jewel_Power_3 = new PrefabGUID(123456790);
        public static readonly PrefabGUID Jewel_Power_4 = new PrefabGUID(123456791);
        public static readonly PrefabGUID Jewel_Power_5 = new PrefabGUID(123456792);
        public static readonly PrefabGUID Jewel_PvP_1 = new PrefabGUID(-987654322);
        public static readonly PrefabGUID Jewel_Healer_1 = new PrefabGUID(-987654323);
        public static readonly PrefabGUID Jewel_Healer_2 = new PrefabGUID(123456793);
        public static readonly PrefabGUID Jewel_Healer_3 = new PrefabGUID(123456794);
        public static readonly PrefabGUID Jewel_Tank_1 = new PrefabGUID(-987654324);
        public static readonly PrefabGUID Jewel_Tank_2 = new PrefabGUID(123456795);
        public static readonly PrefabGUID Jewel_Tank_3 = new PrefabGUID(123456796);
        public static readonly PrefabGUID Jewel_Tank_4 = new PrefabGUID(123456797);
        public static readonly PrefabGUID Jewel_Tank_5 = new PrefabGUID(123456798);
        public static readonly PrefabGUID Jewel_Speed_1 = new PrefabGUID(-987654325);
        public static readonly PrefabGUID Jewel_Speed_2 = new PrefabGUID(123456799);
        public static readonly PrefabGUID Jewel_Speed_3 = new PrefabGUID(123456800);
        public static readonly PrefabGUID Jewel_Starter_1 = new PrefabGUID(-987654326);
        public static readonly PrefabGUID Jewel_Starter_2 = new PrefabGUID(123456801);

        // Nested classes for organization
        public static class ArenaGlow
        {
            public static readonly PrefabGUID Default = ArenaGlow_Default;
        }

        public static class ArenaGlowPrefabs
        {
            public static readonly PrefabGUID Default = ArenaGlowPrefabs_Default;
        }

        #endregion

        #region Prefab Registry

        private static readonly Dictionary<string, PrefabGUID> _prefabRegistry = new Dictionary<string, PrefabGUID>(StringComparer.OrdinalIgnoreCase)
        {
            // Arena Glow
            { "ArenaGlow.Default", ArenaGlow_Default },
            { "ArenaGlowPrefabs.Default", ArenaGlowPrefabs_Default },

            // Zone Glows
            { "ZoneGlow_01", ZoneGlow_01 },
            { "ZoneGlow_02", ZoneGlow_02 },
            { "ZoneGlow_03", ZoneGlow_03 },
            { "ZoneGlow_04", ZoneGlow_04 },
            { "ZoneGlow_05", ZoneGlow_05 },
            { "ZoneGlow_06", ZoneGlow_06 },
            { "ZoneGlow_07", ZoneGlow_07 },
            { "ZoneGlow_08", ZoneGlow_08 },
            { "ZoneGlow_09", ZoneGlow_09 },
            { "ZoneGlow_10", ZoneGlow_10 },
            { "ZoneGlow_11", ZoneGlow_11 },

            // Buffs
            { "Buff_General_Vampire_Wounded_Buff", Buff_General_Vampire_Wounded_Buff },
            { "ImprisonedBuff", ImprisonedBuff },

            // Inventory
            { "External_Inventory", External_Inventory },

            // Emotes
            { "AB_Emote_Vampire_Beckon_AbilityGroup", AB_Emote_Vampire_Beckon_AbilityGroup },
            { "AB_Emote_Vampire_Clap_AbilityGroup", AB_Emote_Vampire_Clap_AbilityGroup },
            { "AB_Emote_Vampire_Bow_AbilityGroup", AB_Emote_Vampire_Bow_AbilityGroup },
            { "AB_Emote_Vampire_Yes_AbilityGroup", AB_Emote_Vampire_Yes_AbilityGroup },
            { "AB_Emote_Vampire_Shrug_AbilityGroup", AB_Emote_Vampire_Shrug_AbilityGroup },
            { "AB_Emote_Vampire_Salute_AbilityGroup", AB_Emote_Vampire_Salute_AbilityGroup },
            { "AB_Emote_Vampire_No_AbilityGroup", AB_Emote_Vampire_No_AbilityGroup },

            // Weapons
            { "Weapon_Greatsword_GS91", Weapon_Greatsword_GS91 },
            { "Weapon_Fangs_Vampire", Weapon_Fangs_Vampire },
            { "Weapon_Sword_PvP", Weapon_Sword_PvP },
            { "Shield_PvP", Shield_PvP },
            { "Weapon_Staff_Healer", Weapon_Staff_Healer },
            { "Weapon_Mace_Tank", Weapon_Mace_Tank },
            { "Shield_Tank", Shield_Tank },
            { "Weapon_Dagger_Speed", Weapon_Dagger_Speed },
            { "Weapon_Sword_Starter", Weapon_Sword_Starter },

            // Armor - Head
            { "Head_Plate_GS91", Head_Plate_GS91 },
            { "Head_Plate_PvP", Head_Plate_PvP },
            { "Head_Cloth_Healer", Head_Cloth_Healer },
            { "Head_Plate_Tank", Head_Plate_Tank },
            { "Head_Leather_Speed", Head_Leather_Speed },
            { "Head_Cloth_Starter", Head_Cloth_Starter },

            // Armor - Chest
            { "Chest_Plate_GS91", Chest_Plate_GS91 },
            { "Chest_Plate_PvP", Chest_Plate_PvP },
            { "Chest_Cloth_Healer", Chest_Cloth_Healer },
            { "Chest_Plate_Tank", Chest_Plate_Tank },
            { "Chest_Leather_Speed", Chest_Leather_Speed },
            { "Chest_Cloth_Starter", Chest_Cloth_Starter },

            // Armor - Legs
            { "Legs_Plate_GS91", Legs_Plate_GS91 },
            { "Legs_Plate_PvP", Legs_Plate_PvP },
            { "Legs_Cloth_Healer", Legs_Cloth_Healer },
            { "Legs_Plate_Tank", Legs_Plate_Tank },
            { "Legs_Leather_Speed", Legs_Leather_Speed },
            { "Legs_Cloth_Starter", Legs_Cloth_Starter },

            // Armor - Feet
            { "Feet_Plate_GS91", Feet_Plate_GS91 },
            { "Feet_Plate_PvP", Feet_Plate_PvP },
            { "Feet_Cloth_Healer", Feet_Cloth_Healer },
            { "Feet_Plate_Tank", Feet_Plate_Tank },
            { "Feet_Leather_Speed", Feet_Leather_Speed },
            { "Feet_Cloth_Starter", Feet_Cloth_Starter },

            // Armor - Hands
            { "Hands_Plate_GS91", Hands_Plate_GS91 },
            { "Hands_Plate_PvP", Hands_Plate_PvP },
            { "Hands_Cloth_Healer", Hands_Cloth_Healer },
            { "Hands_Plate_Tank", Hands_Plate_Tank },
            { "Hands_Leather_Speed", Hands_Leather_Speed },
            { "Hands_Cloth_Starter", Hands_Cloth_Starter },

            // Jewelry - Neck
            { "Neck_GS91", Neck_GS91 },
            { "Neck_PvP", Neck_PvP },
            { "Neck_Healer", Neck_Healer },
            { "Neck_Tank", Neck_Tank },
            { "Neck_Speed", Neck_Speed },
            { "Neck_Starter", Neck_Starter },

            // Jewelry - Finger
            { "Finger1_GS91", Finger1_GS91 },
            { "Finger2_GS91", Finger2_GS91 },
            { "Finger1_PvP", Finger1_PvP },
            { "Finger2_PvP", Finger2_PvP },
            { "Finger1_Healer", Finger1_Healer },
            { "Finger2_Healer", Finger2_Healer },
            { "Finger1_Tank", Finger1_Tank },
            { "Finger2_Tank", Finger2_Tank },
            { "Finger1_Speed", Finger1_Speed },
            { "Finger2_Speed", Finger2_Speed },
            { "Finger1_Starter", Finger1_Starter },
            { "Finger2_Starter", Finger2_Starter },

            // Consumables
            { "Consumable_HealthPotion", Consumable_HealthPotion },
            { "Consumable_BloodPotion", Consumable_BloodPotion },
            { "Consumable_VerminSalve", Consumable_VerminSalve },
            { "Consumable_BloodEssence", Consumable_BloodEssence },
            { "Consumable_BloodPotion_2", Consumable_BloodPotion_2 },
            { "Consumable_BloodPotion_3", Consumable_BloodPotion_3 },
            { "Consumable_HealthPotion_2", Consumable_HealthPotion_2 },
            { "Consumable_HealthPotion_3", Consumable_HealthPotion_3 },
            { "Consumable_BloodPotion_4", Consumable_BloodPotion_4 },
            { "Consumable_HealthPotion_4", Consumable_HealthPotion_4 },
            { "Consumable_HealthPotion_5", Consumable_HealthPotion_5 },
            { "Consumable_HealthPotion_6", Consumable_HealthPotion_6 },
            { "Consumable_HealthPotion_7", Consumable_HealthPotion_7 },
            { "Consumable_HealthPotion_8", Consumable_HealthPotion_8 },

            // Jewels
            { "Jewel_Power_1", Jewel_Power_1 },
            { "Jewel_Power_2", Jewel_Power_2 },
            { "Jewel_Power_3", Jewel_Power_3 },
            { "Jewel_Power_4", Jewel_Power_4 },
            { "Jewel_Power_5", Jewel_Power_5 },
            { "Jewel_PvP_1", Jewel_PvP_1 },
            { "Jewel_Healer_1", Jewel_Healer_1 },
            { "Jewel_Healer_2", Jewel_Healer_2 },
            { "Jewel_Healer_3", Jewel_Healer_3 },
            { "Jewel_Tank_1", Jewel_Tank_1 },
            { "Jewel_Tank_2", Jewel_Tank_2 },
            { "Jewel_Tank_3", Jewel_Tank_3 },
            { "Jewel_Tank_4", Jewel_Tank_4 },
            { "Jewel_Tank_5", Jewel_Tank_5 },
            { "Jewel_Speed_1", Jewel_Speed_1 },
            { "Jewel_Speed_2", Jewel_Speed_2 },
            { "Jewel_Speed_3", Jewel_Speed_3 },
            { "Jewel_Starter_1", Jewel_Starter_1 },
            { "Jewel_Starter_2", Jewel_Starter_2 },
        };

        #endregion

        #region Helper Methods

        /// <summary>
        /// Get a PrefabGUID by name
        /// </summary>
        public static PrefabGUID? GetPrefabGuid(string name)
        {
            if (_prefabRegistry.TryGetValue(name, out var guid))
            {
                return guid;
            }
            return null;
        }

        /// <summary>
        /// Try to get a PrefabGUID by name
        /// </summary>
        public static bool TryGetValue(string name, out PrefabGUID guid)
        {
            return _prefabRegistry.TryGetValue(name, out guid);
        }

        /// <summary>
        /// Get all registered prefab names
        /// </summary>
        public static IEnumerable<string> GetAllPrefabNames()
        {
            return _prefabRegistry.Keys;
        }

        /// <summary>
        /// Get the name of a prefab from its GUID
        /// </summary>
        public static string GetPrefabName(PrefabGUID guid)
        {
            foreach (var kvp in _prefabRegistry)
            {
                if (kvp.Value.GuidHash == guid.GuidHash)
                {
                    return kvp.Key;
                }
            }
            return null;
        }

        #endregion
    }
}
