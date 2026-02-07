using HarmonyLib;
using ProjectM;
using Unity.Entities;
using Unity.Collections;
using UnityEngine;

namespace VLifecycle.Services.Lifecycle
{
    /// <summary>
    /// Patches ClientBootstrapSystem.OnUpdate to momentarily block all game inputs during ZUI interactions.
    /// Only blocks when explicitly triggered by ZUIInputBlocker.BlockMomentarily().
    /// </summary>
    [HarmonyPatch(typeof(ClientBootstrapSystem), nameof(ClientBootstrapSystem.OnUpdate))]
    public static class InputSystemUpdatePatch
    {
        private static bool _previousBlockState = false;

        [HarmonyPostfix]
        public static void Postfix(ClientBootstrapSystem __instance)
        {
            try
            {
                var shouldBlock = ZUIInputBlocker.ShouldBlock;

                // CRITICAL: Only do something if the block state has CHANGED
                if (shouldBlock == _previousBlockState)
                {
                    return; // No change, do nothing
                }

                Plugin.Log.LogDebug($"[ZUI] Block state changed: {_previousBlockState} -> {shouldBlock}");
                _previousBlockState = shouldBlock;

                var entityManager = __instance.EntityManager;
                var playerQuery = entityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<PlayerCharacter>(),
                    ComponentType.ReadOnly<EntityInput>()
                );

                if (playerQuery.IsEmpty)
                {
                    playerQuery.Dispose();
                    return;
                }

                var playerEntities = playerQuery.ToEntityArray(Allocator.Temp);

                foreach (var playerEntity in playerEntities)
                {
                    if (shouldBlock)
                    {
                        // BLOCK: Add components
                        if (!entityManager.HasComponent<InputActionsDisabled>(playerEntity))
                        {
                            entityManager.AddComponentData(playerEntity, new InputActionsDisabled());
                            Plugin.Log.LogDebug("[ZUI] Added InputActionsDisabled");
                        }

                        if (!entityManager.HasComponent<Disabled>(playerEntity))
                        {
                            entityManager.AddComponentData(playerEntity, new Disabled());
                            Plugin.Log.LogDebug("[ZUI] Added Disabled");
                        }
                    }
                    else
                    {
                        // UNBLOCK: Remove components
                        if (entityManager.HasComponent<InputActionsDisabled>(playerEntity))
                        {
                            entityManager.RemoveComponent<InputActionsDisabled>(playerEntity);
                            Plugin.Log.LogDebug("[ZUI] Removed InputActionsDisabled");
                        }

                        if (entityManager.HasComponent<Disabled>(playerEntity))
                        {
                            entityManager.RemoveComponent<Disabled>(playerEntity);
                            Plugin.Log.LogDebug("[ZUI] Removed Disabled");
                        }
                    }
                }

                playerEntities.Dispose();
                playerQuery.Dispose();
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"[ZUI] Error in input blocking: {ex}");
            }
        }
    }
}
