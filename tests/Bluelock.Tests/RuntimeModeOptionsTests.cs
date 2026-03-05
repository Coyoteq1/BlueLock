using VAutomationCore.Core.Lifecycle;
using Xunit;

namespace Bluelock.Tests
{
    public class RuntimeModeOptionsTests
    {
        [Fact]
        public void Legacy_Mode_IsNormalizedToEcsPipeline()
        {
            var options = ZoneRuntimeModeOptions.FromMode(ZoneRuntimeMode.Legacy);
            Assert.Equal(ZoneRuntimeMode.Legacy, options.RuntimeMode);
            // Legacy mode is normalized to ECS pipeline at boot
            Assert.True(options.EcsZoneDetectionEnabled);
            Assert.True(options.EcsFlowExecutionEnabled);
            Assert.True(options.EcsTemplateLifecycleEnabled);
            Assert.False(options.LegacyZonePipelineEnabled);
            Assert.True(options.WasDeprecatedModeRequested);
            Assert.Contains("deprecated", options.GetDeprecationWarning(), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Hybrid_Mode_IsNormalizedToEcsPipeline()
        {
            var options = ZoneRuntimeModeOptions.FromMode(ZoneRuntimeMode.Hybrid);
            Assert.Equal(ZoneRuntimeMode.Hybrid, options.RuntimeMode);
            // Hybrid mode is normalized to ECS-only pipeline
            Assert.True(options.EcsZoneDetectionEnabled);
            Assert.True(options.EcsFlowExecutionEnabled);
            Assert.True(options.EcsTemplateLifecycleEnabled);
            Assert.False(options.LegacyZonePipelineEnabled);
            Assert.True(options.WasDeprecatedModeRequested);
            Assert.Contains("deprecated", options.GetDeprecationWarning(), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void EcsOnly_Mode_MapsToEcsPipelineOnly()
        {
            var options = ZoneRuntimeModeOptions.FromMode(ZoneRuntimeMode.EcsOnly);
            Assert.Equal(ZoneRuntimeMode.EcsOnly, options.RuntimeMode);
            Assert.True(options.EcsZoneDetectionEnabled);
            Assert.True(options.EcsFlowExecutionEnabled);
            Assert.True(options.EcsTemplateLifecycleEnabled);
            Assert.False(options.LegacyZonePipelineEnabled);
            Assert.False(options.WasDeprecatedModeRequested);
            Assert.Equal(string.Empty, options.GetDeprecationWarning());
        }
    }
}
