using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using BepInEx;
using ProjectM;
using Stunlock.Core;
using Unity.Entities;
using VAuto.Zone.Models;
using VAuto.Zone.Core;
using VAutomationCore.Core.Config;

namespace VAuto.Zone.Services
{
    /// <summary>
    /// Ability UI / slot control service.
    /// Implements the methods referenced by Plugin.cs:
    /// - Initialize()
    /// - OnZoneEnter()
    /// - OnZoneExit()
    /// - CheckAbilityUsage()
    ///
    /// Hotbar: 4 slots mapped to keys:
    /// 0=T, 1=C, 2=R, 3=SPACE
    /// </summary>
    public static class AbilityUi
    {
        public const int SlotCount = 4;

        public static readonly PrefabGUID SlotBuff = new PrefabGUID(-480024072); // Admin_Invulnerable_Buff

        /// <summary>4-slot index mapping: T=0, C=1, R=2, Space=3.</summary>
        public enum SlotIndex : int
        {
            T = 0,
            C = 1,
            R = 2,
            Space = 3
        }

        // -----------------------------
        // Injectable hooks (wire in Plugin.cs)
        // -----------------------------

        /// <summary>Provide the server EntityManager (authoritative).</summary>
        public static Func<EntityManager>? GetEntityManager { get; set; }

        /// <summary>Resolve SteamId for a player entity.</summary>
        public static Func<Entity, ulong>? GetSteamId { get; set; }

        /// <summary>
        /// Resolve a string identifier to a PrefabGUID.
        /// Allows JSON to specify either numeric GuidHash or prefab name (if you implement lookup).
        /// Default implementation accepts numeric strings only.
        /// </summary>
        public static Func<string, PrefabGUID?>? ResolveAbilityGuid { get; set; }

        /// <summary>Logger hooks; if unset will no-op.</summary>
        public static Action<string>? LogInfo { get; set; }
        public static Action<string>? LogWarn { get; set; }
        public static Action<string>? LogError { get; set; }

        // -----------------------------
        // Internal state
        // -----------------------------

        private static bool _initialized;

        // ZoneId -> config
        private static readonly Dictionary<string, ZoneAbilityConfig> _zoneConfigs = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> _abilityAliases = new(StringComparer.OrdinalIgnoreCase);
        private static ZoneAbilityConfig _defaultConfig = ZoneAbilityConfig.Default("*");
        private static bool _hasDefaultConfig;

        // SteamId -> player state while in a lifecycle zone
        private static readonly Dictionary<ulong, PlayerAbilityState> _playerStates = new();
        private sealed class PendingSlotApply
        {
            public Entity Player { get; set; }
            public PrefabGUID[] Slots { get; set; } = Array.Empty<PrefabGUID>();
            public string ZoneId { get; set; } = string.Empty;
            public bool MergeWithCurrent { get; set; }
            public DateTime ExpiresUtc { get; set; }
        }

        // SteamId -> pending slot apply (when player entity isn't ready yet).
        private static readonly Dictionary<ulong, PendingSlotApply> _pendingSlotApplies = new();
        private static readonly Dictionary<ulong, DateTime> _lastDeferredSlotWarnUtc = new();
        private static readonly Dictionary<int, ulong> _playerIndexToSteamId = new();
        private const int DeferredSlotWarnCooldownSeconds = 10;

        /// <summary>Where the zone ability configuration lives.</summary>
        private static readonly string _configPath = ResolveAbilityConfigPath();
        private static readonly string _abilityPrefabConfigPath = ResolveAbilityPrefabConfigPath();
        public static string ConfigPath => _configPath;

