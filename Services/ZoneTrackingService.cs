using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using BepInEx.Logging;
using VAutomationCore.Core;
using VAutomationCore.Core.Logging;
using VAuto.Zone.Models;

namespace VAuto.Zone.Services
{
    /// <summary>
    /// Service for tracking zones and player positions.
    /// </summary>
    public static class ZoneTrackingService
    {
        private static readonly ManualLogSource _log = new ManualLogSource("ZoneTrackingService");
        private static readonly Dictionary<Entity, PlayerZoneData> _playerZoneData = new Dictionary<Entity, PlayerZoneData>();
        private static readonly List<ZoneDefinition> _zones = new List<ZoneDefinition>();

        /// <summary>
        /// Player zone tracking data.
        /// </summary>
        public class PlayerZoneData
        {
            public string CurrentZoneId { get; set; } = string.Empty;
            public float3 LastPosition { get; set; }
            public DateTime LastUpdate { get; set; }
        }

        /// <summary>
        /// Initialize the zone tracking service.
        /// </summary>
        public static void Initialize()
        {
            _log.LogInfo("[ZoneTrackingService] Initialized");
        }

        /// <summary>
        /// Add a zone to the tracking list.
        /// </summary>
        public static void AddZone(ZoneDefinition zone)
        {
            if (zone != null)
            {
                _zones.Add(zone);
                _log.LogInfo($"[ZoneTrackingService] Added zone '{zone.Id}' to tracking");
            }
        }

        /// <summary>
        /// Get all tracked zones.
        /// </summary>
        public static List<ZoneDefinition> GetZones()
        {
            return _zones;
        }

        /// <summary>
        /// Update player zone state.
        /// </summary>
        public static void UpdatePlayerZone(Entity player, float3 position, string zoneId)
        {
            try
            {
                if (!_playerZoneData.TryGetValue(player, out var data))
                {
                    data = new PlayerZoneData();
                    _playerZoneData[player] = data;
                }

                data.CurrentZoneId = zoneId ?? string.Empty;
                data.LastPosition = position;
                data.LastUpdate = DateTime.Now;

                if (!string.IsNullOrEmpty(zoneId))
                {
                    _log.LogDebug($"[ZoneTrackingService] Player entered zone '{zoneId}' at ({position.x:F1}, {position.y:F1}, {position.z:F1})");
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[ZoneTrackingService] UpdatePlayerZone failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Get player's current zone.
        /// </summary>
        public static string GetPlayerZone(Entity player)
        {
            try
            {
                if (_playerZoneData.TryGetValue(player, out var data))
                {
                    return data.CurrentZoneId;
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[ZoneTrackingService] GetPlayerZone failed: {ex.Message}");
            }

            return string.Empty;
        }

        /// <summary>
        /// Get all player zone data.
        /// </summary>
        public static Dictionary<Entity, PlayerZoneData> GetAllPlayerZoneData()
        {
            return _playerZoneData;
        }

        /// <summary>
        /// Clear stale player data.
        /// </summary>
        public static void ClearStalePlayers()
        {
            var stalePlayers = new List<Entity>();
            var now = DateTime.Now;

            foreach (var kvp in _playerZoneData)
            {
                // Remove players that haven't been updated in the last 5 minutes
                if ((now - kvp.Value.LastUpdate).TotalMinutes > 5)
                {
                    stalePlayers.Add(kvp.Key);
                }
            }

            foreach (var stale in stalePlayers)
            {
                _playerZoneData.Remove(stale);
                _log.LogDebug($"[ZoneTrackingService] Cleared stale player data for entity {stale.Index}");
            }
        }
    }
}
