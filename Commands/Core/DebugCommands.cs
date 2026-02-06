using System;
using ProjectM;
using ProjectM.Network;
using Unity.Entities;
using Unity.Transforms;
using VampireCommandFramework;
using VAuto.Core;
using VAuto.Core.Logging;

namespace VAuto.Commands.Core
{
    /// <summary>
    /// Debugging commands for ECS diagnostics.
    /// </summary>
    public static class DebugCommands
    {
        [Command("debugecs", shortHand: "decs", description: "Dump ECS diagnostics", adminOnly: true)]
        public static void DebugEcs(ChatCommandContext ctx)
        {
            var entityManager = VRCore.EntityManager;
            if (entityManager == default)
            {
                ctx.Reply("[Debug] EntityManager unavailable.");
                return;
            }

            try
            {
                var userQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<User>());
                var playerQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerCharacter>());
                var userCount = userQuery.CalculateEntityCount();
                var playerCount = playerQuery.CalculateEntityCount();

                VAutoLogger.LogInfo("DebugCommands", $"ECS counts: users={userCount}, players={playerCount}");
                ctx.Reply($"[Debug] Users: {userCount} | Players: {playerCount}");
            }
            catch (Exception ex)
            {
                VAutoLogger.LogError("DebugCommands", "DebugEcs failed", ex);
                ctx.Reply("[Debug] Error while dumping ECS state.");
            }
        }

        [Command("debugentity", shortHand: "dent", description: "Check basic entity components", adminOnly: true)]
        public static void DebugEntity(ChatCommandContext ctx, int entityIndex, int entityVersion)
        {
            var entityManager = VRCore.EntityManager;
            if (entityManager == default)
            {
                ctx.Reply("[Debug] EntityManager unavailable.");
                return;
            }

            var entity = new Entity { Index = entityIndex, Version = entityVersion };
            if (!entityManager.Exists(entity))
            {
                ctx.Reply($"[Debug] Entity {entityIndex}:{entityVersion} does not exist.");
                return;
            }

            var hasUser = entityManager.HasComponent<User>(entity);
            var hasPlayer = entityManager.HasComponent<PlayerCharacter>(entity);
            var hasTranslation = entityManager.HasComponent<Translation>(entity);
            var hasLocalTransform = entityManager.HasComponent<LocalTransform>(entity);

            ctx.Reply($"[Debug] Entity {entity.Index}:{entity.Version} exists.");
            ctx.Reply($"[Debug] User={hasUser} PlayerCharacter={hasPlayer} Translation={hasTranslation} LocalTransform={hasLocalTransform}");
        }
    }
}
