using System;
using System.Collections.Generic;
using ProjectM;
using ProjectM.Network;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using System.Reflection;
using VAuto.Core;
using VAutoZone;

namespace VAuto.Zone.Services
{
    internal static class ArenaPlayerService
    {
        private static readonly HashSet<ulong> PlayersInArena = new();
        private static readonly Dictionary<ulong, Entity> PlayerEntities = new();
        private static readonly Dictionary<Entity, float3> LastPlayerPositions = new();

        public static bool DebugEnabled { get; set; }

        public static float3 ArenaCenter { get; private set; } = new float3(-1500, 0, -500);
        public static float ArenaRadius { get; private set; } = 70f;
        public static float3 EntryPoint { get; private set; } = new float3(-1500, 0, -500);
        public static float EntryRadius { get; private set; } = 50f;
        public static float3 ExitPoint { get; private set; } = new float3(-1500, 0, -500);
        public static float ExitRadius { get; private set; } = 70f;
        public static float3 SpawnPoint { get; private set; } = new float3(-1500, 0, -500);

        private static EntityManager EM => VRCore.EntityManager;

        public static int PlayerCount => PlayersInArena.Count;

        public static void InitializeFromTerritory()
        {
            // Ensure sane non-zero defaults so teleport doesn't send players to (0,0,0).
            ArenaTerritory.InitializeArenaGrid();
            var gridCenter = ArenaTerritory.ArenaGridCenter;
            var gridRadius = Math.Max(1f, ArenaTerritory.ArenaGridRadius);
            var useDefaults = gridCenter.Equals(float3.zero);

            var center = useDefaults ? ArenaCenter : gridCenter;
            var radius = useDefaults ? ArenaRadius : gridRadius;

            SetArenaZone(center, radius);
            SetEntryPoint(center, EntryRadius);
            SetExitPoint(center, ExitRadius);
        }

        public static Entity GetUserFromCharacter(Entity characterEntity)
        {
            try
            {
                if (characterEntity == Entity.Null || !EM.HasComponent<PlayerCharacter>(characterEntity))
                    return Entity.Null;

                var playerCharacter = EM.GetComponentData<PlayerCharacter>(characterEntity);
                return playerCharacter.UserEntity;
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogError($"[Arena] GetUserFromCharacter failed: {ex.Message}");
                return Entity.Null;
            }
        }

        public static void SetArenaZone(float3 center, float radius)
        {
            ArenaCenter = center;
            ArenaRadius = radius;
            SpawnPoint = ArenaCenter;

            Plugin.Logger?.LogInfo($"[Arena] Arena zone set: Center {center}, Radius {radius}");
            Plugin.Logger?.LogInfo($"[Arena] Spawn point set to arena center: {SpawnPoint}");
        }

        public static void SetEntryPoint(float3 point, float radius)
        {
            EntryPoint = point;
            EntryRadius = radius;
            Plugin.Logger?.LogInfo($"[Arena] Entry point set: {point}, Radius {radius}");
        }

        public static void SetExitPoint(float3 point, float radius)
        {
            ExitPoint = point;
            ExitRadius = radius;
            Plugin.Logger?.LogInfo($"[Arena] Exit point set: {point}, Radius {radius}");
        }

        public static void SetSpawnPoint(float3 point)
        {
            SpawnPoint = point;
            Plugin.Logger?.LogInfo($"[Arena] Spawn point set: {point}");
        }

