using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace VAuto.Zone.Services
{
    /// <summary>
    /// Service for routing arena players to respawn at the arena zone center.
    /// Handles teleportation on arena-caused death.
    /// </summary>
    public static class ArenaRespawnHelper
    {
        /// <summary>
        /// Teleports a player entity to the specified arena zone's center coordinates.
        /// Called when arena death is detected and before standard respawn processing.
        /// </summary>
        /// <param name="playerEntity">The player entity to teleport</param>
        /// <param name="zoneId">Zone ID for arena respawn location</param>
        /// <returns>True if teleport was successful</returns>
        public static bool TeleportToArenaCenter(Entity playerEntity, string zoneId)
        {
            try
            {
                var zone = ZoneConfigService.GetZoneById(zoneId);
                if (zone == null)
                    return false;

                var respawnPos = new Vector3(zone.CenterX, zone.GlowSpawnHeight, zone.CenterZ);
                return TeleportPlayerTo(playerEntity, respawnPos);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Teleports a player entity to the specified world coordinates.
        /// </summary>
        /// <param name="playerEntity">The player entity to teleport</param>
        /// <param name="targetPosition">Target world position</param>
        /// <returns>True if teleport was successful</returns>
        private static bool TeleportPlayerTo(Entity playerEntity, Vector3 targetPosition)
        {
            try
            {
                var em = VAuto.Zone.Core.ZoneCore.EntityManager;
                if (em == default || !em.Exists(playerEntity))
                    return false;

                // Update LocalTransform to teleport player
                if (em.HasComponent<LocalTransform>(playerEntity))
                {
                    var transform = em.GetComponentData<LocalTransform>(playerEntity);
                    transform.Position = targetPosition;
                    em.SetComponentData(playerEntity, transform);
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
