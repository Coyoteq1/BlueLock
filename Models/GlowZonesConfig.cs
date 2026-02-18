using System.Collections.Generic;

namespace VAuto.Zone.Models
{
    public sealed class GlowZonesConfig
    {
        public int SchemaVersion { get; set; } = 2;
        public bool Enabled { get; set; } = false;
        public string? DefaultGlowPrefab { get; set; }
        public List<GlowZoneEntry> Zones { get; set; } = new();
    }
}
