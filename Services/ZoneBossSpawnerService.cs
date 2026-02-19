using System;
using System.Collections.Generic;
using ProjectM;
using Stunlock.Core;
using Unity.Entities;
using Unity.Mathematics;
using VAuto.Zone.Core;

namespace VAuto.Zone.Services
{
    public static class ZoneBossSpawnerService
    {
        private static readonly Dictionary<string, Entity> _activeBossByZone = new(StringComparer.OrdinalIgnoreCase);
        private static readonly System.Random _rng = new();

        private static readonly string[] BossPrefabs =
        {
            "CHAR_Vampire_Dracula_VBlood",
            "CHAR_Vampire_HighLord_VBlood",
            "CHAR_Vampire_BloodKnight_VBlood",
            "CHAR_Winter_Yeti_VBlood",
            "CHAR_Wendigo_VBlood",
            "CHAR_Spider_Queen_VBlood",
            "CHAR_VHunter_Leader_VBlood",
            "CHAR_Bandit_Stalker_VBlood"
        };

        public static void Initialize()
        {
            _activeBossByZone.Clear();
        }

        public static bool TryHandlePlayerEnter(Entity player, string zoneId, out string message)
        {
            message = string.Empty;

            if (string.IsNullOrWhiteSpace(zoneId))
            {
                return false;
            }

            var em = ZoneCore.EntityManager;
            if (em == default)
            {
                message = "EntityManager unavailable.";
                return false;
            }

            if (_activeBossByZone.TryGetValue(zoneId, out var existing) && existing != Entity.Null && em.Exists(existing))
            {
                return false;
            }

            var zone = ZoneConfigService.GetZoneById(zoneId);
            if (zone == null)
            {
                message = $"Zone '{zoneId}' not found.";
                return false;
            }

            if (!TryPickRandomBossPrefab(out var bossName, out var bossGuid, out var bossPrefab))
            {
                message = "No resolvable boss prefabs available.";
                return false;
            }

            try
            {
                var spawned = em.Instantiate(bossPrefab);
                var spawnPos = new float3(zone.CenterX + 3f, zone.CenterY, zone.CenterZ + 3f);
                ZoneCore.SetPosition(spawned, spawnPos);

                // Default to end-game level for event zones.
                TrySetUnitLevel(spawned, 99, em);

                _activeBossByZone[zoneId] = spawned;
                message = $"Zone '{zoneId}' started at ({zone.CenterX:F1}, {zone.CenterY:F1}, {zone.CenterZ:F1}) - random boss '{bossName}' ({bossGuid.GuidHash}) level 99.";
                return true;
            }
            catch (Exception ex)
            {
                message = $"Boss spawn failed: {ex.Message}";
                return false;
            }
        }

        public static void HandlePlayerExit(Entity player, string zoneId)
        {
            // Keep active boss alive while zone is active; do not despawn on each player exit.
        }

        public static bool IsZoneBossAlive(string zoneId)
        {
            if (string.IsNullOrWhiteSpace(zoneId))
            {
                return false;
            }

            var em = ZoneCore.EntityManager;
            if (em == default)
            {
                return false;
            }

            if (_activeBossByZone.TryGetValue(zoneId, out var boss) && boss != Entity.Null && em.Exists(boss))
            {
                return true;
            }

            _activeBossByZone.Remove(zoneId);
            return false;
        }

        private static bool TryPickRandomBossPrefab(out string bossName, out PrefabGUID guid, out Entity prefabEntity)
        {
            bossName = string.Empty;
            guid = PrefabGUID.Empty;
            prefabEntity = Entity.Null;

            if (BossPrefabs.Length == 0)
            {
                return false;
            }

            var start = _rng.Next(BossPrefabs.Length);
            for (var i = 0; i < BossPrefabs.Length; i++)
            {
                var idx = (start + i) % BossPrefabs.Length;
                var candidate = BossPrefabs[idx];
                if (!ZoneCore.TryResolvePrefabEntity(candidate, out guid, out prefabEntity))
                {
                    continue;
                }

                if (prefabEntity == Entity.Null)
                {
                    continue;
                }

                bossName = candidate;
                return true;
            }

            return false;
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
