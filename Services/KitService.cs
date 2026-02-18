using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using BepInEx;
using BepInEx.Logging;
using ProjectM;
using ProjectM.Network;
using Unity.Entities;
using Stunlock.Core;
using VAuto.Zone.Core;
using VAutomationCore.Core;
using VAutomationCore.Core.Config;
using VAutomationCore.Core.Services;
using VampireCommandFramework;
using VampireCommandFramework.Breadstone;

namespace VAuto.Zone.Services
{
    /// <summary>
    /// Zone kit orchestration service.
    /// - Loads kit definitions from JSON.
    /// - Resolves zone -> kit with default fallback.
    /// - Tracks active kit per player for enter/exit flow.
    /// </summary>
    public static class KitService
    {
        private sealed class KitDefinition
        {
            public string Id { get; set; } = string.Empty;
            public string Info { get; set; } = string.Empty;
            public bool RestoreOnExit { get; set; } = true;
            public bool BroadcastOnEquip { get; set; }
            public bool CaptureSnapshot { get; set; } = true;
            public List<KitItem> Items { get; } = new List<KitItem>();
        }

        private sealed class KitItem
        {
            public int PrefabGuid { get; set; }
            public string PrefabName { get; set; } = string.Empty;
            public int Amount { get; set; } = 1;
        }

        private sealed class KitConfigRoot
        {
            public Dictionary<string, KitDefinitionJson> Kits { get; set; } = new Dictionary<string, KitDefinitionJson>(StringComparer.OrdinalIgnoreCase);
        }

        private sealed class KitDefinitionJson
        {
            public string? Info { get; set; }
            public bool RestoreOnExit { get; set; } = true;
            public bool BroadcastOnEquip { get; set; }
            public bool CaptureSnapshot { get; set; } = true;
            public List<KitItemJson> Items { get; set; } = new List<KitItemJson>();
        }

        private sealed class KitItemJson
        {
            public int PrefabGuid { get; set; }
            public string PrefabName { get; set; } = string.Empty;
            public int Amount { get; set; } = 1;
        }

        private sealed class UsageState
        {
            public Dictionary<string, int> KitUsageCounts { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        private static readonly ManualLogSource _log = Plugin.Logger;
        private static readonly string[] RequiredDefaultKitIds = { "Kit1", "Kit2", "Kit3" };
        private static readonly Dictionary<string, KitDefinition> _kits = new Dictionary<string, KitDefinition>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<ulong, string> _activeKitByPlayer = new Dictionary<ulong, string>();
        private static readonly Dictionary<ulong, bool> _restoreOnExitByPlayer = new Dictionary<ulong, bool>();
        private static readonly Dictionary<ulong, bool> _snapshotCapturedByPlayer = new Dictionary<ulong, bool>();
        private static readonly Dictionary<int, ulong> _platformIdByPlayerIndex = new Dictionary<int, ulong>();
        private static readonly Dictionary<string, DateTime> _kitUsageHistory = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, int> _kitUsageCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private static bool _legacyKitsWarningLogged;

        private static readonly string UsageStatePath = Path.Combine(Paths.ConfigPath, "Bluelock", "state", "kit_usage.json");
        private static readonly bool LegacyKitsDisabled = false;
        public static bool IsLegacyKitsDisabled => LegacyKitsDisabled;

        // Optional integration hooks to clear player state before a kit is applied.
        // Wire these from host code to real implementations when available.
        public static Func<EntityManager, Entity, bool> ClearInventory { get; set; }
        public static Func<EntityManager, Entity, bool> ClearEquipment { get; set; }
        public static Func<EntityManager, Entity, bool> ClearBuffs { get; set; }
        public static Func<EntityManager, Entity, bool> ClearAbilities { get; set; }

        /// <summary>
        /// Initialize kit definitions.
        /// </summary>
        public static void Initialize()
        {
            if (LegacyKitsDisabled)
            {
                _kits.Clear();
                _activeKitByPlayer.Clear();
                _restoreOnExitByPlayer.Clear();
                _snapshotCapturedByPlayer.Clear();
                _platformIdByPlayerIndex.Clear();
                _kitUsageHistory.Clear();
                _kitUsageCounts.Clear();
                ResetAllUsage();
                if (!_legacyKitsWarningLogged)
                {
                    _legacyKitsWarningLogged = true;
                    _log.LogWarning($"[KitService] Legacy kits are deprecated and ignored. '{Plugin.KitsConfigPathValue}' will not be loaded.");
                }
                return;
            }

            LoadKitDefinitions();
            LoadUsageState();
            _log.LogInfo($"[KitService] Initialized. Kits loaded: {_kits.Count}");
        }

        /// <summary>
        /// Reload kit definitions from disk.
        /// </summary>
        public static void Reload()
        {
            if (LegacyKitsDisabled)
            {
                _log.LogDebug("[KitService] Reload skipped (legacy kits disabled).");
                return;
            }

            LoadKitDefinitions();
            _log.LogInfo($"[KitService] Reloaded kit definitions. Kits loaded: {_kits.Count}");
        }

        /// <summary>KitUsageCounts
        /// Apply zone kit when entering a zone.
        /// </summary>
        public static void ApplyKitOnEnter(string zoneId, Entity player, EntityManager em)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(zoneId))
                {
                    return;
                }

                if (!TryResolveUser(em, player, out var userEntity, out var platformId))
                {
                    return;
                }

                TrackPlayerPlatform(player, platformId);

                if (LegacyKitsDisabled)
                {
                    if (ArenaBuildExecutor.TryGiveBuildForZone(zoneId, platformId, player, em, out var msg))
                    {
                        _log.LogInfo($"[KitService] ArenaBuild applied for zone '{zoneId}' to player={platformId}. {msg}");
                    }
                    else
                    {
                        _log.LogWarning($"[KitService] ArenaBuild apply failed for zone '{zoneId}' to player={platformId}. {msg}");
                    }
                    return;
                }

                var configuredKit = ZoneConfigService.GetKitIdForZone(zoneId);
                var fallbackKit = Plugin.KitDefaultNameValue;
                var kitId = string.IsNullOrWhiteSpace(configuredKit) ? fallbackKit : configuredKit;

                if (string.IsNullOrWhiteSpace(kitId))
                {
                    _log.LogDebug($"[KitService] No kit resolved for zone '{zoneId}'.");
                    return;
                }

                if (!_kits.TryGetValue(kitId, out var kit))
                {
                    var fallbackKitId = Plugin.KitDefaultNameValue;
                    if (!string.IsNullOrWhiteSpace(fallbackKitId) && _kits.TryGetValue(fallbackKitId, out var fallbackKitDef))
                    {
                        _log.LogWarning($"[KitService] Resolved kit '{kitId}' for zone '{zoneId}' was missing; using fallback '{fallbackKitId}'.");
                        kit = fallbackKitDef;
                        kitId = fallbackKitId;
                    }
                    else if (_kits.Count > 0)
                    {
                        var first = _kits.Values.First();
                        _log.LogWarning($"[KitService] Resolved kit '{kitId}' for zone '{zoneId}' was missing; using first loaded kit '{first.Id}'.");
                        kit = first;
                        kitId = first.Id;
                    }
                    else
                    {
                        _log.LogWarning($"[KitService] Resolved kit '{kitId}' for zone '{zoneId}' but no kit definitions are available.");
                        return;
                    }
                }

                _activeKitByPlayer[platformId] = kit.Id;
                _restoreOnExitByPlayer[platformId] = kit.RestoreOnExit;
                RecordKitUsage(platformId, kit.Id);

                if (kit.CaptureSnapshot && Plugin.KitRestoreOnExitValue)
                {
                    if (!_snapshotCapturedByPlayer.ContainsKey(platformId))
                    {
                        if (PlayerSnapshotService.SaveSnapshot(player, out var snapshotError))
                        {
                            _snapshotCapturedByPlayer[platformId] = true;
                        }
                        else
                        {
                            _log.LogWarning($"[KitService] Snapshot save failed for player={platformId}: {snapshotError}");
                        }
                    }
                }

