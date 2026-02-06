using System.IO;
using System.Reflection;
using System.Text.Json;
using System;
using BepInEx;
using Unity.Entities;
using Unity.Mathematics;
using VampireCommandFramework;
using VAuto.Arena.Services;
using VAuto.Core;

namespace VAuto.Commands.Arena
{
    /// <summary>
    /// Unified arena commands with debug and test helpers.
    /// </summary>
    public static class ArenaCommands
    {
        [Command("arena", shortHand: "arena", description: "Arena command hub", adminOnly: false)]
        public static void ArenaHelp(ChatCommandContext ctx)
        {
            ctx.Reply("[Arena] Commands:");
            ctx.Reply("  .arena status - Show arena status");
            ctx.Reply("  .arena debug <true/false> - Toggle debug logs");
            ctx.Reply("  .arena test - Diagnostics for arena enter/exit + lifecycle");
            ctx.Reply("  .arena set <radius> - Set arena center at your position");
            ctx.Reply("  .arena setentry <radius> - Set entry point at your position");
            ctx.Reply("  .arena setexit <radius> - Set exit point at your position");
            ctx.Reply("  .arena setspawn - Set spawn point at your position");
            ctx.Reply("  .arena enter / .ae - Enter arena");
            ctx.Reply("  .arena exit / .ax - Exit arena");
            ctx.Reply("  .arena tp - Teleport to spawn point");
            ctx.Reply("  .arena preset default - Apply default preset settings");
            ctx.Reply("  .arena glow validate - Validate glow config");
            ctx.Reply("  .arena glow test <spacing> - Test glow point count");
            ctx.Reply("  .arena glow spawn <spacing> [prefabName] - Spawn glow border");
            ctx.Reply("  .arena glow clear - Clear glow border");
            ctx.Reply("  .arena territory show - Show territory settings");
            ctx.Reply("  .arena territory reload - Reload territory config");
        }

        [Command("arena status", shortHand: "as", description: "Show arena status", adminOnly: false)]
        public static void ArenaStatus(ChatCommandContext ctx)
        {
            ctx.Reply($"[Arena] Players in arena: {ArenaPlayerService.PlayerCount}");
            ctx.Reply($"[Arena] Zone center: {ArenaPlayerService.ArenaCenter} | Radius: {ArenaPlayerService.ArenaRadius:F1}");
            ctx.Reply($"[Arena] Entry: {ArenaPlayerService.EntryPoint} | Radius: {ArenaPlayerService.EntryRadius:F1}");
            ctx.Reply($"[Arena] Exit: {ArenaPlayerService.ExitPoint} | Radius: {ArenaPlayerService.ExitRadius:F1}");
            ctx.Reply($"[Arena] Spawn: {ArenaPlayerService.SpawnPoint}");
            ctx.Reply($"[Arena] Territory center: {ArenaTerritory.ArenaGridCenter} | Radius: {ArenaTerritory.ArenaGridRadius:F1} | Block: {ArenaTerritory.BlockSize:F1}");
            ctx.Reply($"[Arena] Glow border: {(ArenaTerritory.EnableGlowBorder ? "ENABLED" : "DISABLED")} | Prefab: {ArenaTerritory.GlowPrefab} | Spacing: {ArenaTerritory.GlowSpacingMeters:F1}");
            ctx.Reply($"[Arena] Debug: {(ArenaPlayerService.DebugEnabled ? "ON" : "OFF")}");
        }

        [Command("arena debug", shortHand: "ad", description: "Enable or disable arena debug logs", adminOnly: true)]
        public static void ArenaDebug(ChatCommandContext ctx, bool enabled = true)
        {
            ArenaPlayerService.DebugEnabled = enabled;
            ctx.Reply($"[Arena] Debug logging {(enabled ? "enabled" : "disabled")}");
        }

