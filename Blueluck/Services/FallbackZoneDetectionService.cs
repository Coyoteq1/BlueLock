using System;
using System.Collections.Generic;
using System.Linq;
using ProjectM;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Blueluck.Services
{
    internal static class FallbackZoneDetectionService
    {
        private static float _nextDetectionTickTime;
        private static readonly Dictionary<Entity, float3> _lastKnownPositions = new();

        public static void ProcessTick()
        {
            if (!Plugin.FallbackZoneDetectionEnabled ||
                Plugin.ZoneConfig?.IsInitialized != true ||
                Plugin.ZoneTransition?.IsInitialized != true)
            {
                return;
            }

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            var now = UnityEngine.Time.realtimeSinceStartup;
            var intervalMs = Plugin.ZoneConfig.GetDetectionConfig().CheckIntervalMs;
            var intervalSeconds = Math.Max(0.05f, intervalMs / 1000f);
            if (now < _nextDetectionTickTime)
            {
                return;
            }

            _nextDetectionTickTime = now + intervalSeconds;

            var em = world.EntityManager;
            var query = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerCharacter>());
            var players = query.ToEntityArray(Allocator.Temp);

            try
            {
                var zones = Plugin.ZoneConfig.GetZones()
                    .OrderByDescending(z => z.Priority)
                    .ThenByDescending(z => z.EntryRadiusSq)
                    .ThenBy(z => z.Hash)
                    .ToArray();

                var movementThreshold = Math.Max(0f, Plugin.ZoneConfig.GetDetectionConfig().PositionThreshold);
                var movementThresholdSq = movementThreshold * movementThreshold;

                foreach (var player in players)
                {
                    if (!em.Exists(player) || !TryGetBestPosition(em, player, out var position))
                    {
                        continue;
                    }

                    var currentHash = Plugin.ZoneTransition.GetPlayerZone(player);
                    if (_lastKnownPositions.TryGetValue(player, out var lastPosition) &&
                        currentHash != 0 &&
                        math.distancesq(position, lastPosition) < movementThresholdSq)
                    {
                        continue;
                    }

                    _lastKnownPositions[player] = position;
                    var newHash = DetectZoneHash(position, currentHash, zones);
                    if (newHash == currentHash)
                    {
                        continue;
                    }

                    if (currentHash != 0 && Plugin.ZoneConfig.TryGetZoneByHash(currentHash, out var oldZone))
                    {
                        Plugin.ZoneTransition.OnZoneExit(player, oldZone);
                    }

                    if (newHash != 0 && Plugin.ZoneConfig.TryGetZoneByHash(newHash, out var newZone))
                    {
                        Plugin.ZoneTransition.OnZoneEnter(player, newZone);
                    }

                    if (Plugin.ZoneDetectionDebugMode?.Value == true)
                    {
                        Plugin.LogInfo($"[ZoneTransition][fallback] player={player.Index} oldHash={currentHash} newHash={newHash}");
                    }
                }
            }
            finally
            {
                players.Dispose();
            }
        }

        private static int DetectZoneHash(float3 position, int currentHash, IReadOnlyList<Models.ZoneDefinition> zones)
        {
            foreach (var zone in zones)
            {
                var distSq = math.distancesq(position, zone.GetCenterFloat3());
                var inside = currentHash == zone.Hash
                    ? distSq <= zone.ExitRadiusSq
                    : distSq <= zone.EntryRadiusSq;

                if (inside)
                {
                    return zone.Hash;
                }
            }

            return 0;
        }

        private static bool TryGetBestPosition(EntityManager em, Entity entity, out float3 position)
        {
            position = default;

            try
            {
                if (em.HasComponent<LocalTransform>(entity))
                {
                    position = em.GetComponentData<LocalTransform>(entity).Position;
                    return true;
                }

                if (em.HasComponent<Translation>(entity))
                {
                    position = em.GetComponentData<Translation>(entity).Value;
                    return true;
                }

                if (em.HasComponent<LastTranslation>(entity))
                {
                    position = em.GetComponentData<LastTranslation>(entity).Value;
                    return true;
                }

                if (em.HasComponent<SpawnTransform>(entity))
                {
                    position = em.GetComponentData<SpawnTransform>(entity).Position;
                    return true;
                }

                if (em.HasComponent<LocalToWorld>(entity))
                {
                    position = em.GetComponentData<LocalToWorld>(entity).Position;
                    return true;
                }
            }
            catch
            {
                // ignored
            }

            return false;
        }
    }
}
