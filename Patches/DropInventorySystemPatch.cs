using System;
using System.Runtime.InteropServices;
using HarmonyLib;
using ProjectM;
using ProjectM.Shared;
using Stunlock.Core;
using Unity.Entities;
using Unity.Transforms;
using VAuto.Zone.Core;
using VAuto.Zone.Services;

namespace VAuto.Zone.ArenaPatches
{
    [HarmonyPatch(typeof(DropInventorySystem), nameof(DropInventorySystem.DropItem))]
    internal static class DropInventorySystemPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(
            DropInventorySystem __instance,
            EntityCommandBuffer commandBuffer,
            [In] ref Translation translation,
            PrefabGUID itemHash,
            int amount,
            Entity itemEntity,
            Il2CppSystem.Nullable_Unboxed<float> customDropArc,
            Il2CppSystem.Nullable_Unboxed<float> minRange,
            Il2CppSystem.Nullable_Unboxed<float> maxRange)
        {
            try
            {
                var em = ZoneCore.EntityManager;
                if (em == default || itemEntity == Entity.Null || !em.Exists(itemEntity))
                {
                    return true;
                }

                if (!em.HasComponent<EntityOwner>(itemEntity))
                {
                    return true;
                }

                var owner = em.GetComponentData<EntityOwner>(itemEntity).Owner;
                if (owner == Entity.Null || !em.Exists(owner))
                {
                    return true;
                }

                var now = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000d;
                if (ArenaDeathTracker.TryGetRecentArenaDeath(owner, now, out _))
                {
                    // Cancel vanilla drop for arena-caused deaths.
                    return false;
                }
            }
            catch (System.Exception ex)
            {
                ZoneCore.LogWarning($"[ArenaLoot] Drop hook failed: {ex.Message}");
            }

            return true;
        }
    }
}
