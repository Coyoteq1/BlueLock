using System;
using System.Collections.Generic;
using HarmonyLib;
using Unity.Entities;
using VAuto.Core.Services;
using VAutomationCore.Core.Logging;

namespace VAutomationCore.Core.Patches
{
    /// <summary>
    /// Patch-first hooks for glow spawning, config override, validation, and rollback.
    /// </summary>
    [HarmonyPatch]
    internal static class GlowLifecyclePatch
    {
        public static Func<int, bool> CanSpawnGlow;
        public static Func<EntitySpawner.GlowConfig, EntitySpawner.GlowConfig> OverrideGlowConfig;
        public static Func<EntitySpawner.GlowConfig, EntitySpawner.GlowConfig> ValidateGlowConfig;
        public static Action<IReadOnlyList<Entity>> OnSpawnFailure;

        [HarmonyPatch(typeof(EntitySpawner), nameof(EntitySpawner.SpawnGlowingBuffEntities))]
        [HarmonyPrefix]
        private static bool SpawnGlowingBuffEntitiesPrefix(ref EntitySpawner.SpawnConfig? config, ref EntitySpawner.SpawnResult __result)
        {
            try
            {
                var cfg = config ?? EntitySpawner.SpawnConfig.Default;
                if (CanSpawnGlow != null && !CanSpawnGlow(cfg.Count))
                {
                    __result = default;
                    return false;
                }

                if (OverrideGlowConfig != null)
                {
                    cfg.Glow = OverrideGlowConfig(cfg.Glow);
                    config = cfg;
                }

                return true;
            }
            catch (Exception ex)
            {
                CoreLogger.LogException("Error in GlowLifecyclePatch.SpawnGlowingBuffEntitiesPrefix", ex);
                __result = default;
                return false;
            }
        }

        [HarmonyPatch(typeof(EntitySpawner), nameof(EntitySpawner.SpawnGlowingBuffEntities))]
        [HarmonyPostfix]
        private static void SpawnGlowingBuffEntitiesPostfix(EntitySpawner.SpawnResult __result)
        {
            try
            {
                if (OnSpawnFailure == null || __result.FailCount <= 0 || !__result.SpawnedEntities.IsCreated)
                {
                    return;
                }

                var list = new List<Entity>(__result.SpawnedEntities.Length);
                for (var i = 0; i < __result.SpawnedEntities.Length; i++)
                {
                    list.Add(__result.SpawnedEntities[i]);
                }

                OnSpawnFailure(list);
            }
            catch (Exception ex)
            {
                CoreLogger.LogException("Error in GlowLifecyclePatch.SpawnGlowingBuffEntitiesPostfix", ex);
            }
        }

        [HarmonyPatch(typeof(EntitySpawner), nameof(EntitySpawner.SpawnSingleGlowingBuffEntity))]
        [HarmonyPrefix]
        private static void SpawnSingleGlowingBuffEntityPrefix(ref EntitySpawner.GlowConfig? glowConfig)
        {
            try
            {
                if (OverrideGlowConfig == null && ValidateGlowConfig == null)
                {
                    return;
                }

                var cfg = glowConfig ?? EntitySpawner.GlowConfig.Default;
                if (OverrideGlowConfig != null)
                {
                    cfg = OverrideGlowConfig(cfg);
                }

                if (ValidateGlowConfig != null)
                {
                    cfg = ValidateGlowConfig(cfg);
                }

                glowConfig = cfg;
            }
            catch (Exception ex)
            {
                CoreLogger.LogException("Error in GlowLifecyclePatch.SpawnSingleGlowingBuffEntityPrefix", ex);
            }
        }

        [HarmonyPatch(typeof(EntitySpawner), nameof(EntitySpawner.SpawnGlowBorder))]
        [HarmonyPrefix]
        private static bool SpawnGlowBorderPrefix(int entityCount, ref EntitySpawner.GlowConfig? glowConfig, ref EntitySpawner.SpawnResult __result)
        {
            try
            {
                if (CanSpawnGlow != null && !CanSpawnGlow(entityCount))
                {
                    __result = default;
                    return false;
                }

                if (OverrideGlowConfig != null)
                {
                    var cfg = glowConfig ?? EntitySpawner.GlowConfig.Default;
                    glowConfig = OverrideGlowConfig(cfg);
                }

                return true;
            }
            catch (Exception ex)
            {
                CoreLogger.LogException("Error in GlowLifecyclePatch.SpawnGlowBorderPrefix", ex);
                __result = default;
                return false;
            }
        }

        [HarmonyPatch(typeof(EntitySpawner), nameof(EntitySpawner.SpawnGlowGrid))]
        [HarmonyPrefix]
        private static bool SpawnGlowGridPrefix(int rows, int columns, ref EntitySpawner.GlowConfig? glowConfig, ref EntitySpawner.SpawnResult __result)
        {
            try
            {
                var count = rows * columns;
                if (CanSpawnGlow != null && !CanSpawnGlow(count))
                {
                    __result = default;
                    return false;
                }

                if (OverrideGlowConfig != null)
                {
                    var cfg = glowConfig ?? EntitySpawner.GlowConfig.Default;
                    glowConfig = OverrideGlowConfig(cfg);
                }

                return true;
            }
            catch (Exception ex)
            {
                CoreLogger.LogException("Error in GlowLifecyclePatch.SpawnGlowGridPrefix", ex);
                __result = default;
                return false;
            }
        }

        [HarmonyPatch(typeof(EntitySpawner), nameof(EntitySpawner.UpdateGlowConfig))]
        [HarmonyPrefix]
        private static void UpdateGlowConfigPrefix(ref EntitySpawner.GlowConfig newConfig)
        {
            try
            {
                if (ValidateGlowConfig == null)
                {
                    return;
                }

                newConfig = ValidateGlowConfig(newConfig);
            }
            catch (Exception ex)
            {
                CoreLogger.LogException("Error in GlowLifecyclePatch.UpdateGlowConfigPrefix", ex);
            }
        }
    }
}
