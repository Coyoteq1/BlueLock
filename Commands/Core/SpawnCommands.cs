using System;
using ProjectM;
using Stunlock.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireCommandFramework;
using VAuto.Zone.Core;

namespace VAuto.Zone.Commands
{
    [CommandGroup("spawn", "sp")]
    public static class SpawnCommands
    {
        [Command("help", shortHand: "h", description: "Spawn command help", adminOnly: true)]
        public static void Help(ChatCommandContext ctx)
        {
            ctx.Reply("<color=#FFD700>[Spawn]</color> .sp unit <prefab|guid> [count] [level] [spread]");
            ctx.Reply("<color=#FFD700>[Spawn]</color> .sp boss <prefab|guid> [level]");
        }

        [Command("unit", shortHand: "u", description: "Spawn units near you", adminOnly: true)]
        public static void SpawnUnit(ChatCommandContext ctx, string prefabOrGuid, int count = 1, int level = 0, float spread = 2f)
        {
            if (count < 1) count = 1;
            if (count > 50) count = 50;
            if (spread < 0f) spread = 0f;

            if (!TryResolveSpawnPrefab(prefabOrGuid, out var guid, out var prefabEntity))
            {
                ctx.Reply($"<color=#FF0000>Could not resolve prefab '{prefabOrGuid}'.</color>");
                return;
            }

            if (!TryGetPlayerPosition(ctx.Event.SenderCharacterEntity, out var center))
            {
                ctx.Reply("<color=#FF0000>Could not resolve your position.</color>");
                return;
            }

            var em = ZoneCore.EntityManager;
            var spawned = 0;
            for (var i = 0; i < count; i++)
            {
                var offset = ComputeOffset(i, count, spread);
                var pos = center + offset;
                var entity = SpawnPrefabEntity(prefabEntity, pos, em);
                if (entity == Entity.Null)
                {
                    continue;
                }

                if (level > 0)
                {
                    TrySetUnitLevel(entity, level, em);
                }

                spawned++;
            }

            ctx.Reply($"<color=#00FF00>Spawned {spawned}/{count} '{guid.GuidHash}' (level {Math.Max(level, 0)}).</color>");
        }

        [Command("boss", shortHand: "b", description: "Spawn one boss near you", adminOnly: true)]
        public static void SpawnBoss(ChatCommandContext ctx, string prefabOrGuid, int level = 99)
        {
            if (level < 1) level = 1;

            if (!TryResolveSpawnPrefab(prefabOrGuid, out var guid, out var prefabEntity))
            {
                ctx.Reply($"<color=#FF0000>Could not resolve boss prefab '{prefabOrGuid}'.</color>");
                return;
            }

            if (!TryGetPlayerPosition(ctx.Event.SenderCharacterEntity, out var center))
            {
                ctx.Reply("<color=#FF0000>Could not resolve your position.</color>");
                return;
            }

            var em = ZoneCore.EntityManager;
            var spawnPos = center + new float3(2f, 0f, 0f);
            var entity = SpawnPrefabEntity(prefabEntity, spawnPos, em);
            if (entity == Entity.Null)
            {
                ctx.Reply("<color=#FF0000>Boss spawn failed.</color>");
                return;
            }

            TrySetUnitLevel(entity, level, em);
            ctx.Reply($"<color=#00FF00>Spawned boss '{guid.GuidHash}' at level {level}.</color>");
        }

        private static bool TryResolveSpawnPrefab(string input, out PrefabGUID guid, out Entity prefabEntity)
        {
            guid = PrefabGUID.Empty;
            prefabEntity = Entity.Null;

            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            if (int.TryParse(input.Trim(), out var hash))
            {
                guid = new PrefabGUID(hash);
                return ZoneCore.TryGetPrefabEntity(guid, out prefabEntity);
            }

            if (ZoneCore.TryResolvePrefabEntity(input.Trim(), out guid, out prefabEntity))
            {
                return true;
            }

            var withPrefix = input.StartsWith("CHAR_", StringComparison.OrdinalIgnoreCase)
                ? input.Trim()
                : $"CHAR_{input.Trim()}";
            return ZoneCore.TryResolvePrefabEntity(withPrefix, out guid, out prefabEntity);
        }

        private static bool TryGetPlayerPosition(Entity character, out float3 position)
        {
            position = float3.zero;
            var em = ZoneCore.EntityManager;
            if (em == default || character == Entity.Null || !em.Exists(character))
            {
                return false;
            }

            if (em.HasComponent<LocalToWorld>(character))
            {
                position = em.GetComponentData<LocalToWorld>(character).Position;
                return true;
            }

            if (em.HasComponent<LocalTransform>(character))
            {
                position = em.GetComponentData<LocalTransform>(character).Position;
                return true;
            }

            if (em.HasComponent<Translation>(character))
            {
                position = em.GetComponentData<Translation>(character).Value;
                return true;
            }

            return false;
        }

        private static float3 ComputeOffset(int index, int count, float spread)
        {
            if (count <= 1 || spread <= 0f)
            {
                return float3.zero;
            }

            var angle = (index / (float)count) * (2f * math.PI);
            return new float3(math.cos(angle) * spread, 0f, math.sin(angle) * spread);
        }

        private static Entity SpawnPrefabEntity(Entity prefabEntity, float3 position, EntityManager em)
        {
            try
            {
                if (prefabEntity == Entity.Null || em == default || !em.Exists(prefabEntity))
                {
                    return Entity.Null;
                }

                var spawned = em.Instantiate(prefabEntity);
                ZoneCore.SetPosition(spawned, position);
                return spawned;
            }
            catch
            {
                return Entity.Null;
            }
        }

        private static void TrySetUnitLevel(Entity entity, int level, EntityManager em)
        {
            try
            {
                if (!em.Exists(entity) || level <= 0)
                {
                    return;
                }

                if (em.HasComponent<UnitLevel>(entity))
                {
                    var unitLevel = em.GetComponentData<UnitLevel>(entity);
                    unitLevel.Level._Value = level;
                    em.SetComponentData(entity, unitLevel);
                }
            }
            catch
            {
                // Best-effort only.
            }
        }
    }
}

