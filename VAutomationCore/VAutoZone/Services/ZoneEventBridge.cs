using System;
using System.Collections.Generic;
using Unity.Entities;
using VAuto.Zone.Core;
using VAuto.Zone.Models;

namespace VAuto.Zone.Services
{
    /// <summary>
    /// Zone event bridge for player zone transitions.
    /// Provides event publishing with delegate pattern for cross-plugin communication.
    /// </summary>
    public static class ZoneEventBridge
    {
        /// <summary>
        /// Event fired when a player enters a zone.
        /// </summary>
        public static event Action<Entity, string>? OnPlayerEntered;

        /// <summary>
        /// Event fired when a player exits a zone.
        /// </summary>
        public static event Action<Entity, string>? OnPlayerExited;

        // Player zone state storage - in-memory only for this implementation
        private static readonly Dictionary<Entity, PlayerZoneState> _playerStates = new Dictionary<Entity, PlayerZoneState>();

        public static void Initialize()
        {
            ZoneCore.LogInfo("[ZoneEventBridge] Initialized");
        }

        public static void PublishPlayerEntered(Entity player, string zoneId)
        {
            if (!_playerStates.TryGetValue(player, out var state))
            {
                state = new PlayerZoneState();
                _playerStates[player] = state;
            }
            state.PreviousZoneId = state.CurrentZoneId;
            state.CurrentZoneId = zoneId;
            state.WasInZone = true;
            state.EnteredAt = DateTime.UtcNow;
            ZoneCore.LogInfo($"[ZoneEventBridge] Player entered zone: {zoneId}");
            
            // Fire event for subscribers
            OnPlayerEntered?.Invoke(player, zoneId);
        }

        public static void PublishPlayerExited(Entity player, string zoneId)
        {
            if (_playerStates.TryGetValue(player, out var state))
            {
                state.PreviousZoneId = state.CurrentZoneId;
                state.CurrentZoneId = string.Empty;
                state.ExitedAt = DateTime.UtcNow;
                ZoneCore.LogInfo($"[ZoneEventBridge] Player exited zone: {zoneId}");
            }
            
            // Fire event for subscribers
            OnPlayerExited?.Invoke(player, zoneId);
        }

        public static PlayerZoneState GetPlayerState(Entity player)
        {
            return _playerStates.TryGetValue(player, out var state) ? state : null;
        }

        public static void SetPlayerState(Entity player, PlayerZoneState state)
        {
            _playerStates[player] = state;
        }

        public static void RemovePlayerState(Entity player)
        {
            _playerStates.Remove(player);
        }
    }
}
