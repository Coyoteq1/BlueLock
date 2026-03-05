using System;
using System.Collections.Generic;
using HarmonyLib;
using ProjectM;
using ProjectM.Network;
using Stunlock.Network;
using Unity.Collections;
using Unity.Entities;
using VAutomationCore.Core;
using VAutomationCore.Core.Services;

namespace VAutomationCore.Core.Patches
{
    /// <summary>
    /// Player connectivity patches for VAutomationCore.
    /// </summary>
    [HarmonyPatch]
    internal static class PlayerConnectivityPatches
    {
        /// <summary>
        /// Connection reasons that should be ignored (user never fully connected).
        /// </summary>
        private static readonly HashSet<ConnectionStatusChangeReason> IgnoreReasons = new()
        {
            ConnectionStatusChangeReason.IncorrectPassword,
            ConnectionStatusChangeReason.ServerFull,
            ConnectionStatusChangeReason.Unknown,
            ConnectionStatusChangeReason.AuthenticationError,
            ConnectionStatusChangeReason.AuthSessionCancelled
        };

        [HarmonyPatch(typeof(ServerBootstrapSystem), nameof(ServerBootstrapSystem.OnUserConnected))]
        [HarmonyPostfix]
        private static void OnUserConnectedPostfix(ServerBootstrapSystem __instance, NetConnectionId netConnectionId)
        {
            if (!ServerReadySystem.Instance.IsServerReady)
            {
                ServerReadySystem.Instance.SetServerReady();
            }
            
            // Mark shard as taken when a player connects
            if (!ServerReadySystem.Instance.IsShardTaken)
            {
                ServerReadySystem.Instance.SetShardTaken();
            }

            try
            {
                // Validate server bootstrap system state
                if (__instance._NetEndPointToApprovedUserIndex == null || __instance._ApprovedUsersLookup == null)
                {
                    UnifiedCore.LogWarning("ServerBootstrapSystem instance or lookups are null.");
                    return;
                }

                // Get user index from connection ID
                if (!__instance._NetEndPointToApprovedUserIndex.TryGetValue(netConnectionId, out var index))
                {
                    UnifiedCore.LogWarning("Failed to get user index for connection.");
                    return;
                }

                // Validate user index bounds
                if (index < 0 || index >= __instance._ApprovedUsersLookup.Length)
                {
                    UnifiedCore.LogWarning("User index is out of bounds.");
                    return;
                }

                // Get client data
                var client = __instance._ApprovedUsersLookup[index];
                if (client == null || client.UserEntity.Equals(Entity.Null))
                {
                    UnifiedCore.LogWarning("Failed to get user entity.");
                    return;
                }

                // Emit connection event
                if (TypedEventBus.HasSubscribers<PlayerConnectedEvent>())
                {
                    TypedEventBus.Publish(new PlayerConnectedEvent
                    {
                        UserEntity = client.UserEntity,
                        UserIndex = index,
                        ConnectionId = netConnectionId
                    });
                }
            }
            catch (Exception e)
            {
                UnifiedCore.LogError($"Error processing player connection: {e.Message}");
            }
        }

        [HarmonyPatch(typeof(ServerBootstrapSystem), nameof(ServerBootstrapSystem.OnUserDisconnected))]
        [HarmonyPrefix]
        private static void OnUserDisconnectedPrefix(ServerBootstrapSystem __instance, NetConnectionId netConnectionId, ConnectionStatusChangeReason reason)
        {
            // Skip ignored reasons
            if (IgnoreReasons.Contains(reason)) return;

            if (!ServerReadySystem.Instance.IsServerReady)
            {
                ServerReadySystem.Instance.SetServerReady();
            }

            try
            {
                if (!__instance._NetEndPointToApprovedUserIndex.TryGetValue(netConnectionId, out var index))
                {
                    UnifiedCore.LogWarning("Failed to get user index for disconnection.");
                    return;
                }

                var client = __instance._ApprovedUsersLookup[index];
                if (client == null || client.UserEntity.Equals(Entity.Null))
                {
                    UnifiedCore.LogWarning("Failed to get user entity during disconnect.");
                    return;
                }

                // Emit disconnection event
                if (TypedEventBus.HasSubscribers<PlayerDisconnectedEvent>())
                {
                    TypedEventBus.Publish(new PlayerDisconnectedEvent
                    {
                        UserEntity = client.UserEntity,
                        UserIndex = index,
                        Reason = reason
                    });
                }
            }
            catch (Exception e)
            {
                UnifiedCore.LogError($"Error processing player disconnection: {e.Message}");
            }
        }
    }

