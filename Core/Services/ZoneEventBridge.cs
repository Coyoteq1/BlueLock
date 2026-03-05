using System;
using System.Collections.Generic;
using VAutomationCore.Core.ECS;
using Unity.Entities;
using VAutomationCore.Core.Events;
using VAutomationCore.Models;
using VAutomationCore.Core.Logging;

namespace VAutomationCore.Services
{
    /// <summary>
    /// Event bridge for zone-related events with observability.
    /// </summary>
    public static class ZoneEventBridge
    {
        private static readonly object Sync = new();
        private static readonly Dictionary<Entity, PlayerZoneState> PlayerStates = new();
        private static bool _initialized;
        
        // Observability constants
        private static readonly string FlowName = "zone-lifecycle";
        
        // Debouncing constants
        private static readonly TimeSpan DebounceWindow = TimeSpan.FromSeconds(2);
        private static readonly int MaxRapidTransitions = 3;
        private static readonly TimeSpan CooldownDuration = TimeSpan.FromSeconds(5);

        public static event Action<Entity, string>? OnPlayerEntered;
        public static event Action<Entity, string>? OnPlayerExited;

        public static void Initialize()
        {
            lock (Sync)
            {
                _initialized = true;
            }
            
            // Log initialization
            var correlationId = $"init:{DateTime.UtcNow:HHmmss.fff}";
            CoreLogger.LogInfoStatic(
                $"flow={FlowName} | stage=initialize | id={correlationId} | ctx=service=ZoneEventBridge,version=1.0",
                "ZoneEventBridge");
        }

        /// <summary>
        /// Publishes a player entered zone event with state machine and debouncing.
        /// </summary>
        public static void PublishPlayerEntered(Entity player, string zoneId)
        {
            EnsureInitialized();
            var normalizedZoneId = zoneId ?? string.Empty;
            var correlationId = $"enter:{DateTime.UtcNow:HHmmss.fff}";
            var playerId = GetPlayerId(player);

            lock (Sync)
            {
                if (!PlayerStates.TryGetValue(player, out var state))
                {
                    state = new PlayerZoneState();
                    PlayerStates[player] = state;
                }

                // Check for rapid transitions and apply debouncing
                if (ShouldApplyDebounce(state, normalizedZoneId))
                {
                    // START log: Debounce applied
                    CoreLogger.LogInfoStatic(
                        $"flow={FlowName} | stage=enter_debounce | id={correlationId} | ctx=playerId={playerId},zoneId={normalizedZoneId},rapidCount={state.RapidTransitionCount},cooldown=true",
                        "ZoneEventBridge");
                    
                    state.State = ZoneLifecycleState.Cooldown;
                    state.LastTransitionTime = DateTime.UtcNow;
                    return; // Skip processing during cooldown
                }

                // Check if already in this zone (idempotent check)
                if (state.State == ZoneLifecycleState.Active && state.CurrentZoneId == normalizedZoneId)
                {
                    // START log: Idempotent operation
                    CoreLogger.LogInfoStatic(
                        $"flow={FlowName} | stage=enter_idempotent | id={correlationId} | ctx=playerId={playerId},zoneId={normalizedZoneId},alreadyActive=true",
                        "ZoneEventBridge");
                    return;
                }

                // START log: Zone enter transition
                CoreLogger.LogInfoStatic(
                    $"flow={FlowName} | stage=enter_start | id={correlationId} | ctx=playerId={playerId},zoneId={normalizedZoneId},previousState={state.State},previousZone={state.CurrentZoneId}",
                    "ZoneEventBridge");

                // Apply state transition
                state.State = ZoneLifecycleState.Entering;
                state.LastTransitionTime = DateTime.UtcNow;
                state.PreviousZoneId = state.CurrentZoneId ?? string.Empty;
                state.CurrentZoneId = normalizedZoneId;
                state.LastZoneId = normalizedZoneId;
                state.EnteredAt = DateTime.UtcNow;
                state.WasInZone = true;
                state.IsInAnyZone = !string.IsNullOrWhiteSpace(normalizedZoneId);
                state.LastUpdateTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                // Complete transition to active state
                state.State = ZoneLifecycleState.Active;
            }

            InvokeSafely(OnPlayerEntered, player, normalizedZoneId);
            TypedEventBus.Publish(new PlayerEnteredZoneEvent
            {
                Player = player,
                ZoneId = normalizedZoneId
            });

            // END log: Zone enter completed
            CoreLogger.LogInfoStatic(
                $"flow={FlowName} | stage=enter_complete | id={correlationId} | ctx=playerId={playerId},zoneId={normalizedZoneId},state=Active",
                "ZoneEventBridge");
        }

        /// <summary>
        /// Gets a player identifier for logging purposes.
        /// </summary>
        private static string GetPlayerId(Entity player)
        {
            return player.Index.ToString();
        }

