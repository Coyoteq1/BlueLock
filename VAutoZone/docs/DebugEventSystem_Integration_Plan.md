# DebugEventSystem Integration Plan

**Branch:** `fix/vrising-1.1-migration`  
**Created:** 2026-02-08  
**Status:** Draft

## Overview

Replace the speculative VBlood progress tracking in [`ZoneLifecycleBridge.cs`](Services/ZoneLifecycleBridge.cs) with proper DebugEventSystem integration using V Rising's official API calls. This plan focuses on using `UnlockAllVVBloods` and `CreateGameplayEvent` through the lifecycle system.

## Current Architecture Issues

### Problems with Current Implementation

1. **Non-existent Component**: [`ZoneLifecycleBridge.cs:636`](Services/ZoneLifecycleBridge.cs:636) references `VBloodProgression` which doesn't exist in V Rising ECS
2. **Manual ECS Mutation**: Current code directly modifies ECS components which is fragile
3. **No Rollback Safety**: `SetVBloodProgress()` doesn't properly restore player state
4. **Missing API Integration**: No use of proper game progression systems

### Current Flow (Problematic)

```
Player Enter Zone
    ↓
HandleVBloodOnEnter()
    ↓
GetCurrentVBloodProgress() ← Queries non-existent VBloodProgression
    ↓
SetVBloodProgress() ← Direct ECS mutation
    ↓
No proper rollback on exit
```

## Proposed Architecture

### New Integration Points

```
Player Enter Zone
    ↓
DebugEventBridge.OnPlayerEnter()
    ↓
Backup Player Progression (VBloods, Techs, Blueprints, Shapeshifts)
    ↓
Execute DebugEvent Actions (configurable)
    ↓
Player Exit Zone
    ↓
DebugEventBridge.OnPlayerExit()
    ↓
Restore Player Progression from Backup
    ↓
Cleanup
```

### Core Components

#### 1. DebugEventBridge Service

**File:** `Services/DebugEventBridge.cs`

```csharp
namespace VAuto.Zone
{
    public class DebugEventBridge : IDisposable
    {
        // Player progression backups (SteamId → Backup)
        private readonly Dictionary<ulong, PlayerProgressionBackup> _backups;

        // Configuration
        private DebugEventConfig _config;

        // Lifecycle hooks
        public void OnPlayerEnter(ulong steamId, string zoneId);
        public void OnPlayerExit(ulong steamId, string zoneId);
        public void ExecuteDebugEvent(ulong steamId, string eventType, string eventData);
        public void RestorePlayer(ulong steamId);
    }

    public class PlayerProgressionBackup
    {
        public HashSet<string> UnlockedVBloods;
        public HashSet<string> UnlockedTechs;
        public HashSet<string> UnlockedBlueprints;
        public HashSet<string> UnlockedShapeshifts;
        public DateTime BackupTime;
    }

    public class DebugEventConfig
    {
        public bool Enabled;
        public Dictionary<string, DebugEventZoneConfig> Zones;
    }

    public class DebugEventZoneConfig
    {
        public bool UnlockVBloods;
        public bool UnlockAllTechs;
        public bool UnlockAllBlueprints;
        public bool UnlockAllShapeshifts;
        public string[] GameplayEvents;  // EventPrefab names to spawn
        public bool RestoreOnExit;  // Default: true
    }
}
```

#### 2. Lifecycle Action Integration

**File:** `Vlifecycle/Actions/DebugEventActions.cs` (in Vlifecycle project)

