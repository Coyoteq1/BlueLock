using System;
using System.Collections.Generic;

namespace VAuto.Core.Lifecycle.Snapshots
{
    public enum VBloodSnapshotMode
    {
        Ignore,
        RepairOnly,
        RestoreExact
    }

    public sealed class CharacterSnapshot
    {
        public int SchemaVersion { get; set; } = 1;
        public string ArenaId { get; set; } = string.Empty;
        public ulong PlatformId { get; set; }
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        public List<InventorySlotSnapshot> Inventory { get; set; } = new();
        public List<EquipmentSlotSnapshot> Equipment { get; set; } = new();
        public List<JewelSocketSnapshot> Jewels { get; set; } = new();
        public List<SpellSlotSnapshot> Spells { get; set; } = new();
        public List<long> Buffs { get; set; } = new();

        public VBloodSnapshotMode VBloodMode { get; set; } = VBloodSnapshotMode.RepairOnly;
        public List<long>? VBloodUnlocks { get; set; }
    }

    public sealed class InventorySlotSnapshot
    {
        public int Slot { get; set; }
        public long PrefabGuid { get; set; }
        public int Amount { get; set; }
    }

    public sealed class EquipmentSlotSnapshot
    {
        public string Slot { get; set; } = string.Empty;
        public long PrefabGuid { get; set; }
    }

    public sealed class JewelSocketSnapshot
    {
        public int WeaponSlot { get; set; }
        public int SocketIndex { get; set; }
        public long PrefabGuid { get; set; }
    }

    public sealed class SpellSlotSnapshot
    {
        public int SlotIndex { get; set; }
        public long PrefabGuid { get; set; }
        public int? Level { get; set; }
    }
}