        [Command("arena test", shortHand: "atest", description: "Run diagnostics for arena enter/exit and lifecycle wiring", adminOnly: true)]
        public static void ArenaTest(ChatCommandContext ctx)
        {
            VRCore.Initialize();

            ctx.Reply("[ArenaTest] === Diagnostics ===");
            ctx.Reply($"[ArenaTest] ServerWorld: {(VRCore.ServerWorld != null ? "OK" : "MISSING")}");
            ctx.Reply($"[ArenaTest] Arena SpawnPoint: {ArenaPlayerService.SpawnPoint}");

            var territoryPath = ArenaTerritory.GetPreferredConfigPath();
            ctx.Reply($"[ArenaTest] Territory config: {(File.Exists(territoryPath) ? "OK" : "MISSING")} ({territoryPath})");

            var stepsPath = Path.Combine(Paths.ConfigPath, "VAuto", "LifecycleSteps.json");
            ctx.Reply($"[ArenaTest] LifecycleSteps: {(File.Exists(stepsPath) ? "OK" : "MISSING")} ({stepsPath})");
            TryDumpSteps(ctx, stepsPath);

            var kitPath = Path.Combine(Paths.ConfigPath, "EndGameKit.json");
            ctx.Reply($"[ArenaTest] EndGameKit: {(File.Exists(kitPath) ? "OK" : "MISSING")} ({kitPath})");

            TryDumpLifecycleServices(ctx);
        }

        [Command("arena set", shortHand: "aset", description: "Set arena center and radius at your position", adminOnly: true)]
        public static void ArenaSet(ChatCommandContext ctx, float radius = 50f)
        {
            if (!TryGetPlayerPosition(ctx, out var pos))
            {
                ctx.Reply("[Arena] Error: Could not get your position.");
                return;
            }

            ArenaPlayerService.SetArenaZone(pos, radius);
            ctx.Reply($"[Arena] Zone set at {pos} with radius {radius:F1}");
        }

        [Command("arena setentry", shortHand: "ase", description: "Set arena entry point at your position", adminOnly: true)]
        public static void ArenaSetEntry(ChatCommandContext ctx, float radius = 10f)
        {
            if (!TryGetPlayerPosition(ctx, out var pos))
            {
                ctx.Reply("[Arena] Error: Could not get your position.");
                return;
            }

            ArenaPlayerService.SetEntryPoint(pos, radius);
            ctx.Reply($"[Arena] Entry point set at {pos} with radius {radius:F1}");
        }

        [Command("arena setexit", shortHand: "asx", description: "Set arena exit point at your position", adminOnly: true)]
        public static void ArenaSetExit(ChatCommandContext ctx, float radius = 10f)
        {
            if (!TryGetPlayerPosition(ctx, out var pos))
            {
                ctx.Reply("[Arena] Error: Could not get your position.");
                return;
            }

            ArenaPlayerService.SetExitPoint(pos, radius);
            ctx.Reply($"[Arena] Exit point set at {pos} with radius {radius:F1}");
        }

        [Command("arena setspawn", shortHand: "ass", description: "Set arena spawn point at your position", adminOnly: true)]
        public static void ArenaSetSpawn(ChatCommandContext ctx)
        {
            if (!TryGetPlayerPosition(ctx, out var pos))
            {
                ctx.Reply("[Arena] Error: Could not get your position.");
                return;
            }

            ArenaPlayerService.SetSpawnPoint(pos);
            ctx.Reply($"[Arena] Spawn point set at {pos}");
        }

        [Command("arena enter", shortHand: "ae", description: "Enter the arena", adminOnly: false)]
        public static void ArenaEnter(ChatCommandContext ctx)
        {
            var character = GetCharacterEntity(ctx);
            if (character == Entity.Null)
            {
                ctx.Reply("[Arena] Error: Could not find your character.");
                return;
            }

            if (ArenaPlayerService.ManualEnterArena(character, out var error))
            {
                ctx.Reply("[Arena] Entered arena.");
            }
            else
            {
                ctx.Reply($"[Arena] Enter failed: {error}");
            }
        }

