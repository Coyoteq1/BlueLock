using System;
using System.Collections.Generic;
using System.Linq;
using ProjectM;
using ProjectM.WeaponComponents;
using Stunlock.Core;
using Unity.Entities;
using VAuto.EndGameKit.Helpers;

namespace VAuto.EndGameKit.Services
{
    /// <summary>
    /// Service responsible for jewel socket operations.
    /// Handles attaching jewels to weapons using WeaponSocketBuffer.
    /// 
    /// Critical Rules:
    /// - MUST run AFTER equipment is equipped (weapon must exist)
    /// - Cannot be applied directly; must use WeaponSocketBuffer
    /// - Requires matching weapon type for some jewels
    /// </summary>
    public class JewelService
    {
        private readonly EntityManager _entityManager;

        /// <summary>
        /// Creates a new JewelService instance.
        /// </summary>
        /// <param name="entityManager">Entity manager for socket operations.</param>
        public JewelService(EntityManager entityManager)
        {
            _entityManager = entityManager ?? throw new ArgumentNullException(nameof(entityManager));
        }

        /// <summary>
        /// Attach a single jewel to the first available socket.
        /// </summary>
        /// <param name="player">Player entity.</param>
        /// <param name="jewelGuid">Prefab GUID of the jewel.</param>
        /// <returns>True if successfully attached.</returns>
        public bool AttachJewel(Entity player, PrefabGUID jewelGuid)
        {
            if (!PlayerHelper.IsValidPlayer(_entityManager, player))
            {
                Plugin.Log.LogWarning($"[JewelService] Invalid player entity for jewel attachment");
                return false;
            }

            if (!GuidHelper.IsValid(jewelGuid))
            {
                Plugin.Log.LogWarning($"[JewelService] Invalid jewel GUID: {jewelGuid.GuidHash}");
                return false;
            }

            // Get equipped weapon
            if (!PlayerHelper.HasEquippedWeapon(_entityManager, player, out var weapon))
            {
                Plugin.Log.LogWarning($"[JewelService] Player {player.Index} has no equipped weapon");
                return false;
            }

            if (!_entityManager.Exists(weapon))
            {
                Plugin.Log.LogWarning($"[JewelService] Player {player.Index} has invalid weapon entity");
                return false;
            }

            // Check for socket buffer
            // TODO: WeaponSocketBuffer type not found in V Rising assemblies
            // if (!_entityManager.HasComponent<WeaponSocketBuffer>(weapon))
            // {
            //     Plugin.Log.LogWarning($"[JewelService] Weapon {weapon.Index} has no WeaponSocketBuffer (not socketable)");
            //     return false;
            // }

            // Find first empty socket
            // var sockets = _entityManager.GetBuffer<WeaponSocketBuffer>(weapon);

            // TODO: Temporarily disable jewel socket functionality until WeaponSocketBuffer type is resolved
            // for (int i = 0; i < sockets.Length; i++)
            // {
            //     if (sockets[i].Jewel.Equals(PrefabGUID.Empty))
            //     {
            //         sockets[i] = new WeaponSocketBuffer
            //         {
            //             Jewel = jewelGuid
            //         };

            //         Plugin.Log.LogDebug($"[JewelService] Attached jewel {jewelGuid.GuidHash} to socket {i} for player {player.Index}");
            //         return true;
            //     }
            // }

            Plugin.Log.LogWarning($"[JewelService] Jewel socket functionality temporarily disabled - WeaponSocketBuffer type not found");
            return false;
        }

        /// <summary>
        /// Attach a single jewel from long value.
        /// </summary>
        public bool AttachJewel(Entity player, long guidValue)
        {
            return AttachJewel(player, new PrefabGUID((int)guidValue));
        }

