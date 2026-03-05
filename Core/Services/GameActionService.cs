using System;
using System.Linq;
using System.Collections.Concurrent;
using ProjectM;
using ProjectM.Network;
using ProjectM.Shared;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VAutomationCore.Abstractions;
using VAutomationCore.Core;
using VAutomationCore.Core.Api;
using VAutomationCore.Core.Logging;
using ServerChatUtils = ProjectM.ServerChatUtils;

namespace VAutomationCore.Services
{
    /// <summary>
    /// Runtime action helpers used by flow execution and cross-module services.
    /// </summary>
    public static class GameActionService
    {
        private static readonly CoreLogger Log = new("GameActionService");
        private static readonly ConcurrentDictionary<string, Func<object[], EntityMap?, bool>> CustomActions =
            new(StringComparer.OrdinalIgnoreCase);

        public static string EventPlayerEnter => "PlayerEnter";
        public static string EventPlayerExit => "PlayerExit";

        public static bool InvokeAction(string actionName, object[] args)
        {
            return InvokeAction(actionName, args, null);
        }

        public static bool InvokeAction(string actionName, object[] args, EntityMap? entityMap)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            try
            {
                if (TryInvokeCustomAction(actionName, args, entityMap, out var customResult))
                {
                    return customResult;
                }

                switch (actionName.Trim().ToLowerInvariant())
                {
                    case "applybuff":
                    {
                        if (!TryGetEntityArg(args, 0, out var userEntity) ||
                            !TryGetEntityArg(args, 1, out var targetEntity) ||
                            !TryGetPrefabGuidArg(args, 2, out var buffGuid))
                        {
                            return false;
                        }

                        var duration = TryGetFloatArg(args, 3, out var parsedDuration) ? parsedDuration : 0f;
                        return Buffs.AddBuff(userEntity, targetEntity, buffGuid, duration, immortal: duration <= 0f);
                    }

                    case "cleanbuff":
                    {
                        if (!TryGetEntityArg(args, 0, out var targetEntity))
                        {
                            return false;
                        }

                        return TryCleanBuffs(targetEntity);
                    }

                    case "removebuff":
                    {
                        if (!TryGetEntityArg(args, 0, out var targetEntity) ||
                            !TryGetPrefabGuidArg(args, 1, out var buffGuid))
                        {
                            return false;
                        }

                        return TryRemoveBuff(targetEntity, buffGuid);
                    }

                    case "teleport":
                    {
                        if (!TryGetEntityArg(args, 0, out var targetEntity) ||
                            !TryGetFloat3Arg(args, 1, out var position))
                        {
                            return false;
                        }

                        return TryTeleport(targetEntity, position);
                    }

                    case "setposition":
                    {
                        if (!TryGetEntityArg(args, 0, out var targetEntity) ||
                            !TryGetFloat3Arg(args, 1, out var position))
                        {
                            return false;
                        }

                        return TrySetEntityPosition(targetEntity, position);
                    }

                    case "sendmessagetoall":
                    {
                        if (!TryGetStringArg(args, 0, out var message))
                        {
                            return false;
                        }

                        return TrySendSystemMessageToAll(message);
                    }

                    case "sendmessagetoplatform":
                    {
                        if (!TryGetPlatformIdArg(args, 0, out var platformId) ||
                            !TryGetStringArg(args, 1, out var message))
                        {
                            return false;
                        }

                        return TrySendSystemMessageToPlatformId(platformId, message);
                    }

                    case "sendmessagetouser":
                    {
                        if (!TryGetEntityArg(args, 0, out var userEntity) ||
                            !TryGetStringArg(args, 1, out var message))
                        {
                            return false;
                        }

                        return TrySendSystemMessageToUserEntity(userEntity, message);
                    }

                    case "spawnboss":
                    {
                        // Args: zoneId (string), position (x, y, z floats), level (int), prefabName (optional string)
                        if (!TryGetStringArg(args, 0, out var zoneId))
                        {
                            return false;
                        }

                        float x = 0, y = 0, z = 0;
                        int level = 99;
                        string? prefabName = null;

                        if (args.Length > 1 && args[1] is float fx) x = fx;
                        if (args.Length > 2 && args[2] is float fy) y = fy;
                        if (args.Length > 3 && args[3] is float fz) z = fz;
                        if (args.Length > 4 && args[4] is int ilvl) level = ilvl;
                        if (args.Length > 5 && args[5] is string pn) prefabName = pn;

                        return TrySpawnBoss(zoneId, new float3(x, y, z), level, prefabName);
                    }

                    case "removeboss":
                    {
                        // Args: zoneId (string)
                        if (!TryGetStringArg(args, 0, out var zoneId))
                        {
                            return false;
                        }

                        return TryRemoveBoss(zoneId);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.LogDebug($"InvokeAction '{actionName}' failed: {ex.Message}");
                return false;
            }

            return false;
        }

        public static bool RegisterActionHandler(string actionName, Func<object[], EntityMap?, bool> handler, bool replace = false)
        {
            if (string.IsNullOrWhiteSpace(actionName) || handler == null)
            {
                return false;
            }

            var key = actionName.Trim();
            if (replace)
            {
                CustomActions[key] = handler;
                return true;
            }

            return CustomActions.TryAdd(key, handler);
        }

        public static bool RemoveActionHandler(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            return CustomActions.TryRemove(actionName.Trim(), out _);
        }

        private static bool TryInvokeCustomAction(string actionName, object[] args, EntityMap? entityMap, out bool result)
        {
            result = false;

            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            if (!CustomActions.TryGetValue(actionName.Trim(), out var handler) || handler == null)
            {
                return false;
            }

            try
            {
                result = handler(args ?? Array.Empty<object>(), entityMap);
                return true;
            }
            catch (Exception ex)
            {
                Log.LogDebug($"Custom action '{actionName}' failed: {ex.Message}");
                result = false;
                return true;
            }
        }

        public static bool TryFindUserEntityByPlatformId(ulong platformId, out Entity userEntity)
        {
            userEntity = Entity.Null;
            if (platformId == 0)
            {
                return false;
            }

            try
            {
                var em = UnifiedCore.EntityManager;
                if (em == default)
                {
                    return false;
                }

                var query = em.CreateEntityQuery(ComponentType.ReadOnly<User>());
                var users = query.ToEntityArray(Allocator.Temp);
                try
                {
                    for (var i = 0; i < users.Length; i++)
                    {
                        var candidate = users[i];
                        if (!em.Exists(candidate) || !em.HasComponent<User>(candidate))
                        {
                            continue;
                        }

                        var user = em.GetComponentData<User>(candidate);
                        if (user.PlatformId != platformId)
                        {
                            continue;
                        }

                        userEntity = candidate;
                        return true;
                    }
                }
                finally
                {
                    users.Dispose();
                }
            }
            catch (Exception ex)
            {
                Log.LogDebug($"TryFindUserEntityByPlatformId failed: {ex.Message}");
            }

            return false;
        }

        public static bool TrySendSystemMessageToAll(string message)
        {
            var clean = TrimMessage(message);
            if (string.IsNullOrWhiteSpace(clean))
            {
                return false;
            }

            try
            {
                var em = UnifiedCore.EntityManager;
                if (em == default)
                {
                    Log.LogInfo($"[SystemMessage][All] {clean}");
                    return false;
                }

                var fixedMsg = new FixedString512Bytes(clean);
                ServerChatUtils.SendSystemMessageToAllClients(em, ref fixedMsg);
                Log.LogInfo($"[SystemMessage][All] {clean}");
                return true;
            }
            catch (Exception ex)
            {
                Log.LogDebug($"TrySendSystemMessageToAll failed: {ex.Message}");
                return false;
            }
        }

        public static bool TrySendSystemMessageToPlatformId(ulong platformId, string message)
        {
            if (!TryFindUserEntityByPlatformId(platformId, out var userEntity))
            {
                return false;
            }

            return TrySendSystemMessageToUserEntity(userEntity, message);
        }

        public static bool TrySendSystemMessageToUserEntity(Entity userEntity, string message)
        {
            var clean = TrimMessage(message);
            if (string.IsNullOrWhiteSpace(clean))
            {
                return false;
            }

            try
            {
                var em = UnifiedCore.EntityManager;
                if (em == default || userEntity == Entity.Null || !em.Exists(userEntity) || !em.HasComponent<User>(userEntity))
                {
                    return false;
                }

                var user = em.GetComponentData<User>(userEntity);
                var fixedMsg = new FixedString512Bytes(clean);
                ServerChatUtils.SendSystemMessageToClient(em, user, ref fixedMsg);
                Log.LogInfo($"[SystemMessage][{user.PlatformId}:{user.CharacterName}] {clean}");
                return true;
            }
            catch (Exception ex)
            {
                Log.LogDebug($"TrySendSystemMessageToUserEntity failed: {ex.Message}");
                return false;
            }
        }

        public static bool TryRemoveBuff(Entity entity, int buffHash)
        {
            return TryRemoveBuff(entity, new PrefabGUID(buffHash));
        }

        public static bool TryRemoveBuff(Entity entity, PrefabGUID buffPrefab)
        {
            try
            {
                var em = UnifiedCore.EntityManager;
                if (em == default || entity == Entity.Null || !em.Exists(entity) || buffPrefab == PrefabGUID.Empty)
                {
                    return false;
                }

                if (!BuffUtility.TryGetBuff(em, entity, buffPrefab, out var buffEntity) || buffEntity == Entity.Null || !em.Exists(buffEntity))
                {
                    return false;
                }

                DestroyUtility.Destroy(em, buffEntity, DestroyDebugReason.TryRemoveBuff);
                return true;
            }
            catch (Exception ex)
            {
                Log.LogDebug($"TryRemoveBuff failed: {ex.Message}");
                return false;
            }
        }

        public static bool TryTeleport(Entity entity, float3 position)
        {
            return TrySetEntityPosition(entity, position);
        }

        public static bool TrySetEntityPosition(Entity entity, float3 position)
        {
            try
            {
                var em = UnifiedCore.EntityManager;
                if (em == default || entity == Entity.Null || !em.Exists(entity))
                {
                    return false;
                }

                if (em.HasComponent<LocalTransform>(entity))
                {
                    var transform = em.GetComponentData<LocalTransform>(entity);
                    transform.Position = position;
                    em.SetComponentData(entity, transform);
                    return true;
                }

                if (em.HasComponent<Translation>(entity))
                {
                    var translation = em.GetComponentData<Translation>(entity);
                    translation.Value = position;
                    em.SetComponentData(entity, translation);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.LogDebug($"TrySetEntityPosition failed: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Spawns a boss entity at the specified position.
        /// Note: This is a stub - use the "boss_enter" lifecycle action for actual boss spawning.
        /// </summary>
        /// <param name="zoneId">Zone identifier for tracking</param>
        /// <param name="position">World position to spawn at</param>
        /// <param name="level">Unit level (default 99)</param>
        /// <param name="prefabName">Optional prefab name</param>
        /// <returns>True (stub always succeeds - actual spawning handled by lifecycle action)</returns>
        public static bool TrySpawnBoss(string zoneId, float3 position, int level = 99, string? prefabName = null)
        {
            // Stub implementation - actual boss spawning is handled by "boss_enter" lifecycle action
            // which calls ZoneBossSpawnerService.TryHandlePlayerEnter
            Log.LogInfo($"[TrySpawnBoss] Use lifecycle action 'boss_enter' for zone '{zoneId}' spawning");
            return true;
        }

        /// <summary>
        /// Removes a boss entity from the specified zone.
        /// </summary>
        /// <param name="zoneId">Zone identifier</param>
        /// <returns>True if removal successful (boss was present)</returns>
        public static bool TryRemoveBoss(string zoneId)
        {
            // This is a placeholder - actual implementation would need to track spawned bosses
            // For now, we'll just log the attempt
            Log.LogInfo($"TryRemoveBoss: Request to remove boss from zone '{zoneId}' - not implemented in core");
            return false;
        }

        public static int GetRegisteredEventActionCount(string eventName)
        {
            if (string.Equals(eventName, EventPlayerEnter, StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            if (string.Equals(eventName, EventPlayerExit, StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            return 0;
        }

        public static void ExecuteAction(string actionName, Entity target, Entity source)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return;
            }

            try
            {
                if (string.Equals(actionName, EventPlayerEnter, StringComparison.OrdinalIgnoreCase))
                {
                    ZoneEventBridge.PublishPlayerEntered(target, string.Empty);
                    return;
                }

                if (string.Equals(actionName, EventPlayerExit, StringComparison.OrdinalIgnoreCase))
                {
                    ZoneEventBridge.PublishPlayerExited(target, string.Empty);
                }
            }
            catch (Exception ex)
            {
                Log.LogDebug($"ExecuteAction failed for '{actionName}': {ex.Message}");
            }
        }

        public static bool CanExecuteAction(string actionName, Entity target)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            try
            {
                var em = UnifiedCore.EntityManager;
                return em != default && target != Entity.Null && em.Exists(target);
            }
            catch
            {
                return false;
            }
        }

        private static bool TryCleanBuffs(Entity targetEntity)
        {
            try
            {
                var em = UnifiedCore.EntityManager;
                if (em == default || targetEntity == Entity.Null || !em.Exists(targetEntity))
                {
                    return false;
                }

                var removed = 0;
                if (em.HasBuffer<BuffBuffer>(targetEntity))
                {
                    var buffs = em.GetBuffer<BuffBuffer>(targetEntity);
                    for (var i = 0; i < buffs.Length; i++)
                    {
                        var buffEntity = buffs[i].Entity;
                        if (buffEntity == Entity.Null || !em.Exists(buffEntity) || !em.HasComponent<PrefabGUID>(buffEntity))
                        {
                            continue;
                        }

                        var buff = em.GetComponentData<PrefabGUID>(buffEntity);
                        if (TryRemoveBuff(targetEntity, buff))
                        {
                            removed++;
                        }
                    }
                }

                return removed > 0;
            }
            catch (Exception ex)
            {
                Log.LogDebug($"TryCleanBuffs failed: {ex.Message}");
                return false;
            }
        }

        private static bool TryGetEntityArg(object[]? args, int index, out Entity entity)
        {
            entity = Entity.Null;
            if (args == null || index < 0 || index >= args.Length || args[index] == null)
            {
                return false;
            }

            var value = args[index];
            if (value is Entity e)
            {
                entity = e;
                return true;
            }

            return false;
        }

        private static bool TryGetPrefabGuidArg(object[]? args, int index, out PrefabGUID guid)
        {
            guid = PrefabGUID.Empty;
            if (args == null || index < 0 || index >= args.Length || args[index] == null)
            {
                return false;
            }

            var value = args[index];
            switch (value)
            {
                case PrefabGUID prefabGuid:
                    guid = prefabGuid;
                    return guid != PrefabGUID.Empty;
                case int hash:
                    guid = new PrefabGUID(hash);
                    return guid != PrefabGUID.Empty;
                case string str when int.TryParse(str, out var parsed):
                    guid = new PrefabGUID(parsed);
                    return guid != PrefabGUID.Empty;
                default:
                    return false;
            }
        }

        private static bool TryGetFloat3Arg(object[]? args, int index, out float3 value)
        {
            value = float3.zero;
            if (args == null || index < 0 || index >= args.Length || args[index] == null)
            {
                return false;
            }

            var raw = args[index];
            if (raw is float3 v)
            {
                value = v;
                return true;
            }

            return false;
        }

        private static bool TryGetFloatArg(object[]? args, int index, out float value)
        {
            value = 0f;
            if (args == null || index < 0 || index >= args.Length || args[index] == null)
            {
                return false;
            }

            var raw = args[index];
            switch (raw)
            {
                case float f:
                    value = f;
                    return true;
                case double d:
                    value = (float)d;
                    return true;
                case int i:
                    value = i;
                    return true;
                case string s when float.TryParse(s, out var parsed):
                    value = parsed;
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryGetStringArg(object[]? args, int index, out string value)
        {
            value = string.Empty;
            if (args == null || index < 0 || index >= args.Length || args[index] == null)
            {
                return false;
            }

            value = args[index]?.ToString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(value);
        }

        private static bool TryGetPlatformIdArg(object[]? args, int index, out ulong platformId)
        {
            platformId = 0;
            if (args == null || index < 0 || index >= args.Length || args[index] == null)
            {
                return false;
            }

            var raw = args[index];
            switch (raw)
            {
                case ulong ul:
                    platformId = ul;
                    return platformId != 0;
                case long l when l > 0:
                    platformId = (ulong)l;
                    return true;
                case int i when i > 0:
                    platformId = (ulong)i;
                    return true;
                case string s when ulong.TryParse(s, out var parsed):
                    platformId = parsed;
                    return platformId != 0;
                default:
                    return false;
            }
        }

        private static string TrimMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return string.Empty;
            }

            var clean = message.Trim();
            if (clean.Length > 512)
            {
                clean = clean[..512];
            }

            return clean.Replace("\r", " ").Replace("\n", " ");
        }
    }
}
