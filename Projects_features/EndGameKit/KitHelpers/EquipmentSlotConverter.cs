using System;
using System.Collections.Generic;
using System.Linq;
using ProjectM;

namespace VAuto.EndGameKit.Helpers
{
    /// <summary>
    /// Converter utilities for equipment slot operations.
    /// Provides safe parsing and conversion between slot names and enum values.
    /// </summary>
    public static class EquipmentSlotConverter
    {
        /// <summary>
        /// Mapping of slot name variations to EquipmentSlot enum.
        /// </summary>
        private static readonly Dictionary<string, EquipmentSlot> SlotMappings = new(StringComparer.OrdinalIgnoreCase)
        {
            // Main hand weapons
            ["MainHand"] = EquipmentSlot.MainHand,
            ["Main Hand"] = EquipmentSlot.MainHand,
            ["mainhand"] = EquipmentSlot.MainHand,
            
            // Off hand
            ["OffHand"] = EquipmentSlot.OffHand,
            ["Off Hand"] = EquipmentSlot.OffHand,
            ["offhand"] = EquipmentSlot.OffHand,
            
            // Armor slots
            ["Head"] = EquipmentSlot.Head,
            ["helm"] = EquipmentSlot.Head,
            
            ["Chest"] = EquipmentSlot.Chest,
            ["body"] = EquipmentSlot.Chest,
            
            ["Legs"] = EquipmentSlot.Legs,
            ["pants"] = EquipmentSlot.Legs,
            
            ["Feet"] = EquipmentSlot.Feet,
            ["boots"] = EquipmentSlot.Feet,
            
            ["Hands"] = EquipmentSlot.Hands,
            ["gloves"] = EquipmentSlot.Hands,
            
            // Jewelry slots
            ["Neck"] = EquipmentSlot.Neck,
            ["necklace"] = EquipmentSlot.Neck,
            
            ["Finger1"] = EquipmentSlot.Finger1,
            ["Ring1"] = EquipmentSlot.Finger1,
            ["Finger 1"] = EquipmentSlot.Finger1,
            
            ["Finger2"] = EquipmentSlot.Finger2,
            ["Ring2"] = EquipmentSlot.Finger2,
            ["Finger 2"] = EquipmentSlot.Finger2,
        };

        /// <summary>
        /// Tries to parse a slot name to EquipmentSlot enum.
        /// </summary>
        /// <param name="slotName">The slot name to parse.</param>
        /// <param name="slot">Output slot enum value.</param>
        /// <returns>True if parsing successful.</returns>
        public static bool TryParse(string slotName, out EquipmentSlot slot)
        {
            if (string.IsNullOrEmpty(slotName))
            {
                slot = EquipmentSlot.None;
                return false;
            }

            // Try direct enum parsing first (case-sensitive)
            if (Enum.TryParse<EquipmentSlot>(slotName, out var directResult))
            {
                slot = directResult;
                return true;
            }

            // Try mapping lookup
            if (SlotMappings.TryGetValue(slotName, out var mappedSlot))
            {
                slot = mappedSlot;
                return true;
            }

            slot = EquipmentSlot.None;
            return false;
        }

        /// <summary>
        /// Parses a slot name to EquipmentSlot enum, throwing on failure.
        /// </summary>
        /// <param name="slotName">The slot name to parse.</param>
        /// <returns>The EquipmentSlot enum value.</returns>
        /// <exception cref="ArgumentException">Thrown when slot name is invalid.</exception>
        public static EquipmentSlot Parse(string slotName)
        {
            if (TryParse(slotName, out var slot))
            {
                return slot;
            }

            throw new ArgumentException($"Invalid equipment slot name: {slotName}", nameof(slotName));
        }

        /// <summary>
        /// Converts EquipmentSlot to integer index for ServerGameManager.EquipItem.
        /// </summary>
        public static int ToSlotIndex(this EquipmentSlot slot)
        {
            return (int)slot;
        }

        /// <summary>
        /// Converts slot name to integer index.
        /// </summary>
        public static int ToSlotIndex(string slotName)
        {
            if (TryParse(slotName, out var slot))
            {
                return slot.ToSlotIndex();
            }

            throw new ArgumentException($"Invalid equipment slot name: {slotName}", nameof(slotName));
        }

        /// <summary>
        /// Gets all valid slot names.
        /// </summary>
        public static IEnumerable<string> GetAllSlotNames()
        {
            return Enum.GetNames(typeof(EquipmentSlot))
                .Where(n => n != "None" && n != "__None")
                .Concat(SlotMappings.Keys.Where(k => !Enum.GetNames(typeof(EquipmentSlot)).Contains(k, StringComparer.OrdinalIgnoreCase)))
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if a slot name is valid.
        /// </summary>
        public static bool IsValidSlot(string slotName)
        {
            return TryParse(slotName, out var slot) && slot != EquipmentSlot.None;
        }

        /// <summary>
        /// Gets all equipment slots as an enumerable.
        /// </summary>
        public static IEnumerable<EquipmentSlot> GetAllSlots()
        {
            return Enum.GetValues(typeof(EquipmentSlot))
                .Cast<EquipmentSlot>()
                .Where(s => s != EquipmentSlot.None);
        }

        /// <summary>
        /// Gets the friendly name for a slot.
        /// </summary>
        public static string GetFriendlyName(EquipmentSlot slot)
        {
            return slot switch
            {
                EquipmentSlot.MainHand => "Main Hand",
                EquipmentSlot.OffHand => "Off Hand",
                EquipmentSlot.Head => "Head",
                EquipmentSlot.Chest => "Chest",
                EquipmentSlot.Legs => "Legs",
                EquipmentSlot.Feet => "Feet",
                EquipmentSlot.Hands => "Hands",
                EquipmentSlot.Neck => "Neck",
                EquipmentSlot.Finger1 => "Ring 1",
                EquipmentSlot.Finger2 => "Ring 2",
                _ => slot.ToString()
            };
        }

        /// <summary>
        /// Gets the friendly name for a slot by name.
        /// </summary>
        public static string GetFriendlyName(string slotName)
        {
            if (TryParse(slotName, out var slot))
            {
                return GetFriendlyName(slot);
            }

            return slotName;
        }
    }
}