    /// <summary>
    /// Chat message system patch for VAutomationCore.
    /// </summary>
    [HarmonyPatch]
    internal static class ChatMessagePatch
    {
        [HarmonyPatch(typeof(ChatMessageSystem), nameof(ChatMessageSystem.OnUpdate))]
        [HarmonyPrefix]
        private static void Prefix(ChatMessageSystem __instance)
        {
            if (!ServerReadySystem.Instance.IsServerReady) return;
            if (!TypedEventBus.HasSubscribers<ChatMessageEvent>()) return;

            var entities = __instance.__query_661171423_0.ToEntityArray(Allocator.Temp);
            try
            {
                if (entities.Length == 0) return;

                foreach (var entity in entities)
                {
                    if (!entity.Has<ChatMessage>()) continue;

                    var message = entity.Read<ChatMessage>();
                    TypedEventBus.Publish(new ChatMessageEvent
                    {
                        SenderEntity = entity.Has<FromCharacter>() ? entity.Read<FromCharacter>().Character : Entity.Null,
                        Message = message.Message.ToString(),
                        MessageType = message.MessageType
                    });
                }
            }
            catch (Exception e)
            {
                UnifiedCore.LogError($"Error processing chat message: {e.Message}");
            }
            finally
            {
                entities.Dispose();
            }
        }
    }

    /// <summary>
    /// Death event system patch for VAutomationCore.
    /// Uses existing PatchEvents.DeathOccurredEvent instead of creating new events.
    /// </summary>
    [HarmonyPatch]
    internal static class DeathEventPatch
    {
        [HarmonyPatch(typeof(DeathEventListenerSystem), nameof(DeathEventListenerSystem.OnUpdate))]
        [HarmonyPrefix]
        private static void Prefix(DeathEventListenerSystem __instance)
        {
            if (!ServerReadySystem.Instance.IsServerReady) return;
            if (!TypedEventBus.HasSubscribers<DeathOccurredEvent>()) return;

            var deathEvents = __instance._DeathEventQuery.ToEntityArray(Allocator.Temp);
            try
            {
                if (deathEvents.Length == 0) return;

                foreach (var entity in deathEvents)
                {
                    if (!entity.Has<DeathEvent>()) continue;

                    var deathEvent = entity.Read<DeathEvent>();
                    TypedEventBus.Publish(new DeathOccurredEvent
                    {
                        Killer = deathEvent.Killer,
                        Victim = deathEvent.Victim,
                        Reason = deathEvent.StatChangeReason
                    });
                }
            }
            catch (Exception e)
            {
                UnifiedCore.LogError($"Error processing death events: {e.Message}");
            }
            finally
            {
                deathEvents.Dispose();
            }
        }
    }

    /// <summary>
    /// Ability cast system patch for VAutomationCore.
    /// </summary>
    [HarmonyPatch]
    internal static class AbilityCastPatch
    {
        [HarmonyPatch(typeof(AbilityRunScriptsSystem), nameof(AbilityRunScriptsSystem.OnUpdate))]
        [HarmonyPriority(Priority.First)]
        [HarmonyPrefix]
        private static void Prefix(AbilityRunScriptsSystem __instance)
        {
            if (!ServerReadySystem.Instance.IsServerReady) return;
            if (!TypedEventBus.HasSubscribers<AbilityCastEvent>()) return;

            // Cast started events
            var castStarted = __instance._OnCastStartedQuery.ToEntityArray(Allocator.Temp);
            try
            {
                foreach (var entity in castStarted)
                {
                    // Emit basic ability cast event with entity data
                    // Components can be read by subscribers as needed
                    TypedEventBus.Publish(new AbilityCastEvent
                    {
                        Caster = entity,
                        AbilityGuid = PrefabGUID.Empty,
                        SlotIndex = -1
                    });
                }
            }
            finally
            {
                castStarted.Dispose();
            }
        }
    }

