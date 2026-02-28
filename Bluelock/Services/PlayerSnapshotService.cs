using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using ProjectM;
using ProjectM.Network;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VAuto.Zone.Core;
using VAuto.Zone.Models;
using VAutomationCore;
using VAutomationCore.Core;
using VAutomationCore.Core.ECS;

namespace VAuto.Zone.Services
{
    /// <summary>
    /// Stores and restores player data when entering/exiting the arena zone.
    /// All snapshot data is stored as pure data types - Entity access only during save/restore.
    /// Captures: position, rotation, health, blood, inventory contents, and equipment.
    /// </summary>
    /// <remarks>
    /// Thread-safe implementation using ConcurrentDictionary for snapshot storage.
    /// Component access uses TryGet pattern to avoid exceptions on missing components.
    /// </remarks>
    public static class PlayerSnapshotService
    {
        private static readonly string[] RestoreSystemTypeNames =
        {
            // Names taken from your shared systems list, focused on restore-critical paths.
            "ProjectM.GameDataSystem",
            "ProjectM.GiveInventoryItemCommandSystem",
            "ProjectM.EquipItemFromInventorySystem",
            "ProjectM.EquipItemSystem",
            "ProjectM.BuffSystem_Spawn_Server",
            "ProjectM.UpdateBuffsBuffer_Destroy",
            "ProjectM.ReplaceAbilityOnSlotSystem",
            "ProjectM.Update_ReplaceAbilityOnSlotSystem",
            "ProjectM.Shared.SpellModCollectionSystem"
        };

        // Using ConcurrentDictionary eliminates need for manual locking
        private static readonly ConcurrentDictionary<Entity, PlayerSnapshot> _snapshots = new ConcurrentDictionary<Entity, PlayerSnapshot>();
        private static readonly ConcurrentDictionary<Type, AddItemReflectionInfo> _addItemInfoCache = new();
        private static readonly ConcurrentDictionary<Type, byte> _warnedUnknownAddItemTypes = new();

        // Cache for reflection-based system lookups to avoid repeated expensive operations
        private static readonly Dictionary<string, Type> _systemTypeCache = new Dictionary<string, Type>(RestoreSystemTypeNames.Length);
        private static MethodInfo _getSystemMethod;
        private static bool _reflectionInitialized;

        private sealed class AddItemReflectionInfo
        {
            public Func<object, bool?> SuccessReader { get; init; }
            public Func<object, bool?> FailureReader { get; init; }
            public Func<object, int> AmountReader { get; init; }
            public Func<object, Entity> EntityReader { get; init; }
            public Func<object, string> ResultTextReader { get; init; }
        }

        /// <summary>
        /// Result type for snapshot operations combining success state with error information.
        /// </summary>
        public readonly struct SnapshotResult
        {
            public bool Success { get; }
            public string Error { get; }

            private SnapshotResult(bool success, string error)
            {
                Success = success;
                Error = error;
            }

            public static SnapshotResult Ok() => new SnapshotResult(true, string.Empty);
            public static SnapshotResult Fail(string error) => new SnapshotResult(false, error);
            
            // Implicit conversion to bool for backward compatibility
            public static implicit operator bool(SnapshotResult result) => result.Success;
        }

        /// <summary>
        /// Stores all player data before arena entry.
        /// </summary>
        /// <param name="playerEntity">The entity to snapshot.</param>
        /// <param name="error">Error message if operation failed.</param>
        /// <returns>True if snapshot was saved successfully.</returns>
        public static bool SaveSnapshot(Entity playerEntity, out string error)
        {
            var result = SaveSnapshotCore(playerEntity);
            error = result.Error;
            return result.Success;
        }

        /// <summary>
        /// Stores all player data before arena entry using Result pattern.
        /// </summary>
        /// <param name="playerEntity">The entity to snapshot.</param>
        /// <returns>Result indicating success or failure with error details.</returns>
        public static SnapshotResult SaveSnapshot(Entity playerEntity)
        {
            return SaveSnapshotCore(playerEntity);
        }

