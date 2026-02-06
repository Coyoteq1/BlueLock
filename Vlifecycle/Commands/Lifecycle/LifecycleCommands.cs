using System.Linq;
using VampireCommandFramework;
using VAuto.Core.Lifecycle;

namespace VAuto.Commands.Lifecycle
{
    public static class LifecycleCommands
    {
        [Command("lifecycle help", shortHand: "lh", description: "Show lifecycle commands", adminOnly: false)]
        public static void Help(ChatCommandContext ctx)
        {
            ctx.Reply("[Lifecycle] Commands:");
            ctx.Reply("  .lifecycle status - Show lifecycle status");
            ctx.Reply("  .lifecycle debug <true/false> - Toggle debug");
            ctx.Reply("  .lifecycle test - Run basic tests");
        }

        [Command("lifecycle status", shortHand: "ls", description: "Show lifecycle status", adminOnly: false)]
        public static void Status(ChatCommandContext ctx)
        {
            ctx.Reply($"[Lifecycle] Services registered: {ArenaLifecycleManager.Instance.ServiceCount}");
            foreach (var name in ArenaLifecycleManager.Instance.GetServiceNames())
            {
                ctx.Reply($"  - {name}");
            }
            ctx.Reply($"[Lifecycle] ServiceManager count: {ServiceManager.Instance.ServiceCount}");
            foreach (var name in ServiceManager.Instance.GetServiceNames())
            {
                ctx.Reply($"  - {name}");
            }
            ctx.Reply($"[Lifecycle] Debug: {(LifecycleDebug.Enabled ? "ON" : "OFF")} (verbose={(LifecycleDebug.Verbose ? "ON" : "OFF")})");
        }

        [Command("lifecycle debug", shortHand: "ld", description: "Toggle lifecycle debug", adminOnly: true)]
        public static void Debug(ChatCommandContext ctx, bool enabled = true, bool verbose = false)
        {
            LifecycleDebug.Set(enabled, verbose);
            ctx.Reply($"[Lifecycle] Debug {(enabled ? "enabled" : "disabled")} (verbose={(verbose ? "enabled" : "disabled")})");
        }

        [Command("lifecycle test", shortHand: "lt", description: "Run basic lifecycle tests", adminOnly: true)]
        public static void Test(ChatCommandContext ctx)
        {
            ctx.Reply("[Lifecycle] Running basic tests");
            ctx.Reply($"[Lifecycle] Service count: {ArenaLifecycleManager.Instance.ServiceCount}");
            ctx.Reply("[Lifecycle] Use .arena enter/.arena exit for full flow tests");
        }
    }
}