```csharp
namespace VAuto.Core.Lifecycle.Actions
{
    /// <summary>
    /// Debug event lifecycle actions for VAutoZone integration.
    /// Uses proper V Rising API calls for progression manipulation.
    /// </summary>
    public class UnlockAllVVBloodsAction : ILifecycleAction
    {
        public void Execute(LifecycleContext context)
        {
            var bridge = DebugEventBridge.Instance;
            if (bridge != null)
            {
                bridge.ExecuteDebugEvent(
                    context.SteamId,
                    "unlock_vbloods",
                    "all"
                );
            }
        }
    }

    public class CreateGameplayEventAction : ILifecycleAction
    {
        public string EventPrefab { get; set; }

        public void Execute(LifecycleContext context)
        {
            var bridge = DebugEventBridge.Instance;
            if (bridge != null)
            {
                bridge.ExecuteDebugEvent(
                    context.SteamId,
                    "gameplay_event",
                    EventPrefab
                );
            }
        }
    }

    public class RestoreProgressionAction : ILifecycleAction
    {
        public void Execute(LifecycleContext context)
        {
            var bridge = DebugEventBridge.Instance;
            if (bridge != null)
            {
                bridge.RestorePlayer(context.SteamId);
            }
        }
    }
}
```

#### 3. ZoneLifecycleConfig Extension

**File:** `Services/ZoneLifecycleConfig.cs`

Add support for debug events:

```csharp
public class DebugEventZoneConfig
{
    public bool Enabled { get; set; }
    public bool UnlockVBloods { get; set; }
    public bool UnlockAllTechs { get; set; }
    public bool UnlockAllBlueprints { get; set; }
    public bool UnlockAllShapeshifts { get; set; }
    public List<string> GameplayEvents { get; set; }
    public bool RestoreOnExit { get; set; } = true;
}
```

### Harmony Patches

**File:** `Services/DebugEventPatches.cs`

```csharp
[HarmonyPatch(typeof(ArenaLifecycleManager), "OnPlayerEnter")]
public class ArenaEnterDebugPatch
{
    [HarmonyPostfix]
    private static void Postfix(Player player)
    {
        if (DebugEventBridge.Instance != null)
        {
            DebugEventBridge.Instance.OnPlayerEnter(
                player.PlatformId,
                "debug_arena"  // Zone ID from context
            );
        }
    }
}

[HarmonyPatch(typeof(ArenaLifecycleManager), "OnPlayerExit")]
public class ArenaExitDebugPatch
{
    [HarmonyPostfix]
    private static void Postfix(Player player)
    {
        if (DebugEventBridge.Instance != null)
        {
            DebugEventBridge.Instance.OnPlayerExit(
                player.PlatformId,
                "debug_arena"
            );
        }
    }
}
```

## Configuration Schema

### VAuto.ZoneLifecycle.json Extension

```json
{
  "debugEvents": {
    "enabled": true,
    "zones": {
      "debug_arena": {
        "unlockVBloods": true,
        "unlockAllTechs": true,
        "unlockAllBlueprints": true,
        "unlockAllShapeshifts": true,
        "gameplayEvents": [
          "Event_VBlood_Unlock_Test",
          "Event_GiveResources"
        ],
        "restoreOnExit": true
      }
    }
  }
}
```

## Complete Implementation (Refined)

### Thread-Safe DebugEventBridge