        private static string ResolveAbilityConfigPath()
        {
            var rootDir = Path.Combine(Paths.ConfigPath, "Bluelock");
            Directory.CreateDirectory(rootDir);

            var rootPath = Path.Combine(rootDir, "ability_zones.json");
            var legacyPath = Path.Combine(rootDir, "config", "ability_zones.json");
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

        private static string ResolveAbilityPrefabConfigPath()
        {
            var rootDir = Path.Combine(Paths.ConfigPath, "Bluelock");
            Directory.CreateDirectory(rootDir);

            var rootPath = Path.Combine(rootDir, "ability_prefabs.json");
            var legacyPath = Path.Combine(rootDir, "config", "ability_prefabs.json");
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

        // -----------------------------
        // Public API required by Plugin.cs
        // -----------------------------

        /// <summary>
        /// Must be called once during plugin startup.
        /// Loads zone configs and sets default hooks.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;

            try
            {
                // Default hooks (safe no-op/fallbacks)
                LogInfo ??= _ => { };
                LogWarn ??= _ => { };
                LogError ??= _ => { };

                if (ResolveAbilityGuid == null)
                {
                    ResolveAbilityGuid = (s) =>
                    {
                        s = NormalizeAbilityToken(s);
                        if (string.IsNullOrWhiteSpace(s)) return null;

                        if (PrefabResolver.TryResolve(s, out var guid))
                            return guid;

                        return null;
                    };
                }

                // EntityManager and SteamId resolver must be injected; keep soft-fail behavior.
                if (GetEntityManager == null)
                    LogWarn("[Initialize] GetEntityManager not set. AbilityUi will be inert until wired.");
                if (GetSteamId == null)
                    LogWarn("[Initialize] GetSteamId not set. AbilityUi will be inert until wired.");

                LoadAbilityPrefabAliases();
                LoadZoneConfigs();
                _initialized = true;

                LogInfo($"Initialized. Loaded {_zoneConfigs.Count} zone config(s). SlotCount={SlotCount} (T,C,R,SPACE).");
            }
            catch (Exception ex)
            {
                LogError($"[AbilityUi] Failed to initialize: {ex.Message}");
                // Still mark as initialized to prevent repeated attempts
                _initialized = true;
            }
        }

        /// <summary>
        /// Reload ability-zone configuration from disk.
        /// </summary>
        public static void Reload()
        {
            if (!_initialized)
            {
                Initialize();
                return;
            }

            LoadAbilityPrefabAliases();
            LoadZoneConfigs();
            LogInfo?.Invoke($"Reloaded. Loaded {_zoneConfigs.Count} zone config(s).");
        }

        /// <summary>
        /// Called when a player enters a zone (from zone tracking / lifecycle handler).
        /// Applies preset slots, restrictions, and optional cooldown reset.
        /// </summary>
        public static void OnZoneEnter(Entity playerEntity, string zoneId)
        {
            try
            {
                if (!_initialized) Initialize();

                if (!TryGetCore(playerEntity, out _, out var steamId, out var err))
                {
                    LogWarn?.Invoke($"[OnZoneEnter] {err}");
                    return;
                }

                if (!TryGetZoneConfig(zoneId, out var cfg))
                {
                    // Not configured: clear state and do nothing.
                    _playerStates.Remove(steamId);
                    LogInfo?.Invoke($"[OnZoneEnter] Zone '{zoneId}' has no config; no ability changes applied.");
                    return;
                }

                var state = GetOrCreateState(steamId);
                state.SteamId = steamId;
                state.CurrentZoneId = zoneId;
                state.ZoneEnterTime = DateTime.UtcNow;
                _playerIndexToSteamId[playerEntity.Index] = steamId;
                var hotbarSnapshot = GetPlayerAbilitySlots(playerEntity, out _);

                if (cfg.SaveAndRestoreSlots)
                    state.SavedSlots = hotbarSnapshot;

                if (cfg.SaveAndRestoreCooldowns)
                {
                    // Save only active hotbar cooldowns, not the entire ability pool.
                    var trackedSlots = hotbarSnapshot.Length == SlotCount ? hotbarSnapshot : null;
                    state.SavedCooldowns = GetPlayerCooldowns(playerEntity, trackedSlots);
                }

                // Apply preset slots if provided (only overwrites indices provided)
                if (cfg.PresetSlots != null && cfg.PresetSlots.Length > 0)
                {
                    var current = GetPlayerAbilitySlots(playerEntity, out _);
                    if (current.Length == SlotCount)
                    {
                        for (int i = 0; i < cfg.PresetSlots.Length && i < SlotCount; i++)
                            current[i] = cfg.PresetSlots[i];

                        ApplySlots(playerEntity, current, out var applyErr);
                        if (!string.IsNullOrWhiteSpace(applyErr))
                        {
                            // If the player isn't fully initialized yet, defer until buffer exists.
                            QueuePendingSlotApply(steamId, playerEntity, zoneId, current, mergeWithCurrent: false, applyErr);
                        }
                    }
                    else
                    {
                        // Player entity not ready; defer.
                        QueuePendingSlotApply(steamId, playerEntity, zoneId, cfg.PresetSlots, mergeWithCurrent: true, "Ability slots not readable yet");
                    }
                }

                if (cfg.ResetCooldownsOnEnter)
                    ResetAbilityCooldowns(playerEntity);

                LogInfo?.Invoke($"[OnZoneEnter] Player {steamId} entered zone '{zoneId}'.");
            }
            catch (Exception ex)
            {
                LogError?.Invoke($"[OnZoneEnter] Unhandled error: {ex}");
            }
        }

        /// <summary>
        /// Called when a player exits a zone.
        /// Restores saved slots/cooldowns if configured and optionally resets cooldowns.
        /// </summary>
        public static void OnZoneExit(Entity playerEntity, string zoneId)
        {
            try
            {
                if (!_initialized) Initialize();

                if (!TryGetCore(playerEntity, out _, out var steamId, out var err))
                {
                    LogWarn?.Invoke($"[OnZoneExit] {err}");
                    return;
                }

                if (!TryGetZoneConfig(zoneId, out var cfg))
                {
                    _playerStates.Remove(steamId);
                    LogInfo?.Invoke($"[OnZoneExit] Zone '{zoneId}' had no config; cleared player state.");
                    return;
                }

                if (_playerStates.TryGetValue(steamId, out var state))
                {
                    if (cfg.SaveAndRestoreSlots && state.SavedSlots != null && state.SavedSlots.Length > 0)
                    {
                        ApplySlots(playerEntity, state.SavedSlots, out var restoreErr);
                        if (!string.IsNullOrWhiteSpace(restoreErr))
                            LogWarn?.Invoke($"[OnZoneExit] Restore slots failed: {restoreErr}");
                    }

                    if (cfg.SaveAndRestoreCooldowns && state.SavedCooldowns != null && state.SavedCooldowns.Count > 0)
                        ApplyCooldowns(playerEntity, state.SavedCooldowns);

                    if (cfg.ResetCooldownsOnExit)
                        ResetAbilityCooldowns(playerEntity);

                    _playerStates.Remove(steamId);
                }

                _pendingSlotApplies.Remove(steamId);
                _lastDeferredSlotWarnUtc.Remove(steamId);
                _playerIndexToSteamId.Remove(playerEntity.Index);

                LogInfo?.Invoke($"[OnZoneExit] Player {steamId} exited zone '{zoneId}'.");
            }
            catch (Exception ex)
            {
                LogError?.Invoke($"[OnZoneExit] Unhandled error: {ex}");
            }
        }

        public static void ClearStateForDisconnectedPlayer(Entity playerEntity, EntityManager em = default)
        {
            if (!_initialized)
            {
                return;
            }

            var steamId = 0UL;
            if (playerEntity != Entity.Null && _playerIndexToSteamId.TryGetValue(playerEntity.Index, out var tracked))
            {
                steamId = tracked;
            }
            else if (GetSteamId != null)
            {
                try
                {
                    steamId = GetSteamId(playerEntity);
                }
                catch
                {
                    steamId = 0;
                }
            }

            if (steamId == 0)
            {
                return;
            }

            _playerStates.Remove(steamId);
            _pendingSlotApplies.Remove(steamId);
            _lastDeferredSlotWarnUtc.Remove(steamId);
            _playerIndexToSteamId.Remove(playerEntity.Index);
            LogInfo?.Invoke($"[ClearStateForDisconnectedPlayer] Cleared ability state for {steamId}.");
        }

        /// <summary>
        /// Call every server tick to apply any deferred slot writes (player entity not ready at enter time).
        /// </summary>
        public static void ProcessPendingSlotApplies()
        {
            if (!_initialized) return;
            if (_pendingSlotApplies.Count == 0) return;
            if (GetEntityManager == null) return;

            var em = GetEntityManager();
            if (em == default) return;

            var now = DateTime.UtcNow;
            var remove = new List<ulong>();

            foreach (var kv in _pendingSlotApplies)
            {
                var steamId = kv.Key;
                var pending = kv.Value;

                if (pending == null || pending.ExpiresUtc <= now)
                {
                    if (pending != null && pending.ExpiresUtc <= now)
                    {
                        LogWarn?.Invoke($"[ProcessPendingSlotApplies] Deferred preset slots timed out for {steamId}; ability slots never became readable.");
                    }

                    remove.Add(steamId);
                    continue;
                }

                if (!em.Exists(pending.Player))
                {
                    remove.Add(steamId);
                    continue;
                }

                if (!_playerStates.TryGetValue(steamId, out var activeState) ||
                    string.IsNullOrWhiteSpace(activeState.CurrentZoneId) ||
                    (!string.IsNullOrWhiteSpace(pending.ZoneId) &&
                     !string.Equals(activeState.CurrentZoneId, pending.ZoneId, StringComparison.OrdinalIgnoreCase)))
                {
                    remove.Add(steamId);
                    continue;
                }

                if (pending.Slots == null || pending.Slots.Length == 0)
                {
                    remove.Add(steamId);
                    continue;
                }

                PrefabGUID[] targetSlots;
                if (pending.MergeWithCurrent || pending.Slots.Length != SlotCount)
                {
                    var current = GetPlayerAbilitySlots(pending.Player, out _);
                    if (current.Length != SlotCount)
                    {
                        continue;
                    }

                    for (var i = 0; i < pending.Slots.Length && i < SlotCount; i++)
                    {
                        current[i] = pending.Slots[i];
                    }

                    targetSlots = current;
                }
                else
                {
                    targetSlots = pending.Slots;
                }

                // IMPORTANT: do not call EntityManager.HasComponent<T> here.
                // On some IL2CPP builds, the generic HasComponent trampoline can throw TypeInitializationException.
                // ApplySlots already wraps slot-buffer access in a try/catch, so we can use it as the probe.
                try
                {
                    if (ApplySlots(pending.Player, targetSlots, out var err) && string.IsNullOrWhiteSpace(err))
                    {
                        LogInfo?.Invoke($"[ProcessPendingSlotApplies] Applied deferred slots for {steamId}.");
                        remove.Add(steamId);
                    }
                }
                catch
                {
                    // If any unexpected IL2CPP trampoline throws here, keep pending until it expires.
                }
            }

            for (int i = 0; i < remove.Count; i++)
            {
                var steamId = remove[i];
                _pendingSlotApplies.Remove(steamId);
                _lastDeferredSlotWarnUtc.Remove(steamId);
            }
        }

        private static void QueuePendingSlotApply(ulong steamId, Entity playerEntity, string zoneId, PrefabGUID[] slots, bool mergeWithCurrent, string reason)
        {
            // Only keep one pending apply per player.
            _pendingSlotApplies[steamId] = new PendingSlotApply
            {
                Player = playerEntity,
                Slots = slots,
                ZoneId = zoneId ?? string.Empty,
                MergeWithCurrent = mergeWithCurrent,
                ExpiresUtc = DateTime.UtcNow.AddSeconds(45)
            };

            var now = DateTime.UtcNow;
            if (!_lastDeferredSlotWarnUtc.TryGetValue(steamId, out var lastWarnUtc) ||
                (now - lastWarnUtc).TotalSeconds >= DeferredSlotWarnCooldownSeconds)
            {
                _lastDeferredSlotWarnUtc[steamId] = now;
                LogWarn?.Invoke($"[OnZoneEnter] Deferring preset slots for {steamId}: {reason}");
            }
            else
            {
                LogInfo?.Invoke($"[OnZoneEnter] Preset slot apply still pending for {steamId}.");
            }
        }

        /// <summary>
        /// Called when the plugin intercepts an ability cast attempt.
        /// Return true to BLOCK the cast, false to allow.
        /// </summary>
        public static bool CheckAbilityUsage(Entity playerEntity, PrefabGUID abilityGuid)
        {
            if (!_initialized) Initialize();

            var steamId = GetSteamId?.Invoke(playerEntity) ?? 0;
            if (steamId == 0) return false;

            if (!_playerStates.TryGetValue(steamId, out var state)) return false;
            if (string.IsNullOrWhiteSpace(state.CurrentZoneId)) return false;

            if (!TryGetZoneConfig(state.CurrentZoneId, out var cfg))
            {
                return false;
            }

            if (!cfg.IsAbilityAllowed(abilityGuid))
            {
                LogInfo?.Invoke($"[CheckAbilityUsage] Blocked ability {abilityGuid.GuidHash} for {steamId} in zone '{state.CurrentZoneId}'.");
                return true;
            }

            return false;
        }

        // -----------------------------
        // Slot management helpers
        // -----------------------------

        /// <summary>
        /// Updates one of the 4 hotbar slots. Optionally resets cooldown for that ability.
        /// </summary>
        public static bool UpdateAbilityOnSlot(Entity playerEntity, int slotIndex, PrefabGUID abilityGuid, bool resetCooldown, out string error)
        {
            error = string.Empty;

            if (slotIndex < 0 || slotIndex >= SlotCount)
            {
                error = $"slotIndex {slotIndex} out of range [0..{SlotCount - 1}] (T,C,R,SPACE).";
                return false;
            }

            var slots = GetPlayerAbilitySlots(playerEntity, out var readErr);
            if (slots.Length != SlotCount)
            {
                error = $"Unable to read player ability slots: {readErr}";
                return false;
            }

            slots[slotIndex] = abilityGuid;

            if (!ApplySlots(playerEntity, slots, out var applyErr))
            {
                error = applyErr;
                return false;
            }

            if (resetCooldown)
                ResetAbilityCooldowns(playerEntity, abilityGuid);

            return true;
        }

        /// <summary>Returns exactly 4 slots (T,C,R,SPACE). Returns empty array on error.</summary>
        public static PrefabGUID[] GetPlayerAbilitySlots(Entity playerEntity, out string error)
        {
            error = string.Empty;

            if (GetEntityManager == null)
            {
                error = "GetEntityManager not set.";
                return Array.Empty<PrefabGUID>();
            }

            var em = GetEntityManager();
            if (em == default || !em.Exists(playerEntity))
            {
                error = "EntityManager unavailable or player entity missing.";
                return Array.Empty<PrefabGUID>();
            }

            try
            {
                if (!em.HasComponent<ProjectM.AbilitySlotBuffer>(playerEntity))
                {
                    error = "AbilitySlotBuffer missing on player.";
                    return Array.Empty<PrefabGUID>();
                }

                var buf = em.GetBuffer<ProjectM.AbilitySlotBuffer>(playerEntity);
                var slots = new PrefabGUID[SlotCount];

                for (int i = 0; i < SlotCount; i++)
                    slots[i] = i < buf.Length ? buf[i].Ability : default;

                return slots;
            }
            catch (Exception ex) when (ex is TypeInitializationException || ex is NullReferenceException)
            {
                error = $"AbilitySlotBuffer unavailable: {ex.GetType().Name}";
                return Array.Empty<PrefabGUID>();
            }
        }

        private static bool ApplySlots(Entity playerEntity, PrefabGUID[] slots, out string error)
        {
            error = string.Empty;

            if (slots == null || slots.Length != SlotCount)
            {
                error = $"Expected {SlotCount} slots, got {slots?.Length ?? 0}.";
                return false;
            }

            if (GetEntityManager == null)
            {
                error = "GetEntityManager not set.";
                return false;
            }

            var em = GetEntityManager();
            if (em == default || !em.Exists(playerEntity))
            {
                error = "EntityManager unavailable or player entity missing.";
                return false;
            }

            try
            {
                if (!em.HasComponent<ProjectM.AbilitySlotBuffer>(playerEntity))
                {
                    error = "AbilitySlotBuffer missing on player.";
                    return false;
                }

                var buf = em.GetBuffer<ProjectM.AbilitySlotBuffer>(playerEntity);

                // Ensure at least 4 entries, write first 4, then trim.
                for (int i = 0; i < SlotCount; i++)
                {
                    var entry = new ProjectM.AbilitySlotBuffer { Ability = slots[i] };
                    if (i < buf.Length) buf[i] = entry;
                    else buf.Add(entry);
                }

                while (buf.Length > SlotCount)
                    buf.RemoveAt(buf.Length - 1);

                return true;
            }
            catch (Exception ex) when (ex is TypeInitializationException || ex is NullReferenceException)
            {
                error = $"AbilitySlotBuffer unavailable: {ex.GetType().Name}";
                return false;
            }
        }

        // -----------------------------
        // Cooldown management (best-effort)
        // -----------------------------

        /// <summary>Reset all cooldowns for player, or only a specific ability if provided.</summary>
        public static void ResetAbilityCooldowns(Entity playerEntity, PrefabGUID? ability = null)
        {
            if (GetEntityManager == null) return;

            var em = GetEntityManager();
            if (em == default || !em.Exists(playerEntity)) return;

            try
            {
                if (!em.HasComponent<ProjectM.AbilityCooldownBuffer>(playerEntity)) return;

                var buf = em.GetBuffer<ProjectM.AbilityCooldownBuffer>(playerEntity);
                for (int i = 0; i < buf.Length; i++)
                {
                    var cd = buf[i];
                    if (ability.HasValue && cd.Ability.GuidHash != ability.Value.GuidHash)
                        continue;

                    cd.RemainingSeconds = 0f;
                    buf[i] = cd;
                }
            }
            catch
            {
                // best-effort; ignore
            }
        }

        private static Dictionary<PrefabGUID, float> GetPlayerCooldowns(Entity playerEntity, PrefabGUID[] trackedSlots)
        {
            var result = new Dictionary<PrefabGUID, float>();
            if (GetEntityManager == null) return result;

            var em = GetEntityManager();
            if (em == default || !em.Exists(playerEntity)) return result;

            try
            {
                if (!em.HasComponent<ProjectM.AbilityCooldownBuffer>(playerEntity)) return result;

                var tracked = new HashSet<int>();
                if (trackedSlots != null)
                {
                    for (int i = 0; i < trackedSlots.Length; i++)
                    {
                        if (trackedSlots[i].GuidHash != 0)
                        {
                            tracked.Add(trackedSlots[i].GuidHash);
                        }
                    }
                }

                var restrictToTracked = tracked.Count > 0;

                var buf = em.GetBuffer<ProjectM.AbilityCooldownBuffer>(playerEntity);
                for (int i = 0; i < buf.Length; i++)
                {
                    var cd = buf[i];
                    if (restrictToTracked && !tracked.Contains(cd.Ability.GuidHash))
                    {
                        continue;
                    }

                    result[cd.Ability] = cd.RemainingSeconds;
                }
            }
            catch
            {
                // ignore; return what we collected
            }

            return result;
        }

        private static void ApplyCooldowns(Entity playerEntity, Dictionary<PrefabGUID, float> saved)
        {
            if (saved == null || saved.Count == 0) return;
            if (GetEntityManager == null) return;

            var em = GetEntityManager();
            if (em == default || !em.Exists(playerEntity)) return;

            try
            {
                if (!em.HasComponent<ProjectM.AbilityCooldownBuffer>(playerEntity)) return;

                var buf = em.GetBuffer<ProjectM.AbilityCooldownBuffer>(playerEntity);
                for (int i = 0; i < buf.Length; i++)
                {
                    var cd = buf[i];
                    if (!saved.TryGetValue(cd.Ability, out var secs)) continue;
                    cd.RemainingSeconds = Math.Max(0f, secs);
                    buf[i] = cd;
                }
            }
            catch
            {
                // best-effort; ignore
            }
        }

        // -----------------------------
        // Config + internal helpers
        // -----------------------------

        private static void LoadZoneConfigs()
        {
            _zoneConfigs.Clear();

            try
            {
                TypedJsonConfigManager.TryLoadOrCreate(
                    _configPath,
                    CreateDefaultAbilityConfig,
                    out AbilityZonesConfig root,
                    out var createdDefault,
                    CreateAbilityConfigSerializerOptions(writeIndented: true),
                    ValidateAbilityZonesConfig,
                    LogInfo,
                    LogWarn,
                    LogError);

                if (createdDefault)
                {
                    LogInfo?.Invoke($"Config not found/invalid. Created default file at '{_configPath}'.");
                }

                foreach (var z in root.Zones ?? Enumerable.Empty<ZoneAbilityConfigJson>())
                {
                    if (string.IsNullOrWhiteSpace(z.ZoneId)) continue;

                    // Pre-parse preset slots
                    var presetSlots = (z.PresetSlots ?? new List<string>())
                        .Select(s => ResolveAbilityGuid?.Invoke(NormalizeAbilityToken(s)))
                        .Where(g => g.HasValue)
                        .Select(g => g.Value)
                        .Take(SlotCount)
                        .ToArray();

                    // Pre-parse restricted set
                    var restrictedAbilities = (z.RestrictedAbilities ?? new List<string>())
                        .Select(s => ResolveAbilityGuid?.Invoke(NormalizeAbilityToken(s)))
                        .Where(g => g.HasValue)
                        .Select(g => g.Value)
                        .ToArray();

                    // Pre-parse allowed set
                    var allowedAbilities = (z.AllowedAbilities ?? new List<string>())
                        .Select(s => ResolveAbilityGuid?.Invoke(NormalizeAbilityToken(s)))
                        .Where(g => g.HasValue)
                        .Select(g => g.Value)
                        .ToArray();

                    var config = new ZoneAbilityConfig
                    {
                        ZoneId = z.ZoneId,
                        ResetCooldownsOnEnter = z.ResetCooldownsOnEnter,
                        ResetCooldownsOnExit = z.ResetCooldownsOnExit,
                        SaveAndRestoreSlots = z.SaveAndRestoreSlots,
                        SaveAndRestoreCooldowns = z.SaveAndRestoreCooldowns,
                        PresetSlots = presetSlots,
                        RestrictedAbilities = restrictedAbilities,
                        AllowedAbilities = allowedAbilities
                    };

                    if (z.ZoneId == "*")
                    {
                        _defaultConfig = config;
                        _hasDefaultConfig = true;
                    }
                    else
                    {
                        _zoneConfigs[z.ZoneId] = config;
                    }
                }
            }
            catch (Exception ex)
            {
                LogError?.Invoke($"Failed to read/parse '{_configPath}': {ex}");
                _zoneConfigs.Clear();
                _hasDefaultConfig = false;
                _defaultConfig = ZoneAbilityConfig.Default("*");
            }
        }

        private static bool TryGetZoneConfig(string zoneId, out ZoneAbilityConfig config)
        {
            if (!string.IsNullOrWhiteSpace(zoneId) && _zoneConfigs.TryGetValue(zoneId, out config))
            {
                return true;
            }

            // Zone-driven fallback: if ability_zones.json doesn't specify this zone,
            // read preset slots directly from VAuto.Zones.json for deterministic behavior.
            if (!string.IsNullOrWhiteSpace(zoneId))
            {
                var zone = ZoneConfigService.GetZoneById(zoneId);
                if (zone != null && zone.AbilityPresetSlots != null && zone.AbilityPresetSlots.Length > 0)
                {
                    var presetSlots = zone.AbilityPresetSlots
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Select(s => ResolveAbilityGuid?.Invoke(NormalizeAbilityToken(s)))
                        .Where(g => g.HasValue)
                        .Select(g => g.Value)
                        .ToArray();

                    config = new ZoneAbilityConfig
                    {
                        ZoneId = zoneId,
                        ResetCooldownsOnEnter = true,
                        ResetCooldownsOnExit = false,
                        SaveAndRestoreSlots = true,
                        SaveAndRestoreCooldowns = true,
                        PresetSlots = presetSlots,
                        RestrictedAbilities = Array.Empty<PrefabGUID>(),
                        AllowedAbilities = Array.Empty<PrefabGUID>()
                    };
                    return true;
                }
            }

            if (_hasDefaultConfig)
            {
                config = _defaultConfig;
                return true;
            }

            config = default;
            return false;
        }

        private static JsonSerializerOptions CreateAbilityConfigSerializerOptions(bool writeIndented)
        {
            return new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
                WriteIndented = writeIndented
            };
        }

