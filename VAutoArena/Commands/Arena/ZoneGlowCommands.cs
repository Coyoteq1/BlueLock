using VampireCommandFramework;
using VAuto.Arena.Services;

namespace VAuto.Arena.Commands.Arena
{
    [CommandGroup("arena.zoneglow")]
    internal class ZoneGlowCommands
    {
        [Command("build", description: "Build glows for all zones")]
        public void Build(ChatCommandContext ctx)
        {
            ZoneGlowBorderService.BuildAll(rebuild: true);
            ctx.Reply("[ZoneGlow] Build requested.");
        }

        [Command("clear", description: "Clear all zone glows")]
        public void Clear(ChatCommandContext ctx)
        {
            ZoneGlowBorderService.ClearAll();
            ctx.Reply("[ZoneGlow] Cleared.");
        }

        [Command("rotate", description: "Force rotate glows now")]
        public void Rotate(ChatCommandContext ctx)
        {
            ZoneGlowBorderService.RotateAll();
            ctx.Reply("[ZoneGlow] Rotation triggered.");
        }

        [Command("status", description: "Show zone glow status")]
        public void Status(ChatCommandContext ctx)
        {
            foreach (var line in ZoneGlowBorderService.Status())
            {
                ctx.Reply(line);
            }
        }
    }
}
