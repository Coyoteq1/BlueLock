using System;
using System.Collections.Generic;
using HarmonyLib;
using ProjectM;
using Unity.Collections;
using Unity.Entities;
using VAutomationCore.Core.Events;
using VAutomationCore.Core.Services;

namespace VAutomationCore.Core.Patches
{
    /// <summary>
    /// Base class for VAutomationCore patches with proper initialization guards and event emission.
    /// Follows patterns from ScarletCore but integrates with VAutomationCore's TypedEventBus and ServerReadySystem.
    /// </summary>
    public abstract class VAutoPatch
    {
        /// <summary>
        /// Check if the server is ready for patch execution.
        /// Uses VAutomationCore's ServerReadySystem instead of custom GameSystems.
        /// </summary>
        protected static bool IsServerReady => ServerReadySystem.Instance.IsServerReady;

        /// <summary>
        /// Check if there are subscribers for a typed event before allocating NativeArray.
        /// </summary>
        protected static bool HasEventSubscribers<T>()
        {
            return TypedEventBus.HasSubscribers<T>();
        }

        /// <summary>
        /// Safely emit a typed event with automatic subscriber count checking.
        /// </summary>
        protected static void EmitEvent<T>(T eventData) where T : class
        {
            if (TypedEventBus.HasSubscribers<T>())
            {
                TypedEventBus.Publish(eventData);
            }
        }

        /// <summary>
        /// Safely emit a typed event with entities array.
        /// </summary>
        protected static void EmitEntitiesEvent<T>(NativeArray<Entity> entities, Func<Entity, T> eventFactory) where T : class
        {
            if (entities.Length == 0 || !TypedEventBus.HasSubscribers<T>())
            {
                return;
            }

            foreach (var entity in entities)
            {
                var evt = eventFactory(entity);
                TypedEventBus.Publish(evt);
            }
        }
    }

    /// <summary>
    /// Player connection event data for VAutomationCore events.
    /// </summary>
    public sealed class PlayerConnectedEvent
    {
        public required Entity UserEntity { get; init; }
        public required int UserIndex { get; init; }
        public required NetConnectionId ConnectionId { get; init; }
    }

    /// <summary>
    /// Player disconnection event data.
    /// </summary>
    public sealed class PlayerDisconnectedEvent
    {
        public required Entity UserEntity { get; init; }
        public required int UserIndex { get; init; }
        public required ConnectionStatusChangeReason Reason { get; init; }
    }

    /// <summary>
    /// Chat message event data.
    /// </summary>
    public sealed class ChatMessageEvent
    {
        public required Entity SenderEntity { get; init; }
        public required string Message { get; init; }
        public required ChatMessageType MessageType { get; init; }
    }

    /// <summary>
    /// Ability cast event data.
    /// </summary>
    public sealed class AbilityCastEvent
    {
        public required Entity Caster { get; init; }
        public required PrefabGUID AbilityGuid { get; init; }
        public required int SlotIndex { get; init; }
    }

    /// <summary>
    /// Shapeshift event data.
    /// </summary>
    public sealed class ShapeshiftEvent
    {
        public required Entity Player { get; init; }
        public required PrefabGUID FormGuid { get; init; }
        public required bool IsTransforming { get; init; }
    }

    /// <summary>
    /// Waypoint teleport event data.
    /// </summary>
    public sealed class WaypointTeleportEvent
    {
        public required Entity Player { get; init; }
        public required int WaypointIndex { get; init; }
    }

    /// <summary>
    /// Inventory change event data.
    /// </summary>
    public sealed class InventoryChangeEvent
    {
        public required Entity Player { get; init; }
        public required Entity ItemEntity { get; init; }
        public required InventoryChangeOperation Operation { get; init; }
    }

    /// <summary>
    /// Unit spawn event data.
    /// </summary>
    public sealed class UnitSpawnedPatchEvent
    {
        public required Entity SpawnedUnit { get; init; }
        public required Entity Spawner { get; init; }
        public required PrefabGUID PrefabGuid { get; init; }
        public required float Duration { get; init; }
    }

    /// <summary>
    /// War event data.
    /// </summary>
    public sealed class WarEventData
    {
        public required Entity EventEntity { get; init; }
        public required string WarName { get; init; }
        public required int AttackerCount { get; init; }
        public required int DefenderCount { get; init; }
    }

    /// <summary>
    /// Save event data.
    /// </summary>
    public sealed class ServerSaveEvent
    {
        public required string SaveName { get; init; }
        public required SaveReason Reason { get; init; }
    }

    /// <summary>
    /// Interaction event data.
    /// </summary>
    public sealed class InteractEvent
    {
        public required Entity Interactor { get; init; }
        public required Entity Target { get; init; }
        public required PrefabGUID TargetPrefab { get; init; }
    }

    /// <summary>
    /// Player downed event data.
    /// </summary>
    public sealed class PlayerDownedEvent
    {
        public required Entity Player { get; init; }
        public required Entity Attacker { get; init; }
    }

    /// <summary>
    /// Travel buff destroyed event (character spawn).
    /// </summary>
    public sealed class TravelBuffDestroyedEvent
    {
        public required Entity Player { get; init; }
        public required Entity CharacterEntity { get; init; }
    }
}
