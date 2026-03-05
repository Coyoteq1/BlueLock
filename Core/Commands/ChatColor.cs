namespace VAutomationCore.Core.Commands
{
    /// <summary>
    /// Extension methods for ChatCommandContext to provide convenient reply methods.
    /// </summary>
    public static class ChatCommandContextExtensions
    {
        /// <summary>
        /// Reply with a warning message (yellow).
        /// </summary>
        public static void Warning(this VampireCommandFramework.ChatCommandContext ctx, string message)
        {
            ctx.Reply($"<color={ChatColor.Yellow}>{message}</color>");
        }

        /// <summary>
        /// Reply with an error message (red).
        /// </summary>
        public static void Error(this VampireCommandFramework.ChatCommandContext ctx, string message)
        {
            ctx.Reply($"<color={ChatColor.Red}>{message}</color>");
        }
    }
}

namespace VAutomationCore.Core.Commands
{
    /// <summary>
    /// Defines standard chat colors for use in command responses.
    /// </summary>
    public static class ChatColor
    {
        /// <summary>
        /// White color (default).
        /// </summary>
        public const string White = "white";
        
        /// <summary>
        /// Green color for success messages.
        /// </summary>
        public const string Green = "#00FF00";
        
        /// <summary>
        /// Red color for error messages.
        /// </summary>
        public const string Red = "#FF0000";
        
        /// <summary>
        /// Yellow color for warning messages.
        /// </summary>
        public const string Yellow = "#FFFF00";
        
        /// <summary>
        /// Cyan color for info messages.
        /// </summary>
        public const string Cyan = "#00FFFF";
        
        /// <summary>
        /// Magenta color.
        /// </summary>
        public const string Magenta = "#FF00FF";
        
        /// <summary>
        /// Orange/Amber color.
        /// </summary>
        public const string Orange = "#FFA500";
        
        /// <summary>
        /// Light gray color for debug messages.
        /// </summary>
        public const string Gray = "#CCCCCC";
        
        /// <summary>
        /// Dark gray color.
        /// </summary>
        public const string DarkGray = "#888888";
        
        /// <summary>
        /// Light blue color.
        /// </summary>
        public const string LightBlue = "#ADD8E6";
        
        /// <summary>
        /// Pink color.
        /// </summary>
        public const string Pink = "#FFC0CB";
        
        /// <summary>
        /// Lime green color.
        /// </summary>
        public const string Lime = "#32CD32";
        
        /// <summary>
        /// Gold color.
        /// </summary>
        public const string Gold = "#FFD700";
    }
}
