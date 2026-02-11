using System;
using System.Runtime.CompilerServices;
using BepInEx.Logging;

namespace VAutomationCore.Core.Logging
{
    /// <summary>
    /// Centralized logging service with caller context support.
    /// Provides structured logging methods for consistent output across all mods.
    /// </summary>
    public class CoreLogger
    {
        private readonly ManualLogSource _log;
        private readonly string _source;

        /// <summary>
        /// Creates a new logger with the specified source.
        /// </summary>
        /// <param name="source">The source name for log messages.</param>
        public CoreLogger(string source)
        {
            _log = Plugin.Log;
            _source = source;
        }

        /// <summary>
        /// Logs an informational message with caller context.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <param name="caller">The calling member name (auto-populated).</param>
        public void Info(string message, [CallerMemberName] string caller = null)
            => _log.LogInfo($"[{_source}][{caller}] {message}");

        /// <summary>
        /// Logs an error message with caller context.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <param name="caller">The calling member name (auto-populated).</param>
        public void Error(string message, [CallerMemberName] string caller = null)
            => _log.LogError($"[{_source}][{caller}] {message}");

        /// <summary>
        /// Logs a warning message with caller context.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <param name="caller">The calling member name (auto-populated).</param>
        public void Warning(string message, [CallerMemberName] string caller = null)
            => _log.LogWarning($"[{_source}][{caller}] {message}");

        /// <summary>
        /// Logs a debug message with caller context. Only outputs in DEBUG builds.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <param name="caller">The calling member name (auto-populated).</param>
        [System.Diagnostics.Conditional("DEBUG")]
        public void Debug(string message, [CallerMemberName] string caller = null)
            => _log.LogInfo($"[DEBUG][{_source}][{caller}] {message}");

        /// <summary>
        /// Logs an exception with full stack trace and caller context.
        /// </summary>
        /// <param name="ex">The exception to log.</param>
        /// <param name="caller">The calling member name (auto-populated).</param>
        public void Exception(Exception ex, [CallerMemberName] string caller = null)
        {
            var message = $"[{_source}][{caller}] Exception: {ex.Message}";
            if (ex.StackTrace != null)
            {
                message += $"\n{ex.StackTrace}";
            }
            _log.LogError(message);
        }

        /// <summary>
        /// Logs a formatted informational message.
        /// </summary>
        /// <param name="format">The format string.</param>
        /// <param name="args">The format arguments.</param>
        /// <param name="caller">The calling member name (auto-populated).</param>
        public void InfoFormat(string format, [CallerMemberName] string caller = null, params object[] args)
            => _log.LogInfo($"[{_source}][{caller}] {string.Format(format, args)}");

        /// <summary>
        /// Logs a formatted error message.
        /// </summary>
        /// <param name="format">The format string.</param>
        /// <param name="args">The format arguments.</param>
        /// <param name="caller">The calling member name (auto-populated).</param>
        public void ErrorFormat(string format, [CallerMemberName] string caller = null, params object[] args)
            => _log.LogError($"[{_source}][{caller}] {string.Format(format, args)}");

        #region Static Methods

        private static ManualLogSource? _staticLog;
        private static bool _isInitialized;
        
        /// <summary>
        /// Gets whether the CoreLogger has been initialized.
        /// </summary>
        public static bool IsInitialized => _isInitialized;
        
        /// <summary>
        /// Initialize the static logger with a log source.
        /// </summary>
        public static void Initialize(ManualLogSource logSource)
        {
            _staticLog = logSource ?? throw new ArgumentNullException(nameof(logSource));
            _isInitialized = true;
        }

        /// <summary>
        /// Logs an informational message statically.
        /// </summary>
        public static void LogInfo(string message, string source = "Core")
            => _staticLog.LogInfo($"[{source}] {message}");

        /// <summary>
        /// Logs a warning message statically.
        /// </summary>
        public static void LogWarning(string message, string source = "Core")
            => _staticLog.LogWarning($"[{source}] {message}");

        /// <summary>
        /// Logs an error message statically.
        /// </summary>
        public static void LogError(string message, string source = "Core")
            => _staticLog.LogError($"[{source}] {message}");

        /// <summary>
        /// Logs a debug message statically.
        /// </summary>
        [System.Diagnostics.Conditional("DEBUG")]
        public static void LogDebug(string message, string source = "Core")
            => _staticLog.LogInfo($"[DEBUG][{source}] {message}");

        /// <summary>
        /// Logs an exception statically.
        /// </summary>
        public static void LogException(Exception ex, string source = "Core")
            => _staticLog.LogError($"[{source}] Exception: {ex.Message}\n{ex.StackTrace}");

        #endregion
    }
}