        /// <summary>
        /// Publishes a player exited zone event with state machine and cleanup.
        /// </summary>
        public static void PublishPlayerExited(Entity player, string zoneId)
        {
            EnsureInitialized();
            var normalizedZoneId = zoneId ?? string.Empty;
            var correlationId = $"exit:{DateTime.UtcNow:HHmmss.fff}";
            var playerId = GetPlayerId(player);

            lock (Sync)
            {
                if (!PlayerStates.TryGetValue(player, out var state))
                {
                    state = new PlayerZoneState();
                    PlayerStates[player] = state;
                }

                // Check if player is in cooldown (should not happen but handle gracefully)
                if (state.State == ZoneLifecycleState.Cooldown)
                {
                    // START log: Exit during cooldown
                    CoreLogger.LogInfoStatic(
                        $"flow={FlowName} | stage=exit_cooldown | id={correlationId} | ctx=playerId={playerId},zoneId={normalizedZoneId},warning=exit_during_cooldown",
                        "ZoneEventBridge");
                    
                    // Force exit from cooldown
                    state.State = ZoneLifecycleState.Exiting;
                }

                // Check if not in any zone (idempotent check)
                if (state.State == ZoneLifecycleState.None && string.IsNullOrEmpty(state.CurrentZoneId))
                {
                    // START log: Idempotent exit
                    CoreLogger.LogInfoStatic(
                        $"flow={FlowName} | stage=exit_idempotent | id={correlationId} | ctx=playerId={playerId},zoneId={normalizedZoneId},alreadyExited=true",
                        "ZoneEventBridge");
                    return;
                }

                // START log: Zone exit transition
                CoreLogger.LogInfoStatic(
                    $"flow={FlowName} | stage=exit_start | id={correlationId} | ctx=playerId={playerId},zoneId={normalizedZoneId},currentState={state.State}",
                    "ZoneEventBridge");

                // Apply state transition
                state.State = ZoneLifecycleState.Exiting;
                state.LastTransitionTime = DateTime.UtcNow;
                state.PreviousZoneId = state.CurrentZoneId ?? normalizedZoneId;
                state.CurrentZoneId = string.Empty;
                state.LastZoneId = state.CurrentZoneId;
                state.ExitedAt = DateTime.UtcNow;
                state.IsInAnyZone = false;
                state.LastUpdateTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                // Complete transition to none state
                state.State = ZoneLifecycleState.None;
                
                // Cleanup: Reset rapid transition count after successful exit
                state.RapidTransitionCount = 0;
            }

            InvokeSafely(OnPlayerExited, player, normalizedZoneId);
            TypedEventBus.Publish(new PlayerExitedZoneEvent
            {
                Player = player,
                ZoneId = normalizedZoneId
            });

            // END log: Zone exit completed with cleanup
            CoreLogger.LogInfoStatic(
                $"flow={FlowName} | stage=exit_complete | id={correlationId} | ctx=playerId={playerId},zoneId={normalizedZoneId},state=None,cleanup=true",
                "ZoneEventBridge");
        }

        /// <summary>
        /// Gets the current zone state for a player.
        /// </summary>
        public static PlayerZoneState GetPlayerZoneState(Entity player)
        {
            EnsureInitialized();

            lock (Sync)
            {
                if (PlayerStates.TryGetValue(player, out var state))
                {
                    return state;
                }
            }

            return new PlayerZoneState();
        }

        /// <summary>
        /// Updates the zone state for a player.
        /// </summary>
        public static void UpdatePlayerZoneState(Entity player, PlayerZoneState state)
        {
            if (state == null)
            {
                return;
            }

            EnsureInitialized();

            lock (Sync)
            {
                state.LastUpdateTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                PlayerStates[player] = state;
            }
        }

        /// <summary>
        /// Removes the zone state for a player.
        /// </summary>
        public static void RemovePlayerZoneState(Entity player)
        {
            EnsureInitialized();

            lock (Sync)
            {
                PlayerStates.Remove(player);
            }
        }

        public static void RemovePlayerState(Entity player)
        {
            RemovePlayerZoneState(player);
        }

        public static Core.ECS.Components.EcsPlayerZoneState GetPlayerZoneComponentState(Entity player)
        {
            var modelState = GetPlayerZoneState(player);
            return ZoneStateMapper.ToComponent(modelState, Core.ECS.ZoneHashUtility.GetZoneHash);
        }

        public static void UpdateFromComponentState(Entity player, Core.ECS.Components.EcsPlayerZoneState state)
        {
            var model = ZoneStateMapper.ToModel(state, Core.ECS.ZoneHashUtility.GetZoneId);
            UpdatePlayerZoneState(player, model);
        }

        private static void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            Initialize();
        }

        private static void InvokeSafely(Action<Entity, string>? handlers, Entity player, string zoneId)
        {
            if (handlers == null)
            {
                return;
            }

            foreach (var handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<Entity, string>)handler).Invoke(player, zoneId);
                }
                catch
                {
                    // One subscriber should never block zone transition processing.
                }
            }
        }

        /// <summary>
        /// Checks if debouncing should be applied based on rapid transition detection.
        /// </summary>
        private static bool ShouldApplyDebounce(PlayerZoneState state, string newZoneId)
        {
            var now = DateTime.UtcNow;
            
            // Check for rapid zone changes within debounce window
            if (state.LastTransitionTime.HasValue && 
                now - state.LastTransitionTime.Value < DebounceWindow)
            {
                state.RapidTransitionCount++;
                
                // Apply cooldown if too many rapid transitions
                if (state.RapidTransitionCount >= MaxRapidTransitions)
                {
                    return true;
                }
            }
            
            // Reset rapid transition count if enough time has passed
            if (!state.LastTransitionTime.HasValue || 
                now - state.LastTransitionTime.Value > DebounceWindow)
            {
                state.RapidTransitionCount = 0;
            }
            
            return false;
        }

        /// <summary>
        /// Gets the current zone for a player for logging purposes.
        /// </summary>
        private static string GetCurrentZone(Entity player)
        {
            lock (Sync)
            {
                if (PlayerStates.TryGetValue(player, out var state))
                {
                    return state.CurrentZoneId ?? string.Empty;
                }
            }
            return string.Empty;
        }

        /// <summary>
        /// Gets the previous zone for a player for logging purposes.
        /// </summary>
        private static string GetPreviousZone(Entity player)
        {
            lock (Sync)
            {
                if (PlayerStates.TryGetValue(player, out var state))
                {
                    return state.PreviousZoneId ?? string.Empty;
                }
            }
            return string.Empty;
        }
    }
}