        private static void LoadAbilityPrefabAliases()
        {
            _abilityAliases.Clear();

            try
            {
                TypedJsonConfigManager.TryLoadOrCreate(
                    _abilityPrefabConfigPath,
                    CreateDefaultAbilityPrefabAliasesConfig,
                    out AbilityPrefabAliasesConfig aliasesConfig,
                    out var createdDefault,
                    CreateAbilityConfigSerializerOptions(writeIndented: true),
                    ValidateAbilityPrefabAliasesConfig,
                    LogInfo,
                    LogWarn,
                    LogError);

                foreach (var entry in aliasesConfig.Aliases)
                {
                    if (string.IsNullOrWhiteSpace(entry.Key) || string.IsNullOrWhiteSpace(entry.Value))
                    {
                        continue;
                    }

                    _abilityAliases[entry.Key.Trim()] = entry.Value.Trim();
                }

                if (createdDefault)
                {
                    LogInfo?.Invoke($"Created default ability prefab alias config at '{_abilityPrefabConfigPath}'.");
                }
            }
            catch (Exception ex)
            {
                LogError?.Invoke($"Failed to load ability prefab alias config '{_abilityPrefabConfigPath}': {ex.Message}");
            }
        }

        private static string NormalizeAbilityToken(string raw)
        {
            var token = (raw ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(token))
            {
                return string.Empty;
            }

            if (_abilityAliases.TryGetValue(token, out var mapped) && !string.IsNullOrWhiteSpace(mapped))
            {
                token = mapped.Trim();
            }

            // Hard fallback aliases if config was not created yet.
            if (token.Equals("Spell_VeilOfBlood", StringComparison.OrdinalIgnoreCase))
                token = "AB_Vampire_VeilOfBlood_Group";
            else if (token.Equals("Spell_VeilOfChaos", StringComparison.OrdinalIgnoreCase))
                token = "AB_Vampire_VeilOfChaos_Group";
            else if (token.Equals("Spell_VeilOfFrost", StringComparison.OrdinalIgnoreCase))
                token = "AB_Vampire_VeilOfFrost_Group";
            else if (token.Equals("Spell_VeilOfBones", StringComparison.OrdinalIgnoreCase))
                token = "AB_Vampire_VeilOfBones_AbilityGroup";
            else if (token.Equals("AB_BloodRite_AbilityGroup", StringComparison.OrdinalIgnoreCase))
                token = "AB_Blood_BloodRite_AbilityGroup";

            return token;
        }

