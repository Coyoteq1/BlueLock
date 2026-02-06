using Unity.Entities;
using Unity.Mathematics;

namespace VAuto.Core.Components
{
    /// <summary>
    /// Container trap configuration and state.
    /// Attached to container entities that have traps.
    /// </summary>
    public struct ContainerTrap : IComponentData
    {
        public bool Armed;
        public ulong OwnerPlatformId;
        public int DamageAbilityPrefabId;  // glowing damage entity prefab
        public int VfxPrefabId;            // optional extra VFX prefab
        public float DamageRadius;
        public float DamageAmount;
        public float LifetimeSeconds;
        public int MaxTriggers;
        public int TriggerCount;
        public double LastTriggeredTime;
        public float CooldownSeconds;
    }

    /// <summary>
    /// Event generated when a trap is triggered.
    /// Used for notifications and logging.
    /// </summary>
    public struct TrapTriggeredEvent : IComponentData
    {
        public ulong OwnerPlatformId;
        public ulong IntruderPlatformId;
        public float3 Position;
        public int WaypointId;
        public int RegionId;
    }

    /// <summary>
    /// Buffer to track trigger history for a trap.
    /// </summary>
    public struct TrapTriggerHistory : IBufferElementData
    {
        public double Timestamp;
        public ulong IntruderPlatformId;
        public float3 IntruderPosition;
    }
}
