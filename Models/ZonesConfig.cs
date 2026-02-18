using System.Collections.Generic;
using Unity.Mathematics;

namespace VAuto.Zone.Models
{
    /// <summary>
    /// Container for all zone configurations.
    /// </summary>
    public class ZonesConfig
    {
        /// <summary>
        /// Description of this zone configuration.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Default kit ID to apply when no zone-specific kit is set.
        /// </summary>
        public string DefaultKitId { get; set; }

        /// <summary>
        /// Default zone ID to use.
        /// </summary>
        public string DefaultZoneId { get; set; }

        /// <summary>
        /// Default teleport position for the zone system.
        /// </summary>
        public float3 DefaultTeleport { get; set; }

        /// <summary>
        /// Default border config used when per-zone border config is missing or incomplete.
        /// </summary>
        public ZoneBorderConfig DefaultBorder { get; set; } = new ZoneBorderConfig();

        /// <summary>
        /// List of zone definitions.
        /// </summary>
        public List<ZoneDefinition> Zones { get; set; } = new List<ZoneDefinition>();
    }
}
