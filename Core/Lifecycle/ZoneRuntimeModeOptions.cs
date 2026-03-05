using System;

namespace VAutomationCore.Core.Lifecycle
{
    /// <summary>
    /// Runtime mode options for zone execution pipeline.
    /// Note: Legacy and Hybrid modes are deprecated and normalized to ECS-only behavior.
    /// </summary>
    public sealed class ZoneRuntimeModeOptions
    {
        public ZoneRuntimeMode RuntimeMode { get; set; } = ZoneRuntimeMode.EcsOnly;
        public bool EcsZoneDetectionEnabled { get; set; }
        public bool EcsFlowExecutionEnabled { get; set; }
        public bool EcsTemplateLifecycleEnabled { get; set; }
        public bool EcsStepExecutionEnabled { get; set; }
        public bool LegacyZonePipelineEnabled { get; set; }

        /// <summary>
        /// True if a deprecated runtime mode (Legacy or Hybrid) was requested.
        /// Consumers should log a warning when this is true.
        /// </summary>
        public bool WasDeprecatedModeRequested { get; private set; }

        /// <summary>
        /// Gets a human-readable warning message for deprecated mode usage.
        /// </summary>
        public string GetDeprecationWarning()
        {
            if (!WasDeprecatedModeRequested)
            {
                return string.Empty;
            }

            return $"Runtime mode '{RuntimeMode}' is deprecated. Effective mode is 'EcsOnly' with full ECS pipeline enabled. " +
                   "Legacy pipeline support has been removed. Please update your configuration to use 'EcsOnly'.";
        }

        public static ZoneRuntimeModeOptions FromMode(ZoneRuntimeMode mode)
        {
            if (!Enum.IsDefined(typeof(ZoneRuntimeMode), mode))
            {
                throw new InvalidOperationException("Invalid zone runtime mode.");
            }

            // Compatibility API: keep accepting legacy/hybrid enum values while runtime
            // behavior is normalized to ECS-only execution.
            var wasDeprecated = mode is ZoneRuntimeMode.Legacy or ZoneRuntimeMode.Hybrid;

            return new ZoneRuntimeModeOptions
            {
                RuntimeMode = mode,
                EcsZoneDetectionEnabled = true,
                EcsFlowExecutionEnabled = true,
                EcsTemplateLifecycleEnabled = true,
                EcsStepExecutionEnabled = false,
                LegacyZonePipelineEnabled = false,
                WasDeprecatedModeRequested = wasDeprecated
            };
        }
    }
}