                // Optional: start from a clean slate before giving the kit.
                TryClearPlayerState(player, em);

                var spawnedCount = 0;
                foreach (var item in kit.Items)
                {
                    if (TrySpawnItemForPlayer(item, player, userEntity, em))
                    {
                        spawnedCount++;
                    }
                }

                _log.LogInfo($"[KitService] Enter zone '{zoneId}': player={platformId}, kit='{kit.Id}', itemsApplied={spawnedCount}/{kit.Items.Count}");

                if (kit.BroadcastOnEquip || Plugin.KitBroadcastEquipsValue)
                {
                    _ = GameActionService.TrySendSystemMessageToUserEntity(
                        userEntity,
                        $"Kit '{kit.Id}' applied for zone '{zoneId}'.");
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[KitService] ApplyKitOnEnter failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Restore flow when leaving zone.
        /// </summary>
        public static void RestoreKitOnExit(string zoneId, Entity player, EntityManager em)
        {
            try
            {
                if (LegacyKitsDisabled)
                {
                    // Ensure tracking cleared even when kits are disabled and clear ArenaBuild loadout.
                    if (TryResolveUser(em, player, out _, out var platformIdExit) ||
                        TryResolveTrackedPlatformId(player, out platformIdExit))
                    {
                        ClearTrackingByPlatform(platformIdExit, player.Index);
                    }

                    if (Plugin.KitRestoreOnExitValue)
                    {
                        if (!PlayerSnapshotService.RestoreSnapshot(player, out var snapshotError))
                        {
                            _log.LogDebug($"[KitService] Snapshot restore skipped/failed in ArenaBuild mode for zone '{zoneId}': {snapshotError}");
                        }
                    }

                    ArenaBuildExecutor.TryClearBuild(player, em);
                    return;
                }

                if (!Plugin.KitRestoreOnExitValue)
                {
                    return;
                }

                Entity userEntity = Entity.Null;
                ulong platformId;
                if (!TryResolveUser(em, player, out userEntity, out platformId))
                {
                    if (!TryResolveTrackedPlatformId(player, out platformId))
                    {
                        return;
                    }
                }

                TrackPlayerPlatform(player, platformId);

                if (!_activeKitByPlayer.TryGetValue(platformId, out var kitId))
                {
                    _platformIdByPlayerIndex.Remove(player.Index);
                    return;
                }

                var shouldRestore = _restoreOnExitByPlayer.TryGetValue(platformId, out var perKitRestore)
                    ? perKitRestore
                    : true;

                if (shouldRestore)
                {
                    if (_snapshotCapturedByPlayer.TryGetValue(platformId, out var captured) && captured)
                    {
                        if (!PlayerSnapshotService.RestoreSnapshot(player, out var snapshotError))
                        {
                            _log.LogWarning($"[KitService] Snapshot restore failed for player={platformId}: {snapshotError}");
                        }
                    }

                    _log.LogInfo($"[KitService] Exit zone '{zoneId}': player={platformId}, restore flow completed for kit '{kitId}'.");

                    if (Plugin.KitBroadcastEquipsValue && userEntity != Entity.Null && em.Exists(userEntity))
                    {
                        _ = GameActionService.TrySendSystemMessageToUserEntity(
                            userEntity,
                            $"Restored pre-zone loadout after exiting '{zoneId}'.");
                    }
                }

                ClearTrackingByPlatform(platformId, player.Index);
            }
            catch (Exception ex)
            {
                _log.LogError($"[KitService] RestoreKitOnExit failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Register user into kit tracking state.
        /// </summary>
        public static void EnsurePlayerRegistered(Entity userEntity)
        {
            try
            {
                var em = UnifiedCore.EntityManager;
                if (!em.Exists(userEntity))
                {
                    return;
                }

                if (!em.HasComponent<User>(userEntity))
                {
                    return;
                }

                var user = em.GetComponentData<User>(userEntity);
                var platformId = user.PlatformId;
                var key = $"registration_{platformId}";
                _kitUsageHistory[key] = DateTime.UtcNow;
                _kitUsageCounts[key] = _kitUsageCounts.TryGetValue(key, out var count) ? count + 1 : 1;
            }
            catch (Exception ex)
            {
                _log.LogError($"[KitService] EnsurePlayerRegistered failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Persist usage counters.
        /// </summary>
        public static void SaveUsageData()
        {
            if (LegacyKitsDisabled)
            {
                return;
            }

            try
            {
                var state = new UsageState
                {
                    KitUsageCounts = new Dictionary<string, int>(_kitUsageCounts, StringComparer.OrdinalIgnoreCase)
                };

                TypedJsonConfigManager.TrySave(
                    UsageStatePath,
                    state,
                    CreateUsageStateSerializerOptions(writeIndented: true),
                    message => _log.LogDebug($"[KitService] {message}"),
                    message => _log.LogError($"[KitService] {message}"));

                _log.LogInfo($"[KitService] Saved usage data. Entries={_kitUsageCounts.Count}");
            }
            catch (Exception ex)
            {
                _log.LogError($"[KitService] SaveUsageData failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Compatibility cleanup for legacy kit usage tracking.
        /// Clears in-memory counters and deletes the persisted usage file.
        /// </summary>
        public static void ResetAllUsage()
        {
            _kitUsageHistory.Clear();
            _kitUsageCounts.Clear();

            try
            {
                if (File.Exists(UsageStatePath))
                {
                    File.Delete(UsageStatePath);
                    _log.LogInfo($"[KitService] Deleted legacy usage state '{UsageStatePath}'.");
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning($"[KitService] Failed to delete legacy usage state '{UsageStatePath}': {ex.Message}");
            }
        }

        public static string GetActiveKitId(ulong platformId)
        {
            if (platformId == 0)
            {
                return string.Empty;
            }

            return _activeKitByPlayer.TryGetValue(platformId, out var kitId) ? kitId : string.Empty;
        }

        public static bool HasSnapshotCaptured(ulong platformId)
        {
            if (platformId == 0)
            {
                return false;
            }

            return _snapshotCapturedByPlayer.TryGetValue(platformId, out var captured) && captured;
        }

        public static bool GetRestoreOnExitFlag(ulong platformId)
        {
            if (platformId == 0)
            {
                return false;
            }

            return _restoreOnExitByPlayer.TryGetValue(platformId, out var restore) && restore;
        }

        public static void ClearPlayerTrackingForEntity(Entity playerEntity, EntityManager em)
        {
            try
            {
                if (playerEntity == Entity.Null)
                {
                    return;
                }

                if (TryResolveUser(em, playerEntity, out _, out var platformId) ||
                    TryResolveTrackedPlatformId(playerEntity, out platformId))
                {
                    ClearTrackingByPlatform(platformId, playerEntity.Index);
                }
                else
                {
                    _platformIdByPlayerIndex.Remove(playerEntity.Index);
                }
            }
            catch (Exception ex)
            {
                _log.LogDebug($"[KitService] ClearPlayerTrackingForEntity failed: {ex.Message}");
            }
        }

        private static void LoadKitDefinitions()
        {
            _kits.Clear();

            var path = ResolveKitConfigPath();
            try
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    _log.LogWarning("[KitService] Kit definition path is empty.");
                    return;
                }

                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                TypedJsonConfigManager.TryLoadOrCreate(
                    path,
                    CreateDefaultKitConfigModel,
                    out KitConfigRoot root,
                    out var createdDefault,
                    CreateKitConfigSerializerOptions(writeIndented: true),
                    ValidateKitConfigRoot,
                    message => _log.LogInfo($"[KitService] {message}"),
                    message => _log.LogWarning($"[KitService] {message}"),
                    message => _log.LogError($"[KitService] {message}"));

                if (createdDefault)
                {
                    _log.LogInfo($"[KitService] Created default kit config at '{path}'.");
                }

                var normalizedConfig = NormalizeKitConfig(root);
                if (normalizedConfig)
                {
                    TypedJsonConfigManager.TrySave(
                        path,
                        root,
                        CreateKitConfigSerializerOptions(writeIndented: true),
                        message => _log.LogDebug($"[KitService] {message}"),
                        message => _log.LogError($"[KitService] {message}"));
                    _log.LogInfo("[KitService] Normalized kit config values.");
                }

                foreach (var entry in root.Kits)
                {
                    if (string.IsNullOrWhiteSpace(entry.Key))
                    {
                        continue;
                    }

                    var kit = new KitDefinition
                    {
                        Id = entry.Key.Trim(),
                        Info = entry.Value?.Info ?? string.Empty,
                        RestoreOnExit = entry.Value?.RestoreOnExit ?? true,
                        BroadcastOnEquip = entry.Value?.BroadcastOnEquip ?? false,
                        CaptureSnapshot = entry.Value?.CaptureSnapshot ?? true
                    };

                    if (entry.Value?.Items != null)
                    {
                        foreach (var item in entry.Value.Items)
                        {
                            if (item == null)
                            {
                                continue;
                            }

                            kit.Items.Add(new KitItem
                            {
                                PrefabGuid = item.PrefabGuid,
                                PrefabName = item.PrefabName ?? string.Empty,
                                Amount = Math.Max(1, item.Amount)
                            });
                        }
                    }

                    _kits[kit.Id] = kit;
                }

                EnsureDefaultKits(root, path);

                _log.LogInfo($"[KitService] Loaded {_kits.Count} kit definitions from '{path}'.");
            }
            catch (Exception ex)
            {
                _log.LogError($"[KitService] Failed to load kit definitions from '{path}': {ex.Message}");
            }
        }

        private static bool NormalizeKitConfig(KitConfigRoot root)
        {
            if (root?.Kits == null)
            {
                return false;
            }

            var changed = false;
            var keys = root.Kits.Keys.ToList();
            foreach (var key in keys)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                var kit = root.Kits[key] ?? CreateEmptyKitJson();
                if (root.Kits[key] == null)
                {
                    root.Kits[key] = kit;
                    changed = true;
                }

                kit.Items ??= new List<KitItemJson>();

                for (var i = kit.Items.Count - 1; i >= 0; i--)
                {
                    var item = kit.Items[i];
                    if (item == null)
                    {
                        kit.Items.RemoveAt(i);
                        changed = true;
                        continue;
                    }

                    var originalAmount = item.Amount;
                    item.Amount = Math.Max(1, item.Amount);
                    if (item.Amount != originalAmount)
                    {
                        changed = true;
                    }

                    var originalName = item.PrefabName ?? string.Empty;
                    var normalizedName = NormalizeKitPrefabName(originalName);
                    if (!string.Equals(originalName, normalizedName, StringComparison.Ordinal))
                    {
                        item.PrefabName = normalizedName;
                        changed = true;
                    }

                    if (item.PrefabGuid == 0 && !string.IsNullOrWhiteSpace(item.PrefabName) &&
                        PrefabResolver.TryResolve(item.PrefabName, out var resolvedGuid))
                    {
                        item.PrefabGuid = resolvedGuid.GuidHash;
                        changed = true;
                    }

                    if (item.PrefabGuid == 0 && string.IsNullOrWhiteSpace(item.PrefabName))
                    {
                        kit.Items.RemoveAt(i);
                        changed = true;
                    }
                }
            }

            return changed;
        }

        private static void EnsureDefaultKits(KitConfigRoot root, string path)
        {
            var changed = false;

            foreach (var requiredId in RequiredDefaultKitIds)
            {
                if (_kits.ContainsKey(requiredId))
                {
                    continue;
                }

                var autoKit = new KitDefinition
                {
                    Id = requiredId,
                    Info = requiredId switch
                    {
                        "Kit1" => "Starter melee kit",
                        "Kit2" => "Starter spear kit",
                        "Kit3" => "Starter pistols kit",
                        _ => string.Empty
                    },
                    RestoreOnExit = true,
                    BroadcastOnEquip = false,
                    CaptureSnapshot = true
                };

                _kits[requiredId] = autoKit;
                var kitJson = CreateEmptyKitJson();
                kitJson.Items = CreateSeedKitItems(requiredId);
                kitJson.Info = autoKit.Info;
                root.Kits[requiredId] = kitJson;
                changed = true;
            }

            // If default kits exist but have no items, seed them once so zones work out of the box.
            foreach (var requiredId in RequiredDefaultKitIds)
            {
                if (root.Kits.TryGetValue(requiredId, out var existingJson))
                {
                    existingJson.Items ??= new List<KitItemJson>();
                    if (existingJson.Items.Count == 0)
                    {
                        existingJson.Items = CreateSeedKitItems(requiredId);
                        changed = true;
                    }
                }
            }

            // Also seed the configured default kit name if present but empty.
            var defaultKitId = string.IsNullOrWhiteSpace(Plugin.KitDefaultNameValue) ? "startkit" : Plugin.KitDefaultNameValue;
            if (!string.IsNullOrWhiteSpace(defaultKitId) && root.Kits.TryGetValue(defaultKitId, out var defaultJson))
            {
                defaultJson.Items ??= new List<KitItemJson>();
                if (defaultJson.Items.Count == 0)
                {
                    defaultJson.Items = CreateSeedKitItems(defaultKitId);
                    changed = true;
                }
            }

            if (!changed)
            {
                return;
            }

            try
            {
                TypedJsonConfigManager.TrySave(
                    path,
                    root,
                    CreateKitConfigSerializerOptions(writeIndented: true),
                    message => _log.LogDebug($"[KitService] {message}"),
                    message => _log.LogError($"[KitService] {message}"));
                _log.LogInfo("[KitService] Added missing default kits (Kit1/Kit2/Kit3) to kit configuration.");
            }
            catch (Exception ex)
            {
                _log.LogWarning($"[KitService] Failed to persist default kits: {ex.Message}");
            }
        }

        private static KitDefinitionJson CreateEmptyKitJson()
        {
            return new KitDefinitionJson
            {
                Info = string.Empty,
                RestoreOnExit = true,
                BroadcastOnEquip = false,
                CaptureSnapshot = true,
                Items = new List<KitItemJson>()
            };
        }

        private static JsonSerializerOptions CreateKitConfigSerializerOptions(bool writeIndented)
        {
            return new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
                WriteIndented = writeIndented
            };
        }

        private static JsonSerializerOptions CreateUsageStateSerializerOptions(bool writeIndented)
        {
            return new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
                WriteIndented = writeIndented
            };
        }

        private static KitConfigRoot CreateDefaultKitConfigModel()
        {
            var defaultKit = string.IsNullOrWhiteSpace(Plugin.KitDefaultNameValue) ? "startkit" : Plugin.KitDefaultNameValue;
            var root = new KitConfigRoot
            {
                Kits = new Dictionary<string, KitDefinitionJson>(StringComparer.OrdinalIgnoreCase)
            };

            foreach (var requiredId in RequiredDefaultKitIds)
            {
                // Seed a working default kit so zones "just work" without hand-editing.
                // These names must exist in the prefab resolver catalog (item prefabs are often not spawnable).
                var kit = CreateEmptyKitJson();
                kit.Items = CreateSeedKitItems(requiredId);
                kit.Info = requiredId switch
                {
                    "Kit1" => "Starter melee kit",
                    "Kit2" => "Starter spear kit",
                    "Kit3" => "Starter pistols kit",
                    _ => string.Empty
                };
                root.Kits[requiredId] = kit;
            }

            var defaultKitJson = CreateEmptyKitJson();
            defaultKitJson.Items = CreateSeedKitItems(defaultKit);
            defaultKitJson.Info = "Default kit";
            root.Kits[defaultKit] = defaultKitJson;
            return root;
        }

        private static List<KitItemJson> CreateSeedKitItems(string kitId)
        {
            // Default to a single max-tier (T09) armor set + weapon + basic consumables.
            // If you want different kits per zone, swap the weapon per kit id.
            var weapon = kitId?.Equals("Kit2", StringComparison.OrdinalIgnoreCase) == true
                ? "Item_Weapon_Spear_T09_ShadowMatter"
                : kitId?.Equals("Kit3", StringComparison.OrdinalIgnoreCase) == true
                    ? "Item_Weapon_Pistols_T09_ShadowMatter"
                    : "Item_Weapon_Sword_T09_ShadowMatter";

            return new List<KitItemJson>
            {
                // Armor: Dracula Warrior set (T09) is present in catalog/resolver.
                new() { PrefabName = "Item_Headgear_DraculaHelmet", Amount = 1 },
                new() { PrefabName = "Item_Chest_T09_Dracula_Warrior", Amount = 1 },
                new() { PrefabName = "Item_Legs_T09_Dracula_Warrior", Amount = 1 },
                new() { PrefabName = "Item_Gloves_T09_Dracula_Warrior", Amount = 1 },
                new() { PrefabName = "Item_Boots_T09_Dracula_Warrior", Amount = 1 },

                // Weapon
                new() { PrefabName = weapon, Amount = 1 },

                // Consumables
                new() { PrefabName = "Item_Consumable_Salve_Vermin", Amount = 10 },
                new() { PrefabName = "Item_Consumable_HealingPotion_T02", Amount = 10 },
                new() { PrefabName = "Item_Consumable_SpellPowerPotion_T02", Amount = 10 },
                new() { PrefabName = "Item_Consumable_PhysicalPowerPotion_T02", Amount = 10 },
            };
        }

        private static (bool IsValid, string Error) ValidateKitConfigRoot(KitConfigRoot root)
        {
            if (root == null)
            {
                return (false, "Kit config root is null");
            }

            if (root.Kits == null)
            {
                return (false, "Kit definitions map is null");
            }

            return (true, string.Empty);
        }

        private static (bool IsValid, string Error) ValidateUsageState(UsageState state)
        {
            if (state == null)
            {
                return (false, "Usage state root is null");
            }

            if (state.KitUsageCounts == null)
            {
                return (false, "Usage state count map is null");
            }

            return (true, string.Empty);
        }

        private static string ResolveKitConfigPath()
        {
            var configured = Plugin.KitsConfigPathValue;
            if (string.IsNullOrWhiteSpace(configured))
            {
                var rootDir = Path.Combine(Paths.ConfigPath, "Bluelock");
                Directory.CreateDirectory(rootDir);

                var rootPath = Path.Combine(rootDir, "VAuto.Kits.json");
                var legacyPath = Path.Combine(rootDir, "config", "VAuto.Kits.json");
                try
                {
                    if (!File.Exists(rootPath) && File.Exists(legacyPath))
                    {
                        File.Copy(legacyPath, rootPath, overwrite: false);
                    }
                }
                catch
                {
                    // Best-effort migration.
                }

                return rootPath;
            }

            if (Path.IsPathRooted(configured))
            {
                return configured;
            }

            return Path.Combine(Paths.ConfigPath, configured);
        }

        private static void LoadUsageState()
        {
            if (LegacyKitsDisabled)
            {
                return;
            }

            try
            {
                TypedJsonConfigManager.TryLoadOrCreate(
                    UsageStatePath,
                    () => new UsageState(),
                    out UsageState state,
                    out _,
                    CreateUsageStateSerializerOptions(writeIndented: true),
                    ValidateUsageState,
                    message => _log.LogDebug($"[KitService] {message}"),
                    message => _log.LogWarning($"[KitService] {message}"),
                    message => _log.LogError($"[KitService] {message}"));

                if (state?.KitUsageCounts == null)
                {
                    return;
                }

                _kitUsageCounts.Clear();
                foreach (var kv in state.KitUsageCounts)
                {
                    _kitUsageCounts[kv.Key] = kv.Value;
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning($"[KitService] Failed to load usage state: {ex.Message}");
            }
        }

        internal static bool TryResolveUser(EntityManager em, Entity playerEntity, out Entity userEntity, out ulong platformId)
        {
            userEntity = Entity.Null;
            platformId = 0;

            if (!em.Exists(playerEntity))
            {
                return false;
            }

            if (!em.HasComponent<PlayerCharacter>(playerEntity))
            {
                return false;
            }

            var playerCharacter = em.GetComponentData<PlayerCharacter>(playerEntity);
            userEntity = playerCharacter.UserEntity;
            if (userEntity == Entity.Null || !em.Exists(userEntity) || !em.HasComponent<User>(userEntity))
            {
                return false;
            }

            var user = em.GetComponentData<User>(userEntity);
            platformId = user.PlatformId;
            if (platformId != 0)
            {
                TrackPlayerPlatform(playerEntity, platformId);
            }

            return platformId != 0;
        }

        private static void TrackPlayerPlatform(Entity playerEntity, ulong platformId)
        {
            if (playerEntity == Entity.Null || platformId == 0)
            {
                return;
            }

            _platformIdByPlayerIndex[playerEntity.Index] = platformId;
        }

        private static bool TryResolveTrackedPlatformId(Entity playerEntity, out ulong platformId)
        {
            platformId = 0;
            if (playerEntity == Entity.Null)
            {
                return false;
            }

            return _platformIdByPlayerIndex.TryGetValue(playerEntity.Index, out platformId) && platformId != 0;
        }

        private static void ClearTrackingByPlatform(ulong platformId, int playerIndex = -1)
        {
            if (platformId == 0)
            {
                if (playerIndex >= 0)
                {
                    _platformIdByPlayerIndex.Remove(playerIndex);
                }

                return;
            }

            _activeKitByPlayer.Remove(platformId);
            _restoreOnExitByPlayer.Remove(platformId);
            _snapshotCapturedByPlayer.Remove(platformId);

            if (playerIndex >= 0)
            {
                _platformIdByPlayerIndex.Remove(playerIndex);
                return;
            }

            var staleIndexes = _platformIdByPlayerIndex
                .Where(pair => pair.Value == platformId)
                .Select(pair => pair.Key)
                .ToArray();
            foreach (var index in staleIndexes)
            {
                _platformIdByPlayerIndex.Remove(index);
            }
        }

        private static bool TrySpawnItemForPlayer(KitItem item, Entity playerEntity, Entity userEntity, EntityManager em)
        {
            if (item == null || item.Amount <= 0)
            {
                return false;
            }

            if (!TryResolveKitItemGuid(item, out var itemGuid, out var resolution))
            {
                var normalized = NormalizeKitPrefabName(item.PrefabName);
                _log.LogWarning($"[KitService] Could not resolve kit item prefab (Guid={item.PrefabGuid}, Name='{item.PrefabName}', Normalized='{normalized}').");
                return false;
            }

            var characterName = ResolveCharacterName(em, playerEntity, userEntity);

            try
            {
                // Preferred path: direct inventory add via reflection (Kindred/VCF-style), no console command.
                if (TryAddItemToInventoryDirect(playerEntity, itemGuid, item.Amount, out var addMethod))
                {
                    _log.LogDebug($"[KitService] Resolved kit item via {resolution}: {itemGuid.GuidHash} x{item.Amount} to '{characterName}'");
                    _log.LogDebug($"[KitService] Direct item add via {addMethod}: {itemGuid.GuidHash} x{item.Amount} to '{characterName}'");
                    return true;
                }

                // Fallback: console give command path for compatibility.
                _log.LogDebug($"[KitService] Resolved kit item via {resolution}: {itemGuid.GuidHash} x{item.Amount} to '{characterName}'");
                _log.LogDebug($"[KitService] Direct add unavailable; fallback give command for {itemGuid.GuidHash} x{item.Amount} to '{characterName}'");
                GiveItemCommandUtility.RunGiveItemCommand(itemGuid, item.Amount, true, characterName);
                _log.LogDebug($"[KitService] Item grant command executed for {itemGuid.GuidHash} x{item.Amount}");
                return true;
            }
            catch (Exception ex)
            {
                if (!TryResolvePrefabNameFallback(item, itemGuid, out var fallbackGuidFromName))
                {
                    fallbackGuidFromName = itemGuid;
                }

                if (fallbackGuidFromName != itemGuid)
                {
                    try
                    {
                        _log.LogDebug($"[KitService] Retrying with fallback GUID {fallbackGuidFromName.GuidHash} x{item.Amount} for '{characterName}'");
                        GiveItemCommandUtility.RunGiveItemCommand(fallbackGuidFromName, item.Amount, false, characterName);
                        _log.LogDebug($"[KitService] Fallback grant command executed for {fallbackGuidFromName.GuidHash} x{item.Amount}");
                        return true;
                    }
                    catch (Exception nameFallbackEx)
                    {
                        _log.LogWarning($"[KitService] Name-fallback give item failed for '{characterName}' ({item.PrefabName} => {fallbackGuidFromName.GuidHash} x{item.Amount}): {nameFallbackEx.ToString()}");
                    }
                }

                // Fallback: run without target name to preserve compatibility with command resolver changes.
                if (!string.IsNullOrWhiteSpace(characterName))
                {
                    try
                    {
                        _log.LogDebug($"[KitService] Retrying without character name for {fallbackGuidFromName.GuidHash} x{item.Amount}");
                        GiveItemCommandUtility.RunGiveItemCommand(fallbackGuidFromName, item.Amount, false, null);
                        _log.LogDebug($"[KitService] Nameless grant command executed for {fallbackGuidFromName.GuidHash} x{item.Amount}");
                        return true;
                    }
                    catch (Exception fallbackEx)
                    {
                        _log.LogWarning($"[KitService] Give item failed for '{characterName}' ({item.PrefabGuid} x{item.Amount}): {fallbackEx.ToString()}");
                        return false;
                    }
                }

                _log.LogWarning($"[KitService] Give item failed ({item.PrefabGuid} x{item.Amount}): {ex.ToString()}");
                return false;
            }
        }

        private static bool TryAddItemToInventoryDirect(Entity characterEntity, PrefabGUID itemGuid, int amount, out string methodUsed)
        {
            methodUsed = string.Empty;

            if (characterEntity == Entity.Null || itemGuid == PrefabGUID.Empty || amount <= 0)
            {
                return false;
            }

            try
            {
                // Most common location used by community mods.
                var helperType = Type.GetType("VampireCommandFramework.Helper, VampireCommandFramework");
                if (helperType == null)
                {
                    return false;
                }

                // Candidate signatures observed across mod/tool versions.
                var candidates = new[]
                {
                    new { Name = "AddItemToInventory", Args = new[] { typeof(Entity), typeof(PrefabGUID), typeof(int) } },
                    new { Name = "AddItemToInventory", Args = new[] { typeof(Entity), typeof(PrefabGUID), typeof(int), typeof(bool) } },
                    new { Name = "TryAddItemToInventory", Args = new[] { typeof(Entity), typeof(PrefabGUID), typeof(int) } },
                    new { Name = "TryAddItemToInventory", Args = new[] { typeof(Entity), typeof(PrefabGUID), typeof(int), typeof(bool) } },
                };

                foreach (var candidate in candidates)
                {
                    var method = helperType.GetMethod(candidate.Name, BindingFlags.Public | BindingFlags.Static, null, candidate.Args, null);
                    if (method == null)
                    {
                        continue;
                    }

                    var args = candidate.Args.Length switch
                    {
                        3 => new object[] { characterEntity, itemGuid, amount },
                        4 => new object[] { characterEntity, itemGuid, amount, false },
                        _ => null
                    };

                    if (args == null)
                    {
                        continue;
                    }

                    var result = method.Invoke(null, args);
                    var ok = result switch
                    {
                        null => true,
                        bool b => b,
                        _ => true
                    };

                    if (!ok)
                    {
                        continue;
                    }

                    methodUsed = $"{helperType.FullName}.{method.Name}";
                    return true;
                }
            }
            catch (Exception ex)
            {
                _log.LogDebug($"[KitService] Direct add reflection failed: {ex.Message}");
            }

            return false;
        }

        private enum KitItemResolutionSource
        {
            Unknown = 0,
            DirectGuid,
            PrefabResolverByName,
            PrefabResolverByRemap,
            ZoneCoreByRemap,
            ZoneCoreByName,
            Failed
        }

        private sealed class KitItemResolution
        {
            public KitItemResolutionSource Source { get; set; } = KitItemResolutionSource.Unknown;
            public string NormalizedName { get; set; } = string.Empty;
            public string RemappedName { get; set; } = string.Empty;
            public PrefabGUID Guid { get; set; } = PrefabGUID.Empty;
        }

        private static bool TryResolveKitItem(KitItem item, out KitItemResolution resolution)
        {
            resolution = new KitItemResolution();
            if (item == null)
            {
                resolution.Source = KitItemResolutionSource.Failed;
                return false;
            }

            if (item.PrefabGuid != 0)
            {
                resolution.Source = KitItemResolutionSource.DirectGuid;
                resolution.Guid = new PrefabGUID(item.PrefabGuid);
                return true;
            }

            if (!string.IsNullOrWhiteSpace(item.PrefabName))
            {
                resolution.NormalizedName = NormalizeKitPrefabName(item.PrefabName);

                if (PrefabResolver.TryResolve(resolution.NormalizedName, out var resolvedFromCatalog))
                {
                    resolution.Source = KitItemResolutionSource.PrefabResolverByName;
                    resolution.Guid = resolvedFromCatalog;
                    return true;
                }

                if (PrefabRemapService.TryRemap(resolution.NormalizedName, out var remappedName))
                {
                    resolution.RemappedName = remappedName;
                    if (PrefabResolver.TryResolve(remappedName, out var resolvedFromRemap))
                    {
                        resolution.Source = KitItemResolutionSource.PrefabResolverByRemap;
                        resolution.Guid = resolvedFromRemap;
                        return true;
                    }

                    if (ZoneCore.TryResolvePrefabEntity(remappedName, out var remappedGuid, out _))
                    {
                        resolution.Source = KitItemResolutionSource.ZoneCoreByRemap;
                        resolution.Guid = remappedGuid;
                        return true;
                    }
                }

                if (ZoneCore.TryResolvePrefabEntity(resolution.NormalizedName, out var resolvedGuid, out _))
                {
                    resolution.Source = KitItemResolutionSource.ZoneCoreByName;
                    resolution.Guid = resolvedGuid;
                    return true;
                }
            }

            resolution.Source = KitItemResolutionSource.Failed;
            return false;
        }

        private static bool TryResolveKitItemGuid(KitItem item, out PrefabGUID guid, out KitItemResolutionSource source)
        {
            guid = PrefabGUID.Empty;
            source = KitItemResolutionSource.Unknown;

            if (!TryResolveKitItem(item, out var resolution) || resolution == null)
            {
                source = KitItemResolutionSource.Failed;
                return false;
            }

            guid = resolution.Guid;
            source = resolution.Source;
            return guid != PrefabGUID.Empty && source != KitItemResolutionSource.Failed;
        }

        private static string NormalizeKitPrefabName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            name = name.Trim();

            // Common historical config mistakes:
            // - Using Item_Armor_* when real prefabs are Item_Chest_ / Item_Legs_ / ...
            if (name.StartsWith("Item_Armor_", StringComparison.OrdinalIgnoreCase))
            {
                name = name.Replace("Item_Armor_Chest_", "Item_Chest_", StringComparison.OrdinalIgnoreCase);
                name = name.Replace("Item_Armor_Legs_", "Item_Legs_", StringComparison.OrdinalIgnoreCase);
                name = name.Replace("Item_Armor_Gloves_", "Item_Gloves_", StringComparison.OrdinalIgnoreCase);
                name = name.Replace("Item_Armor_Boots_", "Item_Boots_", StringComparison.OrdinalIgnoreCase);
                name = name.Replace("Item_Armor_Headgear_", "Item_Headgear_", StringComparison.OrdinalIgnoreCase);
            }

            // Potion/consumable naming drift across mods.
            if (name.Equals("Item_Consumable_VerminSalve", StringComparison.OrdinalIgnoreCase))
            {
                name = "Item_Consumable_Salve_Vermin";
            }
            else if (name.Equals("Item_Consumable_HealingPotion_T03", StringComparison.OrdinalIgnoreCase) ||
                     name.Equals("Item_Consumable_Potion_Healing_T03", StringComparison.OrdinalIgnoreCase))
            {
                // There is no T03 healing potion item in the catalog in this build; map to T02.
                name = "Item_Consumable_HealingPotion_T02";
            }
            else if (name.Equals("Item_Consumable_Potion_SpellPower_T03", StringComparison.OrdinalIgnoreCase) ||
                     name.Equals("Item_Consumable_SpellPowerPotion_T03", StringComparison.OrdinalIgnoreCase))
            {
                name = "Item_Consumable_SpellPowerPotion_T02";
            }
            else if (name.Equals("Item_Consumable_Potion_PhysicalPower_T03", StringComparison.OrdinalIgnoreCase) ||
                     name.Equals("Item_Consumable_PhysicalPowerPotion_T03", StringComparison.OrdinalIgnoreCase))
            {
                name = "Item_Consumable_PhysicalPowerPotion_T02";
            }

            // ShadowMatter armor item names are often assumed, but this build only exposes Dracula T09 armor items.
            // Map the common assumed tokens to a real T09 armor set so kits "just work".
            if (name.Contains("_T09_ShadowMatter", StringComparison.OrdinalIgnoreCase))
            {
                if (name.StartsWith("Item_Chest_", StringComparison.OrdinalIgnoreCase)) return "Item_Chest_T09_Dracula_Warrior";
                if (name.StartsWith("Item_Legs_", StringComparison.OrdinalIgnoreCase)) return "Item_Legs_T09_Dracula_Warrior";
                if (name.StartsWith("Item_Gloves_", StringComparison.OrdinalIgnoreCase)) return "Item_Gloves_T09_Dracula_Warrior";
                if (name.StartsWith("Item_Boots_", StringComparison.OrdinalIgnoreCase)) return "Item_Boots_T09_Dracula_Warrior";
                if (name.StartsWith("Item_Headgear_", StringComparison.OrdinalIgnoreCase)) return "Item_Headgear_DraculaHelmet";
            }

            return name;
        }

        public static bool TryBuildKitVerifyReportForZone(string zoneId, out string report)
        {
            report = string.Empty;
            try
            {
                if (string.IsNullOrWhiteSpace(zoneId))
                {
                    report = "Error: zoneId required.";
                    return false;
                }

                var configuredKit = ZoneConfigService.GetKitIdForZone(zoneId);
                var fallbackKit = Plugin.KitDefaultNameValue;
                var kitId = string.IsNullOrWhiteSpace(configuredKit) ? fallbackKit : configuredKit;

                if (string.IsNullOrWhiteSpace(kitId))
                {
                    report = $"Zone '{zoneId}': no kit configured and no DefaultKit is set.";
                    return false;
                }

                return TryBuildKitVerifyReport(kitId, zoneId, configuredKit, fallbackKit, out report);
            }
            catch (Exception ex)
            {
                report = $"Error building kit verify report: {ex.Message}";
                return false;
            }
        }

        public static bool TryBuildKitVerifyReport(string kitId, out string report)
        {
            return TryBuildKitVerifyReport(kitId, zoneId: string.Empty, configuredKit: string.Empty, fallbackKit: string.Empty, out report);
        }

        private static bool TryBuildKitVerifyReport(string kitId, string zoneId, string configuredKit, string fallbackKit, out string report)
        {
            report = string.Empty;
            if (string.IsNullOrWhiteSpace(kitId))
            {
                report = "Error: kitId required.";
                return false;
            }

            if (!_kits.TryGetValue(kitId, out var kit))
            {
                report = string.IsNullOrWhiteSpace(zoneId)
                    ? $"Kit '{kitId}' not found in loaded kit definitions."
                    : $"Zone '{zoneId}': resolved kit '{kitId}' not found (configured='{configuredKit}', default='{fallbackKit}').";
                return false;
            }

            var helperType = Type.GetType("VampireCommandFramework.Helper, VampireCommandFramework");
            var directAddBindingPresent = helperType != null;

            var lines = new List<string>();
            if (!string.IsNullOrWhiteSpace(zoneId))
            {
                lines.Add($"[Kit Verify] zone='{zoneId}' configured='{(string.IsNullOrWhiteSpace(configuredKit) ? "none" : configuredKit)}' default='{(string.IsNullOrWhiteSpace(fallbackKit) ? "none" : fallbackKit)}'");
            }
            lines.Add($"kit='{kit.Id}' items={kit.Items.Count} restoreOnExit={kit.RestoreOnExit} captureSnapshot={kit.CaptureSnapshot} directAddBinding={(directAddBindingPresent ? "present" : "missing")}");

            var okCount = 0;
            for (var i = 0; i < kit.Items.Count; i++)
            {
                var item = kit.Items[i];
                if (item == null)
                {
                    lines.Add($"[{i + 1}] ERROR: null item");
                    continue;
                }

                if (item.Amount <= 0)
                {
                    lines.Add($"[{i + 1}] ERROR: amount={item.Amount} (must be > 0)");
                    continue;
                }

                var token = item.PrefabGuid != 0 ? $"guid={item.PrefabGuid}" : $"name='{item.PrefabName}'";
                if (!TryResolveKitItem(item, out var resolution) || resolution.Guid == PrefabGUID.Empty)
                {
                    lines.Add($"[{i + 1}] FAIL: {token} amount={item.Amount} normalized='{resolution.NormalizedName}'");
                    continue;
                }

                okCount++;
                var remap = string.IsNullOrWhiteSpace(resolution.RemappedName) ? "" : $" remap='{resolution.RemappedName}'";
                lines.Add($"[{i + 1}] OK: {token} amount={item.Amount} -> {resolution.Guid.GuidHash} via {resolution.Source} normalized='{resolution.NormalizedName}'{remap}");
            }

            lines.Add($"summary: resolved={okCount}/{kit.Items.Count}");
            report = string.Join("\n", lines);
            return okCount == kit.Items.Count;
        }

        private static void TryClearPlayerState(Entity playerEntity, EntityManager em)
        {
            if (playerEntity == Entity.Null || em == default || em.World == null || !em.World.IsCreated || !em.Exists(playerEntity))
            {
                return;
            }

            void CallHook(string label, Func<EntityManager, Entity, bool> hook)
            {
                if (hook == null) return;
                try
                {
                    if (!hook(em, playerEntity))
                    {
                        _log.LogDebug($"[KitService] {label} returned false for player {playerEntity.Index}.");
                    }
                }
                catch (Exception ex)
                {
                    _log.LogDebug($"[KitService] {label} threw: {ex.Message}");
                }
            }

            CallHook("ClearInventory", ClearInventory);
            CallHook("ClearEquipment", ClearEquipment);
            CallHook("ClearBuffs", ClearBuffs);
            CallHook("ClearAbilities", ClearAbilities);
        }

        private static bool TryResolvePrefabNameFallback(KitItem item, PrefabGUID currentGuid, out PrefabGUID fallbackGuid)
        {
            fallbackGuid = currentGuid;

            if (string.IsNullOrWhiteSpace(item.PrefabName))
            {
                return false;
            }

            if (!ZoneCore.TryResolvePrefabEntity(item.PrefabName, out var resolvedGuid, out _))
            {
                return false;
            }

            if (resolvedGuid == currentGuid)
            {
                return false;
            }

            fallbackGuid = resolvedGuid;
            return true;
        }

        private static string ResolveCharacterName(EntityManager em, Entity playerEntity, Entity userEntity)
        {
            // Prefer User.CharacterName - it is stable for command targeting and avoids debug-format names.
            if (userEntity != Entity.Null && em.Exists(userEntity) && em.HasComponent<User>(userEntity))
            {
                var userName = em.GetComponentData<User>(userEntity).CharacterName.ToString();
                return SanitizeCharacterName(userName);
            }

            if (em.Exists(playerEntity) && em.HasComponent<PlayerCharacter>(playerEntity))
            {
                var player = em.GetComponentData<PlayerCharacter>(playerEntity);
                var name = player.Name.ToString();
                return SanitizeCharacterName(name);
            }

            return string.Empty;
        }

        private static string SanitizeCharacterName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName))
            {
                return string.Empty;
            }

            var cleaned = rawName.Trim();
            var suffix = cleaned.IndexOf(" - Guid:", StringComparison.OrdinalIgnoreCase);
            if (suffix > 0)
            {
                cleaned = cleaned.Substring(0, suffix).Trim();
            }

            return cleaned;
        }

        private static void RecordKitUsage(ulong platformId, string kitId)
        {
            var key = $"kit_{platformId}_{kitId}";
            _kitUsageHistory[key] = DateTime.UtcNow;
            _kitUsageCounts[key] = _kitUsageCounts.TryGetValue(key, out var count) ? count + 1 : 1;
        }
    }

    /// <summary>
    /// ArenaBuild integration through command execution only.
    /// This avoids reflection against ArenaBuild internal data/model types.
    /// </summary>
    internal static class ArenaBuildExecutor
    {
        private const string DefaultArenaBuildId = "brute";
        private static readonly string[] GiveBuildPrefixes = { ".", string.Empty, "/" };
        private static readonly string[] GiveBuildCommandBases = { "give_build", "giveb", "givebuild" };
        private static readonly string[] ClearBuildCommandBases = { "clear_build", "clearb", "clearbuild" };

        public static bool TryGiveBuildForZone(string zoneId, ulong platformId, Entity player, EntityManager em, out string message)
        {
            message = string.Empty;
            var buildId = ResolveBuildId(zoneId);
            if (string.IsNullOrWhiteSpace(buildId))
            {
                message = "No build mapped for zone.";
                return false;
            }

            if (!TryFindArenaBuildsAssembly(out var asm))
            {
                message = "ArenaBuilds plugin is not loaded (ArenaBuilds.dll missing).";
                return false;
            }

            var giveBuildCommands = GiveBuildCommandBases.Select(commandBase => $"{commandBase} {buildId}");
            var appliedVia = "command";
            if (!TryExecuteCommandVariantsForPlayer(player, em, giveBuildCommands, out var detail))
            {
                if (!TryApplyBuildViaReflection(asm, buildId, platformId, player, em, out detail))
                {
                    message = $"command+reflection failed ({detail})";
                    return false;
                }

                appliedVia = "reflection";
            }

            var unlockOk = TryUnlockAll(player, em, out var unlockDetail, asm);
            message = unlockOk
                ? $"build '{buildId}' {appliedVia} applied ({detail}); unlock_all applied ({unlockDetail})"
                : $"build '{buildId}' {appliedVia} applied ({detail}); unlock_all skipped ({unlockDetail})";
            return true;
        }

        public static void TryClearBuild(Entity player, EntityManager em)
        {
            if (TryExecuteCommandVariantsForPlayer(player, em, ClearBuildCommandBases, out _))
            {
                return;
            }

            if (!TryFindArenaBuildsAssembly(out var asm))
            {
                return;
            }

            if (!TryClearBuildViaReflection(asm, player, em, out var detail))
            {
                Plugin.Logger.LogDebug($"[KitService] ArenaBuild clear command failed: {detail}");
            }
        }

        private static string ResolveBuildId(string zoneId)
        {
            // Single default build for now; future: map zone->build via CFG.
            return DefaultArenaBuildId;
        }

        private static bool TryExecuteForPlayer(Entity player, EntityManager em, string commandText, out string detail)
        {
            detail = string.Empty;
            if (!KitService.TryResolveUser(em, player, out var userEntity, out _))
            {
                detail = "no user entity";
                return false;
            }

            if (!em.Exists(userEntity) || !em.HasComponent<User>(userEntity))
            {
                detail = "user component missing";
                return false;
            }

            var user = em.GetComponentData<User>(userEntity);
            var attempts = new List<string>(GiveBuildPrefixes.Length);

            foreach (var prefix in GiveBuildPrefixes)
            {
                var input = $"{prefix}{commandText}";
                try
                {
                    if (!TryCreateChatEvent(userEntity, player, input, user, out var chatEvent, out var chatEventError))
                    {
                        attempts.Add($"{input}:chat-event({chatEventError})");
                        continue;
                    }

                    var context = new ChatCommandContext(chatEvent);
                    var result = CommandRegistry.Handle(context, input);
                    attempts.Add($"{input}:{result}");

                    if (result == CommandResult.Success)
                    {
                        detail = $"input='{input}', result={result}";
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    attempts.Add($"{input}:exception({ex.Message})");
                }
            }

            detail = string.Join("; ", attempts);
            return false;
        }

        private static bool TryExecuteCommandVariantsForPlayer(Entity player, EntityManager em, IEnumerable<string> commandTexts, out string detail)
        {
            var attempts = new List<string>();
            foreach (var commandText in commandTexts.Where(text => !string.IsNullOrWhiteSpace(text)))
            {
                if (TryExecuteForPlayer(player, em, commandText, out var currentDetail))
                {
                    detail = currentDetail;
                    return true;
                }

                attempts.Add(currentDetail);
            }

            detail = string.Join("; ", attempts.Where(a => !string.IsNullOrWhiteSpace(a)));
            return false;
        }

        private static bool TryApplyBuildViaReflection(Assembly asm, string buildId, ulong platformId, Entity player, EntityManager em, out string detail)
        {
            detail = string.Empty;
            if (string.IsNullOrWhiteSpace(buildId))
            {
                detail = "build id missing";
                return false;
            }

            var userResolved = KitService.TryResolveUser(em, player, out var userEntity, out var resolvedPlatformId);
            if (userResolved && resolvedPlatformId != 0)
            {
                platformId = resolvedPlatformId;
            }

            var helperTypes = new[]
            {
                "ArenaBuilds.Helpers.PlayerHelper",
                "ArenaBuilds.Services.PlayerBuildService",
                "ArenaBuilds.Services.BuildService",
                "ArenaBuilds.BuildService"
            };
            var methodNames = new[] { "GiveBuild", "ApplyBuild", "LoadBuild", "SetBuild", "TryGiveBuild", "TryApplyBuild" };
            var attempts = new List<string>();

            foreach (var typeName in helperTypes)
            {
                Type helperType = null;
                try
                {
                    helperType = asm.GetType(typeName) ?? asm.GetTypes().FirstOrDefault(t =>
                        string.Equals(t.FullName, typeName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(t.Name, typeName.Split('.').LastOrDefault(), StringComparison.OrdinalIgnoreCase));
                }
                catch (Exception ex)
                {
                    attempts.Add($"{typeName}:type-lookup({ex.Message})");
                    continue;
                }

                if (helperType == null)
                {
                    continue;
                }

                foreach (var methodName in methodNames)
                {
                    var methods = helperType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                        .Where(m => string.Equals(m.Name, methodName, StringComparison.Ordinal))
                        .ToArray();
                    if (methods.Length == 0)
                    {
                        continue;
                    }

                    foreach (var method in methods)
                    {
                        if (!TryBuildArenaMethodArgs(method.GetParameters(), buildId, platformId, userEntity, player, out var args))
                        {
                            continue;
                        }

                        try
                        {
                            var result = method.Invoke(null, args);
                            if (result is bool boolResult && !boolResult)
                            {
                                attempts.Add($"{helperType.Name}.{method.Name}:false");
                                continue;
                            }

                            detail = $"invoked {helperType.FullName}.{method.Name}";
                            return true;
                        }
                        catch (Exception ex)
                        {
                            attempts.Add($"{helperType.Name}.{method.Name}:{ex.Message}");
                        }
                    }
                }
            }

            detail = attempts.Count > 0 ? string.Join("; ", attempts) : "no compatible reflection helper found";
            return false;
        }

        private static bool TryClearBuildViaReflection(Assembly asm, Entity player, EntityManager em, out string detail)
        {
            detail = string.Empty;
            if (!KitService.TryResolveUser(em, player, out var userEntity, out var platformId))
            {
                detail = "no user entity";
                return false;
            }

            var helperTypes = new[]
            {
                "ArenaBuilds.Helpers.PlayerHelper",
                "ArenaBuilds.Services.PlayerBuildService",
                "ArenaBuilds.Services.BuildService",
                "ArenaBuilds.BuildService"
            };
            var methodNames = new[] { "ClearBuild", "ResetBuild", "RemoveBuild", "TryClearBuild" };

            foreach (var typeName in helperTypes)
            {
                Type helperType = null;
                try
                {
                    helperType = asm.GetType(typeName) ?? asm.GetTypes().FirstOrDefault(t =>
                        string.Equals(t.FullName, typeName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(t.Name, typeName.Split('.').LastOrDefault(), StringComparison.OrdinalIgnoreCase));
                }
                catch
                {
                    continue;
                }

                if (helperType == null)
                {
                    continue;
                }

                foreach (var methodName in methodNames)
                {
                    foreach (var method in helperType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                                 .Where(m => string.Equals(m.Name, methodName, StringComparison.Ordinal)))
                    {
                        if (!TryBuildArenaMethodArgs(method.GetParameters(), string.Empty, platformId, userEntity, player, out var args))
                        {
                            continue;
                        }

                        try
                        {
                            var result = method.Invoke(null, args);
                            if (result is bool boolResult && !boolResult)
                            {
                                continue;
                            }

                            detail = $"invoked {helperType.FullName}.{method.Name}";
                            return true;
                        }
                        catch
                        {
                            // Try the next candidate.
                        }
                    }
                }
            }

            detail = "no compatible clear helper found";
            return false;
        }

        private static bool TryBuildArenaMethodArgs(ParameterInfo[] parameters, string buildId, ulong platformId, Entity userEntity, Entity playerEntity, out object[] args)
        {
            args = Array.Empty<object>();
            if (parameters == null)
            {
                return false;
            }

            var built = new object[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
            {
                var parameterType = parameters[i].ParameterType;

                if (parameterType == typeof(string))
                {
                    built[i] = buildId ?? string.Empty;
                    continue;
                }

                if (parameterType == typeof(ulong))
                {
                    if (platformId == 0)
                    {
                        return false;
                    }

                    built[i] = platformId;
                    continue;
                }

                if (parameterType == typeof(Entity))
                {
                    var parameterName = parameters[i].Name ?? string.Empty;
                    if (parameterName.IndexOf("user", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (userEntity == Entity.Null)
                        {
                            return false;
                        }

                        built[i] = userEntity;
                        continue;
                    }

                    if (parameterName.IndexOf("player", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        parameterName.IndexOf("character", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (playerEntity == Entity.Null)
                        {
                            return false;
                        }

                        built[i] = playerEntity;
                        continue;
                    }

                    if (userEntity != Entity.Null)
                    {
                        built[i] = userEntity;
                        userEntity = Entity.Null;
                        continue;
                    }

                    if (playerEntity != Entity.Null)
                    {
                        built[i] = playerEntity;
                        playerEntity = Entity.Null;
                        continue;
                    }

                    return false;
                }

                if (parameterType == typeof(bool))
                {
                    built[i] = true;
                    continue;
                }

                return false;
            }

            args = built;
            return true;
        }

        private static bool TryUnlockAll(Entity player, EntityManager em, out string detail, Assembly asm = null)
        {
            detail = string.Empty;
            if (!KitService.TryResolveUser(em, player, out var userEntity, out _))
            {
                detail = "no user entity";
                return false;
            }

            if (asm == null && !TryFindArenaBuildsAssembly(out asm))
            {
                detail = "ArenaBuilds assembly not loaded";
                return false;
            }

            if (!TryInvokeStaticHelper(
                    asm,
                    "ArenaBuilds.Helpers.PlayerHelper",
                    "UnlockAll",
                    new object[] { userEntity, player },
                    out var invokeDetail))
            {
                detail = invokeDetail;
                return false;
            }

            detail = invokeDetail;
            return true;
        }

        private static bool TryFindArenaBuildsAssembly(out Assembly asm)
        {
            asm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a =>
                    string.Equals(a.GetName().Name, "ArenaBuilds", StringComparison.OrdinalIgnoreCase) ||
                    (a.GetName().Name?.IndexOf("ArenaBuilds", StringComparison.OrdinalIgnoreCase) ?? -1) >= 0);
            return asm != null;
        }

        private static bool TryInvokeStaticHelper(Assembly asm, string fullTypeName, string methodName, object[] args, out string detail)
        {
            detail = string.Empty;
            Type helperType;
            try
            {
                helperType = asm.GetType(fullTypeName) ?? asm.GetTypes()
                    .FirstOrDefault(t => string.Equals(t.Name, fullTypeName.Split('.').LastOrDefault(), StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                detail = $"type lookup failed: {ex.Message}";
                return false;
            }

            if (helperType == null)
            {
                detail = $"type '{fullTypeName}' missing";
                return false;
            }

            var method = helperType.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (method == null)
            {
                detail = $"method '{methodName}' missing on {helperType.FullName}";
                return false;
            }

            try
            {
                method.Invoke(null, args);
                detail = $"invoked {helperType.FullName}.{methodName}";
                return true;
            }
            catch (Exception ex)
            {
                detail = $"invoke failed: {ex.Message}";
                return false;
            }
        }

        private static bool TryCreateChatEvent(Entity userEntity, Entity player, string input, User user, out VChatEvent chatEvent, out string detail)
        {
            chatEvent = null!;
            detail = string.Empty;

            var ctorArgs = new object[] { userEntity, player, input, ChatMessageType.System, user };
            var ctor = typeof(VChatEvent).GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(c =>
                {
                    var p = c.GetParameters();
                    return p.Length == ctorArgs.Length
                        && p[0].ParameterType == typeof(Entity)
                        && p[1].ParameterType == typeof(Entity)
                        && p[2].ParameterType == typeof(string)
                        && p[3].ParameterType == typeof(ChatMessageType)
                        && p[4].ParameterType == typeof(User);
                });

            if (ctor == null)
            {
                detail = "constructor missing";
                return false;
            }

            try
            {
                chatEvent = (VChatEvent)ctor.Invoke(ctorArgs);
                return true;
            }
            catch (Exception ex)
            {
                detail = ex.Message;
                return false;
            }
        }
    }
}