```csharp
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using BepInEx.Logging;
using Unity.Entities;

namespace VAuto.Zone
{
    /// <summary>
    /// Thread-safe debug event bridge for zone-based progression manipulation.
    /// </summary>
    public class DebugEventBridge : IDisposable
    {
        private static DebugEventBridge _instance;
        public static DebugEventBridge Instance => _instance;
        
        private readonly ManualLogSource _log;
        private readonly ConcurrentDictionary<ulong, PlayerProgressionBackup> _backups;
        private readonly ConcurrentDictionary<ulong, Entity> _spawnedBosses;
        private readonly ConcurrentDictionary<int, string> _prefabIndex;
        private DebugEventConfig _config;
        private bool _disposed;

        public DebugEventBridge(ManualLogSource log)
        {
            _instance = this;
            _log = log;
            _backups = new();
            _spawnedBosses = new();
            _prefabIndex = new();
            LoadPrefabIndex();
            LoadConfig();
        }

        private void LoadPrefabIndex()
        {
            try
            {
                var jsonPath = Path.Combine(System.AppContext.BaseDirectory, "PrefabIndex.json");
                if (File.Exists(jsonPath))
                {
                    var json = File.ReadAllText(jsonPath);
                    var dict = JsonSerializer.Deserialize<Dictionary<string, int>>(json);
                    foreach (var kvp in dict ?? new Dictionary<string, int>())
                    {
                        _prefabIndex.TryAdd(kvp.Value, kvp.Key);
                    }
                    _log.LogInfo($"[DebugEventBridge] Loaded {_prefabIndex.Count} prefab mappings");
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning($"[DebugEventBridge] Failed to load PrefabIndex.json: {ex.Message}");
            }
        }

        private void LoadConfig()
        {
            // Load from VAuto.ZoneLifecycle.json debugEvents section
        }

        public void OnPlayerEnter(ulong steamId, string zoneId)
        {
            if (_disposed) return;
            
            var player = PlayerManager.GetPlayer(steamId);
            if (player == null || _backups.ContainsKey(steamId)) return;

            var backup = BackupPlayerProgression(player);
            _backups.TryAdd(steamId, backup);
            ExecuteZoneEvents(player, zoneId);
            
            _log.LogInfo($"[DebugEventBridge] Player {steamId} entered debug zone {zoneId}");
        }

        public void OnPlayerExit(ulong steamId, string zoneId)
        {
            if (_disposed) return;
            
            if (_spawnedBosses.TryRemove(steamId, out var bossEntity))
            {
                BossFeedUtility.DestroyBossEntity(bossEntity);
            }

            RestorePlayer(steamId);
            
            _log.LogInfo($"[DebugEventBridge] Player {steamId} exited debug zone {zoneId}");
        }

        private PlayerProgressionBackup BackupPlayerProgression(Player player)
        {
            return new PlayerProgressionBackup
            {
                UnlockedVBloods = new HashSet<string>(player.BloodAltar.GetUnlockedVBloods()),
                UnlockedTechs = new HashSet<long>(player.Progression.UnlockedTechs),
                UnlockedBlueprints = new HashSet<long>(player.Progression.UnlockedBlueprints),
                UnlockedShapeshifts = new HashSet<long>(player.Progression.UnlockedShapeshifts),
                UnlockedAchievements = new HashSet<string>(player.Achievements.GetUnlocked()),
                BackupTime = System.DateTime.UtcNow
            };
        }

        public void RestorePlayer(ulong steamId)
        {
            if (!_backups.TryRemove(steamId, out var backup)) return;

            var player = PlayerManager.GetPlayer(steamId);
            if (player == null) return;

            player.BloodAltar.ClearAllVBloods();
            foreach (var vblood in backup.UnlockedVBloods)
                player.BloodAltar.UnlockVBlood(vblood);

            player.Progression.ClearAll();
            foreach (var t in backup.UnlockedTechs) player.Progression.UnlockTech(t);
            foreach (var bp in backup.UnlockedBlueprints) player.Progression.UnlockBlueprint(bp);
            foreach (var ss in backup.UnlockedShapeshifts) player.Progression.UnlockShapeshift(ss);

            player.Achievements.ClearAll();
            foreach (var ach in backup.UnlockedAchievements)
                player.Achievements.Unlock(ach);

            player.Client.UpdateProgressionUI();
            
            _log.LogDebug($"[DebugEventBridge] Restored progression for {steamId}");
        }

        private void ExecuteZoneEvents(Player player, string zoneId)
        {
            if (!_config.Zones.TryGetValue(zoneId, out var zoneConfig)) return;
            
            if (zoneConfig.UnlockVBloods)
                UnlockAllVBloods(player);
            
            foreach (var eventPrefab in zoneConfig.GameplayEvents ?? new List<string>())
            {
                SpawnGameplayEvent(player, eventPrefab);
            }
        }

        private void UnlockAllVBloods(Player player)
        {
            foreach (var vBlood in BloodAltarDatabase.GetAllVBloods())
                player.BloodAltar.UnlockVBlood(vBlood);
        }

        private void SpawnGameplayEvent(Player player, string eventPrefabName)
        {
            var eventGuid = ResolvePrefabGuid(eventPrefabName);
            if (eventGuid == default) return;

            var bossEntity = SpawnBossEntity(eventGuid);
            if (bossEntity != Entity.Null)
            {
                _spawnedBosses.TryAdd(player.PlatformId, bossEntity);
            }
        }

        private PrefabGUID ResolvePrefabGuid(string prefabName)
        {
            foreach (var kvp in _prefabIndex)
            {
                if (kvp.Value.Equals(prefabName, System.StringComparison.OrdinalIgnoreCase))
                    return new PrefabGUID(kvp.Key);
            }
            return default;
        }

        private Entity SpawnBossEntity(PrefabGUID eventGuid)
        {
            // Use V Rising spawn systems
            return Entity.Null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            
            foreach (var kvp in _spawnedBosses)
            {
                BossFeedUtility.DestroyBossEntity(kvp.Value);
            }
            
            _backups.Clear();
            _spawnedBosses.Clear();
        }
    }

    public enum DebugEventType
    {
        UnlockVBloods,
        UnlockTechs,
        SpawnEvent
    }

    public class PlayerProgressionBackup
    {
        public HashSet<string> UnlockedVBloods = new();
        public HashSet<long> UnlockedTechs = new();
        public HashSet<long> UnlockedBlueprints = new();
        public HashSet<long> UnlockedShapeshifts = new();
        public HashSet<string> UnlockedAchievements = new();
        public System.DateTime BackupTime;
    }

    public class DebugEventConfig
    {
        public bool Enabled;
        public Dictionary<string, DebugEventZoneConfig> Zones = new();
    }

    public class DebugEventZoneConfig
    {
        public bool UnlockVBloods;
        public bool UnlockAllTechs;
        public List<string> GameplayEvents = new();
        public bool RestoreOnExit = true;
    }
}
```

