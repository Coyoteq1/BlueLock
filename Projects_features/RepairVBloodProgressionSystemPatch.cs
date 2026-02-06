using HarmonyLib;
using ProjectM;
using ProjectM.Network;
using Unity.Collections;
using Unity.Entities;
using BepInEx.Logging;

namespace VAuto.Patches
{
    /// <summary>
    /// Harmony patch for RepairVBloodProgressionSystem to implement arena-based repair control
    /// Entry: skip repair; Exit: allow one repair tick
    /// </summary>
    [HarmonyPatch(typeof(RepairVBloodProgressionSystem), nameof(RepairVBloodProgressionSystem.OnUpdate))]
    internal static class RepairVBloodProgressionSystemPatch
    {
        private static readonly ManualLogSource Log = Plugin.Logger;
        private static bool _isArenaEntryPhase = false;
        private static bool _isArenaExitPhase = false;

        public static bool Prefix(RepairVBloodProgressionSystem __instance)
        {
            try
            {
                var arenaState = GetCurrentArenaState();
                switch (arenaState)
                {
                    case ArenaPhase.Entering:
                        return HandleArenaEntryRepair();
                    case ArenaPhase.Exiting:
                        return HandleArenaExitRepair();
                    case ArenaPhase.Active:
                        Log?.LogDebug("[RepairVBloodPatch] Skipping repair - arena active");
                        return false;
                    default:
                        return true;
                }
            }
            catch (System.Exception ex)
            {
                Log?.LogError($"[RepairVBloodPatch] Error in prefix patch: {ex.Message}");
                return true;
            }
        }

        private static bool HandleArenaEntryRepair()
        {
            if (!_isArenaEntryPhase)
            {
                _isArenaEntryPhase = true;
                _isArenaExitPhase = false;
                Log?.LogInfo("[RepairVBloodPatch] Arena entry phase - skipping repair");
                return false; // block vanilla repair
            }
            return false;
        }

        private static bool HandleArenaExitRepair()
        {
            if (!_isArenaExitPhase)
            {
                Log?.LogInfo("[RepairVBloodPatch] Arena exit phase - allowing one repair tick");
                _isArenaExitPhase = true;
                _isArenaEntryPhase = false;
                return true; // allow one repair tick
            }

            // After repair tick, block further repairs
            Log?.LogInfo("[RepairVBloodPatch] Exit phase complete - blocking further repairs");
            return false;
        }

        private static ArenaPhase GetCurrentArenaState()
        {
            try
            {
                // For testing: check if any connected users exist
                var em = World.DefaultGameObjectInjectionWorld.EntityManager;
                var userQuery = em.CreateEntityQuery(ComponentType.ReadOnly<User>());
                
                using var userEntities = userQuery.ToEntityArray(Allocator.Temp);
                
                if (userEntities.Length == 0)
                {
                    return ArenaPhase.None;
                }
                
                // For now, default to none - the patch will allow vanilla behavior
                // This gives you a baseline to test: repairs should work normally
                return ArenaPhase.None;
            }
            catch (System.Exception ex)
            {
                Log?.LogWarning($"[RepairVBloodPatch] Arena state detection error: {ex.Message}");
                return ArenaPhase.None;
            }
        }

        public static void ResetPhaseTracking()
        {
            _isArenaEntryPhase = false;
            _isArenaExitPhase = false;
            Log?.LogDebug("[RepairVBloodPatch] Reset phase tracking");
        }
    }

    public enum ArenaPhase { None, Entering, Active, Exiting }
}

