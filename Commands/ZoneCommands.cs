using VampireCommandFramework;
using ProjectM;
using Unity.Entities;

namespace VAuto.Commands.Core
{
    [CommandGroup("zone", "Zone management commands")]
    public class ZoneCommands
    {
        [Command("create", "Create zone", adminOnly: true)]
        public void CreateZoneCommand(ChatContext ctx)
        {
            ctx.Reply(Plugin.Log, "[Zone] Zone command system - placeholder");
        }
    }
}
