namespace VAutomationCore.Models
{
    /// <summary>
    /// Represents a player's current zone state.
    /// </summary>
    public class PlayerZoneState
    {
        public string CurrentZoneId { get; set; } = string.Empty;
        public string PreviousZoneId { get; set; } = string.Empty;
        public bool WasInZone { get; set; }
        public bool IsInAnyZone { get; set; }
        public long LastUpdateTimestamp { get; set; }
    }
}
