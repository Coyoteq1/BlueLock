using System;
using System.Collections.Generic;
using Unity.Entities;
using VAuto.Zone.Core;
using VAuto.Zone.Models;

namespace VAuto.Zone.Services
{
    /// <summary>
    /// Stub implementation - Zone event bridge for player zone transitions.
    /// </summary>
    public static class ZoneEventBridge
    {
        // Player zone state storage - in-memory only for this stub
        private static readonly Dictionary<Entity, PlayerZoneState> _playerStates = new Dictionary<Entity, PlayerZoneState>();

        public static void Initialize()
        {
            ZoneCore.LogInfo("[ZoneEventBridge] Initialized (stub)");
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
