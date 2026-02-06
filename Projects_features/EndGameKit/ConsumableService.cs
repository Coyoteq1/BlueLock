using System;
using System.Collections.Generic;
using System.Linq;
using ProjectM;
using Stunlock.Core;
using Unity.Entities;
using VAuto.EndGameKit.Helpers;
using VampireCommandFramework;

namespace VAuto.EndGameKit.Services
{
    /// <summary>
    /// Service responsible for consumable operations in end-game kits.
    /// Handles potions and coatings using the give -> use pattern.
    /// 
    /// Guarantees:
    /// - Buff icons appear correctly
    /// - Duration timers start properly
    /// - Server authority is maintained
    /// - Weapon coatings apply to currently equipped weapon
    /// </summary>
    public class ConsumableService
    {
        private readonly ServerGameManager? _serverGameManager;
        private readonly EntityManager _entityManager;

        /// <summary>
        /// Creates a new ConsumableService instance.
        /// </summary>
        /// <param name="serverGameManager">Server game manager for item operations (nullable for safety).</param>
        /// <param name="entityManager">Entity manager for validation.</param>
        public ConsumableService(ServerGameManager? serverGameManager, EntityManager entityManager)
        {
            _serverGameManager = serverGameManager;
            _entityManager = entityManager ?? throw new ArgumentNullException(nameof(entityManager));
        }

        /// <summary>
        /// Updates the ServerGameManager reference.
        /// </summary>
        public void UpdateServerGameManager(ServerGameManager? serverGameManager)
        {
            _serverGameManager = serverGameManager;
        }

        /// <summary>
        /// Apply a single consumable (give then use).
        /// </summary>
        /// <param name="player">Player entity.</param>
        /// <param name="itemGuid">Prefab GUID of the consumable.</param>
        /// <returns>True if successfully applied.</returns>
        public bool Apply(Entity player, PrefabGUID itemGuid)
        {
            if (!PlayerHelper.IsValidPlayer(_entityManager, player))
            {
                Plugin.Log.LogWarning($"[ConsumableService] Invalid player entity for consumable");
                return false;
            }

            if (!GuidHelper.IsValid(itemGuid))
            {
                Plugin.Log.LogWarning($"[ConsumableService] Invalid consumable GUID: {itemGuid.Value}");
                return false;
            }

            if (_serverGameManager == null)
            {
                Plugin.Log.LogWarning("[ConsumableService] ServerGameManager not available");
                return false;
            }

            try
            {
                // Step 1: Give the item to player's inventory
                bool given = _serverGameManager.TryGiveItem(player, itemGuid, 1);
                if (!given)
                {
                    Plugin.Log.LogWarning($"[ConsumableService] Failed to give consumable {itemGuid.Value} to player {player.Index}");
                    return false;
                }

                // Step 2: Force use the item (applies buffs, coatings, etc.)
                bool used = _serverGameManager.TryUseItem(player, itemGuid);
                if (!used)
                {
                    Plugin.Log.LogWarning($"[ConsumableService] Failed to use consumable {itemGuid.Value} on player {player.Index}");
                    return false;
                }

                Plugin.Log.LogDebug($"[ConsumableService] Applied consumable {itemGuid.Value} to player {player.Index}");
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[ConsumableService] Exception applying consumable {itemGuid.Value}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Apply a single consumable from long value.
        /// </summary>
        public bool Apply(Entity player, long guidValue)
        {
            return Apply(player, new PrefabGUID((int)guidValue));
        }

        /// <summary>
        /// Apply multiple consumables in sequence.
        /// </summary>
        /// <param name="player">Player entity.</param>
        /// <param name="consumables">List of consumable PrefabGUIDs.</param>
        /// <returns>Number of consumables successfully applied.</returns>
        public int ApplyBatch(Entity player, List<PrefabGUID> consumables)
        {
            if (!PlayerHelper.IsValidPlayer(_entityManager, player))
            {
                Plugin.Log.LogWarning($"[ConsumableService] Invalid player entity for consumables");
                return 0;
            }

            if (consumables == null || consumables.Count == 0)
            {
                return 0;
            }

            int appliedCount = 0;

            foreach (var guid in consumables)
            {
                if (Apply(player, guid))
                {
                    appliedCount++;
                }
            }

            if (appliedCount > 0)
            {
                Plugin.Log.LogInfo($"[ConsumableService] Applied {appliedCount}/{consumables.Count} consumables to player {player.Index}");
            }
            
            return appliedCount;
        }

        /// <summary>
        /// Apply multiple consumables from long values.
        /// </summary>
        public int ApplyBatch(Entity player, List<long> consumableValues)
        {
            if (consumableValues == null || consumableValues.Count == 0)
                return 0;

            var guids = consumableValues.Select(v => new PrefabGUID((int)v)).ToList();
            return ApplyBatch(player, guids);
        }

        /// <summary>
        /// Apply only potions (buff consumables).
        /// </summary>
        public int ApplyPotions(Entity player, List<PrefabGUID> potions)
        {
            if (!PlayerHelper.IsValidPlayer(_entityManager, player))
                return 0;

            int appliedCount = 0;
            
            foreach (var potion in potions)
            {
                if (Apply(player, potion))
                {
                    appliedCount++;
                }
            }

            if (appliedCount > 0)
            {
                Plugin.Log.LogInfo($"[ConsumableService] Applied {appliedCount}/{potions.Count} potions to player {player.Index}");
            }
            
            return appliedCount;
        }

        /// <summary>
        /// Apply weapon coating.
        /// Must be called AFTER weapon is equipped.
        /// </summary>
        public bool ApplyWeaponCoating(Entity player, PrefabGUID coatingGuid)
        {
            if (!PlayerHelper.IsValidPlayer(_entityManager, player))
                return false;

            // Verify player has a weapon equipped
            if (!PlayerHelper.HasEquippedWeapon(_entityManager, player, out _))
            {
                Plugin.Log.LogWarning($"[ConsumableService] Player {player.Index} has no equipped weapon for coating");
                return false;
            }

            return Apply(player, coatingGuid);
        }

        /// <summary>
        /// Validates if a consumable can be applied to a player.
        /// </summary>
        public bool CanApply(Entity player, PrefabGUID itemGuid)
        {
            if (!PlayerHelper.IsValidPlayer(_entityManager, player))
                return false;

            if (!GuidHelper.IsValid(itemGuid))
                return false;

            return _serverGameManager != null;
        }
    }
}
