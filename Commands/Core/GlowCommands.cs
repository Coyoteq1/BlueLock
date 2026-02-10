using VampireCommandFramework;
using VAutomationCore.Core.Commands;
using VAutomationCore.Core.Logging;

namespace VAuto.Commands.Core
{
    /// <summary>
    /// Glow zone management commands for VAutomationEvents
    /// Uses CommandBase for VCF integration, logging, and feedback.
    /// </summary>
    public static class GlowCommands : CommandBase
    {
        private const string CommandName = "glow";
        
        [Command("glowlist", shortHand: "gl", description: "List active glow zones", adminOnly: false)]
        public static void GlowList(ChatCommandContext ctx)
        {
            ExecuteSafely(ctx, "glowlist", () =>
            {
                // Permission check - anyone can view
                RequirePermission(ctx, PermissionLevel.Anyone);
                
                // TODO: Integrate with ZoneGlowBorderService.Status() when available
                Log.Info($"Glow zone list requested by {GetPlayerInfo(ctx).Name}");
                
                SendInfo(ctx, "Glow zone list command executed.");
                SendInfo(ctx, "Use .glowinfo <zoneName> for details on a specific zone.");
            });
        }

        [Command("glowinfo", shortHand: "gi", description: "Show info about a glow zone", adminOnly: false)]
        public static void GlowInfo(ChatCommandContext ctx, string zoneName)
        {
            ExecuteSafely(ctx, "glowinfo", () =>
            {
                RequirePermission(ctx, PermissionLevel.Anyone);
                
                // TODO: Integrate with ZoneGlowBorderService for actual zone info
                Log.Info($"Glow zone info requested for: {zoneName} by {GetPlayerInfo(ctx).Name}");
                
                SendInfo(ctx, $"Glow zone '{zoneName}' info command executed.");
                SendInfo(ctx, $"Use .zone_glow_status for active glow borders.");
            });
        }
    }
}
