using VampireCommandFramework;
using ProjectM;
using Unity.Entities;

namespace VAuto.Commands.Core
{
    [CommandGroup("arena", "Arena management commands")]
    public class ArenaCommands
    {
        [Command("create", "Create arena", adminOnly: true)]
        public void CreateArenaCommand(ICommandContext ctx)
        {
            ctx.Reply(Plugin.Log, "[Arena] Arena command system - placeholder");
        }
    }
}