    /// <summary>
    /// Inventory change patches for VAutomationCore.
    /// </summary>
    [HarmonyPatch]
    internal static class InventoryPatch
    {
        [HarmonyPatch(typeof(ReactToInventoryChangedSystem), nameof(ReactToInventoryChangedSystem.OnUpdate))]
        [HarmonyPrefix]
        private static void Prefix(ReactToInventoryChangedSystem __instance)
        {
            if (!ServerReadySystem.Instance.IsServerReady) return;
            if (!TypedEventBus.HasSubscribers<InventoryChangeEvent>()) return;

            var query = __instance.__query_2096870026_0.ToEntityArray(Allocator.Temp);
            try
            {
                if (query.Length == 0) return;

                foreach (var entity in query)
                {
                    if (!entity.Has<InventoryChangedEvent>()) continue;
                    var evt = entity.Read<InventoryChangedEvent>();

                    TypedEventBus.Publish(new InventoryChangeEvent
                    {
                        Player = evt.Owner,
                        ItemEntity = entity,
                        Operation = evt.Operation
                    });
                }
            }
            catch (Exception e)
            {
                UnifiedCore.LogError($"Error in InventoryPatch: {e.Message}");
            }
            finally
            {
                query.Dispose();
            }
        }
    }

    /// <summary>
    /// Shapeshift system patch for VAutomationCore.
    /// </summary>
    [HarmonyPatch]
    internal static class ShapeshiftPatch
    {
        [HarmonyPatch(typeof(ShapeshiftSystem), nameof(ShapeshiftSystem.OnUpdate))]
        [HarmonyPrefix]
        private static void Prefix(ShapeshiftSystem __instance)
        {
            if (!ServerReadySystem.Instance.IsServerReady) return;
            if (!TypedEventBus.HasSubscribers<ShapeshiftEvent>()) return;

            var entities = __instance._Query.ToEntityArray(Allocator.Temp);
            try
            {
                if (entities.Length == 0) return;

                foreach (var entity in entities)
                {
                    if (!entity.Has<ShapeshiftEvent>()) continue;
                    var evt = entity.Read<ShapeshiftEvent>();
                    TypedEventBus.Publish(new ShapeshiftEventData
                    {
                        Player = evt.Character,
                        FormGuid = evt.ShapeshiftForm,
                        IsTransforming = evt.State == ShapeshiftState.Shapeshift
                    });
                }
            }
            catch (Exception e)
            {
                UnifiedCore.LogError($"Error processing ShapeshiftSystem: {e.Message}");
            }
            finally
            {
                entities.Dispose();
            }
        }
    }

    /// <summary>
    /// Waypoint teleport patch for VAutomationCore.
    /// </summary>
    [HarmonyPatch]
    internal static class WaypointPatch
    {
        [HarmonyPatch(typeof(TeleportToWaypointEventSystem), nameof(TeleportToWaypointEventSystem.OnUpdate))]
        [HarmonyPrefix]
        private static void Prefix(TeleportToWaypointEventSystem __instance)
        {
            if (!ServerReadySystem.Instance.IsServerReady) return;
            if (!TypedEventBus.HasSubscribers<WaypointTeleportEvent>()) return;

            var query = __instance.__query_1956534509_0.ToEntityArray(Allocator.Temp);
            try
            {
                if (query.Length == 0) return;

                foreach (var entity in query)
                {
                    if (!entity.Has<TeleportWaypointEvent>()) continue;
                    var evt = entity.Read<TeleportWaypointEvent>();
                    TypedEventBus.Publish(new WaypointTeleportEvent
                    {
                        Player = evt.Character,
                        WaypointIndex = evt.WaypointIndex
                    });
                }
            }
            catch (Exception e)
            {
                UnifiedCore.LogError($"Error processing TeleportToWaypointEventSystem: {e.Message}");
            }
            finally
            {
                query.Dispose();
            }
        }
    }

