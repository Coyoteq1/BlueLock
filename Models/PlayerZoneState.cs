namespace VAutomationCore.Models
{
    /// <summary>
    /// Zone lifecycle states for explicit state machine.
    /// </summary>
    public enum ZoneLifecycleState
    {
        None,           // Not in any zone
        Entering,       // Transitioning into a zone
        Active,         // Fully inside a zone
        Exiting,        // Transitioning out of a zone
        Cooldown        // Debounce period after rapid transition
    }

    /// <summary>
    /// Represents a player's current zone state with explicit state machine.
    /// </summary>
    public class PlayerZoneState
    {
        public string CurrentZoneId { get; set; } = string.Empty;
        public string PreviousZoneId { get; set; } = string.Empty;
        public bool WasInZone { get; set; }
        public bool IsInAnyZone { get; set; }
        public BloodType BloodType { get; set; } = BloodType.Frail;
        public float BloodQuality { get; set; }
        public long LastUpdateTimestamp { get; set; }
        public DateTime? EnteredAt { get; set; }
        public DateTime? ExitedAt { get; set; }
        
        // State machine properties
        public ZoneLifecycleState State { get; set; } = ZoneLifecycleState.None;
        public DateTime? LastTransitionTime { get; set; }
        public int RapidTransitionCount { get; set; }
        public string LastZoneId { get; set; } = string.Empty;
        
        /// <summary>
        /// Checks if the player is in a stable state (no active transitions).
        /// </summary>
        public bool IsStable => State == ZoneLifecycleState.None || State == ZoneLifecycleState.Active;
        
        /// <summary>
        /// Checks if the player is in a transition state.
        /// </summary>
        public bool IsTransitioning => State == ZoneLifecycleState.Entering || State == ZoneLifecycleState.Exiting;
        
        /// <summary>
        /// Checks if the player is in cooldown period.
        /// </summary>
        public bool IsInCooldown => State == ZoneLifecycleState.Cooldown;
    }
}