        private static SnapshotResult SaveSnapshotCore(Entity playerEntity)
        {
            var em = ZoneCore.EntityManager;
            
            if (!em.Exists(playerEntity))
            {
                var error = "Entity no longer exists";
                ZoneCore.LogWarning($"[Snapshot] Save failed: {error}");
                return SnapshotResult.Fail(error);
            }

            try
            {
                var snapshot = new PlayerSnapshot
                {
                    EntityIndex = playerEntity.Index,
                    Timestamp = DateTime.UtcNow
                };

                // Position: Support both LocalTransform and Translation using TryGet for efficiency
                // LocalTransform is the newer Unity ECS component, Translation is legacy
                if (TryGetComponent(em, playerEntity, out LocalTransform transform))
                {
                    snapshot.Position = transform.Position;
                    snapshot.Rotation = transform.Rotation;
                }
                else if (TryGetComponent(em, playerEntity, out Translation translation))
                {
                    snapshot.Position = translation.Value;
                    if (TryGetComponent(em, playerEntity, out Rotation rotation))
                    {
                        snapshot.Rotation = rotation.Value;
                    }
                }

                // Health: Capture current health value
                if (TryGetComponent(em, playerEntity, out Health health))
                {
                    snapshot.Health = health.Value;
                }

                // Blood: Capture type and quality
                if (TryGetComponent(em, playerEntity, out Blood blood))
                {
                    snapshot.BloodType = blood.BloodType;
                    snapshot.BloodQuality = blood.Quality;
                }

                var inventorySnapshots = new Dictionary<int, int>();
                var equipmentSnapshots = new Dictionary<int, int>();

                MergeInventorySnapshots(inventorySnapshots, CaptureInventoryItems(em, playerEntity));
                MergeInventorySnapshots(equipmentSnapshots, CaptureEquipmentItems(em, playerEntity));

                if (TryGetUserEntityFromCharacter(em, playerEntity, out var userEntity))
                {
                    MergeInventorySnapshots(inventorySnapshots, CaptureInventoryItems(em, userEntity));
                    MergeInventorySnapshots(equipmentSnapshots, CaptureEquipmentItems(em, userEntity));
                }

                snapshot.InventoryItems = ToInventorySnapshotList(inventorySnapshots);
                snapshot.EquipmentItems = ToInventorySnapshotList(equipmentSnapshots);

                _snapshots[playerEntity] = snapshot;
                ZoneCore.LogInfo($"[Snapshot] Saved for Entity {playerEntity.Index} at {snapshot.Position} (Inventory items: {snapshot.InventoryItems.Count}, Equipment items: {snapshot.EquipmentItems.Count})");

                return SnapshotResult.Ok();
            }
            catch (Exception ex)
            {
                ZoneCore.LogError($"[Snapshot] Save failed: {ex}");
                return SnapshotResult.Fail(ex.Message);
            }
        }

        /// <summary>
        /// Restores player data after arena exit.
        /// </summary>
        /// <param name="playerEntity">The entity to restore.</param>
        /// <param name="error">Error message if operation failed.</param>
        /// <returns>True if snapshot was restored successfully.</returns>
        public static bool RestoreSnapshot(Entity playerEntity, out string error)
        {
            var result = RestoreSnapshotCore(playerEntity);
            error = result.Error;
            return result.Success;
        }

        /// <summary>
        /// Restores player data after arena exit using Result pattern.
        /// </summary>
        /// <param name="playerEntity">The entity to restore.</param>
        /// <returns>Result indicating success or failure with error details.</returns>
        public static SnapshotResult RestoreSnapshot(Entity playerEntity)
        {
            return RestoreSnapshotCore(playerEntity);
        }

