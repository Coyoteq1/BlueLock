using System;
using ProjectM;
using Stunlock.Core;
using Unity.Entities;
using Unity.Mathematics;
using VampireCommandFramework;
using VAuto.Zone.Services;
using VAuto.Core;

namespace VAuto.Zone.Commands
{
    public static class ZoneOnlyCommands
    {
        [Command("bp", shortHand: "bp", description: "Set blood type/quality (arena zone only)", adminOnly: false)]
        public static void BloodParams(ChatCommandContext ctx, long bloodTypeGuid, int quality = 100)
        {
            if (!TryGetArenaContext(ctx, out var character, out var pos))
                return;

            try
            {
                var em = VRCore.EntityManager;
                if (!em.HasComponent<Blood>(character))
                {
                    ctx.Reply("[BP] Error: Blood component not found.");
                    return;
                }

                var blood = em.GetComponentData<Blood>(character);
                blood.BloodType = new PrefabGUID((int)bloodTypeGuid);
                blood.Quality = (float)Math.Clamp(quality, 0, 100);
                em.SetComponentData(character, blood);

                ctx.Reply($"[BP] Blood set: type={bloodTypeGuid} quality={blood.Quality:F0}");
            }
            catch (Exception ex)
            {
                ctx.Reply($"[BP] Error: {ex.Message}");
            }
        }

        [Command("arena kit", shortHand: "akit", description: "Apply kit by name (arena zone only)", adminOnly: false)]
        public static void ArenaKit(ChatCommandContext ctx, string kitName)
        {
            if (!TryGetArenaContext(ctx, out var character, out _))
                return;

            if (TryApplyKit(character, kitName, out var error))
                ctx.Reply($"[Arena] Kit applied: {kitName}");
            else
                ctx.Reply($"[Arena] Kit failed: {error}");
        }

        [Command("arena pos", shortHand: "apos", description: "Show your position (arena zone only)", adminOnly: false)]
        public static void ArenaPosition(ChatCommandContext ctx)
        {
            if (!TryGetArenaContext(ctx, out _, out var pos))
                return;

            ctx.Reply($"[Arena] Position: {Fmt(pos)}");
            ctx.Reply($"[Arena] Center: {Fmt(ArenaPlayerService.ArenaCenter)} radius={ArenaPlayerService.ArenaRadius:F1}");
        }

        [Command("arena setentryradius", shortHand: "aser", description: "Set entry radius (arena zone only)", adminOnly: true)]
        public static void SetEntryRadius(ChatCommandContext ctx, float radius)
        {
            if (!TryGetArenaContext(ctx, out _, out _))
                return;

            if (radius <= 0f)
            {
                ctx.Reply("[Arena] Error: radius must be > 0.");
                return;
            }

            ArenaPlayerService.SetEntryPoint(ArenaPlayerService.EntryPoint, radius);
            ctx.Reply($"[Arena] Entry radius set to {radius:F1}");
        }

        [Command("arena setexitradius", shortHand: "asxr", description: "Set exit radius (arena zone only)", adminOnly: true)]
        public static void SetExitRadius(ChatCommandContext ctx, float radius)
        {
            if (!TryGetArenaContext(ctx, out _, out _))
                return;

            if (radius <= 0f)
            {
                ctx.Reply("[Arena] Error: radius must be > 0.");
                return;
            }

            ArenaPlayerService.SetExitPoint(ArenaPlayerService.ExitPoint, radius);
            ctx.Reply($"[Arena] Exit radius set to {radius:F1}");
        }

        private static bool TryGetArenaContext(ChatCommandContext ctx, out Entity character, out float3 position)
        {
            character = ctx.Event?.SenderCharacterEntity ?? Entity.Null;
            position = float3.zero;

            if (character == Entity.Null)
            {
                ctx.Reply("[Arena] Error: Could not resolve your character entity.");
                return false;
            }

            if (!ArenaPlayerService.TryGetCharacterPosition(character, out position))
            {
                ctx.Reply("[Arena] Error: Could not read your position.");
                return false;
            }

            if (!ArenaPlayerService.IsInZone(position, ArenaPlayerService.ArenaCenter, ArenaPlayerService.ArenaRadius))
            {
                ctx.Reply("[Arena] This command is only available inside the arena zone.");
                return false;
            }

            return true;
        }

        private static bool TryApplyKit(Entity character, string kitName, out string error)
        {
            error = string.Empty;
            try
            {
                var factoryType = Type.GetType("VAuto.EndGameKit.EndGameKitSystemFactory, Vlifecycle", throwOnError: false);
                if (factoryType == null)
                {
                    error = "EndGameKitSystemFactory not available (Vlifecycle not loaded)";
                    return false;
                }

                var create = factoryType.GetMethod("Create", new[] { typeof(EntityManager), typeof(string) });
                if (create == null)
                {
                    error = "EndGameKitSystemFactory.Create not found";
                    return false;
                }

                VRCore.Initialize();
                var em = VRCore.EntityManager;
                var system = create.Invoke(null, new object[] { em, null });
                if (system == null)
                {
                    error = "EndGameKitSystem creation failed";
                    return false;
                }

                var apply = system.GetType().GetMethod("ApplyKit", new[] { typeof(Entity), typeof(string) });
                if (apply == null)
                {
                    error = "EndGameKitSystem.ApplyKit not found";
                    return false;
                }

                var result = apply.Invoke(system, new object[] { character, kitName });
                if (result is bool b && b)
                    return true;

                error = "ApplyKit returned false";
                return false;
            }
            catch (Exception ex)
            {
                error = ex.InnerException?.Message ?? ex.Message;
                return false;
            }
        }

        private static string Fmt(float3 pos) => $"({pos.x:F1}, {pos.y:F1}, {pos.z:F1})";
    }
}
