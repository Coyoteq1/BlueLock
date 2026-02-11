using System;

namespace VAuto.Zone.Models
{
    /// <summary>
    /// Player zone state model for tracking arena zone transitions.
    /// </summary>
    public class PlayerZoneState
    {
        public string CurrentZoneId { get; set; } = string.Empty;
        public string PreviousZoneId { get; set; } = string.Empty;
        public bool WasInZone { get; set; }
        public DateTime EnteredAt { get; set; }
        public DateTime ExitedAt { get; set; }
    }
}
