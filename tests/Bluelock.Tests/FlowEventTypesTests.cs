using System;
using Xunit;

namespace Bluelock.Tests
{
    /// <summary>
    /// Tests for flow stabilization framework components.
    /// </summary>
    public class FlowEventTypesTests
    {
        [Fact]
        public void EventTypeAssemblies_AreReferenced()
        {
            // Verify that the events namespace is available
            var eventsNamespace = typeof(VAutomationCore.Core.Events.ServerStartedEvent).Namespace;
            Assert.Equal("VAutomationCore.Core.Events", eventsNamespace);
        }

        [Fact]
        public void ZoneTransitionEvent_IsStruct()
        {
            // Zone Transition Event - struct for ECS efficiency
            var type = typeof(VAutomationCore.Core.ECS.Components.ZoneTransitionEvent);
            Assert.True(type.IsValueType);
        }

        [Fact]
        public void TypedEventBus_Exists()
        {
            var busType = typeof(VAutomationCore.Core.Events.TypedEventBus);
            Assert.NotNull(busType);
            Assert.True(busType.IsPublic);
        }
    }
}
