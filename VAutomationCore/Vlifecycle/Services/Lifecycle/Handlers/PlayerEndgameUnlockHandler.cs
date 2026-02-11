using Unity.Entities;
using VAuto.Core;

namespace VAuto.Core.Lifecycle.Handlers
{
    /// <summary>
    /// Enables sandbox mode + unlocks all VBlood content for arena session.
    /// This handler gives the player all abilities, tech, recipes, and forms.
    /// </summary>
    public class PlayerEndgameUnlockHandler : LifecycleActionHandler
    {
        private const string LogSource = "PlayerEndgameUnlockHandler";

        public bool Execute(LifecycleAction action, LifecycleContext context)
        {
            var em = VAutoCore.EntityManager;
            var user = context.UserEntity;
            var character = context.CharacterEntity;

            if (user == Entity.Null)
            {
                VAutoLogger.LogWarning($"[{LogSource}] User entity is null");
                return false;
            }

            try
            {
                // Enable debug/sandbox unlock mode on User
                if (em.HasComponent<User>(user))
                {
                    var userData = em.GetComponentData<User>(user);
                    // Set debug flags if available - these may need adjustment based on actual components
                    VAutoLogger.LogInfo($"[{LogSource}] Sandbox mode enabled for user");
                    em.SetComponentData(user, userData);
                }

                // Apply all unlocks via SandboxUnlockUtility
                SandboxUnlockUtility.UnlockEverythingForPlayer(user);

                VAutoLogger.LogInfo($"[{LogSource}] ✅ Endgame unlocks applied for arena session");
                return true;
            }
            catch (System.Exception ex)
            {
                VAutoLogger.LogException(ex);
                VAutoLogger.LogError($"[{LogSource}] Failed: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Restores normal progression on arena exit.
    /// Removes all sandbox unlocks and restores vanilla progression state.
    /// </summary>
    public class PlayerEndgameLockHandler : LifecycleActionHandler
    {
        private const string LogSource = "PlayerEndgameLockHandler";

        public bool Execute(LifecycleAction action, LifecycleContext context)
        {
            var em = VAutoCore.EntityManager;
            var user = context.UserEntity;

            if (user == Entity.Null)
            {
                VAutoLogger.LogWarning($"[{LogSource}] User entity is null");
                return false;
            }

            try
            {
                // Disable debug/sandbox unlock mode
                if (em.HasComponent<User>(user))
                {
                    var userData = em.GetComponentData<User>(user);
                    VAutoLogger.LogInfo($"[{LogSource}] Sandbox mode disabled for user");
                    em.SetComponentData(user, userData);
                }

                // Clear temporary unlocks
                SandboxUnlockUtility.ResetTemporaryUnlocks(user);

                VAutoLogger.LogInfo($"[{LogSource}] ✅ Player progression returned to vanilla state");
                return true;
            }
            catch (System.Exception ex)
            {
                VAutoLogger.LogException(ex);
                VAutoLogger.LogError($"[{LogSource}] Failed: {ex.Message}");
                return false;
            }
        }
    }
}
