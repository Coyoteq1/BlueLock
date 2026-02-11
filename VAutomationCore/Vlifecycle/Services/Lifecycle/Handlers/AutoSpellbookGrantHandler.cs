using System;
using Unity.Entities;
using VAuto.Core.Lifecycle;

namespace VAuto.Core.Lifecycle.Handlers
{
    /// <summary>
    /// Result enum for spellbook granting operations.
    /// </summary>
    public enum GrantResult
    {
        Success,
        InventoryFull,
        AlreadyOwned,
        Failed
    }

    /// <summary>
    /// Handles automatic spellbook granting on zone transitions.
    /// Implements LifecycleActionHandler interface.
    /// </summary>
    public class AutoSpellbookGrantHandler : LifecycleActionHandler
    {
        private const string LogSource = "AutoSpellbookGrantHandler";
        
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

        /// <summary>
        /// Default constructor.
        /// </summary>
        public AutoSpellbookGrantHandler()
        {
        }

        /// <summary>
        /// Executes the spellbook grant action for the given context.
        /// </summary>
        /// <param name="action">The lifecycle action containing grant parameters.</param>
        /// <param name="context">The lifecycle context containing player entity information.</param>
        /// <returns>True if grant was successful.</returns>
        public bool Execute(LifecycleAction action, LifecycleContext context)
        {
            if (action.Type != "SpellbookGrant")
            {
                VLifecycle.Plugin.Log?.LogDebug($"[{LogSource}] Ignoring action type: {action.Type}");
                return false;
            }

            var em = VAutomationCore.UnifiedCore.EntityManager;
            var character = context.CharacterEntity;

            if (character == Entity.Null)
            {
                VLifecycle.Plugin.Log?.LogWarning($"[{LogSource}] Character entity is null");
                return false;
            }

            try
            {
                // Get spellbook ID from action
                if (string.IsNullOrEmpty(action.ConfigId))
                {
                    VLifecycle.Plugin.Log?.LogWarning($"[{LogSource}] No spellbook ID specified in action");
                    return false;
                }

                // Grant spellbook
                var result = GrantSpellbook(character, action.ConfigId, em);
                
                VLifecycle.Plugin.Log?.LogInfo($"[{LogSource}] Spellbook grant result: {result}");
                return result == GrantResult.Success;
            }
            catch (Exception ex)
            {
                VLifecycle.Plugin.Log?.LogError($"[{LogSource}] Exception: {ex.Message}");
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
                VLifecycle.Plugin.Log?.LogDebug($"[{LogSource}] Player already has spellbook: {spellbookId}");
                return GrantResult.AlreadyOwned;
            }

            // Check inventory space
            if (!HasInventorySpace(character, em))
            {
                VLifecycle.Plugin.Log?.LogWarning($"[{LogSource}] Inventory full for spellbook: {spellbookId}");
                
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
                VLifecycle.Plugin.Log?.LogInfo($"[{LogSource}] Granting spellbook: {spellbookId}");
                
                return GrantResult.Success;
            }
            catch (Exception ex)
            {
                VLifecycle.Plugin.Log?.LogError($"[{LogSource}] Grant failed: {ex.Message}");
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
        public static LifecycleAction CreateSpellbookGrantAction(string spellbookId)
        {
            return new LifecycleAction
            {
                Type = "SpellbookGrant",
                ConfigId = spellbookId
            };
        }
    }
}
