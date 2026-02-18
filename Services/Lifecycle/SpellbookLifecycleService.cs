using System;
using BepInEx.Logging;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using ProjectM;
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
            // For now, treat this as a no-op lifecycle hook that always succeeds
            // when a SpellbookGrant action is dispatched. Actual menu handling
            // is driven via AbilityUi and zone enter/exit hooks.
            if (string.IsNullOrEmpty(action.ConfigId))
            {
                _log.LogWarning($"[{LogSource}] No spellbook ID specified in action");
                return false;
            }

            _log.LogInfo($"[{LogSource}] Spellbook grant hook invoked for '{action.ConfigId}'");
            return true;
        }

        // Inventory-based grant helpers have been removed; the spellbook is treated
        // as a UI/menu concept, not a tangible inventory item.

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
