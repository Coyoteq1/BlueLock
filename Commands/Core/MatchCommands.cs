using VampireCommandFramework;
using Unity.Entities;
using VAuto.Zone.Core;
using VAuto.Zone.Services;

namespace VAuto.Zone.Commands
{
    [CommandGroup("match", "m")]
    public static class MatchCommands
    {
        private static EntityManager EntityManager => ZoneCore.EntityManager;

        [Command("start", shortHand: "s", description: "Start a match in a zone", adminOnly: true)]
        public static void StartMatch(ChatCommandContext ctx, string zoneId, int durationSeconds = 300)
        {
            var config = new MatchConfig { DurationSeconds = durationSeconds };
            var result = ArenaMatchManager.Instance.StartMatch(zoneId, config);
            if (result.Success)
            {
                ctx.Reply($"<color=#00FF00>Match started in '{zoneId}'.</color>");
            }
            else
            {
                ctx.Reply($"<color=#FF0000>Failed to start match: {result.Error}</color>");
            }
        }

        [Command("end", shortHand: "e", description: "End current match", adminOnly: true)]
        public static void EndMatch(ChatCommandContext ctx, string zoneId)
        {
            var result = ArenaMatchManager.Instance.EndMatch(zoneId, MatchEndReason.AdminEnded);
            if (result.Success)
            {
                ctx.Reply("<color=#00FF00>Match ended.</color>");
            }
            else
            {
                ctx.Reply($"<color=#FF0000>Failed to end match: {result.Error}</color>");
            }
        }

        [Command("reset", shortHand: "r", description: "Reset arena", adminOnly: true)]
        public static void ResetArena(ChatCommandContext ctx, string zoneId)
        {
            var result = ArenaMatchManager.Instance.ResetArena(zoneId, EntityManager);
            var status = result.Success ? "<color=#00FF00>success</color>" : "<color=#FF0000>failed</color>";
            ctx.Reply($"<color=#00FFFF>Reset arena '{zoneId}': {status}. Cleared {result.EntitiesCleared}, spawned {result.EntitiesSpawned}.</color>");
            if (!result.Success && !string.IsNullOrWhiteSpace(result.Error))
            {
                ctx.Reply($"<color=#FF0000>Error: {result.Error}</color>");
            }
        }
    }
}
