using VampireCommandFramework;
using ProjectM;
using Unity.Entities;

namespace VAuto.Commands.Core
{
    [CommandGroup("portal", "Portal management commands")]
    public class PortalCommands
    {
        [Command("create", "Create portal", adminOnly: true)]
        public void CreatePortalCommand(ChatContext ctx)
        {
            ctx.Reply(Plugin.Log, "[Portal] Portal command system - placeholder");
        }
    }
}
