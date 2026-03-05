using System.Collections.Generic;

namespace Blueluck.Models
{
    /// <summary>
    /// Represents a preset hotbar configuration for zones.
    /// </summary>
    public class HotbarPreset
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Dictionary<int, int> HotbarSlots { get; set; } = new(); // Slot 0-9 for hotbar
        public bool AutoEquip { get; set; } = true;
        public bool ClearExisting { get; set; } = true;
    }

    /// <summary>
    /// Represents a preset equipment configuration for zones.
    /// </summary>
    public class EquipmentPreset
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Dictionary<string, int> Items { get; set; } = new(); // Weapon, Chest, Legs, Boots etc.
        public Dictionary<int, int> HotbarSlots { get; set; } = new(); // Hotbar slots 0-9
        public bool AutoEquip { get; set; } = true;
        public bool ClearExisting { get; set; } = true;
    }

    /// <summary>
    /// Configuration for equipment presets per zone.
    /// </summary>
    public class EquipmentConfig
    {
        public Dictionary<string, EquipmentPreset> Presets { get; set; } = new();
        public Dictionary<string, string> ZonePresets { get; set; } = new();
        public Dictionary<string, int> ItemAliases { get; set; } = new();
    }

    /// <summary>
    /// Player equipment state tracking.
    /// </summary>
    public class PlayerEquipmentState
    {
        public string? CurrentPreset { get; set; }
        public Dictionary<string, int> EquippedItems { get; set; } = new(); // Weapon, Chest, etc.
        public Dictionary<int, int> HotbarSlots { get; set; } = new(); // Hotbar slots 0-9
        public Dictionary<string, int> OriginalItems { get; set; } = new(); // Original equipment
        public Dictionary<int, int> OriginalHotbar { get; set; } = new(); // Original hotbar
        public bool HasOriginalEquipment { get; set; } = false;
    }

    /// <summary>
    /// Hotbar slot constants for V Rising.
    /// </summary>
    public static class HotbarSlots
    {
        public const int Slot1 = 0;
        public const int Slot2 = 1;
        public const int Slot3 = 2;
        public const int Slot4 = 3;
        public const int Slot5 = 4;
        public const int Slot6 = 5;
        public const int Slot7 = 6;
        public const int Slot8 = 7;
        public const int Slot9 = 8;
        public const int Slot0 = 9;
    }
}
