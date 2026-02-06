using System.Collections.Generic;
using Unity.Entities;

namespace VAuto.API
{
    /// <summary>
    /// Public API for zone-related queries.
    /// </summary>
    public static class ZoneAPI
    {
        public static bool IsPlayerInZone(Entity player, string zoneName)
        {
            // TODO: wire to ECS zone tracking when enabled.
            return false;
        }

        public static IEnumerable<string> GetPlayerZones(Entity player)
        {
            // TODO: return tracked zones once ZoneTrackingService is wired.
            return new string[0];
        }
    }
}
