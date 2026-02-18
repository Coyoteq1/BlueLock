using System;

namespace VAuto.Zone.Models
{
    /// <summary>
    /// Border spawning configuration for a zone (visual marker prefab + placement).
    /// </summary>
    public sealed class ZoneBorderConfig
    {
        /// <summary>Whether border spawning is enabled for this zone.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Prefab name for the border marker (preferred if Guid is 0).</summary>
        public string PrefabName { get; set; } = string.Empty;

        /// <summary>Prefab GuidHash for the border marker.</summary>
        public int PrefabGuid { get; set; }

        /// <summary>Distance between border markers along the perimeter.</summary>
        public float Spacing { get; set; } = 3f;

        /// <summary>Extra Y offset applied when spawning border markers.</summary>
        public float HeightOffset { get; set; } = 0f;
    }
}