## Implementation Refinements

### 1. Thread Safety

Use `ConcurrentDictionary` for thread-safe access in multi-player environments:

```csharp
private readonly ConcurrentDictionary<ulong, PlayerProgressionBackup> _backups;
private readonly ConcurrentDictionary<ulong, Entity> _spawnedBosses;
private readonly ConcurrentDictionary<int, string> _prefabIndex;
```

### 2. Dynamic Prefab Loading

Load `PrefabIndex.json` at startup instead of hardcoding:

```csharp
private void LoadPrefabIndex()
{
    var jsonPath = Path.Combine(System.AppContext.BaseDirectory, "PrefabIndex.json");
    if (File.Exists(jsonPath))
    {
        var json = File.ReadAllText(jsonPath);
        var dict = JsonSerializer.Deserialize<Dictionary<string, int>>(json);
        foreach (var kvp in dict ?? new Dictionary<string, int>())
        {
            _prefabIndex.TryAdd(kvp.Value, kvp.Key);
        }
    }
}
```

### 3. Event Type Enum

Replace string-based event types with enum for type safety:

```csharp
public enum DebugEventType
{
    UnlockVBloods,
    UnlockTechs,
    SpawnEvent
}
```

### 4. Automatic Cleanup

Implement disconnect handling via `IDisposable`:

```csharp
public void Dispose()
{
    foreach (var kvp in _spawnedBosses)
    {
        BossFeedUtility.DestroyBossEntity(kvp.Value);
    }
    _backups.Clear();
    _spawnedBosses.Clear();
}
```

### 5. Logging

Add verbose logging for debugging:

```csharp
_log.LogDebug($"[DebugEventBridge] Player {steamId} restored progression with {backup.UnlockedVBloods.Count} VBloods");
```

## Migration Plan

### Phase 1: Create DebugEventBridge Service