        private static SnapshotResult RestoreSnapshotCore(Entity playerEntity)
        {
            var em = ZoneCore.EntityManager;

            if (!em.Exists(playerEntity))
            {
                var error = "Entity no longer exists";
                ZoneCore.LogWarning($"[Snapshot] Restore failed: {error}");
                return SnapshotResult.Fail(error);
            }

            return ExecuteWithRetention(playerEntity, snapshot =>
            {
                LogRestoreSystemAvailability();
                var itemsTracked = snapshot.InventoryItems.Count;
                var equipmentTracked = snapshot.EquipmentItems.Count;
                var positionRestored = false;

                // Clear arena-applied loadout before restoring snapshot state.
                Entity secondaryEntity = Entity.Null;
                _ = TryGetUserEntityFromCharacter(em, playerEntity, out secondaryEntity);
                TryClearCurrentLoadout(em, playerEntity, secondaryEntity, out var clearedInventoryItems, out var clearedEquipmentItems);

                var restoredInventoryEntries = RestoreInventorySnapshots(playerEntity, snapshot.InventoryItems, "inventory");
                var restoredEquipmentEntries = RestoreInventorySnapshots(playerEntity, snapshot.EquipmentItems, "equipment");

                // Restore position using LocalTransform (newer) or Translation (legacy)
                if (TryGetComponent(em, playerEntity, out LocalTransform transform))
                {
                    transform.Position = snapshot.Position;
                    transform.Rotation = snapshot.Rotation;
                    em.SetComponentData(playerEntity, transform);
                    positionRestored = true;
                }
                else if (TryGetComponent(em, playerEntity, out Translation translation))
                {
                    translation.Value = snapshot.Position;
                    em.SetComponentData(playerEntity, translation);

                    if (TryGetComponent(em, playerEntity, out Rotation rotation))
                    {
                        rotation.Value = snapshot.Rotation;
                        em.SetComponentData(playerEntity, rotation);
                    }
                    positionRestored = true;
                }

                // Restore health (current value only - MaxHealth is determined by game state)
                if (TryGetComponent(em, playerEntity, out Health health))
                {
                    health.Value = snapshot.Health;
                    em.SetComponentData(playerEntity, health);
                    ZoneCore.LogInfo($"[Snapshot] Health restored: {health.Value}");
                }

                // Restore blood type and quality
                if (TryGetComponent(em, playerEntity, out Blood blood))
                {
                    blood.BloodType = snapshot.BloodType;
                    blood.Quality = snapshot.BloodQuality;
                    em.SetComponentData(playerEntity, blood);
                    ZoneCore.LogInfo($"[Snapshot] Blood restored: {blood.BloodType} quality {blood.Quality}");
                }

                ZoneCore.LogInfo($"[Snapshot] Restored for Entity {playerEntity.Index} at {snapshot.Position} " +
                                 $"(Position: {positionRestored}, Inventory tracked/restored: {itemsTracked}/{restoredInventoryEntries}, " +
                                 $"Equipment tracked/restored: {equipmentTracked}/{restoredEquipmentEntries}, " +
                                 $"Cleared inventory/equipment entities: {clearedInventoryItems}/{clearedEquipmentItems})");
                return SnapshotResult.Ok();
            }, message => ZoneCore.LogWarning(message));
        }

        internal static SnapshotResult ExecuteWithRetention(Entity playerEntity, Func<PlayerSnapshot, SnapshotResult> restoreFunc, Action<string> logWarn)
        {
            if (!_snapshots.TryGetValue(playerEntity, out var snapshot))
            {
                var error = "No snapshot found for this entity";
                logWarn?.Invoke($"[Snapshot] Restore failed: {error} (Entity: {playerEntity.Index})");
                return SnapshotResult.Fail(error);
            }

            try
            {
                var result = restoreFunc(snapshot);

                if (result.Success)
                {
                    _snapshots.TryRemove(playerEntity, out _);
                }

                return result;
            }
            catch (Exception ex)
            {
                logWarn?.Invoke($"[Snapshot] Restore failed for entity {playerEntity.Index}: {ex.Message}");
                return SnapshotResult.Fail(ex.Message);
            }
        }

        /// <summary>
        /// Clears all stored snapshots.
        /// </summary>
        public static void ClearAll()
        {
            int count = _snapshots.Count;
            _snapshots.Clear();
            if (count > 0)
            {
                ZoneCore.LogInfo($"[Snapshot] All cleared ({count} snapshots removed)");
            }
        }

        /// <summary>
        /// Gets the current number of stored snapshots.
        /// </summary>
        public static int Count => _snapshots.Count;

        /// <summary>
        /// Gets the snapshot for a player entity if it exists.
        /// </summary>
        /// <param name="playerEntity">The entity to look up.</param>
        /// <param name="snapshot">The snapshot if found.</param>
        /// <returns>True if a snapshot exists for this entity.</returns>
        public static bool TryGetSnapshot(Entity playerEntity, out PlayerSnapshot snapshot)
        {
            return _snapshots.TryGetValue(playerEntity, out snapshot);
        }

