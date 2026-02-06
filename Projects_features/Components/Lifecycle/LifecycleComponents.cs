using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

namespace VAuto.Core.Components.Lifecycle
{
    public struct LifecycleState : IComponentData
    {
        public Entity CurrentZone;
        public Entity PendingZone;
        public bool InLifecycleZone;
        public bool AutoEnterEnabled;
        public bool KitApplied;
        public bool VBloodsUnlocked;
        public bool SpellbooksGranted;
        public float LastTransitionTime;
        public int RepairThreshold;
    }

    public struct LifecycleZone : IComponentData
    {
        public ZoneType Type;
        public bool AllowAutoEnter;
        public FixedString32Bytes GearLoadout;
        public bool AutoRepairOnEntry;
        public bool AutoRepairOnExit;
        public int RepairThreshold;
        public bool UnlockVBloods;
        public bool GrantSpellbooks;
        public float Radius;
        public float3 Center;
    }

    public enum ZoneType : byte { World, MainArena, PvPArena, SafeZone, GlowZone, Custom }

    // Request markers
    public struct AutoEnterRequest : IComponentData { }
    public struct KitApplyRequest : IComponentData { public FixedString32Bytes KitName; }
    public struct SpellbookGrantRequest : IComponentData { }
}