1. Create `Services/DebugEventBridge.cs` with:
   - Thread-safe `ConcurrentDictionary` collections
   - Dynamic prefab loading from `PrefabIndex.json`
   - `OnPlayerEnter()` and `OnPlayerExit()` methods
   - `RestorePlayer()` for rollback
   - `Dispose()` for cleanup

2. Create `Services/BossFeedUtility.cs` with:
   - `DestroyBossEntity()` for cleanup
   - Clear `CreateGameplayEventsOnSpawn` buffers

### Phase 2: DELETE Old VBlood Code from ZoneLifecycleBridge

**REMOVE these methods entirely:**

| Lines | Method | Reason | Replacement |
|-------|--------|--------|-------------|
| 507-528 | `HandleVBloodOnEnter()` | Replaced | `DebugEventBridge.OnPlayerEnter()` |
| 531-552 | `HandleVBloodOnExit()` | Replaced | `DebugEventBridge.OnPlayerExit()` |
| 554-568 | `SaveVBloodProgress()` | Replaced | `DebugEventBridge.BackupPlayerProgression()` |
| 571-588 | `RestoreVBloodProgress()` | Replaced | `DebugEventBridge.RestorePlayer()` |
| 590-608 | `LockVBloodProgress()` | Removed | Not needed |
| 610-659 | `GetCurrentVBloodProgress()` | Removed | Non-existent component |
| 661-697 | `SetVBloodProgress()` | Removed | Non-existent component |
| 699-758 | `FindPlayerEntityBySteamId()` | Keep | Used elsewhere |
| 731-756 | `GetUnlockedVBloods()` | Replaced | `BloodAltar.GetUnlockedVBloods()` |
| 758-776 | `GetIndividualVBloodProgress()` | Removed | Speculative |
| 780-792 | `ApplyUnlockedVBloods()` | Replaced | `BloodAltar.UnlockVBlood()` |
| 794-808 | `ApplyIndividualVBloodProgress()` | Removed | Speculative |
| 810-823 | `InitializeVBloodState()` | Removed | Not needed |
| 825-835 | `ClearVBloodState()` | Removed | Not needed |

**REMOVE these fields:**

| Line | Field | Reason |
|-------|--------|--------|
| 67-69 | `private readonly Dictionary<ulong, VBloodProgressState> _vbloodStates` | Replaced by `_backups` |
| 90-93 | `VBLOOD_PROGRESS_SAVE`, `VBLOOD_PROGRESS_RESTORE`, `VBLOOD_UNLOCK_CHECK` | Replaced by `DebugEventType` enum |

**REMOVE these supporting types:**

| Lines | Type | Reason |
|-------|------|--------|
| 1227-1243 | `VBloodProgressState` | Replaced by `PlayerProgressionBackup` |
| 1245-1254 | `VBloodData` | Replaced by `PlayerProgressionBackup` |

### Phase 3: Update Remaining ZoneLifecycleBridge Code

**ADD new field:**

```csharp
// DebugEventBridge integration
private DebugEventBridge _debugEventBridge;
```

**MODIFY initialization:**

```csharp
public ZoneLifecycleBridge(ManualLogSource log, EntityManager entityManager)
{
    // ... existing code ...
    
    // Initialize DebugEventBridge
    _debugEventBridge = new DebugEventBridge(log);
    
    _log.LogInfo($"{_logPrefix} Initialized with DebugEventBridge");
}
```

**MODIFY HandleSpellbookOnEnter/Exit to remove VBlood references**

### Phase 4: Create Lifecycle Actions

1. Create `Vlifecycle/Actions/DebugEventActions.cs`:
   - `UnlockAllVVBloodsAction` - calls `DebugEventBridge`
   - `CreateGameplayEventAction` - calls `DebugEventBridge`
   - `RestoreProgressionAction` - calls `DebugEventBridge.RestorePlayer()`

2. Register actions in Vlifecycle action registry

### Phase 5: Update Configuration

