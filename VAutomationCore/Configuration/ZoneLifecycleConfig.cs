using System.Collections.Generic;

namespace VAutomationCore.Configuration
{
    /// <summary>
    /// Zone-Lifecycle wiring configuration model.
    /// Maps zones to lifecycle actions for enter/exit events.
    /// </summary>
    public class ZoneLifecycleConfig
    {
        /// <summary>
        /// Enable zone-to-lifecycle event wiring
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// How often to check player positions for zone transitions (milliseconds)
        /// </summary>
        public int CheckIntervalMs { get; set; } = 100;

        /// <summary>
        /// Distance threshold to consider a position change significant enough to check zones
        /// </summary>
        public float PositionChangeThreshold { get; set; } = 1.0f;

        /// <summary>
        /// Zone ID to lifecycle action mappings
        /// </summary>
        public Dictionary<string, ZoneLifecycleMapping> Mappings { get; set; } = new();
    }

    /// <summary>
    /// Maps lifecycle actions for entering/exiting a specific zone
    /// </summary>
    public class ZoneLifecycleMapping
    {
        /// <summary>
        /// Actions to execute when entering this zone
        /// </summary>
        public List<string> OnEnter { get; set; } = new();

        /// <summary>
        /// Actions to execute when exiting this zone
        /// </summary>
        public List<string> OnExit { get; set; } = new();

        /// <summary>
        /// Whether to use global defaults if no specific mapping exists
        /// </summary>
        public bool UseGlobalDefaults { get; set; } = true;
    }

    /// <summary>
    /// Default lifecycle actions applied to all zones unless overridden
    /// </summary>
    public class GlobalLifecycleDefaults
    {
        /// <summary>
        /// Default actions when entering any arena zone
        /// </summary>
        public List<string> DefaultEnterActions { get; set; } = new()
        {
            "storeInventory",
            "storeBuffs"
        };

        /// <summary>
        /// Default actions when exiting any arena zone
        /// </summary>
        public List<string> DefaultExitActions { get; set; } = new()
        {
            "restoreInventory",
            "restoreBuffs"
        };
    }
}
