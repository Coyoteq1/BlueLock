using VampireCommandFramework;
using VAuto.Core;

namespace VAuto.Commands.Arena
{
    /// <summary>
    /// Arena entry and exit commands for VAutomationEvents
    /// </summary>
    public static class ArenaEnterExitCommands
    {
        [Command("arenaenter", shortHand: "ae", description: "Enter the arena", adminOnly: false)]
        public static void ArenaEnter(ChatCommandContext ctx)
        {
            Plugin.Log.LogInfo($"[Arena] User requesting arena entry");
            ctx.Reply("[Arena] Arena entry command executed.");
        }

        [Command("arenaexit", shortHand: "ax", description: "Exit the arena", adminOnly: false)]
        public static void ArenaExit(ChatCommandContext ctx)
        {
            Plugin.Log.LogInfo($"[Arena] User requesting arena exit");
            ctx.Reply("[Arena] Arena exit command executed.");
        }

        [Command("pvp", shortHand: "pvp", description: "Toggle PvP mode in arena", adminOnly: false)]
        public static void TogglePvP(ChatCommandContext ctx)
        {
            Plugin.Log.LogInfo($"[Arena] User toggling PvP mode");
            ctx.Reply("[Arena] PvP toggle command executed.");
        }
    }
}
