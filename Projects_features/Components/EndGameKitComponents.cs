using Unity.Entities;
using ProjectM;

namespace VAuto.Core.Components
{
    /// <summary>
    /// Component tag to track end-game kit application state per player.
    /// Prevents re-application spam and desync issues.
    /// </summary>
    public struct PlayerEndGameKitState : IComponentData
    {
        /// <summary>
        /// Whether the full end-game kit has been applied to this player
        /// </summary>
        public bool KitApplied;

        /// <summary>
        /// Name of the applied kit profile
        /// </summary>
        public FixedString64Bytes AppliedKitName;

        /// <summary>
        /// Timestamp when kit was applied (for cooldown/refresh logic)
        /// </summary>
        public double AppliedTimestamp;

        /// <summary>
        /// Whether previous gear was saved for restoration
        /// </summary>
        public bool PreviousGearSaved;
    }

    /// <summary>
    /// Buffer element to store saved equipment for restoration on exit.
    /// </summary>
    [InternalBufferCapacity(12)]
    public struct SavedEquipmentBuffer : IBufferElementData
    {
        public EquipmentSlot Slot;
        public PrefabGUID ItemGuid;
    }

    /// <summary>
    /// Tag component for players in an end-game zone.
    /// Used to trigger auto-kit application on entry.
    /// </summary>
    public struct InEndGameZoneTag : IComponentData
    {
        /// <summary>
        /// Name of the zone configuration
        /// </summary>
        public FixedString64Bytes ZoneName;

        /// <summary>
        /// Kit profile to apply in this zone
        /// </summary>
        public FixedString64Bytes KitProfileName;
    }

    /// <summary>
    /// Singleton component to hold end-game kit configuration.
    /// </summary>
    public struct EndGameKitConfig : IComponentData
    {
        /// <summary>
        /// Whether the end-game kit system is enabled
        /// </summary>
        public bool Enabled;

        /// <summary>
        /// Default kit profile name to use
        /// </summary>
        public FixedString64Bytes DefaultKitProfile;

        /// <summary>
        /// Whether to apply kits automatically on zone entry
        /// </summary>
        public bool AutoApplyOnEntry;

        /// <summary>
        /// Whether to restore gear when exiting end-game zone
        /// </summary>
        public bool RestoreOnExit;

        /// <summary>
        /// Whether to restrict kits to non-PvP zones
        /// </summary>
        public bool PvPRestriction;

        /// <summary>
        /// Minimum gear score required for kit (0 = no requirement)
        /// </summary>
        public int MinimumGearScore;
    }
}
