using System;
using System.IO;
using System.Text.Json;
using BepInEx;
using Unity.Mathematics;
using VampireCommandFramework;
using VAuto.Zone.Models;
using VAuto.Zone.Services;

namespace VAuto.Zone.Commands.Zone
{
    [CommandGroup("zone", "Zone utilities")]
    internal sealed class ZoneSetCommands
    {
        [Command("set here", shortHand: "zsh", description: "Create/update a glow zone at your current position: .zone set here <id> <radius> <glow>", adminOnly: true)]
        public void SetHere(ChatCommandContext ctx, string id, float radius = 25f, bool glow = true)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                ctx.Reply("[Zone] Error: id is required.");
                return;
            }

            if (radius <= 0)
            {
                ctx.Reply("[Zone] Error: radius must be > 0.");
                return;
            }

            var character = ctx.Event?.SenderCharacterEntity ?? Unity.Entities.Entity.Null;
            if (character == Unity.Entities.Entity.Null)
            {
                ctx.Reply("[Zone] Error: could not resolve your character entity.");
                return;
            }

            if (!ArenaPlayerService.TryGetCharacterPosition(character, out var position))
            {
                ctx.Reply("[Zone] Error: could not read your position.");
                return;
            }

            var cfgPath = GetGlowZonesConfigPath();
            if (!TryLoad(cfgPath, out var cfg, out var loadErr))
            {
                ctx.Reply($"[Zone] Error: failed to load config: {loadErr}");
                return;
            }

            var entry = cfg.Zones.Find(z => string.Equals(z.Id, id, StringComparison.OrdinalIgnoreCase));
            if (entry == null)
            {
                entry = new GlowZoneEntry { Id = id };
                cfg.Zones.Add(entry);
            }

            entry.Center = position;
            entry.Radius = radius;
            entry.HalfExtents = null;
            entry.Enabled = glow;

            if (glow)
            {
                if (entry.GlowPrefabs == null)
                    entry.GlowPrefabs = new System.Collections.Generic.List<string>();

                if (entry.GlowPrefabs.Count == 0 && !string.IsNullOrWhiteSpace(ArenaTerritory.GlowPrefab))
                    entry.GlowPrefabs.Add(ArenaTerritory.GlowPrefab);
            }

            if (!TrySave(cfgPath, cfg, out var saveErr))
            {
                ctx.Reply($"[Zone] Error: failed to save config: {saveErr}");
                return;
            }

            ZoneGlowBorderService.BuildAll(rebuild: true);
            ctx.Reply($"[Zone] Set '{id}' at {Fmt(position)} radius={radius:F1} glow={(glow ? "on" : "off")}");
            ctx.Reply($"[Zone] Updated: {cfgPath}");
        }

        private static string GetGlowZonesConfigPath()
        {
            var configDir = Path.Combine(Paths.ConfigPath, "VAuto.Arena");
            Directory.CreateDirectory(configDir);
            return Path.Combine(configDir, "glow_zones.json");
        }

        private static bool TryLoad(string path, out GlowZonesConfig cfg, out string error)
        {
            cfg = new GlowZonesConfig();
            error = string.Empty;

            try
            {
                if (!File.Exists(path))
                    return true;

                var json = File.ReadAllText(path);
                cfg = JsonSerializer.Deserialize<GlowZonesConfig>(json, JsonOptions()) ?? new GlowZonesConfig();
                cfg.Zones ??= new System.Collections.Generic.List<GlowZoneEntry>();
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                cfg = new GlowZonesConfig();
                return false;
            }
        }

        private static bool TrySave(string path, GlowZonesConfig cfg, out string error)
        {
            error = string.Empty;
            try
            {
                var json = JsonSerializer.Serialize(cfg, new JsonSerializerOptions(JsonOptions()) { WriteIndented = true });
                var tmp = path + ".tmp";
                File.WriteAllText(tmp, json);
                File.Copy(tmp, path, overwrite: true);
                File.Delete(tmp);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static JsonSerializerOptions JsonOptions()
        {
            return new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };
        }

        private static string Fmt(float3 v) => $"({v.x:F1},{v.y:F1},{v.z:F1})";
    }
}
