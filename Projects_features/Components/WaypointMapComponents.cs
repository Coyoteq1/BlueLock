using Unity.Entities;
using Unity.Mathematics;

namespace VAuto.Core.Components
{
    /// <summary>
    /// Waypoint data stored in a dynamic buffer on a singleton entity.
    /// </summary>
    public struct Waypoint : IBufferElementData
    {
        public int WaypointId;     // 0..9
        public int RegionId;       // region grouping
        public float3 Position;
        public float Radius;       // for proximity triggers
    }

    /// <summary>
    /// Tag component for the waypoint map singleton entity.
    /// </summary>
    public struct WaypointMapTag : IComponentData { }

    /// <summary>
    /// Singleton containing waypoint trap configuration.
    /// </summary>
    public struct WaypointTrapConfig : IComponentData
    {
        public int RequiredStreakToBypass; // 10
        public int RequiredStreakToArm;    // 10
        public float DamageRadius;
        public float DamageAmount;
        public float CooldownSeconds;
        public int DamageAbilityPrefabId;
        public int VfxPrefabId;
    }
}