        [Command("arena exit", shortHand: "ax", description: "Exit the arena", adminOnly: false)]
        public static void ArenaExit(ChatCommandContext ctx)
        {
            var character = GetCharacterEntity(ctx);
            if (character == Entity.Null)
            {
                ctx.Reply("[Arena] Error: Could not find your character.");
                return;
            }

            if (ArenaPlayerService.ManualExitArena(character, out var error))
            {
                if (string.IsNullOrWhiteSpace(error))
                    ctx.Reply("[Arena] Exited arena.");
                else
                    ctx.Reply($"[Arena] Exited arena (warning: {error})");
            }
            else
            {
                ctx.Reply($"[Arena] Exit failed: {error}");
            }
        }

        [Command("arena tp", shortHand: "atp", description: "Teleport to arena spawn point", adminOnly: false)]
        public static void ArenaTeleport(ChatCommandContext ctx)
        {
            var character = GetCharacterEntity(ctx);
            if (character == Entity.Null)
            {
                ctx.Reply("[Arena] Error: Could not find your character.");
                return;
            }

            ArenaPlayerService.TeleportToSpawn(character);
            ctx.Reply("[Arena] Teleported to spawn point.");
        }

        [Command("arena preset", shortHand: "apreset", description: "Apply arena preset", adminOnly: true)]
        public static void ArenaPreset(ChatCommandContext ctx, string preset = "default")
        {
            if (!string.Equals(preset, "default", StringComparison.OrdinalIgnoreCase))
            {
                ctx.Reply($"[Arena] Unknown preset '{preset}'. Supported: default");
                return;
            }

            var center = new float3(-1500f, 0f, -500f);
            ArenaPlayerService.SetArenaZone(center, 70f);
            ArenaPlayerService.SetEntryPoint(center, 50f);
            ArenaPlayerService.SetExitPoint(center, 70f);
            ArenaPlayerService.SetSpawnPoint(center);

            ctx.Reply("[Arena] Preset 'default' applied (center -1500,0,-500; radius 70; entry 50; exit 70).");
        }

        [Command("arena glow validate", shortHand: "agv", description: "Validate arena territory config", adminOnly: false)]
        public static void ArenaGlowValidate(ChatCommandContext ctx)
        {
            var configPath = GetTerritoryConfigPath();
            if (ArenaGlowBorderService.ValidateConfig(configPath, out _, out var error))
            {
                ctx.Reply("[Arena] OK. Territory config valid.");
            }
            else
            {
                ctx.Reply($"[Arena] ERROR: {error}");
            }
        }

        [Command("arena glow test", shortHand: "agt", description: "Test glow border count (no spawn)", adminOnly: false)]
        public static void ArenaGlowTest(ChatCommandContext ctx, float spacing = 3f)
        {
            var total = ArenaTerritory.GetBorderPoints(spacing).Count;
            ctx.Reply($"[Arena] spacing: {spacing:F1}, points: {total}");
        }

        [Command("arena glow spawn", shortHand: "ag", description: "Spawn glow border entities", adminOnly: true)]
        public static void ArenaGlowSpawn(ChatCommandContext ctx, float spacing = 3f, string prefabName = "")
        {
            var configPath = GetTerritoryConfigPath();
            var name = !string.IsNullOrWhiteSpace(prefabName)
                ? prefabName
                : (!string.IsNullOrWhiteSpace(ArenaTerritory.GlowPrefab) ? ArenaTerritory.GlowPrefab : ArenaGlowBorderService.GetDefaultPrefabName());

            if (ArenaGlowBorderService.SpawnBorderGlows(configPath, name, spacing, out var error))
            {
                ctx.Reply($"[Arena] Glow border spawned with '{name}' at spacing {spacing:F1}");
            }
            else
            {
                ctx.Reply($"[Arena] ERROR: {error}");
            }
        }

        [Command("arena glow clear", shortHand: "agc", description: "Clear all spawned glow borders", adminOnly: true)]
        public static void ArenaGlowClear(ChatCommandContext ctx)
        {
            ArenaGlowBorderService.ClearAll();
            ctx.Reply("[Arena] Cleared spawned glow borders");
        }

