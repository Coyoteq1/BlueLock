using System.Collections.Generic;
using Stunlock.Core;

namespace VAuto.Zone.Data.DataType
{
    /// <summary>
    /// Zone-level defaults (items/unlocks applied on zone enter if desired).
    /// </summary>
    public static class ZoneDefault
    {
        public static readonly Dictionary<string, PrefabGUID> Slots = new()
        {
            ["AbilityGroupSlot"] = new PrefabGUID(-633717863),
            ["External_Inventory"] = new PrefabGUID(1183666186),
        };

        public const string Glow = "AB_Purifier_ChaosVolley_BurnDebuff";
        public const string BloodType = "BloodType_Warrior";

        public static readonly Dictionary<string, string> FullArmorT9 = new()
        {
            ["Head"] = "Item_Armor_Headgear_T09_ShadowMatter",
            ["Chest"] = "Item_Armor_Chest_T09_ShadowMatter",
            ["Legs"] = "Item_Armor_Legs_T09_ShadowMatter",
            ["Gloves"] = "Item_Armor_Gloves_T09_ShadowMatter",
            ["Boots"] = "Item_Armor_Boots_T09_ShadowMatter"
        };

        public static readonly Dictionary<string, string> WeaponsMax = new()
        {
            ["SwordT09"] = "Item_Weapon_Sword_T09_ShadowMatter",
            ["AxeT09"] = "Item_Weapon_Axe_T09_ShadowMatter",
            ["MaceT09"] = "Item_Weapon_Mace_T09_ShadowMatter",
            ["SpearT09"] = "Item_Weapon_Spear_T09_ShadowMatter",
            ["SlashersT09"] = "Item_Weapon_Slashers_T09_ShadowMatter",
            ["ReaperT09"] = "Item_Weapon_Reaper_T09_ShadowMatter",
            ["CrossbowT09"] = "Item_Weapon_Crossbow_T09_ShadowMatter",
            ["LongbowT09"] = "Item_Weapon_Longbow_T09_ShadowMatter",
            ["PistolsT09"] = "Item_Weapon_Pistols_T09_ShadowMatter",
            ["GreatSwordT09"] = "Item_Weapon_GreatSword_T09_ShadowMatter",
            ["WhipT09"] = "Item_Weapon_Whip_T09_ShadowMatter"
        };

        public static readonly Dictionary<string, (string Prefab, int Amount)> Consumables = new()
        {
            ["BloodRoseBrew"] = ("Item_Consumable_BloodRoseBrew", 20),
            ["VerminSalve"] = ("Item_Consumable_VerminSalve", 20),
            ["PotionHealing"] = ("Item_Consumable_Potion_Healing_T03", 10),
            ["PotionSpellPower"] = ("Item_Consumable_Potion_SpellPower_T03", 10),
            ["PotionPhysicalPower"] = ("Item_Consumable_Potion_PhysicalPower_T03", 10),
            ["BrewRage"] = ("Item_Consumable_Brew_Rage_T03", 10),
            ["BrewWrangler"] = ("Item_Consumable_Brew_Wrangler_T03", 10),
            ["GarlicResistance"] = ("Item_Consumable_GarlicResistance_T03", 10),
            ["HolyResistance"] = ("Item_Consumable_HolyResistance_T03", 10),
            ["SilverResistance"] = ("Item_Consumable_SilverResistance_T03", 10)
        };

        public static readonly Dictionary<string, (string Veil, string Other)> AbilityTypes4 = new()
        {
            ["Blood"] = ("Spell_VeilOfBlood", "AB_Blood_BloodRage_Buff"),
            ["Chaos"] = ("Spell_VeilOfChaos", "AB_Chaos_Volley_Projectile"),
            ["Frost"] = ("Spell_VeilOfFrost", "AB_Frost_FrostBat_Projectile"),
            ["Unholy"] = ("Spell_VeilOfBones", "AB_Unholy_CorruptedSkull_Projectile")
        };
    }
}
