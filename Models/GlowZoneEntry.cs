using System.Collections.Generic;
using Unity.Mathematics;

namespace VAuto.Zone.Models
{
    public sealed class GlowZoneEntry
    {
        public string Id { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
        public float3 Center { get; set; }
        public float? Radius { get; set; }
        public float2? HalfExtents { get; set; }
        public float BorderSpacing { get; set; } = 3f;
        public float CornerOffset { get; set; } = 0f;
        public bool SpawnEmptyMarkers { get; set; } = true;
        public List<string> GlowPrefabs { get; set; } = new();
        public GlowRotationConfig Rotation { get; set; } = new();
        public MapIconConfig MapIcon { get; set; } = new();
        // Glow configuration
        public float3? GlowColor { get; set; }
        public float? GlowIntensity { get; set; }
        public float? GlowRadius { get; set; }
        public float? GlowDuration { get; set; }
        public int? BuffId { get; set; }
    }

    public sealed class GlowRotationConfig
    {
        public bool Enabled { get; set; } = false;
        public int IntervalSeconds { get; set; } = 120;
        public string Mode { get; set; } = "sequential";
    }

    public sealed class MapIconConfig
    {
        public bool Enabled { get; set; } = true;
        public string PrefabName { get; set; } = "ZoneIcon_Default";
        public bool ShowWhenPlayersInside { get; set; } = true;
    }
}