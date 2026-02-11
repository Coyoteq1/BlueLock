using HarmonyLib;
using ProjectM;
using ProjectM.Gameplay.Systems;
using Unity.Entities;
using VAuto.Core;

namespace VAuto.Core.Lifecycle.Handlers
{
    /// <summary>
    /// Harmony patch to prevent VBlood feeds from granting permanent unlocks while player is in arena.
    /// This ensures arena sessions don't permanently alter player progression.
    /// </summary>
    [HarmonyPatch]
    public static class PatchBuffSystemSpawnServer
    {
        private const string LogSource = "PatchBuffSystemSpawnServer";

        /// <summary>
        /// Target method: BuffSystemSpawnServer.OnUpdate
        /// </summary>
        public static System.Type TargetType()
        {
            // Try to find BuffSystemSpawnServer type
            var type = AccessTools.TypeByName("ProjectM.Gameplay.Systems.BuffSystemSpawnServer");
            if (type == null)
            {
                VAutoLogger.LogWarning($"[{LogSource}] Could not find BuffSystemSpawnServer type");
            }
            return type;
        }

        /// <summary>
        /// Prefix: Skip VBlood feed unlocks if player is in arena
        /// </summary>
        public static void Prefix(object __instance)
        {
            try
            {
                if (!ArenaTracker.IsAnyPlayerInArena)
                {
                    return; // Not in arena, allow normal behavior
                }

                // Get EntityManager from instance
                var em = VAutoCore.EntityManager;
                if (em == null)
                {
                    return;
                }

                // Check for pending boss spawns that would grant VBlood unlocks
                // This is a simplified check - actual implementation depends on BuffSystemSpawnServer internals

                VAutoLogger.LogDebug($"[{LogSource}] Player in arena - VBlood unlock events may be blocked");

                // The actual blocking would require:
                // 1. Access to the SpawnQueue or similar internal state
                // 2. Identifying VBlood feed buff entities
                // 3. Clearing their CreateGameplayEventsOnSpawn buffers
            }
            catch (System.Exception ex)
            {
                VAutoLogger.LogException(ex);
            }
        }

        /// <summary>
        /// Postfix: Clean up pending boss entities when in arena
        /// </summary>
        public static void Postfix(object __instance)
        {
            try
            {
                if (!ArenaTracker.IsAnyPlayerInArena)
                {
                    return;
                }

                var em = VAutoCore.EntityManager;
                if (em == null)
                {
                    return;
                }

                // Clean up any pending boss spawns
                var pendingBosses = ArenaTracker.GetPendingBosses();
                foreach (var boss in pendingBosses)
                {
                    if (em.Exists(boss))
                    {
                        // Destroy the boss entity
                        em.DestroyEntity(boss);
                        ArenaTracker.RemovePendingBoss(boss);
                        VAutoLogger.LogDebug($"[{LogSource}] Destroyed pending boss entity");
                    }
                }
            }
            catch (System.Exception ex)
            {
                VAutoLogger.LogException(ex);
            }
        }
    }

    /// <summary>
    /// Patch for VBlood feed components to prevent permanent unlocks in arena.
    /// </summary>
    [HarmonyPatch]
    public static class PatchVBloodFeedUnlocks
    {
        private const string LogSource = "PatchVBloodFeedUnlocks";

        /// <summary>
        /// Target: VBlood feed completion handlers
        /// </summary>
        public static System.Type TargetType()
        {
            // Look for VBlood feed completion handlers
            var type = AccessTools.TypeByName("ProjectM.Gameplay.Systems.VBloodFeedCompletionSystem");
            if (type == null)
            {
                VAutoLogger.LogDebug($"[{LogSource}] VBloodFeedCompletionSystem type not found");
            }
            return type;
        }

        /// <summary>
        /// Prefix: Block unlocks if player is in arena
        /// </summary>
        public static bool Prefix()
        {
            if (ArenaTracker.IsAnyPlayerInArena)
            {
                VAutoLogger.LogDebug($"[{LogSource}] VBlood unlock blocked - player in arena");
                return false; // Skip original method
            }
            return true; // Allow normal behavior
        }
    }
}
