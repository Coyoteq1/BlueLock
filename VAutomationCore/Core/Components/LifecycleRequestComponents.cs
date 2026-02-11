using Unity.Entities;
using Unity.Mathematics;
using Stunlock.Core;

namespace VAuto.Core.Components
{
    /// <summary>
    /// Base component for all lifecycle requests with common fields.
    /// Implements IComponentData for Unity ECS compatibility.
    /// </summary>
    public abstract class LifecycleRequestBase : IComponentData
    {
        public RequestType Type { get; set; }
        public float Timestamp { get; set; }
        public Entity SourceZone { get; set; }
        public Entity TargetZone { get; set; }
        public RequestStatus Status { get; set; }
        public string ErrorMessage { get; set; }
    }

    /// <summary>
    /// Enumeration of request types for lifecycle automation.
    /// </summary>
    public enum RequestType : byte
    {
        ZoneTransition = 0,
        Repair = 1,
        VBloodUnlock = 2,
        SpellbookGrant = 3
    }

    /// <summary>
    /// Status tracking for request processing lifecycle.
    /// </summary>
    public enum RequestStatus : byte
    {
        Pending = 0,
        Processing = 1,
        Completed = 2,
        Failed = 3
    }

    /// <summary>
    /// Zone transition request for arena entry/exit events.
    /// Triggers lifecycle handler chains when players cross zone boundaries.
    /// </summary>
    public struct ZoneTransitionRequest : IComponentData
    {
        public TransitionDirection Direction;
        public bool TriggeredByPlayer;
        public float3 Position;
    }

    /// <summary>
    /// Direction of zone transition.
    /// </summary>
    public enum TransitionDirection : byte
    {
        Enter = 0,
        Exit = 1
    }

    /// <summary>
    /// Equipment durability repair request.
    /// Generated when items fall below durability threshold.
    /// </summary>
    public struct RepairRequest : IComponentData
    {
        public int ItemSlot;
        public RepairAmount Amount;
        public RepairTriggerCondition TriggerCondition;
        public int DurabilityThreshold;
    }

    /// <summary>
    /// Amount of durability to restore during repair.
    /// </summary>
    public enum RepairAmount : byte
    {
        Full = 0,
        Partial = 1
    }

    /// <summary>
    /// Condition that triggered the repair request.
    /// </summary>
    public enum RepairTriggerCondition : byte
    {
        OnZoneEnter = 0,
        OnZoneExit = 1,
        OnCommand = 2,
        PeriodicCheck = 3
    }

    /// <summary>
    /// VBlood boss unlock request.
    /// Generated when players defeat VBlood bosses or enter unlock zones.
    /// </summary>
    public struct VBloodUnlockRequest : IComponentData
    {
        public PrefabGUID BossType;
        public int UnlockPriority;
        public bool ForceUnlockOverride;
    }

    /// <summary>
    /// Spellbook granting request.
    /// Generated when players enter zones with spellbook rewards.
    /// </summary>
    public struct SpellbookGrantRequest : IComponentData
    {
        public PrefabGUID SpellId;
        public GrantReason Reason;
        public int Priority;
    }

    /// <summary>
    /// Reason for granting a spellbook.
    /// </summary>
    public enum GrantReason : byte
    {
        ZoneEnter = 0,
        QuestReward = 1,
        AdminCommand = 2
    }
}
