using System;
using System.Threading.Tasks;
using Unity.Entities;
using VAutomationCore.Models;
using VAutomationCore.Services;
using Xunit;

namespace Bluelock.Tests
{
    /// <summary>
    /// Tests for Zone Lifecycle state machine and debouncing functionality.
    /// Covers Flow Stabilization Plan items: C) Tests, F) Verification
    /// </summary>
    public class ZoneLifecycleStateMachineTests
    {
        private readonly Entity _testPlayer = new Entity { Index = 1 };

        [Fact]
        public void ZoneLifecycleState_Enum_HasCorrectValues()
        {
            // Verify state machine enum has correct values
            Assert.Equal(5, Enum.GetValues<ZoneLifecycleState>().Length);
            Assert.Contains(ZoneLifecycleState.None, Enum.GetValues<ZoneLifecycleState>());
            Assert.Contains(ZoneLifecycleState.Entering, Enum.GetValues<ZoneLifecycleState>());
            Assert.Contains(ZoneLifecycleState.Active, Enum.GetValues<ZoneLifecycleState>());
            Assert.Contains(ZoneLifecycleState.Exiting, Enum.GetValues<ZoneLifecycleState>());
            Assert.Contains(ZoneLifecycleState.Cooldown, Enum.GetValues<ZoneLifecycleState>());
        }

        [Fact]
        public void PlayerZoneState_InitialState_HasCorrectDefaults()
        {
            // Verify initial state of PlayerZoneState
            var state = new PlayerZoneState();
            
            Assert.Equal(ZoneLifecycleState.None, state.State);
            Assert.Equal(string.Empty, state.CurrentZoneId);
            Assert.Equal(string.Empty, state.PreviousZoneId);
            Assert.False(state.WasInZone);
            Assert.False(state.IsInAnyZone);
            Assert.Equal(0, state.RapidTransitionCount);
            Assert.Null(state.LastTransitionTime);
            Assert.Equal(string.Empty, state.LastZoneId);
            Assert.True(state.IsStable);
            Assert.False(state.IsTransitioning);
            Assert.False(state.IsInCooldown);
        }

        [Fact]
        public void PlayerZoneState_HelperProperties_WorkCorrectly()
        {
            // Test helper properties
            var state = new PlayerZoneState
            {
                State = ZoneLifecycleState.Active,
                CurrentZoneId = "test-zone",
                PreviousZoneId = "previous-zone"
            };

            Assert.True(state.IsInAnyZone);
            Assert.True(state.IsStable);
            Assert.False(state.IsTransitioning);
            Assert.False(state.IsInCooldown);

            state.State = ZoneLifecycleState.Entering;
            Assert.True(state.IsTransitioning);
            Assert.False(state.IsStable);

            state.State = ZoneLifecycleState.Cooldown;
            Assert.True(state.IsInCooldown);
            Assert.False(state.IsStable);
            Assert.False(state.IsTransitioning);
        }

        [Fact]
        public void ZoneEventBridge_Initialize_SetsUpCorrectly()
        {
            // Test initialization
            ZoneEventBridge.Initialize();
            
            // Should not throw and should be ready for operations
            var state = ZoneEventBridge.GetPlayerZoneState(_testPlayer);
            Assert.NotNull(state);
        }

        [Fact]
        public void ZoneEventBridge_EnterZone_ValidTransition_UpdatesStateCorrectly()
        {
            // Arrange
            ZoneEventBridge.Initialize();
            var initialZoneId = "test-zone-1";
            
            // Act
            ZoneEventBridge.PublishPlayerEntered(_testPlayer, initialZoneId);
            
            // Assert
            var state = ZoneEventBridge.GetPlayerZoneState(_testPlayer);
            Assert.Equal(initialZoneId, state.CurrentZoneId);
            Assert.Equal(ZoneLifecycleState.Active, state.State);
            Assert.Equal(initialZoneId, state.LastZoneId);
            Assert.True(state.IsInAnyZone);
            Assert.True(state.IsStable);
            Assert.NotNull(state.EnteredAt);
        }