        private static AbilityZonesConfig CreateDefaultAbilityConfig()
        {
            return new AbilityZonesConfig
            {
                Zones = new List<ZoneAbilityConfigJson>
                {
                    new ZoneAbilityConfigJson
                    {
                        ZoneId = "*",
                        ResetCooldownsOnEnter = true,
                        ResetCooldownsOnExit = false,
                        SaveAndRestoreSlots = true,
                        SaveAndRestoreCooldowns = true,
                        // Default: Veil on Dash/T/C, Blood Rite on R.
                        PresetSlots = new List<string>
                        {
                            "AB_Vampire_VeilOfBlood_Group",     // T
                            "AB_Vampire_VeilOfBlood_Group",     // C
                            "AB_Blood_BloodRite_AbilityGroup",  // R
                            "AB_Vampire_VeilOfBlood_Group"      // SPACE (Dash)
                        },
                        RestrictedAbilities = new List<string>(),
                        AllowedAbilities = new List<string>()
                    }
                }
            };
        }

        private static AbilityPrefabAliasesConfig CreateDefaultAbilityPrefabAliasesConfig()
        {
            return new AbilityPrefabAliasesConfig
            {
                Aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Spell_VeilOfBlood"] = "AB_Vampire_VeilOfBlood_Group",
                    ["Spell_VeilOfChaos"] = "AB_Vampire_VeilOfChaos_Group",
                    ["Spell_VeilOfFrost"] = "AB_Vampire_VeilOfFrost_Group",
                    ["Spell_VeilOfBones"] = "AB_Vampire_VeilOfBones_AbilityGroup",
                    ["AB_BloodRite_AbilityGroup"] = "AB_Blood_BloodRite_AbilityGroup"
                }
            };
        }

