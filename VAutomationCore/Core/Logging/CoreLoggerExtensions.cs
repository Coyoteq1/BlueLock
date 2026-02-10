using BepInEx.Logging;

namespace VAutomationCore.Core.Logging
{
    /// <summary>
    /// Extension methods for CoreLogger providing ForContext functionality.
    /// </summary>
    public static class CoreLoggerExtensions
    {
        /// <summary>
        /// Creates a logger with a context prefix.
        /// </summary>
        public static CoreLogger ForContext(string context)
        {
            return new CoreLogger(context);
        }
    }
}
