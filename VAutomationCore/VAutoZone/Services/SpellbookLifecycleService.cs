using System;
using BepInEx.Logging;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using VAuto.Zone.Core;
using VAuto.Zone.Core.Lifecycle;

namespace VAuto.Zone.Services
{
    /// <summary>
    /// Service for managing spellbook granting automation on zone transitions.
    /// Implements ILifecycleActionHandler for integration with lifecycle stages.
    /// </summary>
    public class SpellbookLifecycleService : ILifecycleActionHandler
    {
        private const string LogSource = "SpellbookLifecycleService";
        
        /// <summary>
        /// Priority for grant requests (lower = higher priority).
        /// </summary>
        public int Priority { get; set; } = 0;
        
        /// <summary>
        /// Behavior when inventory is full.
        /// </summary>
        public InventoryOverflowBehavior OverflowBehavior { get; set; } = InventoryOverflowBehavior.DropExisting;

        /// <summary>
        /// Behavior when inventory is full.
        /// </summary>
        public enum InventoryOverflowBehavior
        {
            DropExisting,
            FailGracefully,
            CreateMail
        }

        private static ManualLogSource _log => ZoneCore.Log;

        /// <summary>
        /// Executes the spellbook grant action for the given context.
        /// </summary>
        public bool Execute(LifecycleModels.LifecycleAction action, LifecycleModels.LifecycleContext context)
        {
            if (action.Type != "SpellbookGrant")
            {
                _log.LogDebug($"[{LogSource}] Ignoring action type: {action.Type}");
                return false;
            }

            var em = LifecycleCore.EntityManager;
            var character = context.CharacterEntity;

            if (character == Entity.Null)
            {
                _log.LogWarning($"[{LogSource}] Character entity is null");
                return false;
            }

            try
            {
                // Get spellbook ID from action
                if (string.IsNullOrEmpty(action.ConfigId))
                {
                    _log.LogWarning($"[{LogSource}] No spellbook ID specified in action");
                    return false;
                }

                // Grant spellbook
                var result = GrantSpellbook(character, action.ConfigId, em);
                
                _log.LogInfo($"[{LogSource}] Spellbook grant result: {result}");
                return result == GrantResult.Success;
            }
            catch (Exception ex)
            {
                _log.LogError($"[{LogSource}] Exception: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Grants a spellbook to the player.
        /// </summary>
        private GrantResult GrantSpellbook(Entity character, string spellbookId, EntityManager em)
        {
            // Check if player already has the spellbook
            if (PlayerAlreadyHasSpellbook(character, spellbookId, em))
            {
                _log.LogDebug($"[{LogSource}] Player already has spellbook: {spellbookId}");
                return GrantResult.AlreadyOwned;
            }

            // Check inventory space
            if (!HasInventorySpace(character, em))
            {
                _log.LogWarning($"[{LogSource}] Inventory full for spellbook: {spellbookId}");
                
                switch (OverflowBehavior)
                {
                    case InventoryOverflowBehavior.FailGracefully:
                        return GrantResult.InventoryFull;
                    case InventoryOverflowBehavior.CreateMail:
                        // TODO: Create mail with spellbook
                        return GrantResult.InventoryFull;
                    default:
                        break;
                }
            }

            try
            {
                // Grant spellbook using game API
                // This is a placeholder - actual implementation would use the game's item granting API
                _log.LogInfo($"[{LogSource}] Granting spellbook: {spellbookId}");
                
                // Placeholder: Would call actual game API here
                // Example: InventoryUtilities.TryGrantItem(character, spellbookId);
                
                return GrantResult.Success;
            }
            catch (Exception ex)
            {
                _log.LogError($"[{LogSource}] Grant failed: {ex.Message}");
                return GrantResult.Failed;
            }
        }

        /// <summary>
        /// Checks if player already has a spellbook.
        /// </summary>
        private bool PlayerAlreadyHasSpellbook(Entity character, string spellbookId, EntityManager em)
        {
            if (!em.HasComponent<Inventory>(character))
            {
                return false;
            }

            var items = em.GetBuffer<InventoryItem>(character);
            
            foreach (var item in items)
            {
                if (item.ItemEntity == Entity.Null) continue;
                
                // Check if item matches spellbook ID
                // This is a placeholder - actual implementation would compare item GUIDs
                // var itemData = em.GetComponentData<ItemData>(item.ItemEntity);
                // if (itemData.PrefabName == spellbookId) return true;
            }
            
            return false;
        }

        /// <summary>
        /// Checks if player has inventory space.
        /// </summary>
        private bool HasInventorySpace(Entity character, EntityManager em)
        {
            if (!em.HasComponent<Inventory>(character))
            {
                return false;
            }

            var inventory = em.GetComponentData<Inventory>(character);
            var items = em.GetBuffer<InventoryItem>(character);
            
            return items.Length < inventory.MaxSlots;
        }

        /// <summary>
        /// Creates a spellbook grant lifecycle action.
        /// </summary>
        public static LifecycleModels.LifecycleAction CreateSpellbookGrantAction(string spellbookId)
        {
            return new LifecycleModels.LifecycleAction
            {
                Type = "SpellbookGrant",
                ConfigId = spellbookId
            };
        }

        /// <summary>
        /// Grants spellbooks based on configuration.
        /// Called when player enters a lifecycle zone.
        /// </summary>
        public static bool GrantSpellbooksOnZoneEnter(Entity character, string[] spellbookIds)
        {
            if (character == Entity.Null || spellbookIds == null) return false;
            
            var em = LifecycleCore.EntityManager;
            var allGranted = true;

            foreach (var spellbookId in spellbookIds)
            {
                var service = new SpellbookLifecycleService();
                var action = CreateSpellbookGrantAction(spellbookId);
                var context = new LifecycleModels.LifecycleContext
                {
                    CharacterEntity = character,
                    Position = LifecycleCore.GetPosition(character)
                };

                if (!service.Execute(action, context))
                {
                    allGranted = false;
                }
            }

            return allGranted;
        }
    }
}