        private static (bool IsValid, string Error) ValidateAbilityZonesConfig(AbilityZonesConfig config)
        {
            if (config == null)
            {
                return (false, "Ability config root is null");
            }

            if (config.Zones == null)
            {
                return (false, "Ability config zones list is null");
            }

            return (true, string.Empty);
        }

        private static (bool IsValid, string Error) ValidateAbilityPrefabAliasesConfig(AbilityPrefabAliasesConfig config)
        {
            if (config == null)
            {
                return (false, "Ability prefab alias config is null");
            }

            if (config.Aliases == null)
            {
                return (false, "Ability prefab alias map is null");
            }

            return (true, string.Empty);
        }

        private static PlayerAbilityState GetOrCreateState(ulong steamId)
        {
            if (!_playerStates.TryGetValue(steamId, out var st))
            {
                st = new PlayerAbilityState { SteamId = steamId };
                _playerStates[steamId] = st;
            }

            return st;
        }

        private static bool TryGetCore(Entity playerEntity, out EntityManager em, out ulong steamId, out string error)
        {
            em = default;
            steamId = 0;
            error = string.Empty;

            if (GetEntityManager == null)
            {
                error = "GetEntityManager not set.";
                return false;
            }

            em = GetEntityManager();
            if (em == default)
            {
                error = "EntityManager is default/unavailable.";
                return false;
            }

            if (!em.Exists(playerEntity))
            {
                error = "Player entity does not exist.";
                return false;
            }

            if (GetSteamId == null)
            {
                error = "GetSteamId not set.";
                return false;
            }

            steamId = GetSteamId(playerEntity);
            if (steamId == 0)
            {
                error = "SteamId resolver returned 0.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Clear all player states (useful for server shutdown).
        /// </summary>
        public static void ClearAllStates()
        {
            _playerStates.Clear();
            LogInfo?.Invoke($"Cleared all player states");
        }

        /// <summary>
        /// Get the number of active player states.
        /// </summary>
        public static int GetActivePlayerCount()
        {
            return _playerStates.Count;
        }

        // -----------------------------
        // JSON models
        // -----------------------------

        private sealed class AbilityZonesConfig
        {
            public List<ZoneAbilityConfigJson> Zones { get; set; } = new();
        }

        private sealed class AbilityPrefabAliasesConfig
        {
            public Dictionary<string, string> Aliases { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        }

        private sealed class ZoneAbilityConfigJson
        {
            public string ZoneId { get; set; } = string.Empty;

            public bool ResetCooldownsOnEnter { get; set; }
            public bool ResetCooldownsOnExit { get; set; }

            public bool SaveAndRestoreSlots { get; set; }
            public bool SaveAndRestoreCooldowns { get; set; }

            public List<string> PresetSlots { get; set; } = new();
            public List<string> RestrictedAbilities { get; set; } = new();
            public List<string> AllowedAbilities { get; set; } = new();
        }

        // Note: this service now relies on the real ProjectM.AbilitySlotBuffer and
        // ProjectM.AbilityCooldownBuffer types; no local ECS buffer shims are defined here.
    }
}