        /// <summary>
        /// Tries to get component data, returning false instead of throwing if component doesn't exist.
        /// </summary>
        private static bool TryGetComponent<T>(EntityManager em, Entity entity, out T component) where T : struct
        {
            if (em.HasComponent<T>(entity))
            {
                component = em.GetComponentData<T>(entity);
                return true;
            }
            component = default;
            return false;
        }

        private static List<InventorySnapshot> CaptureInventoryItems(EntityManager em, Entity playerEntity)
        {
            var snapshots = new Dictionary<int, int>();
            try
            {
                if (!em.Exists(playerEntity) || !em.HasBuffer<InventoryItem>(playerEntity))
                {
                    return EmptyInventoryItems;
                }

                var buffer = em.GetBuffer<InventoryItem>(playerEntity);
                for (var i = 0; i < buffer.Length; i++)
                {
                    var entry = buffer[i];
                    if (!TryGetInventoryItemEntity(entry, out var itemEntity) ||
                        itemEntity == Entity.Null ||
                        !em.Exists(itemEntity) ||
                        !em.HasComponent<PrefabGUID>(itemEntity))
                    {
                        continue;
                    }

                    var guidHash = em.GetComponentData<PrefabGUID>(itemEntity).GuidHash;
                    if (guidHash == 0)
                    {
                        continue;
                    }

                    var stackSize = TryGetInventoryStackSize(entry);
                    if (stackSize <= 0)
                    {
                        stackSize = 1;
                    }

                    snapshots[guidHash] = snapshots.TryGetValue(guidHash, out var current)
                        ? current + stackSize
                        : stackSize;
                }
            }
            catch (Exception ex)
            {
                ZoneCore.LogWarning($"[Snapshot] Inventory capture failed: {ex.Message}");
            }

            if (snapshots.Count == 0)
            {
                return EmptyInventoryItems;
            }

            return snapshots
                .Select(pair => new InventorySnapshot(pair.Key, pair.Value))
                .ToList();
        }

        private static List<InventorySnapshot> CaptureEquipmentItems(EntityManager em, Entity playerEntity)
        {
            var snapshots = new List<InventorySnapshot>();
            try
            {
                if (!em.Exists(playerEntity) || !em.HasComponent<Equipment>(playerEntity))
                {
                    return EmptyInventoryItems;
                }

                var equipment = em.GetComponentData<Equipment>(playerEntity);
                var equippedEntities = new NativeList<Entity>(Allocator.Temp);
                try
                {
                    equipment.GetAllEquipmentEntities(equippedEntities);
                    for (var i = 0; i < equippedEntities.Length; i++)
                    {
                        var itemEntity = equippedEntities[i];
                        if (itemEntity == Entity.Null ||
                            !em.Exists(itemEntity) ||
                            !em.HasComponent<PrefabGUID>(itemEntity))
                        {
                            continue;
                        }

                        var guidHash = em.GetComponentData<PrefabGUID>(itemEntity).GuidHash;
                        if (guidHash == 0)
                        {
                            continue;
                        }

                        snapshots.Add(new InventorySnapshot(guidHash, 1));
                    }
                }
                finally
                {
                    equippedEntities.Dispose();
                }
            }
            catch (Exception ex)
            {
                ZoneCore.LogWarning($"[Snapshot] Equipment capture failed: {ex.Message}");
            }

            return snapshots.Count > 0 ? snapshots : EmptyInventoryItems;
        }

