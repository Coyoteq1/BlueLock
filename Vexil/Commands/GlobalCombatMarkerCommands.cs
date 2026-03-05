using ProjectM;
using ProjectM.Shared;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireCommandFramework;
using VAutomationCore.Core;

namespace GlobalCombatMarker
{
    public static class GlobalCombatMarkerCommands
    {
        [Command("testmarker", "tm", "Spawn a global map marker at your current chunk center.", adminOnly: true)]
        public static void SpawnTestMarker(ChatCommandContext ctx)
        {
            var em = UnifiedCore.EntityManager;

            var user = ctx.Event.User;
            if (!em.TryGetComponentData(user.LocalCharacter._Entity, out LocalToWorld ltw))
            {
                ctx.Reply("Player position unavailable.");
                return;
            }

            float3 pos = ltw.Position;

            float chunkSize = 64f;
            float3 chunkCenter = new float3(
                math.floor(pos.x / chunkSize) * chunkSize + chunkSize * 0.5f,
                pos.y,
                math.floor(pos.z / chunkSize) * chunkSize + chunkSize * 0.5f
            );

            // Create spawn entity for marker
            Entity spawnEntity = em.CreateEntity();
            em.AddBuffer<MapIconSpawnRequest>(spawnEntity);

            var buffer = em.GetBuffer<MapIconSpawnRequest>(spawnEntity);

            // Use custom marker prefab (1716771727 = MapIcon_PlayerCustomMarker)
            buffer.Add(new MapIconSpawnRequest
            {
                PrefabGuid = new PrefabGUID(1716771727),
                Position = chunkCenter,
                Owner = Entity.Null,
                TeamId = 0,
                FactionMask = FactionMask.All,
                Lifetime = 10f,
                IconType = MapIconType.Ping,
                IsStatic = true
            });

            ctx.Reply($"Spawned global marker at chunk center: {chunkCenter}");
        }

        [Command("vexil.reload", "vr", "Reload Vexil configuration.", adminOnly: true)]
        public static void ReloadConfig(ChatCommandContext ctx)
        {
            // TODO: Implement config reload
            ctx.Reply("Config reload not yet implemented.");
        }
    }
}
