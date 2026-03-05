using System;
using VampireCommandFramework;

namespace Blueluck.Commands
{
    public static class SnapshotCommands
    {
        [Command("snap status", description: "Show snapshot status", adminOnly: true)]
        public static void Status(ChatCommandContext ctx)
        {
            if (Plugin.Progress?.IsInitialized != true)
            {
                ctx.Error("Progress service not initialized.");
                return;
            }

            var player = ctx.Event?.SenderCharacterEntity ?? Unity.Entities.Entity.Null;
            if (player == Unity.Entities.Entity.Null)
            {
                ctx.Error("No player character entity.");
                return;
            }

            if (!Plugin.Progress.TryGetSavedProgress(player, out var snap))
            {
                ctx.Reply("No snapshot saved.");
                return;
            }

            ctx.Reply($"Snapshot saved at {snap.Timestamp:o} buffs={snap.BuffPrefabHashes?.Count ?? 0}");
        }

        [Command("snap save", description: "Save a snapshot of progression + buffs", adminOnly: true)]
        public static void Save(ChatCommandContext ctx)
        {
            if (Plugin.Progress?.IsInitialized != true)
            {
                ctx.Error("Progress service not initialized.");
                return;
            }

            var player = ctx.Event?.SenderCharacterEntity ?? Unity.Entities.Entity.Null;
            if (player == Unity.Entities.Entity.Null)
            {
                ctx.Error("No player character entity.");
                return;
            }

            Plugin.Progress.SaveProgress(player);
            ctx.Reply("Snapshot saved.");
        }

        [Command("snap apply", description: "Apply snapshot (does not remove extra buffs)", adminOnly: true)]
        public static void Apply(ChatCommandContext ctx)
        {
            if (Plugin.Progress?.IsInitialized != true)
            {
                ctx.Error("Progress service not initialized.");
                return;
            }

            var player = ctx.Event?.SenderCharacterEntity ?? Unity.Entities.Entity.Null;
            if (player == Unity.Entities.Entity.Null)
            {
                ctx.Error("No player character entity.");
                return;
            }

            if (!Plugin.Progress.HasSavedProgress(player))
            {
                ctx.Error("No snapshot saved.");
                return;
            }

            Plugin.Progress.ApplyProgress(player);
            ctx.Reply("Snapshot applied.");
        }

        [Command("snap restore", description: "Restore snapshot exactly (removes extra buffs)", adminOnly: true)]
        public static void Restore(ChatCommandContext ctx)
        {
            if (Plugin.Progress?.IsInitialized != true)
            {
                ctx.Error("Progress service not initialized.");
                return;
            }

            var player = ctx.Event?.SenderCharacterEntity ?? Unity.Entities.Entity.Null;
            if (player == Unity.Entities.Entity.Null)
            {
                ctx.Error("No player character entity.");
                return;
            }

            if (!Plugin.Progress.HasSavedProgress(player))
            {
                ctx.Error("No snapshot saved.");
                return;
            }

            Plugin.Progress.RestoreProgress(player, clearAfter: false);
            ctx.Reply("Snapshot restored (kept).");
        }

        [Command("snap clear", description: "Clear saved snapshot", adminOnly: true)]
        public static void Clear(ChatCommandContext ctx)
        {
            if (Plugin.Progress?.IsInitialized != true)
            {
                ctx.Error("Progress service not initialized.");
                return;
            }

            var player = ctx.Event?.SenderCharacterEntity ?? Unity.Entities.Entity.Null;
            if (player == Unity.Entities.Entity.Null)
            {
                ctx.Error("No player character entity.");
                return;
            }

            Plugin.Progress.ClearSavedProgress(player);
            ctx.Reply("Snapshot cleared.");
        }
    }
}

