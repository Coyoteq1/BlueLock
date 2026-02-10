using Unity.Mathematics;
using System.Collections.Generic;
using Stunlock.Core; 
using ProjectM;

namespace VAutomationCore.Models
{
    /// <summary>
    /// Represents a glow zone entry with position, rotation, and prefab information.
    /// </summary>
    public class GlowZoneEntry
    {
        public string Name { get; set; } = string.Empty;
        public float3 Position { get; set; }
        public float Radius { get; set; }
        public List<PrefabGUID> PrefabIds { get; set; } = new List<PrefabGUID>();
        public float RotationIntervalSeconds { get; set; }
        public bool IsActive { get; set; }
    }
}
