using System;
using HarmonyLib;
using ProjectM;
using Unity.Entities;
using VAutomationCore.Core.Logging;

namespace VAutomationCore.Patches
{
    /// <summary>
    /// Patch for Buff spawning to track when buffs are applied to entities.
    /// Useful for tracking buff effects, durations, and stacks.
    /// </summary>
    [HarmonyPatch(typeof(Buff), nameof(Buff.Initialize))]
    internal static class BuffSpawnServerPatch
    {
        public static event EventHandler<BuffEventArgs> OnBuffInitialized;
        
        public class BuffEventArgs : EventArgs
        {
            public Entity Owner { get; set; }
            public Entity Source { get; set; }
            public PrefabGUID BuffGuid { get; set; }
            public float Duration { get; set; }
            public bool IsExtended { get; set; }
        }

        [HarmonyPrefix]
        static bool InitializePrefix(Buff __instance, Entity owner, Entity sourceEntity)
        {
            try
            {
                var args = new BuffEventArgs
                {
                    Owner = owner,
                    Source = sourceEntity,
                    BuffGuid = __instance.PrefabGuid,
                    Duration = __instance.Duration,
                    IsExtended = __instance.IsExtended
                };

                OnBuffInitialized?.Invoke(__instance, args);
            }
            catch (Exception ex)
            {
                CoreLogger.LogException("Error in buff initialize prefix", ex);
            }

            return true; // Continue with original method
        }
    }

    /// <summary>
    /// Patch for Buff destruction to track when buffs are removed.
    /// </summary>
    [HarmonyPatch(typeof(Buff), nameof(Buff.Destroy))]
    internal static class BuffDestroyPatch
    {
        public static event EventHandler<BuffEventArgs> OnBuffDestroyed;
        
        public class BuffEventArgs : EventArgs
        {
            public Entity Owner { get; set; }
            public PrefabGUID BuffGuid { get; set; }
        }

        [HarmonyPrefix]
        static bool DestroyPrefix(Buff __instance, Entity owner)
        {
            try
            {
                var args = new BuffEventArgs
                {
                    Owner = owner,
                    BuffGuid = __instance.PrefabGuid
                };

                OnBuffDestroyed?.Invoke(__instance, args);
            }
            catch (Exception ex)
            {
                CoreLogger.LogException("Error in buff destroy prefix", ex);
            }

            return true; // Continue with original method
        }
    }
}
