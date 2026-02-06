using Unity.Entities;
using Unity.Mathematics;

namespace VAuto.Core.Components
{
    /// <summary>
    /// Waypoint trap configuration and state.
    /// Separate from container traps with stricter requirements (10-kill streak).
    /// </summary>
    public struct WaypointTrap : IComponentData
    {
        public int WaypointId;
        public int RegionId;
        public bool Armed;
        public ulong OwnerPlatformId;
        public int RequiredStreakToBypass; // 10
        public int RequiredStreakToArm;    // 10
        public int DamageAbilityPrefabId;
        public int VfxPrefabId;
        public float DamageRadius;
        public float DamageAmount;
        public float CooldownSeconds;
        public double LastTriggeredTime;
    }

    /// <summary>
    /// Visual effect configuration for traps (glow, particles).
    /// </summary>
    public struct TrapVisualConfig : IComponentData
    {
        public bool HasGlow;
        public float3 GlowColor;
        public float GlowIntensity;
        public float PulseSpeed;
        public int ParticlePrefabId;
    }

    /// <summary>
    /// Event for waypoint trap trigger.
    /// </summary>
    public struct WaypointTrapTriggeredEvent : IComponentData
    {
        public int WaypointId;
        public int RegionId;
        public ulong TriggeredByPlatformId;
        public float3 Position;
        public bool WasBypassed;  // true if player had sufficient streak
    }
}