        public static bool TryGetCharacterPosition(Entity characterEntity, out float3 position)
        {
            position = float3.zero;
            try
            {
                if (characterEntity == Entity.Null || !EM.Exists(characterEntity))
                    return false;

                if (EM.HasComponent<LocalTransform>(characterEntity))
                {
                    position = EM.GetComponentData<LocalTransform>(characterEntity).Position;
                    return true;
                }

                if (EM.HasComponent<Translation>(characterEntity))
                {
                    position = EM.GetComponentData<Translation>(characterEntity).Value;
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogError($"[Arena] GetCharacterPosition failed: {ex.Message}");
                return false;
            }
        }

        public static void CheckPlayerZones(Entity playerEntity, float3 position)
        {
            if (!IsValidEntity(playerEntity))
                return;

            LastPlayerPositions[playerEntity] = position;
        }

        public static bool IsInZone(float3 position, float3 center, float radius)
        {
            return math.distance(position, center) <= radius;
        }

        public static bool IsValidEntity(Entity entity)
        {
            if (entity.Equals(Entity.Null))
            {
                LogDebug("Entity is null");
                return false;
            }

            try
            {
                var em = VRCore.EntityManager;
                if (!em.Exists(entity))
                {
                    LogDebug($"Entity {entity} does not exist");
                    return false;
                }

                if (!em.HasComponent<Translation>(entity) && !em.HasComponent<LocalTransform>(entity))
                {
                    LogDebug($"No position component found on entity {entity}");
                    return false;
                }

                if (em.HasComponent<PlayerCharacter>(entity))
                {
                    var playerCharacter = em.GetComponentData<PlayerCharacter>(entity);
                    var userEntity = playerCharacter.UserEntity;

                    if (userEntity.Equals(Entity.Null) || !em.Exists(userEntity))
                    {
                        LogDebug($"Invalid user entity for player {entity}");
                        return false;
                    }

                    if (!em.HasComponent<User>(userEntity))
                    {
                        LogDebug($"User component missing for user entity {userEntity}");
                        return false;
                    }

                    var userData = em.GetComponentData<User>(userEntity);
                    if (!userData.IsConnected)
                    {
                        LogDebug($"User {userEntity} is not connected");
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogError($"[Arena] IsValidEntity failed: {ex.Message}");
                return false;
            }
        }

        public static bool ManualEnterArena(Entity playerEntity, out string error)
        {
            error = string.Empty;
            ulong steamId = 0;
            string playerName = "Unknown";

            try
            {
                LogDebug($"ManualEnterArena starting for entity {playerEntity}");

                if (!IsValidEntity(playerEntity))
                {
                    error = "Invalid player entity (missing position/User)";
                    Plugin.Logger?.LogWarning($"[Arena] {error}");
                    return false;
                }

                var userEntity = GetUserFromCharacter(playerEntity);
                if (userEntity == Entity.Null)
                {
                    error = "Could not resolve user entity from character";
                    Plugin.Logger?.LogWarning($"[Arena] {error} ({playerEntity})");
                    return false;
                }

                var user = EM.GetComponentData<User>(userEntity);
                steamId = user.PlatformId;
                playerName = user.CharacterName.ToString();

                if (PlayersInArena.Contains(steamId))
                {
                    Plugin.Logger?.LogInfo($"[Arena] {playerName} already in arena");
                    return true;
                }

                PlayersInArena.Add(steamId);
                PlayerEntities[steamId] = playerEntity;

                if (TryGetCharacterPosition(playerEntity, out var pos))
                    LastPlayerPositions[playerEntity] = pos;

                if (!TryInvokeLifecycle("OnPlayerEnter", userEntity, playerEntity, "default", out var lifecycleError))
                {
                    // Roll back local arena state to avoid half-entered players.
                    PlayersInArena.Remove(steamId);
                    PlayerEntities.Remove(steamId);
                    error = $"Lifecycle enter failed: {lifecycleError}";
                    Plugin.Logger?.LogWarning($"[Arena] {error}");
                    return false;
                }

                TeleportToSpawn(playerEntity);
                SpawnArenaEffects(playerEntity);

                Plugin.Logger?.LogInfo($"[Arena] {playerName} entered arena (SteamID: {steamId})");
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogError($"[Arena] ManualEnterArena failed for {playerName} ({steamId}): {ex.Message}");
                if (steamId != 0)
                {
                    PlayersInArena.Remove(steamId);
                    PlayerEntities.Remove(steamId);
                }

                error = ex.Message;
                return false;
            }
        }

        public static bool ManualExitArena(Entity playerEntity, out string error)
        {
            error = string.Empty;
            try
            {
                if (playerEntity == Entity.Null)
                {
                    error = "Exit requested with null entity";
                    Plugin.Logger?.LogWarning($"[Arena] {error}");
                    return false;
                }

                var userEntity = GetUserFromCharacter(playerEntity);
                if (userEntity == Entity.Null)
                {
                    error = "Could not resolve user entity from character";
                    Plugin.Logger?.LogWarning($"[Arena] {error} ({playerEntity})");
                    return false;
                }

                var user = EM.GetComponentData<User>(userEntity);
                var steamId = user.PlatformId;
                var playerName = user.CharacterName.ToString();

                if (PlayersInArena.Remove(steamId))
                {
                    PlayerEntities.Remove(steamId);

                    var lifecycleOk = TryInvokeLifecycle("OnPlayerExit", userEntity, playerEntity, "default", out var lifecycleError);
                    if (!lifecycleOk)
                    {
                        error = $"Lifecycle exit failed: {lifecycleError}";
                        Plugin.Logger?.LogWarning($"[Arena] {error}");
                    }

                    if (LastPlayerPositions.TryGetValue(playerEntity, out var lastPos))
                    {
                        TeleportToPosition(playerEntity, lastPos);
                    }
                    else if (!ExitPoint.Equals(float3.zero))
                    {
                        TeleportToPosition(playerEntity, ExitPoint);
                    }
                    Plugin.Logger?.LogInfo($"[Arena] {playerName} exited arena");
                    return true;
                }
                else
                {
                    Plugin.Logger?.LogInfo($"[Arena] {playerName} was not in arena");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogError($"[Arena] ManualExitArena failed: {ex.Message}");
                error = ex.Message;
                return false;
            }
        }

        public static bool IsPlayerInArena(Entity playerEntity)
        {
            try
            {
                if (playerEntity == Entity.Null) return false;
                var userEntity = GetUserFromCharacter(playerEntity);
                if (userEntity == Entity.Null) return false;

                var user = EM.GetComponentData<User>(userEntity);
                return PlayersInArena.Contains(user.PlatformId);
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogError($"[Arena] IsPlayerInArena failed: {ex.Message}");
                return false;
            }
        }

        public static int GetPlayerTerritoryIndex(Entity playerEntity)
        {
            if (!IsValidEntity(playerEntity))
                return -1;

            if (LastPlayerPositions.TryGetValue(playerEntity, out var position))
            {
                return ArenaTerritory.GetArenaGridIndex(position);
            }

            return -1;
        }

        public static void TeleportToSpawn(Entity playerEntity)
        {
            TeleportToPosition(playerEntity, SpawnPoint);
        }

        private static void TeleportToPosition(Entity playerEntity, float3 position)
        {
            if (!IsValidEntity(playerEntity))
                return;

            if (EM.HasComponent<LocalTransform>(playerEntity))
            {
                var transform = EM.GetComponentData<LocalTransform>(playerEntity);
                transform.Position = position;
                EM.SetComponentData(playerEntity, transform);
            }

            if (EM.HasComponent<Translation>(playerEntity))
            {
                var translation = EM.GetComponentData<Translation>(playerEntity);
                translation.Value = position;
                EM.SetComponentData(playerEntity, translation);
            }

            if (EM.HasComponent<LastTranslation>(playerEntity))
            {
                var last = EM.GetComponentData<LastTranslation>(playerEntity);
                last.Value = position;
                EM.SetComponentData(playerEntity, last);
            }

            Plugin.Logger?.LogInfo($"[Arena] Teleported {playerEntity} to: {position}");
        }

        private static bool TryInvokeLifecycle(string methodName, Entity userEntity, Entity characterEntity, string arenaId, out string error)
        {
            error = string.Empty;
            // Avoid a hard reference from VAutoArena -> Vlifecycle; invoke via reflection if present.
            try
            {
                var mgrType = Type.GetType("VAuto.Core.Lifecycle.ArenaLifecycleManager, Vlifecycle", throwOnError: false);
                if (mgrType == null)
                {
                    error = "Vlifecycle not loaded";
                    return false;
                }

                var instanceProp = mgrType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                var instance = instanceProp?.GetValue(null);
                if (instance == null)
                {
                    error = "ArenaLifecycleManager.Instance missing";
                    return false;
                }

                var mi = mgrType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
                if (mi == null)
                {
                    error = $"Method '{methodName}' not found";
                    return false;
                }

                var result = mi.Invoke(instance, new object[] { userEntity, characterEntity, arenaId });
                if (result is bool b)
                    return b;

                return true;
            }
            catch (Exception ex)
            {
                error = ex.InnerException?.Message ?? ex.Message;
                Plugin.Logger?.LogWarning($"[Arena] Lifecycle invoke '{methodName}' failed: {error}");
                return false;
            }
        }

        private static void SpawnArenaEffects(Entity playerEntity)
        {
            if (!IsValidEntity(playerEntity))
                return;

            LogDebug($"Arena effects would spawn for {playerEntity} at {SpawnPoint}");
        }

        private static void LogDebug(string message)
        {
            if (DebugEnabled)
                Plugin.Logger?.LogInfo($"[Arena][Debug] {message}");
        }
    }
}