        [Command("arena territory show", shortHand: "ats", description: "Show territory config values", adminOnly: false)]
        public static void ArenaTerritoryShow(ChatCommandContext ctx)
        {
            ctx.Reply($"[Arena] Territory center: {ArenaTerritory.ArenaGridCenter}");
            ctx.Reply($"[Arena] Territory radius: {ArenaTerritory.ArenaGridRadius:F1}");
            ctx.Reply($"[Arena] Territory grid index: {ArenaTerritory.ArenaGridIndex}");
            ctx.Reply($"[Arena] Territory region type: {ArenaTerritory.ArenaRegionType}");
            ctx.Reply($"[Arena] Territory block size: {ArenaTerritory.BlockSize:F1}");
            ctx.Reply($"[Arena] Glow border enabled: {ArenaTerritory.EnableGlowBorder}");
            ctx.Reply($"[Arena] Glow prefab: {ArenaTerritory.GlowPrefab}");
            ctx.Reply($"[Arena] Glow spacing: {ArenaTerritory.GlowSpacingMeters:F1}");
            ctx.Reply($"[Arena] Config path: {GetTerritoryConfigPath()}");
        }

        [Command("arena territory reload", shortHand: "atr", description: "Reload territory config from disk", adminOnly: true)]
        public static void ArenaTerritoryReload(ChatCommandContext ctx)
        {
            ArenaTerritory.Reload();
            ctx.Reply("[Arena] Territory reloaded.");
        }

        private static Entity GetCharacterEntity(ChatCommandContext ctx)
        {
            return ctx.Event?.SenderCharacterEntity ?? Entity.Null;
        }

        private static bool TryGetPlayerPosition(ChatCommandContext ctx, out float3 position)
        {
            position = float3.zero;
            var character = GetCharacterEntity(ctx);
            return character != Entity.Null && ArenaPlayerService.TryGetCharacterPosition(character, out position);
        }

        private static string GetTerritoryConfigPath()
        {
            return ArenaTerritory.GetPreferredConfigPath();
        }

        private static void TryDumpLifecycleServices(ChatCommandContext ctx)
        {
            try
            {
                var mgrType = Type.GetType("VAuto.Core.Lifecycle.ArenaLifecycleManager, Vlifecycle", throwOnError: false);
                if (mgrType == null)
                {
                    ctx.Reply("[ArenaTest] Lifecycle: MISSING (Vlifecycle not loaded)");
                    return;
                }

                var instance = mgrType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                if (instance == null)
                {
                    ctx.Reply("[ArenaTest] Lifecycle: MISSING (no Instance)");
                    return;
                }

                var countObj = mgrType.GetProperty("ServiceCount", BindingFlags.Public | BindingFlags.Instance)?.GetValue(instance);
                var count = countObj is int i ? i : 0;
                ctx.Reply($"[ArenaTest] Lifecycle services: {count}");

                var namesObj = mgrType.GetMethod("GetServiceNames", BindingFlags.Public | BindingFlags.Instance)?.Invoke(instance, null);
                if (namesObj is string[] names)
                {
                    foreach (var name in names)
                        ctx.Reply($"  - {name}");
                }
            }
            catch (Exception ex)
            {
                ctx.Reply($"[ArenaTest] Lifecycle: ERROR {ex.Message}");
            }
        }

        private static void TryDumpSteps(ChatCommandContext ctx, string stepsPath)
        {
            try
            {
                if (!File.Exists(stepsPath))
                    return;

                var json = File.ReadAllText(stepsPath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.ValueKind != JsonValueKind.Object)
                    return;

                var parts = new System.Collections.Generic.List<string>();
                foreach (var prop in root.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.True || prop.Value.ValueKind == JsonValueKind.False)
                        parts.Add($"{prop.Name}={(prop.Value.GetBoolean() ? "true" : "false")}");
                }

                if (parts.Count > 0)
                    ctx.Reply($"[ArenaTest] Steps: {string.Join(", ", parts)}");
            }
            catch
            {
                // ignore
            }
        }
    }
}
