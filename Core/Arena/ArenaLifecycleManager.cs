using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using VAutomationCore.Models;
using VAuto.Zone.Services;

namespace VAuto.Zone.Core.Arena
{
    /// <summary>
    /// Manages arena lifecycle events and transitions.
    /// </summary>
    public class ArenaLifecycleManager
    {
        private readonly Dictionary<Entity, PlayerZoneState> _playerStates = new Dictionary<Entity, PlayerZoneState>();

        /// <summary>
        /// Handles player entering an arena zone.
        /// </summary>
        public void OnPlayerEnterArena(Entity character, string zoneId, float3 position)
        {
            try
            {
                var state = new PlayerZoneState
                {
                    CurrentZoneId = zoneId,
                    WasInZone = true,
                    EnteredAt = DateTime.UtcNow
                };
                _playerStates[character] = state;

                ZoneCore.LogDebug($"Player entered arena zone {zoneId}");
                ZoneEventBridge.PublishPlayerEntered(character, zoneId);
            }
            catch (Exception ex)
            {
                ZoneCore.LogException($"Failed to handle player enter arena", ex);
            }
        }

        /// <summary>
        /// Handles player exiting an arena zone.
        /// </summary>
        public void OnPlayerExitArena(Entity character, string zoneId)
        {
            try
            {
                if (_playerStates.TryGetValue(character, out var state))
                {
                    state.PreviousZoneId = state.CurrentZoneId;
                    state.CurrentZoneId = string.Empty;
                    state.ExitedAt = DateTime.UtcNow;
                }

                ZoneCore.LogDebug($"Player exited arena zone {zoneId}");
                ZoneEventBridge.PublishPlayerExited(character, zoneId);
            }
            catch (Exception ex)
            {
                ZoneCore.LogException($"Failed to handle player exit arena", ex);
            }
        }

        /// <summary>
        /// Gets the current zone state for a player.
        /// </summary>
        public PlayerZoneState GetPlayerState(Entity character)
        {
            return _playerStates.TryGetValue(character, out var state) ? state : null;
        }

        /// <summary>
        /// Gets all players currently in arena zones.
        /// </summary>
        public List<Entity> GetPlayersInArenas()
        {
            var players = new List<Entity>();
            foreach (var kvp in _playerStates)
            {
                if (kvp.Value.WasInZone)
                {
                    players.Add(kvp.Key);
                }
            }
            return players;
        }

        /// <summary>
        /// Processes the arena lifecycle tick.
        /// </summary>
        public void Tick()
        {
            // Implementation placeholder
        }

        /// <summary>
        /// Initializes the lifecycle manager.
        /// </summary>
        public void Initialize()
        {
            ZoneCore.LogInfo("ArenaLifecycleManager initialized");
            ZoneEventBridge.Initialize();
        }

        /// <summary>
        /// Shuts down the lifecycle manager.
        /// </summary>
        public void Shutdown()
        {
            _playerStates.Clear();
            ZoneCore.LogInfo("ArenaLifecycleManager shutdown");
        }
    }
}
