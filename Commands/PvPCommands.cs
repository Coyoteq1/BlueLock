using VampireCommandFramework;
using ProjectM;
using Unity.Entities;

namespace VAuto.Commands.Core
{
    [CommandGroup("pvp", "PvP management commands")]
    public class PvPCommands
    {
        [Command("enable", "Enable PvP", adminOnly: true)]
        public void EnablePvPCommand(ChatContext ctx)
        {
            ctx.Reply(Plugin.Log, "[PvP] PvP command system - placeholder");
        }
    }
}
