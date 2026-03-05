using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Blueluck.Models
{
    /// <summary>
    /// Stores player progress data for save/restore functionality in ArenaZone.
    /// </summary>
    public class PlayerProgress
    {
        /// <summary>
        /// Entity index for identification.
        /// </summary>
        public int EntityIndex { get; set; }

        /// <summary>
        /// Timestamp when progress was saved.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Saved position.
        /// </summary>
        public float3 Position { get; set; }

        /// <summary>
        /// Saved health value.
        /// </summary>
        public float Health { get; set; }

        /// <summary>
        /// Saved blood type.
        /// </summary>
        public int BloodType { get; set; }

        /// <summary>
        /// Saved blood quality.
        /// </summary>
        public float BloodQuality { get; set; }

        /// <summary>
        /// Snapshot of active buff prefab hashes present at save time.
        /// Note: this is used for "restore to snapshot" semantics, not for perfect duration replication.
        /// </summary>
        public List<int> BuffPrefabHashes { get; set; } = new();
    }

    /// <summary>
    /// Represents a single inventory item.
    /// </summary>
    public class InventoryItem
    {
        /// <summary>
        /// Prefab GUID hash.
        /// </summary>
        public int PrefabHash { get; set; }

        /// <summary>
        /// Item count/stack size.
        /// </summary>
        public int Count { get; set; }

        /// <summary>
        /// Slot position (for equipment).
        /// </summary>
        public int Slot { get; set; }
    }

    /// <summary>
    /// Player zone state tracking.
    /// </summary>
    public class PlayerZoneState
    {
        /// <summary>
        /// Current zone hash (0 = no zone).
        /// </summary>
        public int CurrentZoneHash { get; set; }

        /// <summary>
        /// Previous zone hash.
        /// </summary>
        public int PreviousZoneHash { get; set; }

        /// <summary>
        /// When player entered current zone.
        /// </summary>
        public DateTime? ZoneEnterTime { get; set; }

        /// <summary>
        /// Whether player has saved progress.
        /// </summary>
        public bool HasSavedProgress { get; set; }
    }
}
