using System;
using VampireCommandFramework;

namespace VAutomationCore.Core.Extensions
{
    /// <summary>
    /// Extension methods for ChatCommandContext to provide consistent reply methods.
    /// </summary>
    public static class ChatCommandContextExtensions
    {
        /// <summary>
        /// Send a success message to the player.
        /// </summary>
        public static void ReplySuccess(this ChatCommandContext ctx, string message)
        {
            ctx.Reply($"[SUCCESS] {message}");
        }

        /// <summary>
        /// Send an error message to the player.
        /// </summary>
        public static void ReplyError(this ChatCommandContext ctx, string message)
        {
            ctx.Reply($"[ERROR] {message}");
        }

        /// <summary>
        /// Send an info message to the player.
        /// </summary>
        public static void ReplyInfo(this ChatCommandContext ctx, string message)
        {
            ctx.Reply($"[INFO] {message}");
        }

        /// <summary>
        /// Send a warning message to the player.
        /// </summary>
        public static void ReplyWarning(this ChatCommandContext ctx, string message)
        {
            ctx.Reply($"[WARNING] {message}");
        }
    }
}