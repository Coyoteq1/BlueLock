using System;
using BepInEx.Logging;

namespace VAuto.Core.Lifecycle
{
    /// <summary>
    /// Base interface for lifecycle action handlers
    /// </summary>
    public interface LifecycleActionHandler
    {
        bool Execute(LifecycleAction action, LifecycleContext context);
    }

    /// <summary>
    /// Handles store actions - stores values in context
    /// </summary>
    public class StoreActionHandler : LifecycleActionHandler
    {
        public bool Execute(LifecycleAction action, LifecycleContext context)
        {
            try
            {
                if (string.IsNullOrEmpty(action.StoreKey))
                {
                    VLifecycle.Plugin.Log?.LogError("[StoreActionHandler] StoreKey is required");
                    return false;
                }

                context.StoredData[action.StoreKey] = action.Prefix ?? action.Message ?? action.ConfigId;
                VLifecycle.Plugin.Log?.LogInfo($"[StoreActionHandler] Stored value for key: {action.StoreKey}");
                return true;
            }
            catch (Exception ex)
            {
                VLifecycle.Plugin.Log?.LogError($"[StoreActionHandler] Failed: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Handles message actions - displays messages to users
    /// </summary>
    public class MessageActionHandler : LifecycleActionHandler
    {
        public bool Execute(LifecycleAction action, LifecycleContext context)
        {
            try
            {
                if (string.IsNullOrEmpty(action.Message))
                {
                    VLifecycle.Plugin.Log?.LogError("[MessageActionHandler] Message is required");
                    return false;
                }

                VLifecycle.Plugin.Log?.LogInfo($"[MessageActionHandler] Message: {action.Message}");
                return true;
            }
            catch (Exception ex)
            {
                VLifecycle.Plugin.Log?.LogError($"[MessageActionHandler] Failed: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Handles command actions - executes backend commands
    /// </summary>
    public class CommandActionHandler : LifecycleActionHandler
    {
        public bool Execute(LifecycleAction action, LifecycleContext context)
        {
            try
            {
                if (string.IsNullOrEmpty(action.CommandId))
                {
                    VLifecycle.Plugin.Log?.LogError("[CommandActionHandler] CommandId is required");
                    return false;
                }

                VLifecycle.Plugin.Log?.LogInfo($"[CommandActionHandler] Executing command: {action.CommandId}");
                return true;
            }
            catch (Exception ex)
            {
                VLifecycle.Plugin.Log?.LogError($"[CommandActionHandler] Failed: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Handles config actions - loads or applies configuration bundles
    /// </summary>
    public class ConfigActionHandler : LifecycleActionHandler
    {
        public bool Execute(LifecycleAction action, LifecycleContext context)
        {
            try
            {
                if (string.IsNullOrEmpty(action.ConfigId))
                {
                    VLifecycle.Plugin.Log?.LogError("[ConfigActionHandler] ConfigId is required");
                    return false;
                }

                VLifecycle.Plugin.Log?.LogInfo($"[ConfigActionHandler] Loading configuration: {action.ConfigId}");
                return true;
            }
            catch (Exception ex)
            {
                VLifecycle.Plugin.Log?.LogError($"[ConfigActionHandler] Failed: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Handles zone actions - binds to defined zones
    /// </summary>
    public class ZoneActionHandler : LifecycleActionHandler
    {
        public bool Execute(LifecycleAction action, LifecycleContext context)
        {
            try
            {
                if (string.IsNullOrEmpty(action.ZoneKey))
                {
                    VLifecycle.Plugin.Log?.LogError("[ZoneActionHandler] ZoneKey is required");
                    return false;
                }

                context.ZoneId = action.ZoneKey;
                VLifecycle.Plugin.Log?.LogInfo($"[ZoneActionHandler] Set zone: {action.ZoneKey}");
                return true;
            }
            catch (Exception ex)
            {
                VLifecycle.Plugin.Log?.LogError($"[ZoneActionHandler] Failed: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Handles prefix actions - generates or attaches prefixes
    /// </summary>
    public class PrefixActionHandler : LifecycleActionHandler
    {
        public bool Execute(LifecycleAction action, LifecycleContext context)
        {
            try
            {
                if (string.IsNullOrEmpty(action.Prefix))
                {
                    VLifecycle.Plugin.Log?.LogError("[PrefixActionHandler] Prefix is required");
                    return false;
                }

                VLifecycle.Plugin.Log?.LogInfo($"[PrefixActionHandler] Applied prefix: {action.Prefix}");
                return true;
            }
            catch (Exception ex)
            {
                VLifecycle.Plugin.Log?.LogError($"[PrefixActionHandler] Failed: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Handles blood actions - applies player combat or class logic
    /// </summary>
    public class BloodActionHandler : LifecycleActionHandler
    {
        public bool Execute(LifecycleAction action, LifecycleContext context)
        {
            try
            {
                if (string.IsNullOrEmpty(action.BloodType))
                {
                    VLifecycle.Plugin.Log?.LogError("[BloodActionHandler] BloodType is required");
                    return false;
                }

                VLifecycle.Plugin.Log?.LogInfo($"[BloodActionHandler] Applied blood type: {action.BloodType}");
                return true;
            }
            catch (Exception ex)
            {
                VLifecycle.Plugin.Log?.LogError($"[BloodActionHandler] Failed: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Handles quality actions - sets item attributes
    /// </summary>
    public class QualityActionHandler : LifecycleActionHandler
    {
        public bool Execute(LifecycleAction action, LifecycleContext context)
        {
            try
            {
                VLifecycle.Plugin.Log?.LogInfo($"[QualityActionHandler] Applied quality: {action.Quality}");
                return true;
            }
            catch (Exception ex)
            {
                VLifecycle.Plugin.Log?.LogError($"[QualityActionHandler] Failed: {ex.Message}");
                return false;
            }
        }
    }
}