        [Fact]
        public void ZoneEventBridge_EnterSameZone_IdempotentHandling()
        {
            // Arrange
            ZoneEventBridge.Initialize();
            var zoneId = "test-zone";
            
            // Act - Enter same zone twice
            ZoneEventBridge.PublishPlayerEntered(_testPlayer, zoneId);
            ZoneEventBridge.PublishPlayerEntered(_testPlayer, zoneId);
            
            // Assert - Should handle idempotently
            var state = ZoneEventBridge.GetPlayerZoneState(_testPlayer);
            Assert.Equal(zoneId, state.CurrentZoneId);
            Assert.Equal(ZoneLifecycleState.Active, state.State);
            // Should not create duplicate entries or break state
        }

        [Fact]
        public void ZoneEventBridge_ExitZone_ValidTransition_UpdatesStateCorrectly()
        {
            // Arrange
            ZoneEventBridge.Initialize();
            var zoneId = "test-zone";
            
            // First enter a zone
            ZoneEventBridge.PublishPlayerEntered(_testPlayer, zoneId);
            
            // Act - Exit the zone
            ZoneEventBridge.PublishPlayerExited(_testPlayer, zoneId);
            
            // Assert
            var state = ZoneEventBridge.GetPlayerZoneState(_testPlayer);
            Assert.Equal(string.Empty, state.CurrentZoneId);
            Assert.Equal(ZoneLifecycleState.None, state.State);
            Assert.Equal(zoneId, state.PreviousZoneId);
            Assert.False(state.IsInAnyZone);
            Assert.True(state.IsStable);
            Assert.NotNull(state.ExitedAt);
        }

        [Fact]
        public void ZoneEventBridge_ExitZoneWithoutEnter_IdempotentHandling()
        {
            // Arrange
            ZoneEventBridge.Initialize();
            
            // Act - Exit without entering first
            ZoneEventBridge.PublishPlayerExited(_testPlayer, "test-zone");
            
            // Assert - Should handle idempotently
            var state = ZoneEventBridge.GetPlayerZoneState(_testPlayer);
            Assert.Equal(ZoneLifecycleState.None, state.State);
            Assert.Equal(string.Empty, state.CurrentZoneId);
            Assert.False(state.IsInAnyZone);
            Assert.True(state.IsStable);
        }

        [Fact]
        public void ZoneEventBridge_RapidTransitions_TriggersDebounce()
        {
            // Arrange
            ZoneEventBridge.Initialize();
            
            // Act - Rapid zone changes (simulate rapid transitions)
            ZoneEventBridge.PublishPlayerEntered(_testPlayer, "zone-1");
            ZoneEventBridge.PublishPlayerExited(_testPlayer, "zone-1");
            ZoneEventBridge.PublishPlayerEntered(_testPlayer, "zone-2");
            ZoneEventBridge.PublishPlayerExited(_testPlayer, "zone-2");
            
            // Third rapid transition should trigger cooldown
            ZoneEventBridge.PublishPlayerEntered(_testPlayer, "zone-3");
            
            // Assert
            var state = ZoneEventBridge.GetPlayerZoneState(_testPlayer);
            Assert.Equal(ZoneLifecycleState.Cooldown, state.State);
            Assert.True(state.IsInCooldown);
            Assert.Equal(3, state.RapidTransitionCount);
        }