        private static bool TryClearCurrentLoadout(EntityManager em, Entity playerEntity, Entity secondaryEntity, out int clearedInventoryItems, out int clearedEquipmentItems)
        {
            clearedInventoryItems = 0;
            clearedEquipmentItems = 0;

            try
            {
                var destroyed = new HashSet<Entity>();

                if (em.Exists(playerEntity) && em.HasBuffer<InventoryItem>(playerEntity))
                {
                    var inventoryItems = em.GetBuffer<InventoryItem>(playerEntity);
                    for (var i = 0; i < inventoryItems.Length; i++)
                    {
                        var entry = inventoryItems[i];
                        if (!TryGetInventoryItemEntity(entry, out var itemEntity) || itemEntity == Entity.Null || !em.Exists(itemEntity))
                        {
                            continue;
                        }

                        if (!destroyed.Add(itemEntity))
                        {
                            continue;
                        }

                        try
                        {
                            em.DestroyEntity(itemEntity);
                            clearedInventoryItems++;
                        }
                        catch
                        {
                            // Best effort only.
                        }
                    }
                }

                if (secondaryEntity != Entity.Null && em.Exists(secondaryEntity) && em.HasBuffer<InventoryItem>(secondaryEntity))
                {
                    var inventoryItems = em.GetBuffer<InventoryItem>(secondaryEntity);
                    for (var i = 0; i < inventoryItems.Length; i++)
                    {
                        var entry = inventoryItems[i];
                        if (!TryGetInventoryItemEntity(entry, out var itemEntity) || itemEntity == Entity.Null || !em.Exists(itemEntity))
                        {
                            continue;
                        }

                        if (!destroyed.Add(itemEntity))
                        {
                            continue;
                        }

                        try
                        {
                            em.DestroyEntity(itemEntity);
                            clearedInventoryItems++;
                        }
                        catch
                        {
                            // Best effort only.
                        }
                    }
                }

                if (em.Exists(playerEntity) && em.HasComponent<Equipment>(playerEntity))
                {
                    var equipment = em.GetComponentData<Equipment>(playerEntity);
                    var equippedEntities = new NativeList<Entity>(Allocator.Temp);
                    try
                    {
                        equipment.GetAllEquipmentEntities(equippedEntities);
                        for (var i = 0; i < equippedEntities.Length; i++)
                        {
                            var itemEntity = equippedEntities[i];
                            if (itemEntity == Entity.Null || !em.Exists(itemEntity))
                            {
                                continue;
                            }

                            if (!destroyed.Add(itemEntity))
                            {
                                continue;
                            }

                            try
                            {
                                em.DestroyEntity(itemEntity);
                                clearedEquipmentItems++;
                            }
                            catch
                            {
                                // Best effort only.
                            }
                        }
                    }
                    finally
                    {
                        equippedEntities.Dispose();
                    }
                }

                if (secondaryEntity != Entity.Null && em.Exists(secondaryEntity) && em.HasComponent<Equipment>(secondaryEntity))
                {
                    var equipment = em.GetComponentData<Equipment>(secondaryEntity);
                    var equippedEntities = new NativeList<Entity>(Allocator.Temp);
                    try
                    {
                        equipment.GetAllEquipmentEntities(equippedEntities);
                        for (var i = 0; i < equippedEntities.Length; i++)
                        {
                            var itemEntity = equippedEntities[i];
                            if (itemEntity == Entity.Null || !em.Exists(itemEntity))
                            {
                                continue;
                            }

                            if (!destroyed.Add(itemEntity))
                            {
                                continue;
                            }

                            try
                            {
                                em.DestroyEntity(itemEntity);
                                clearedEquipmentItems++;
                            }
                            catch
                            {
                                // Best effort only.
                            }
                        }
                    }
                    finally
                    {
                        equippedEntities.Dispose();
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                ZoneCore.LogWarning($"[Snapshot] Loadout clear failed: {ex.Message}");
                return false;
            }
        }

        private static int RestoreInventorySnapshots(Entity playerEntity, IReadOnlyList<InventorySnapshot> snapshots, string sourceLabel)
        {
            if (snapshots == null || snapshots.Count == 0)
            {
                return 0;
            }

            if (!TryGetAddItemSettings(out var itemSettings, out var settingsError))
            {
                ZoneCore.LogWarning($"[Snapshot] Unable to restore {sourceLabel}: {settingsError}");
                return 0;
            }

            var restored = 0;
            for (var i = 0; i < snapshots.Count; i++)
            {
                var snapshot = snapshots[i];
                if (snapshot == null || snapshot.ItemGuid == 0 || snapshot.StackSize <= 0)
                {
                    continue;
                }

                var guid = new PrefabGUID(snapshot.ItemGuid);
                var amount = Math.Max(1, snapshot.StackSize);

                try
                {
                    var addResult = InventoryUtilitiesServer.TryAddItem(itemSettings, playerEntity, guid, amount);
                    if (IsAddItemResultSuccessful(addResult))
                    {
                        restored++;
                        continue;
                    }

                    ZoneCore.LogWarning($"[Snapshot] Restore {sourceLabel} entry failed for guid={guid.GuidHash} amount={amount}.");
                }
                catch (Exception ex)
                {
                    ZoneCore.LogWarning($"[Snapshot] Restore {sourceLabel} entry exception for guid={guid.GuidHash} amount={amount}: {ex.Message}");
                }
            }

            return restored;
        }

        private static bool TryGetAddItemSettings(out AddItemSettings itemSettings, out string error)
        {
            itemSettings = default;
            error = string.Empty;

            try
            {
                var world = UnifiedCore.Server;
                if (world == null || !world.IsCreated)
                {
                    error = "Server world unavailable.";
                    return false;
                }

                var gameData = world.GetExistingSystemManaged<GameDataSystem>();
                if (gameData == null)
                {
                    error = "GameDataSystem unavailable.";
                    return false;
                }

                itemSettings = AddItemSettings.Create(UnifiedCore.EntityManager, gameData.ItemHashLookupMap);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool IsAddItemResultSuccessful(object addResult)
        {
            if (addResult == null)
            {
                return false;
            }

            var info = _addItemInfoCache.GetOrAdd(addResult.GetType(), BuildAddItemReflectionInfo);

            var failed = info.FailureReader(addResult);
            if (failed.HasValue && failed.Value)
            {
                return false;
            }

            var success = info.SuccessReader(addResult);
            if (success.HasValue)
            {
                return success.Value;
            }

            var amount = info.AmountReader(addResult);
            if (amount > 0)
            {
                return true;
            }

            var entity = info.EntityReader(addResult);
            if (entity != Entity.Null)
            {
                return true;
            }

            // Unknown result type: warn once per type, pessimistic false.
            if (_warnedUnknownAddItemTypes.TryAdd(addResult.GetType(), 1))
            {
                var resultText = info.ResultTextReader(addResult);
                ZoneCore.LogWarning($"[Snapshot] Unknown add-item result type '{addResult.GetType().FullName}', treating as failure. Inspectable result: {resultText}");
            }

            return false;
        }

        private static AddItemReflectionInfo BuildAddItemReflectionInfo(Type type)
        {
            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            bool? SuccessReader(object value) => TryReadBoolMember(type, value, "Success", "Succeeded", "WasSuccessful", "Ok", "IsSuccess");
            bool? FailureReader(object value) => TryReadBoolMember(type, value, "Failed", "Error", "IsError", "HasFailed");
            int AmountReader(object value) => TryReadIntMember(type, value, "Amount", "AmountAdded", "AddedAmount", "Count");
            Entity EntityReader(object value) => TryReadEntityMember(type, value, "NewEntity", "ItemEntity", "Entity");

            string ResultTextReader(object value)
            {
                try
                {
                    var prop = type.GetProperty("Result", Flags);
                    var val = prop?.GetValue(value);
                    return val?.ToString() ?? string.Empty;
                }
                catch
                {
                    return string.Empty;
                }
            }

            return new AddItemReflectionInfo
            {
                SuccessReader = SuccessReader,
                FailureReader = FailureReader,
                AmountReader = AmountReader,
                EntityReader = EntityReader,
                ResultTextReader = ResultTextReader
            };
        }

        private static bool TryGetInventoryItemEntity(InventoryItem entry, out Entity itemEntity)
        {
            itemEntity = Entity.Null;

            try
            {
                var boxed = (object)entry;
                var type = boxed.GetType();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                foreach (var memberName in new[] { "ItemEntity", "Entity", "Item", "ItemId" })
                {
                    var property = type.GetProperty(memberName, flags);
                    if (property?.GetValue(boxed) is Entity propertyEntity)
                    {
                        itemEntity = propertyEntity;
                        break;
                    }

                    var field = type.GetField(memberName, flags);
                    if (field?.GetValue(boxed) is Entity fieldEntity)
                    {
                        itemEntity = fieldEntity;
                        break;
                    }
                }
            }
            catch
            {
                // Reflection fallback only.
            }

            return itemEntity != Entity.Null;
        }

        private static int TryGetInventoryStackSize(InventoryItem entry)
        {
            return Math.Max(1, TryReadIntMember(entry, "Amount", "StackSize", "Stack", "Count", "Quantity"));
        }

        private static bool TryGetUserEntityFromCharacter(EntityManager em, Entity characterEntity, out Entity userEntity)
        {
            userEntity = Entity.Null;
            try
            {
                if (!em.Exists(characterEntity) || !em.HasComponent<PlayerCharacter>(characterEntity))
                {
                    return false;
                }

                var pc = em.GetComponentData<PlayerCharacter>(characterEntity);
                if (pc.UserEntity == Entity.Null || !em.Exists(pc.UserEntity))
                {
                    return false;
                }

                userEntity = pc.UserEntity;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void MergeInventorySnapshots(IDictionary<int, int> aggregate, IReadOnlyList<InventorySnapshot> snapshots)
        {
            if (aggregate == null || snapshots == null)
            {
                return;
            }

            for (var i = 0; i < snapshots.Count; i++)
            {
                var row = snapshots[i];
                if (row == null || row.ItemGuid == 0 || row.StackSize <= 0)
                {
                    continue;
                }

                aggregate[row.ItemGuid] = aggregate.TryGetValue(row.ItemGuid, out var existing)
                    ? existing + row.StackSize
                    : row.StackSize;
            }
        }

        private static List<InventorySnapshot> ToInventorySnapshotList(IDictionary<int, int> aggregate)
        {
            if (aggregate == null || aggregate.Count == 0)
            {
                return EmptyInventoryItems;
            }

            return aggregate
                .Where(kv => kv.Key != 0 && kv.Value > 0)
                .Select(kv => new InventorySnapshot(kv.Key, kv.Value))
                .ToList();
        }

        private static int TryReadIntMember(object value, params string[] memberNames)
        {
            if (value == null || memberNames == null || memberNames.Length == 0)
            {
                return 0;
            }

            return TryReadIntMember(value.GetType(), value, memberNames);
        }

        private static int TryReadIntMember(Type type, object value, params string[] memberNames)
        {
            if (type == null || value == null || memberNames == null)
            {
                return 0;
            }

            foreach (var memberName in memberNames)
            {
                if (string.IsNullOrWhiteSpace(memberName))
                {
                    continue;
                }

                try
                {
                    var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                    var property = type.GetProperty(memberName, flags);
                    if (property?.GetValue(value) is object raw)
                    {
                        if (raw is int i) return i;
                        if (raw is uint u) return unchecked((int)u);
                        if (raw is long l) return (int)Math.Clamp(l, int.MinValue, int.MaxValue);
                        if (raw is ulong ul) return (int)Math.Clamp((long)Math.Min(ul, int.MaxValue), int.MinValue, int.MaxValue);
                        if (int.TryParse(raw.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                        {
                            return parsed;
                        }
                    }

                    var field = type.GetField(memberName, flags);
                    if (field?.GetValue(value) is object fieldRaw)
                    {
                        if (fieldRaw is int fi) return fi;
                        if (fieldRaw is uint fu) return unchecked((int)fu);
                        if (fieldRaw is long fl) return (int)Math.Clamp(fl, int.MinValue, int.MaxValue);
                        if (fieldRaw is ulong ful) return (int)Math.Clamp((long)Math.Min(ful, int.MaxValue), int.MinValue, int.MaxValue);
                        if (int.TryParse(fieldRaw.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var fieldParsed))
                        {
                            return fieldParsed;
                        }
                    }
                }
                catch
                {
                    // Ignore individual member lookup issues.
                }
            }

            return 0;
        }

        private static bool? TryReadBoolMember(Type type, object value, params string[] memberNames)
        {
            if (type == null || value == null || memberNames == null)
            {
                return null;
            }

            foreach (var memberName in memberNames)
            {
                if (string.IsNullOrWhiteSpace(memberName))
                {
                    continue;
                }

                try
                {
                    var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                    var property = type.GetProperty(memberName, flags);
                    if (property?.GetValue(value) is bool b)
                    {
                        return b;
                    }

                    var field = type.GetField(memberName, flags);
                    if (field?.GetValue(value) is bool fb)
                    {
                        return fb;
                    }
                }
                catch
                {
                    // Ignore member lookup failure.
                }
            }

            return null;
        }

        private static Entity TryReadEntityMember(Type type, object value, params string[] memberNames)
        {
            if (type == null || value == null || memberNames == null)
            {
                return Entity.Null;
            }

            foreach (var memberName in memberNames)
            {
                if (string.IsNullOrWhiteSpace(memberName))
                {
                    continue;
                }

                try
                {
                    var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                    var property = type.GetProperty(memberName, flags);
                    if (property?.GetValue(value) is Entity pe)
                    {
                        return pe;
                    }

                    var field = type.GetField(memberName, flags);
                    if (field?.GetValue(value) is Entity fe)
                    {
                        return fe;
                    }
                }
                catch
                {
                    // Ignore member lookup failure.
                }
            }

            return Entity.Null;
        }


        // Pre-allocated empty list to avoid allocations for unused inventory
        private static readonly List<InventorySnapshot> EmptyInventoryItems = new List<InventorySnapshot>(0);

        private static void LogRestoreSystemAvailability()
        {
            try
            {
                var world = UnifiedCore.Server;
                if (world == null)
                {
                    ZoneCore.LogWarning("[Snapshot] Restore systems check: server world is null.");
                    return;
                }

                // Initialize reflection cache once
                InitializeReflectionCache(world);

                if (_getSystemMethod == null)
                {
                    ZoneCore.LogWarning("[Snapshot] Restore systems check: GetExistingSystemManaged<T>() not found.");
                    return;
                }

                foreach (var typeName in RestoreSystemTypeNames)
                {
                    // Use cached type lookup
                    if (!_systemTypeCache.TryGetValue(typeName, out var systemType))
                    {
                        systemType = AppDomain.CurrentDomain.GetAssemblies()
                            .Select(a => a.GetType(typeName, false))
                            .FirstOrDefault(t => t != null);
                        
                        _systemTypeCache[typeName] = systemType;
                    }

                    if (systemType == null)
                    {
                        ZoneCore.LogWarning($"[Snapshot] Restore system missing type: {typeName}");
                        continue;
                    }

                    try
                    {
                        var generic = _getSystemMethod.MakeGenericMethod(systemType);
                        var systemInstance = generic.Invoke(world, null);
                        var state = systemInstance != null ? "OK" : "NULL";
                        ZoneCore.LogInfo($"[Snapshot] Restore system {typeName}: {state}");
                    }
                    catch (Exception ex)
                    {
                        ZoneCore.LogWarning($"[Snapshot] Restore system {typeName}: ERROR ({ex.Message})");
                    }
                }
            }
            catch (Exception ex)
            {
                ZoneCore.LogWarning($"[Snapshot] Restore systems check failed: {ex.Message}");
            }
        }

        private static void InitializeReflectionCache(object world)
        {
            if (_reflectionInitialized) return;

            var worldType = world.GetType();
            _getSystemMethod = worldType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(m =>
                    string.Equals(m.Name, "GetExistingSystemManaged", StringComparison.Ordinal) &&
                    m.IsGenericMethodDefinition &&
                    m.GetParameters().Length == 0);

            _reflectionInitialized = true;
        }
    }

    /// <summary>
    /// Snapshot of a single inventory item.
    /// </summary>
    /// <remarks>
    /// Using record type for immutability and value equality semantics.
    /// </remarks>
    public record InventorySnapshot(int ItemGuid, int StackSize);

    /// <summary>
    /// Complete snapshot of player state for save/restore operations.
    /// </summary>
    /// <remarks>
    /// Using record type for immutability, value equality, and pattern matching support.
    /// </remarks>
    public class PlayerSnapshot
    {
        public int EntityIndex { get; init; }
        public DateTime Timestamp { get; init; }
        public float3 Position { get; set; }
        public quaternion Rotation { get; set; }
        public float Health { get; set; }
        public PrefabGUID BloodType { get; set; }
        public float BloodQuality { get; set; }
        public List<InventorySnapshot> InventoryItems { get; set; } = new List<InventorySnapshot>();
        public List<InventorySnapshot> EquipmentItems { get; set; } = new List<InventorySnapshot>();
    }
}