    /// <summary>
    /// Unit spawner patch for VAutomationCore.
    /// </summary>
    [HarmonyPatch]
    internal static class UnitSpawnerPatch
    {
        [HarmonyPatch(typeof(UnitSpawnerReactSystem), nameof(UnitSpawnerReactSystem.OnUpdate))]
        [HarmonyPrefix]
        private static void Prefix(UnitSpawnerReactSystem __instance)
        {
            if (!ServerReadySystem.Instance.IsServerReady) return;
            if (!TypedEventBus.HasSubscribers<UnitSpawnedPatchEvent>()) return;

            var entities = __instance._Query.ToEntityArray(Allocator.Temp);
            try
            {
                if (entities.Length == 0) return;

                foreach (var entity in entities)
                {
                    if (!entity.Has<LifeTime>()) continue;
                    var lifetime = entity.Read<LifeTime>();
                    var prefabGuid = entity.GetPrefabGuid();

                    TypedEventBus.Publish(new UnitSpawnedPatchEvent
                    {
                        SpawnedUnit = entity,
                        Spawner = Entity.Null, // Would need FromSpawnerComponent
                        PrefabGuid = prefabGuid,
                        Duration = lifetime.Duration
                    });
                }
            }
            catch (Exception e)
            {
                UnifiedCore.LogError($"Error in UnitSpawnerPatch: {e.Message}");
            }
            finally
            {
                entities.Dispose();
            }
        }
    }

    /// <summary>
    /// Save system patch for VAutomationCore.
    /// </summary>
    [HarmonyPatch]
    internal static class SavePatch
    {
        [HarmonyPatch(typeof(TriggerPersistenceSaveSystem), nameof(TriggerPersistenceSaveSystem.TriggerSave))]
        [HarmonyPrefix]
        public static void Prefix(TriggerPersistenceSaveSystem __instance, SaveReason reason, FixedString128Bytes saveName, ServerRuntimeSettings saveConfig)
        {
            if (!TypedEventBus.HasSubscribers<ServerSaveEvent>()) return;
            TypedEventBus.Publish(new ServerSaveEvent
            {
                SaveName = saveName.Value,
                Reason = reason
            });
        }
    }

    /// <summary>
    /// Travel buff destroy patch (character spawn detection) for VAutomationCore.
    /// </summary>
    [HarmonyPatch]
    internal static class TravelBuffDestroyPatch
    {
        // Prefab GUID for travel buff (722466953) - from ScarletCore
        private const int TravelBuffGuid = 722466953;

        [HarmonyPatch(typeof(Destroy_TravelBuffSystem), nameof(Destroy_TravelBuffSystem.OnUpdate))]
        [HarmonyPostfix]
        private static void Postfix(Destroy_TravelBuffSystem __instance)
        {
            if (!ServerReadySystem.Instance.IsServerReady) return;

            var query = __instance.__query_615927226_0.ToEntityArray(Allocator.Temp);
            try
            {
                if (query.Length == 0) return;

                foreach (var entity in query)
                {
                    var guid = entity.GetPrefabGuid();
                    if (guid.GuidHash != TravelBuffGuid) continue;

                    var owner = entity.Read<EntityOwner>().Owner;
                    if (owner == Entity.Null || !owner.Has<User>()) continue;

                    // Emit travel buff destroyed event (character spawned)
                    if (TypedEventBus.HasSubscribers<TravelBuffDestroyedEvent>())
                    {
                        TypedEventBus.Publish(new TravelBuffDestroyedEvent
                        {
                            Player = owner,
                            CharacterEntity = entity
                        });
                    }
                }
            }
            catch (Exception e)
            {
                UnifiedCore.LogError($"Error in TravelBuffDestroyPatch: {e.Message}");
            }
            finally
            {
                query.Dispose();
            }
        }
    }
}
