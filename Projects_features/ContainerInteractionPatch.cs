using System;
using HarmonyLib;
using Unity.Entities;
using Unity.Mathematics;
using VAuto.Core;
using VAuto.Core.Components;
using VAuto.Services.Systems;

namespace VAuto.Core.Harmony
{
    /// <summary>
    /// Harmony patches for detecting container interactions and triggering automations.
    /// </summary>
    [HarmonyPatch]
    public class ContainerInteractionPatch
    {
        private static AutomationTriggerSystem _automationSystem;
        private static EntityManager _entityManager;
        
        /// <summary>
        /// Gets or creates the automation system reference.
        /// </summary>
        private static AutomationTriggerSystem GetAutomationSystem()
        {
            if (_automationSystem == null)
            {
                try
                {
                    var world = VRCore.World;
                    if (world != null)
                    {
                        var systemHandle = world.GetExistingSystemManaged<AutomationTriggerSystem>();
                        _automationSystem = world.GetSystem<AutomationTriggerSystem>(systemHandle);
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"[ContainerPatch] Could not get automation system: {ex.Message}");
                }
            }
            return _automationSystem;
        }
        
        /// <summary>
        /// Called when a container is opened. This is the main integration point.
        /// </summary>
        public static void OnContainerOpened(Entity containerEntity, Entity playerEntity)
        {
            try
            {
                var automationSystem = GetAutomationSystem();
                if (automationSystem != null)
                {
                    automationSystem.OnContainerOpened(containerEntity, playerEntity);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[ContainerPatch] Error in OnContainerOpened: {ex.Message}");
            }
        }
        
        #region Example Patches (Uncomment and adjust for actual game methods)
        
        /*
        /// <summary>
        /// Patch for InventoryManager.TryOpenItemContainer
        /// </summary>
        [HarmonyPatch(typeof(InventoryManager), nameof(InventoryManager.TryOpenItemContainer))]
        [HarmonyPrefix]
        static bool TryOpenItemContainer_Prefix(
            InventoryManager __instance,
            Entity user,
            Entity container,
            ref bool __result)
        {
            try
            {
                Plugin.Log.LogInfo($"[ContainerPatch] TryOpenItemContainer called for container {container}");
                
                // Let the original method run
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[ContainerPatch] TryOpenItemContainer prefix error: {ex.Message}");
                return true;
            }
        }
        
        /// <summary>
        /// Postfix for InventoryManager.TryOpenItemContainer
        /// </summary>
        [HarmonyPatch(typeof(InventoryManager), nameof(InventoryManager.TryOpenItemContainer))]
        [HarmonyPostfix]
        static void TryOpenItemContainer_Postfix(
            InventoryManager __instance,
            Entity user,
            Entity container,
            bool __result)
        {
            if (__result)
            {
                OnContainerOpened(container, user);
            }
        }
        
        /// <summary>
        /// Patch for ChestSystem.OpenChest
        /// </summary>
        [HarmonyPatch(typeof(ChestSystem), "OpenChest")]
        [HarmonyPrefix]
        static bool OpenChest_Prefix(ChestSystem __instance, Entity player, Entity chest)
        {
            try
            {
                Plugin.Log.LogInfo($"[ContainerPatch] OpenChest called for chest {chest}");
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[ContainerPatch] OpenChest prefix error: {ex.Message}");
                return true;
            }
        }
        
        [HarmonyPatch(typeof(ChestSystem), "OpenChest")]
        [HarmonyPostfix]
        static void OpenChest_Postfix(ChestSystem __instance, Entity player, Entity chest, bool __result)
        {
            if (__result)
            {
                OnContainerOpened(chest, player);
            }
        }
        */
        
        #endregion
        
        /// <summary>
        /// Apply patches for container interactions.
        /// </summary>
        public static void PatchAll(HarmonyLib.Harmony harmony)
        {
            try
            {
                // Uncomment these when you identify the actual game methods for container interaction
                // var originalTryOpen = AccessTools.Method(typeof(InventoryManager), "TryOpenItemContainer");
                // var prefixTryOpen = new HarmonyMethod(typeof(ContainerInteractionPatch), nameof(TryOpenItemContainer_Prefix));
                // var postfixTryOpen = new HarmonyMethod(typeof(ContainerInteractionPatch), nameof(TryOpenItemContainer_Postfix));
                // harmony.Patch(originalTryOpen, prefixTryOpen, postfixTryOpen);
                
                Plugin.Log.LogInfo("[ContainerPatch] Patches ready (methods not yet identified)");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[ContainerPatch] Failed to patch: {ex.Message}");
            }
        }
    }
}