1. Extend `ZoneLifecycleConfig` with `DebugEventZoneConfig`
2. Update JSON schema documentation
3. Add migration from legacy config format

### Phase 6: Deprecate VBloodUnlockCommands

1. Mark `VBloodUnlockCommands` as `[Obsolete]`
2. Create new `.debug` commands that use `DebugEventBridge`

## Before/After Comparison

### Before (Speculative VBlood Code - TO DELETE)

```csharp
private void HandleVBloodOnEnter(ulong steamId, string zoneId)
{
    var mapping = ResolveMapping(zoneId, out _);
    
    if (mapping.EnableVBloodProgress)
    {
        SaveVBloodProgress(steamId);
        // Uses non-existent VBloodProgression component
        var progress = GetCurrentVBloodProgress(steamId);
    }
}

private VBloodData GetCurrentVBloodProgress(ulong steamId)
{
    // CRASH: VBloodProgression doesn't exist in V Rising ECS
    if (_entityManager.HasComponent<VBloodProgression>(playerEntity))
    {
        var vbloodProgression = _entityManager.GetComponentData<VBloodProgression>(playerEntity);
        // ...
    }
}
```

### After (DebugEventBridge Integration - TO KEEP)

```csharp
private void HandleSpellbookOnEnter(ulong steamId, string zoneId)
{
    // No VBlood handling here anymore
    var mapping = ResolveMapping(zoneId, out _);
    
    if (mapping.EnableSpellbookMenu)
    {
        // Spellbook menu logic only
    }
}

// VBlood is handled entirely by DebugEventBridge
// OnPlayerEnter → Backup → Execute Events → OnPlayerExit → Restore
```

## Checkpoint Summary

| Phase | Action | Status |
|-------|--------|--------|
| 1 | Create DebugEventBridge | Pending |
| 1 | Create BossFeedUtility | Pending |
| 2 | DELETE HandleVBloodOnEnter/Exit | Pending |
| 2 | DELETE Get/Set VBloodProgress | Pending |
| 2 | DELETE VBloodProgressState, VBloodData | Pending |
| 3 | ADD DebugEventBridge field | Pending |
| 3 | ADD DebugEventBridge initialization | Pending |
| 4 | CREATE lifecycle actions | Pending |
| 5 | UPDATE configuration | Pending |
| 6 | DEPRECATE VBloodUnlockCommands | Pending |
3. Update documentation to reflect new approach

## API Integration Points

### V Rising Progression API

Based on V Rising's internal systems:

```csharp
// Unlock all VBloods for a player
player.BloodAltar.UnlockVBlood(vbloodPrefab);

// Unlock tech/prefab
player.Progression.UnlockTech(techPrefab);
player.Progression.UnlockBlueprint(blueprintPrefab);
player.Progression.UnlockShapeshift(shapeshiftPrefab);

// Spawn gameplay event
ServerGameManager.CreateGameplayEvent(
    eventPrefab,
    targetEntity,
    userIndex
);

// Update client UI
player.Client.UpdateProgressionUI();
```

### Entity Query Integration

Using queries from `ServerSystemQueries.txt`:

- `VBloodQuery` - Find VBlood entities
- `PlayerProgressionQuery` - Find player progression components
- `GameplayEventSpawnerQuery` - Event spawning systems

## Rollback Strategy

### Backup Format

```csharp
PlayerProgressionBackup backup = new()
{
    UnlockedVBloods = player.BloodAltar.GetUnlockedVBloods().ToHashSet(),
    UnlockedTechs = player.Progression.UnlockedTechs.ToHashSet(),
    UnlockedBlueprints = player.Progression.UnlockedBlueprints.ToHashSet(),
    UnlockedShapeshifts = player.Progression.UnlockedShapeshifts.ToHashSet(),
    BackupTime = DateTime.UtcNow
};
```

### Restore Process

