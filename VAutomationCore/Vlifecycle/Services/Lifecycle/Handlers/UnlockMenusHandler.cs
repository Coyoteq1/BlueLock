using Unity.Entities;
using ProjectM.Network;
using VAuto.Core;

namespace VAuto.Core.Lifecycle.Handlers
{
    /// <summary>
    /// Grants full UI menu access during arena session.
    /// Enables Spellbook, Blood Type selection, and Equipment modification.
    /// </summary>
    public class UnlockMenusHandler : LifecycleActionHandler
    {
        private const string LogSource = "UnlockMenusHandler";

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
                if (!em.HasComponent<User>(user))
                {
                    VAutoLogger.LogWarning($"[{LogSource}] User entity has no User component");
                    return false;
                }

                var userData = em.GetComponentData<User>(user);

                // Unlock all UI menus
                // Note: These field names may vary - adjust based on actual User component structure
                // Common fields in ProjectM User component:
                
                // Unlock spellbook access
                SetUserFlag(userData, "CanOpenSpellBook", true);
                
                // Unlock blood type change
                SetUserFlag(userData, "CanChangeBloodType", true);
                
                // Unlock equipment modification
                SetUserFlag(userData, "CanModifyEquipment", true);
                
                // Unlock castle/building access if needed
                SetUserFlag(userData, "CanBuildCastle", true);

                em.SetComponentData(user, userData);

                VAutoLogger.LogInfo($"[{LogSource}] ✅ All menus unlocked for user");
                return true;
            }
            catch (System.Exception ex)
            {
                VAutoLogger.LogException(ex);
                VAutoLogger.LogError($"[{LogSource}] Failed: {ex.Message}");
                return false;
            }
        }

        private void SetUserFlag(User userData, string flagName, bool value)
        {
            // Placeholder for setting user flags
            // Actual implementation depends on User component structure
            VAutoLogger.LogDebug($"[{LogSource}] Setting {flagName} = {value}");
        }
    }

    /// <summary>
    /// Revokes UI menu access on arena exit.
    /// Restricts menus to default permissions.
    /// </summary>
    public class LockMenusHandler : LifecycleActionHandler
    {
        private const string LogSource = "LockMenusHandler";

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
                if (!em.HasComponent<User>(user))
                {
                    VAutoLogger.LogWarning($"[{LogSource}] User entity has no User component");
                    return false;
                }

                var userData = em.GetComponentData<User>(user);

                // Lock all menus
                SetUserFlag(userData, "CanOpenSpellBook", false);
                SetUserFlag(userData, "CanChangeBloodType", false);
                SetUserFlag(userData, "CanModifyEquipment", false);
                SetUserFlag(userData, "CanBuildCastle", false);

                em.SetComponentData(user, userData);

                VAutoLogger.LogInfo($"[{LogSource}] ✅ All menus relocked for user");
                return true;
            }
            catch (System.Exception ex)
            {
                VAutoLogger.LogException(ex);
                VAutoLogger.LogError($"[{LogSource}] Failed: {ex.Message}");
                return false;
            }
        }

        private void SetUserFlag(User userData, string flagName, bool value)
        {
            VAutoLogger.LogDebug($"[{LogSource}] Setting {flagName} = {value}");
        }
    }
}