        [Fact]
        public void ZoneEventBridge_DebouncePreventsFurtherTransitions()
        {
            // Arrange
            ZoneEventBridge.Initialize();
            
            // Trigger cooldown with rapid transitions
            ZoneEventBridge.PublishPlayerEntered(_testPlayer, "zone-1");
            ZoneEventBridge.PublishPlayerExited(_testPlayer, "zone-1");
            ZoneEventBridge.PublishPlayerEntered(_testPlayer, "zone-2");
            ZoneEventBridge.PublishPlayerExited(_testPlayer, "zone-2");
            ZoneEventBridge.PublishPlayerEntered(_testPlayer, "zone-3");
            
            // Try to enter another zone during cooldown
            ZoneEventBridge.PublishPlayerEntered(_testPlayer, "zone-4");
            
            // Assert - Should be ignored due to cooldown
            var state = ZoneEventBridge.GetPlayerZoneState(_testPlayer);
            Assert.Equal(ZoneLifecycleState.Cooldown, state.State);
            Assert.Equal("zone-3", state.CurrentZoneId); // Should not change to zone-4
        }

        [Fact]
        public void ZoneEventBoundary_CrossPlayerZones_Isolated()
        {
            // Arrange
            ZoneEventBridge.Initialize();
            var player1 = new Entity { Index = 1 };
            var player2 = new Entity { Index = 2 };
            
            // Act - Different players in different zones
            ZoneEventBridge.PublishPlayerEntered(player1, "zone-1");
            ZoneEventBridge.PublishPlayerEntered(player2, "zone-2");
            
            // Assert
            var state1 = ZoneEventBridge.GetPlayerZoneState(player1);
            var state2 = ZoneEventBridge.GetPlayerZoneState(player2);
            
            Assert.Equal("zone-1", state1.CurrentZoneId);
            Assert.Equal("zone-2", state2.CurrentZoneId);
            Assert.NotEqual(state1.CurrentZoneId, state2.CurrentZoneId);
        }

        [Fact]
        public void ZoneEventBridge_MultiplePlayers_HandlesConcurrency()
        {
            // Arrange
            ZoneEventBridge.Initialize();
            var players = new[]
            {
                new Entity { Index = 1 },
                new Entity { Index = 2 },
                new Entity { Index = 3 }
            };
            
            // Act - Multiple concurrent operations
            Parallel.ForEach(players, player =>
            {
                ZoneEventBridge.PublishPlayerEntered(player, $"zone-{player.Index}");
                ZoneEventBridge.PublishPlayerExited(player, $"zone-{player.Index}");
            });
            
            // Assert - All players should have correct states
            foreach (var player in players)
            {
                var state = ZoneEventBridge.GetPlayerZoneState(player);
                Assert.Equal(string.Empty, state.CurrentZoneId); // All exited
                Assert.Equal(ZoneLifecycleState.None, state.State);
            }
        }

        [Fact]
        public void ZoneEventBridge_RemovePlayerState_CleansUpCorrectly()
        {
            // Arrange
            ZoneEventBridge.Initialize();
            ZoneEventBridge.PublishPlayerEntered(_testPlayer, "test-zone");
            
            // Act
            ZoneEventBridge.RemovePlayerZoneState(_testPlayer);
            
            // Assert
            var state = ZoneEventBridge.GetPlayerZoneState(_testPlayer);
            // Should return fresh state since player was removed
            Assert.Equal(ZoneLifecycleState.None, state.State);
            Assert.Equal(string.Empty, state.CurrentZoneId);
            Assert.Equal(string.Empty, state.PreviousZoneId);
        }

        [Fact]
        public void ZoneEventBridge_UpdatePlayerZoneState_UpdatesExistingState()
        {
            // Arrange
            ZoneEventBridge.Initialize();
            var originalState = ZoneEventBridge.GetPlayerZoneState(_testPlayer);
            var updatedState = new PlayerZoneState
            {
                CurrentZoneId = "updated-zone",
                State = ZoneLifecycleState.Active,
                RapidTransitionCount = 5
            };
            
            // Act
            ZoneEventBridge.UpdatePlayerZoneState(_testPlayer, updatedState);
            
            // Assert
            var finalState = ZoneEventBridge.GetPlayerZoneState(_testPlayer);
            Assert.Equal("updated-zone", finalState.CurrentZoneId);
            Assert.Equal(ZoneLifecycleState.Active, finalState.State);
            Assert.Equal(5, finalState.RapidTransitionCount);
        }
    }
}
