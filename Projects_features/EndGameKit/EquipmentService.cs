using System;
using System.Collections.Generic;
using System.Linq;
using ProjectM;
using Stunlock.Core;
using Unity.Entities;
using VAuto.EndGameKit.Configuration;
using VAuto.EndGameKit.Helpers;
using VampireCommandFramework;

namespace VAuto.EndGameKit.Services
{
    /// <summary>
    /// Service responsible for equipment operations in end-game kits.
    /// Handles auto-equipping of gear using ServerGameManager.
    /// 
    /// Rules:
    /// - Always force replace existing equipment
    /// - Never touch inventory manually
    /// - Never add components directly
    /// </summary>
    public class EquipmentService
    {
        private readonly ServerGameManager? _serverGameManager;
        private readonly EntityManager _entityManager;

        /// <summary>
        /// Creates a new EquipmentService instance.
        /// </summary>
        /// <param name="serverGameManager">Server game manager for equipment operations (nullable for safety).</param>
        /// <param name="entityManager">Entity manager for validation.</param>
        public EquipmentService(ServerGameManager? serverGameManager, EntityManager entityManager)
        {
            _serverGameManager = serverGameManager;
            _entityManager = entityManager ?? throw new ArgumentNullException(nameof(entityManager));
        }

        /// <summary>
        /// Updates the ServerGameManager reference (for hot-reload scenarios).
        /// </summary>
        public void UpdateServerGameManager(ServerGameManager? serverGameManager)
        {
            _serverGameManager = serverGameManager;
        }

        /// <summary>
        /// Equip a full kit of gear to a player.
        /// Each item is force-equipped to its specified slot.
        /// </summary>
        /// <param name="player">Player entity.</param>
        /// <param name="equipment">Dictionary of equipment slot name -> PrefabGUID.</param>
        /// <returns>Number of items successfully equipped.</returns>
        public int EquipKit(Entity player, Dictionary<string, long> equipment)
        {
            if (!PlayerHelper.IsValidPlayer(_entityManager, player))
            {
                Plugin.Log.LogWarning($"[EquipmentService] Invalid player entity for equipment");
                return 0;
            }

            if (equipment == null || equipment.Count == 0)
            {
                Plugin.Log.LogDebug("[EquipmentService] No equipment to equip");
                return 0;
            }

            if (_serverGameManager == null)
            {
                Plugin.Log.LogWarning("[EquipmentService] ServerGameManager not available");
                return 0;
            }

            int equippedCount = 0;

            foreach (var (slotName, guidValue) in equipment)
            {
                if (guidValue == 0)
                {
                    Plugin.Log.LogDebug($"[EquipmentService] Skipping zero GUID for slot {slotName}");
                    continue;
                }

                if (!EquipmentSlotConverter.TryParse(slotName, out var slot))
                {
                    Plugin.Log.LogWarning($"[EquipmentService] Unknown equipment slot: {slotName}");
                    continue;
                }

                try
                {
                    var itemGuid = new PrefabGUID((int)guidValue);
                    _serverGameManager.EquipItem(player, itemGuid, slot.ToSlotIndex(), true);
                    equippedCount++;
                    
                    Plugin.Log.LogDebug($"[EquipmentService] Equipped {itemGuid.Value} to slot {slotName}");
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"[EquipmentService] Failed to equip {guidValue} to slot {slotName}: {ex.Message}");
                }
            }

            if (equippedCount > 0)
            {
                Plugin.Log.LogInfo($"[EquipmentService] Equipped {equippedCount}/{equipment.Count} items for player {player.Index}");
            }
            
            return equippedCount;
        }

        /// <summary>
        /// Equip a full kit using proper EquipmentSlot enum.
        /// </summary>
        /// <param name="player">Player entity.</param>
        /// <param name="equipment">Dictionary of EquipmentSlot -> PrefabGUID.</param>
        /// <returns>Number of items successfully equipped.</returns>
        public int EquipKit(Entity player, Dictionary<EquipmentSlot, PrefabGUID> equipment)
        {
            if (!PlayerHelper.IsValidPlayer(_entityManager, player))
            {
                return 0;
            }

            if (equipment == null || equipment.Count == 0)
            {
                return 0;
            }

            if (_serverGameManager == null)
            {
                Plugin.Log.LogWarning("[EquipmentService] ServerGameManager not available");
                return 0;
            }

            int equippedCount = 0;

            foreach (var (slot, guid) in equipment)
            {
                if (guid.Value == 0)
                    continue;

                try
                {
                    _serverGameManager.EquipItem(player, guid, slot.ToSlotIndex(), true);
                    equippedCount++;
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"[EquipmentService] Failed to equip {guid.Value} to slot {slot}: {ex.Message}");
                }
            }

            return equippedCount;
        }

        /// <summary>
        /// Equip a single item to a specific slot.
        /// </summary>
        /// <param name="player">Player entity.</param>
        /// <param name="slotName">Equipment slot name.</param>
        /// <param name="itemGuid">Prefab GUID of the item.</param>
        /// <returns>True if successfully equipped.</returns>
        public bool EquipItem(Entity player, string slotName, PrefabGUID itemGuid)
        {
            if (!PlayerHelper.IsValidPlayer(_entityManager, player))
                return false;

            if (itemGuid.Value == 0)
                return false;

            if (!EquipmentSlotConverter.TryParse(slotName, out var slot))
                return false;

            if (_serverGameManager == null)
                return false;

            try
            {
                _serverGameManager.EquipItem(player, itemGuid, slot.ToSlotIndex(), true);
                Plugin.Log.LogDebug($"[EquipmentService] Equipped {itemGuid.Value} to slot {slotName}");
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[EquipmentService] Failed to equip {itemGuid.Value} to slot {slotName}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets the equipment currently equipped on a player.
        /// </summary>
        /// <param name="player">Player entity.</param>
        /// <returns>Dictionary of slot name -> PrefabGUID.</returns>
        public Dictionary<string, PrefabGUID> GetEquippedGear(Entity player)
        {
            var result = new Dictionary<string, PrefabGUID>();

            if (!PlayerHelper.IsValidPlayer(_entityManager, player))
                return result;

            // Get all equipment slots and check for equipped items
            var slots = Enum.GetValues(typeof(EquipmentSlot)).Cast<EquipmentSlot>();
            
            foreach (var slot in slots)
            {
                if (slot == EquipmentSlot.None)
                    continue;

                // Note: Direct equipment checking requires inventory component access
                // This is a placeholder for the pattern
                result[slot.ToString()] = PrefabGUID.Empty;
            }

            return result;
        }

        /// <summary>
        /// Clears all equipment from a player.
        /// </summary>
        public void ClearEquipment(Entity player)
        {
            if (!PlayerHelper.IsValidPlayer(_entityManager, player))
                return;

            Plugin.Log.LogDebug($"[EquipmentService] Equipment clear requested for player {player.Index}");

            // Note: Actual unequip requires inventory manipulation
            // For now, we log the request
        }
    }
}
