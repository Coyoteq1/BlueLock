using System;
using System.Runtime.CompilerServices;
using BepInEx.Logging;

namespace VAutomation.Core
{
    /// <summary>
    /// Centralized logging for VAuto framework.
    /// </summary>
    public class VAutoLogger
    {
        private readonly ManualLogSource _logger;
        private readonly string _prefix;

        public VAutoLogger(string prefix)
        {
            _prefix = prefix;
            _logger = BepInEx.Logging.Logger.CreateLogSource(prefix);
        }

        public void LogInfo(string message, [CallerMemberName] string memberName = null)
        {
            _logger.LogInfo($"[{_prefix}] [{memberName}] {message}");
        }

        public void LogWarning(string message, [CallerMemberName] string memberName = null)
        {
            _logger.LogWarning($"[{_prefix}] [{memberName}] {message}");
        }

        public void LogError(string message, [CallerMemberName] string memberName = null)
        {
            _logger.LogError($"[{_prefix}] [{memberName}] {message}");
        }

        public void LogDebug(string message, [CallerMemberName] string memberName = null)
        {
#if DEBUG
            _logger.LogDebug($"[{_prefix}] [{memberName}] {message}");
#endif
        }
    }
}
