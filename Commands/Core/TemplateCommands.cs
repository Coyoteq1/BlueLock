using System;
using System.Linq;
using Unity.Entities;
using VampireCommandFramework;
using VAuto.Zone.Core;
using VAuto.Zone.Services;

namespace VAuto.Zone.Commands
{
    [CommandGroup("template", "tm")]
    public static class TemplateCommands
    {
        private static EntityManager EntityManager => ZoneCore.EntityManager;

        [Command("list", shortHand: "l", description: "List templates for a zone", adminOnly: true)]
        public static void ListTemplates(ChatCommandContext ctx, string zoneId)
        {
            var zone = ZoneConfigService.GetZoneById(zoneId);
            if (zone == null)
            {
                ctx.Reply($"<color=#FF0000>Error: Zone '{zoneId}' not found.</color>");
                return;
            }

            ctx.Reply($"<color=#FFD700>[Zone '{zoneId}' Templates]</color>");
            if (zone.Templates == null || zone.Templates.Count == 0)
            {
                ctx.Reply("<color=#808080>No templates configured.</color>");
                return;
            }

            var stats = ZoneTemplateService.GetZoneTemplateStats(zoneId);
            foreach (var kvp in zone.Templates)
            {
                var count = stats.TryGetValue(kvp.Key, out var saved) ? saved : 0;
                var spawned = ZoneTemplateService.IsTemplateSpawned(zoneId, kvp.Key);
                var status = spawned ? $"<color=#00FF00>spawned ({count} entities)</color>" : "<color=#FF0000>not spawned</color>";
                ctx.Reply($"  {kvp.Key}: {kvp.Value} {status}");
            }
        }

        [Command("spawn", shortHand: "s", description: "Spawn a template type for a zone", adminOnly: true)]
        public static void SpawnTemplate(ChatCommandContext ctx, string zoneId, string templateType)
        {
            var result = ZoneTemplateService.SpawnZoneTemplateType(zoneId, templateType, EntityManager);
            if (result.Success)
            {
                ctx.Reply($"<color=#00FF00>Spawned {result.EntityCount} entities for '{templateType}' in '{zoneId}'.</color>");
            }
            else
            {
                ctx.Reply($"<color=#FF0000>Failed to spawn '{templateType}': {result.Error}</color>");
            }
        }

        [Command("spawnall", shortHand: "sa", description: "Spawn all templates for a zone", adminOnly: true)]
        public static void SpawnAllTemplates(ChatCommandContext ctx, string zoneId)
        {
            var results = ZoneTemplateService.SpawnAllZoneTemplates(zoneId, EntityManager);
            var total = results.Sum(r => r.EntityCount);
            ctx.Reply($"<color=#00FF00>Spawned {total} entities across {results.Count} template types for '{zoneId}'.</color>");
        }

        [Command("clear", shortHand: "c", description: "Clear a template type from a zone", adminOnly: true)]
        public static void ClearTemplate(ChatCommandContext ctx, string zoneId, string templateType)
        {
            var destroyed = ZoneTemplateService.ClearZoneTemplateType(zoneId, templateType, EntityManager);
            ctx.Reply($"<color=#FFD700>Cleared {destroyed} entities for template '{templateType}' in '{zoneId}'.</color>");
        }

        [Command("clearall", shortHand: "ca", description: "Clear all templates from a zone", adminOnly: true)]
        public static void ClearAllTemplates(ChatCommandContext ctx, string zoneId)
        {
            var total = ZoneTemplateService.ClearAllZoneTemplates(zoneId, EntityManager);
            ctx.Reply($"<color=#FFD700>Cleared {total} template entities in '{zoneId}'.</color>");
        }

        [Command("rebuild", shortHand: "rb", description: "Rebuild all templates for a zone", adminOnly: true)]
        public static void RebuildTemplates(ChatCommandContext ctx, string zoneId)
        {
            var results = ZoneTemplateService.RebuildZoneTemplates(zoneId, EntityManager);
            var total = results.Sum(r => r.EntityCount);
            ctx.Reply($"<color=#00FF00>Rebuilt zone '{zoneId}' with {total} spawned entities.</color>");
        }

        [Command("status", shortHand: "st", description: "Show template status for a zone", adminOnly: true)]
        public static void TemplateStatus(ChatCommandContext ctx, string zoneId)
        {
            var stats = ZoneTemplateService.GetZoneTemplateStats(zoneId);
            var total = stats.Values.Sum();
            ctx.Reply($"<color=#00FFFF>Zone '{zoneId}' template status:</color>");
            ctx.Reply($"  Total entities: {total}");
            foreach (var kvp in stats)
            {
                ctx.Reply($"  {kvp.Key}: {kvp.Value} entities");
            }

            var glowCount = GlowTileService.GetGlowTileCount(zoneId);
            var glowState = GlowTileService.IsGlowTilesSpawned(zoneId) ? "<color=#00FF00>spawned</color>" : "<color=#FF0000>not spawned</color>";
            ctx.Reply($"  Glow tiles: {glowCount} ({glowState})");
        }

        [Command("glow", shortHand: "g", description: "Manage glow tiles for a zone", adminOnly: true)]
        public static void GlowCommand(ChatCommandContext ctx, string action, string zoneId)
        {
            if (string.IsNullOrWhiteSpace(action) || string.IsNullOrWhiteSpace(zoneId))
            {
                ctx.Reply("<color=#FF0000>Error: Usage: .tm glow <spawn|clear|status> <zoneId></color>");
                return;
            }

            switch (action.Trim().ToLowerInvariant())
            {
                case "spawn":
                {
                    var spawn = GlowTileService.SpawnGlowTiles(zoneId, EntityManager);
                    if (spawn.Success)
                    {
                        ctx.Reply($"<color=#00FF00>Spawned {spawn.EntityCount} glow tiles in '{zoneId}'.</color>");
                    }
                    else
                    {
                        ctx.Reply($"<color=#FF0000>Glow spawn failed: {spawn.Error}</color>");
                    }

                    break;
                }

                case "clear":
                {
                    var destroyed = GlowTileService.ClearGlowTiles(zoneId, EntityManager);
                    ctx.Reply($"<color=#FFD700>Cleared {destroyed} glow tiles in '{zoneId}'.</color>");
                    break;
                }

                case "status":
                    var spawnedGlow = GlowTileService.GetGlowTileCount(zoneId);
                    var metadata = ZoneTemplateRegistry.GetMetadata(zoneId, "glowTM");
                    var when = metadata?.SpawnedAt.ToString("u") ?? "N/A";
                    ctx.Reply($"<color=#00FFFF>Glow tiles in '{zoneId}': {spawnedGlow} ({when})</color>");
                    break;

                default:
                    ctx.Reply("<color=#FF0000>Error: Unknown glow action. Use spawn, clear, or status.</color>");
                    break;
            }
        }
    }
}
