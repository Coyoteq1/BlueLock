using VampireCommandFramework;
using VAuto.Core;

namespace VAuto.Commands.Core
{
    /// <summary>
    /// Glow zone management commands for VAutomationEvents
    /// </summary>
    public static class GlowCommands
    {
        [Command("glowlist", shortHand: "gl", description: "List active glow zones", adminOnly: false)]
        public static void GlowList(ChatCommandContext ctx)
        {
            Plugin.Log.LogInfo($"[Glow] User requesting glow zone list");
            ctx.Reply("[Glow] Glow zone list command executed.");
        }

        [Command("glowinfo", shortHand: "gi", description: "Show info about a glow zone", adminOnly: false)]
        public static void GlowInfo(ChatCommandContext ctx, string zoneName)
        {
            Plugin.Log.LogInfo($"[Glow] User requesting info for: {zoneName}");
            ctx.Reply($"[Glow] Glow zone '{zoneName}' info command executed.");
        }
    }
}