```csharp
// Clear all unlocks
player.BloodAltar.ClearAllVBloods();
player.Progression.ClearAllTechs();
player.Progression.ClearAllBlueprints();
player.Progression.ClearAllShapeshifts();

// Restore from backup
foreach (var vblood in backup.UnlockedVBloods)
    player.BloodAltar.UnlockVBlood(vblood);

// ... restore other systems

player.Client.UpdateProgressionUI();
```

## Boss Entity Cleanup

### DestroyUtility Integration

When debug zones spawn boss entities for events, ensure proper cleanup on player exit using `DestroyUtility.Destroy`:

```csharp
public static class BossFeedUtility
{
    public static void DestroyBossEntity(Entity bossEntity)
    {
        if (bossEntity == null) return;

        // Clear any CreateGameplayEventsOnSpawn buffers first
        var buffs = bossEntity.GetBuffs();
        foreach (var buff in buffs)
        {
            buff.ClearCreateGameplayEventsOnSpawn();
        }

        // Manually destroy the boss entity server-side
        DestroyUtility.Destroy(bossEntity);
    }
}
```

### Key Points

- **Server-side only**: Ensures no client desync
- **Clear buffers first**: Prevents vanilla feed rewards (techs, trophies, blood progression)
- **Custom unlocks still apply**: DebugEventSystem unlocks happen before/after cleanup
- **Visuals intact**: Player still sees feed animation and boss disappearance

### Integration with DebugEventBridge

```csharp
public class DebugEventBridge
{
    private readonly Dictionary<ulong, Entity> _spawnedBosses = new();

    public void OnPlayerEnter(ulong steamId, string zoneId)
    {
        var player = PlayerManager.GetPlayer(steamId);
        if (player == null) return;

        // Spawn boss entity for event
        if (ShouldSpawnBoss(zoneId))
        {
            var bossEntity = SpawnBossForEvent(zoneId);
            _spawnedBosses[steamId] = bossEntity;
        }

        // Unlock all progression
        UnlockAll(player);
    }

    public void OnPlayerExit(ulong steamId, string zoneId)
    {
        // Cleanup spawned boss entity
        if (_spawnedBosses.TryGetValue(steamId, out var bossEntity))
        {
            BossFeedUtility.DestroyBossEntity(bossEntity);
            _spawnedBosses.Remove(steamId);
        }

        // Restore player progression
        RestorePlayer(steamId);
    }

    private Entity SpawnBossForEvent(string zoneId)
    {
        // Spawn boss entity using V Rising spawn systems
        // Configure to not fire vanilla rewards
        return entity;
    }
}
```

## Testing Plan

### Unit Tests

1. Test backup creation
2. Test rollback accuracy
3. Test zone transition handling
4. Test concurrent player handling

### Integration Tests

1. Test with local V Rising server
2. Test with multiple players
3. Test with different zone configurations
4. Test save/load persistence

## Risks and Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| API changes in updates | High | Use reflection patterns, version checks |
| Concurrent access | Medium | Thread-safe dictionaries, locks |
| Player disconnect during zone | Low | Proper cleanup in Dispose |
| Large backup memory | Low | Limit backup size, cleanup old entries |

## Files to Modify/Create

### Create

- `Services/DebugEventBridge.cs` (NEW)
- `Services/DebugEventPatches.cs` (NEW)
- `Services/BossFeedUtility.cs` (NEW) - Boss entity cleanup with DestroyUtility
- `Vlifecycle/Actions/DebugEventActions.cs` (Vlifecycle project)

### Modify

- `Services/ZoneLifecycleBridge.cs` - Replace VBlood methods
- `Services/ZoneLifecycleConfig.cs` - Add DebugEvent config
- `config/VAuto.ZoneLifecycle.json` - Add debugEvents section
- `Commands/Core/VBloodUnlockCommands.cs` - Deprecate, create new commands

### Remove (After Migration)

- `GetCurrentVBloodProgress()` - Replaced by backup
- `SetVBloodProgress()` - Replaced by restore
- `VBloodProgression` component usage - Doesn't exist