        /// <summary>
        /// Attach multiple jewels to available sockets.
        /// </summary>
        /// <param name="player">Player entity.</param>
        /// <param name="jewels">List of jewel PrefabGUIDs.</param>
        /// <returns>Number of jewels successfully attached.</returns>
        public int AttachJewels(Entity player, List<PrefabGUID> jewels)
        {
            if (!PlayerHelper.IsValidPlayer(_entityManager, player))
            {
                Plugin.Log.LogWarning($"[JewelService] Invalid player entity for jewel attachment");
                return 0;
            }

            if (jewels == null || jewels.Count == 0)
            {
                return 0;
            }

            int attachedCount = 0;

            foreach (var jewel in jewels)
            {
                if (AttachJewel(player, jewel))
                {
                    attachedCount++;
                }
            }

            if (attachedCount > 0)
            {
                Plugin.Log.LogInfo($"[JewelService] Attached {attachedCount}/{jewels.Count} jewels to player {player.Index}");
            }
            
            return attachedCount;
        }

        /// <summary>
        /// Attach multiple jewels from long values.
        /// </summary>
        public int AttachJewels(Entity player, List<long> jewelValues)
        {
            if (jewelValues == null || jewelValues.Count == 0)
                return 0;

            var guids = jewelValues.Select(v => new PrefabGUID((int)v)).ToList();
            return AttachJewels(player, guids);
        }

        /// <summary>
        /// Get count of available sockets on equipped weapon.
        /// </summary>
        public int GetAvailableSocketCount(Entity player)
        {
            if (!PlayerHelper.HasEquippedWeapon(_entityManager, player, out var weapon))
                return 0;

            if (!_entityManager.HasComponent<WeaponSocketBuffer>(weapon))
                return 0;

            var sockets = _entityManager.GetBuffer<WeaponSocketBuffer>(weapon);
            int count = 0;

            for (int i = 0; i < sockets.Length; i++)
            {
                if (sockets[i].Jewel.Equals(PrefabGUID.Empty))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Get count of filled sockets on equipped weapon.
        /// </summary>
        public int GetFilledSocketCount(Entity player)
        {
            if (!PlayerHelper.HasEquippedWeapon(_entityManager, player, out var weapon))
                return 0;

            if (!_entityManager.HasComponent<WeaponSocketBuffer>(weapon))
                return 0;

            var sockets = _entityManager.GetBuffer<WeaponSocketBuffer>(weapon);
            int count = 0;

            for (int i = 0; i < sockets.Length; i++)
            {
                if (!sockets[i].Jewel.Equals(PrefabGUID.Empty))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Get info about current socket state.
        /// </summary>
        public string GetSocketInfo(Entity player)
        {
            if (!PlayerHelper.HasEquippedWeapon(_entityManager, player, out var weapon))
                return "No weapon equipped";

            if (!_entityManager.HasComponent<WeaponSocketBuffer>(weapon))
                return "Weapon not socketable";

            var sockets = _entityManager.GetBuffer<WeaponSocketBuffer>(weapon);
            int filled = 0;
            int empty = 0;

            for (int i = 0; i < sockets.Length; i++)
            {
                if (sockets[i].Jewel.Equals(PrefabGUID.Empty))
                    empty++;
                else
                    filled++;
            }

            return $"Sockets: {filled}/{sockets.Length} filled, {empty} empty";
        }

        /// <summary>
        /// Clear all jewels from equipped weapon.
        /// </summary>
        public void ClearJewels(Entity player)
        {
            if (!PlayerHelper.HasEquippedWeapon(_entityManager, player, out var weapon))
                return;

            if (!_entityManager.HasComponent<WeaponSocketBuffer>(weapon))
                return;

            var sockets = _entityManager.GetBuffer<WeaponSocketBuffer>(weapon);

            for (int i = 0; i < sockets.Length; i++)
            {
                if (!sockets[i].Jewel.Equals(PrefabGUID.Empty))
                {
                    sockets[i] = new WeaponSocketBuffer
                    {
                        Jewel = PrefabGUID.Empty
                    };
                }
            }

            Plugin.Log.LogDebug($"[JewelService] Cleared all jewels from player {player.Index}");
        }

        /// <summary>
        /// Check if a jewel can be attached (weapon exists and has sockets).
        /// </summary>
        public bool CanAttachJewel(Entity player)
        {
            if (!PlayerHelper.HasEquippedWeapon(_entityManager, player, out var weapon))
                return false;

            if (!_entityManager.Exists(weapon))
                return false;

            if (!_entityManager.HasComponent<WeaponSocketBuffer>(weapon))
                return false;

            return GetAvailableSocketCount(player) > 0;
        }
    }
}
