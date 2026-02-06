using System.Collections.Generic;
using Unity.Mathematics;

namespace VAuto.Arena.Models
{
    internal sealed class GlowZonesConfig
    {
        public int SchemaVersion { get; set; } = 2;
        public string? DefaultGlowPrefab { get; set; }
        public List<GlowZoneEntry> Zones { get; set; } = new();
    }

    internal sealed class GlowZoneEntry
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
    }

    internal sealed class GlowRotationConfig
    {
        public bool Enabled { get; set; } = false;
        public int IntervalSeconds { get; set; } = 120;
        public string Mode { get; set; } = "sequential";
    }

    internal sealed class MapIconConfig
    {
        public bool Enabled { get; set; } = true;
        public string PrefabName { get; set; } = "ZoneIcon_Default";
        public bool ShowWhenPlayersInside { get; set; } = true;
    }
}
