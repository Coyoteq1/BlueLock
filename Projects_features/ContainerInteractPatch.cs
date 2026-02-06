using System;
using HarmonyLib;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VAuto.Core;
using VAuto.Core.Components;
using VAuto.Services.Systems;

namespace VAuto.Core.Harmony
{
    /// <summary>
    /// Harmony patches for container interaction interception.
    /// - Blocks opening if player doesn't meet kill streak requirement
    /// - Traps containers when unauthorized access is attempted
    /// </summary>
    [HarmonyPatch]
    public class ContainerInteractPatch
    {
        /// <summary>
        /// Called when a player attempts to open a container.
        /// Returns true to allow, false to block.
        /// </summary>
        public static bool OnContainerInteract(Entity player, Entity container, out string blockReason)
        {
            blockReason = null;

            try
            {
                // Check eligibility via ChestEligibilitySystem
                if (!ChestEligibilitySystem.TryOpenChest(player, container))
                {
                    blockReason = "Kill streak requirement not met";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[ContainerInteract] Error: {ex.Message}");
                return true; // Allow on error
            }
        }

        /// <summary>
        /// Called when a player kills another player.
        /// Integrates with KillStreakTrackingSystem.
        /// </summary>
        public static void OnPlayerKill(Entity killer, Entity victim)
        {
            try
            {
                KillStreakTrackingSystem.RecordKill(killer, victim);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[ContainerInteract] Kill record error: {ex.Message}");
            }
        }

        #region Example Patches (uncomment and adjust for actual game methods)

        /*
        /// <summary>
        /// Patch for InventoryManager.TryOpenItemContainer
        /// </summary>
        [HarmonyPatch(typeof(InventoryManager), "TryOpenItemContainer")]
        [HarmonyPrefix]
        static bool TryOpenItemContainer_Prefix(
            InventoryManager __instance,
            Entity user,
            Entity container,
            ref bool __result)
        {
            try
            {
                Plugin.Log.LogDebug($"[ContainerInteract] TryOpenItemContainer: user={user}, container={container}");

                if (!OnContainerInteract(user, container, out var reason))
                {
                    __result = false;
                    Plugin.Log.LogInfo($"[ContainerInteract] Blocked container open: {reason}");
                    return false;
                }

                return true; // Continue to original method
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[ContainerInteract] Prefix error: {ex.Message}");
                return true;
            }
        }

        /// <summary>
        /// Patch for damage/death events
        /// </summary>
        [HarmonyPatch(typeof(DamageSystem), "OnDeath")]
        [HarmonyPostfix]
        static void OnDeath_Postfix(
            DamageSystem __instance,
            Entity victim,
            Entity attacker,
            DamageEvent damageEvent)
        {
            try
            {
                if (attacker != Entity.Null && attacker != victim)
                {
                    OnPlayerKill(attacker, victim);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[ContainerInteract] Death patch error: {ex.Message}");
            }
        }
        */

        #endregion

        /// <summary>
        /// Applies all patches.
        /// </summary>
        public static void PatchAll(HarmonyLib.Harmony harmony)
        {
            try
            {
                // Uncomment when game methods are identified:
                // var tryOpen = AccessTools.Method(typeof(InventoryManager), "TryOpenItemContainer");
                // if (tryOpen != null)
                // {
                //     harmony.Patch(tryOpen, 
                //         new HarmonyMethod(typeof(ContainerInteractPatch), nameof(TryOpenItemContainer_Prefix)));
                // }

                Plugin.Log.LogInfo("[ContainerInteract] Patches ready (methods not yet identified)");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[ContainerInteract] Failed to patch: {ex.Message}");
            }
        }
    }
}
