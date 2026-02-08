# V Rising Mods Roadmap

## Overview
Unified automation plugins for V Rising servers with arena zones, traps, and lifecycle management.

## Plugins
| Plugin | Purpose | Dependencies |
|--------|---------|--------------|
| VAutomationCore | Core utilities, ECS helpers | BepInEx 5.4+ |
| VAutoZone | Arena territory, glow borders | VAutomationCore |
| Vlifecycle | State persistence, lifecycle stages | VAutomationCore |
| VAutoTraps | Chest spawns, container traps | VAutomationCore |
| VAutoannounce | Broadcasts, alerts | VAutomationCore |

## Architecture
VAutoZone (Position Monitoring) -> ZoneLifecycleBridge -> Vlifecycle (Action Execution) -> Lifecycle Stages: onEnter -> isInZone -> onExit

## Three-Stage Lifecycle Pattern
- **onEnter**: One-time effects (store inventory, apply buffs, send messages)
- **isInZone**: Repeated enforcement (reassert buffs, enforce blood type) - IDEMPOTENT
- **onExit**: Cleanup (restore inventory, remove buffs, cleanup markers)

## Commands

### VAutoZone (.zone / .z)
| Command | Description |
|---------|-------------|
| .zone help | Show help |
| .zone status | Current position/zone info |
| .zone glow [spawn\|clear\|count] | Manage glow borders |
| .zone reload | Reload configuration |

### Vlifecycle (.lifecycle / .lc)
| Command | Description |
|---------|-------------|
| .lifecycle help | Show help |
| .lifecycle status | Current lifecycle state |
| .lifecycle enter [zone] | Force zone entry (Admin) |
| .lifecycle exit | Force zone exit (Admin) |
| .lifecycle stages | List available stages (Admin) |
| .lifecycle config | Show configuration (Admin) |
| .lifecycle trigger [stage] | Trigger stage manually (Admin) |

## Configuration Files

### VAutoZone
- VAuto.ZoneLifecycle.json - Unified zone-lifecycle mappings (RECOMMENDED)
- arena_territory.json - Arena boundaries (LEGACY)
- arena_zones.json - Zone definitions (LEGACY)
- arena_glow_prefabs.json - Glow prefab configs

### Vlifecycle
- VLifecycle.json - Lifecycle stages and actions
- pvp_item.toml - PvP item configs

## UnifiedZoneLifecycleConfig

### Overview
Unified configuration model that combines zone definitions and lifecycle mappings into a single JSON file. Replaces multiple legacy config files.

### Configuration Precedence
1. Zone-specific mapping (highest priority)
2. Wildcard mapping (*)
3. Legacy default (if legacy mode enabled)

### JSON Structure
```json
{
  "zones": [
    {
      "id": "arena_main",
      "name": "Main Arena",
      "territory": {
        "center": [0, 0, 0],
        "radius": 50
      },
      "lifecycle": {
        "onEnterStage": "zone.arena_main.onEnter",
        "onExitStage": "zone.arena_main.onExit",
        "isInZoneStage": "zone.arena_main.isInZone",
        "enableVBloodProgress": true
      }
    }
  ],
  "wildcardMapping": {
    "onEnterStage": "zone.default.onEnter",
    "onExitStage": "zone.default.onExit",
    "useWildcardDefaults": true
  }
}
```

### UnifiedLifecycleMapping Properties
| Property | Type | Description |
|----------|------|-------------|
| onEnterStage | string | Stage name for zone entry |
| onExitStage | string | Stage name for zone exit |
| isInZoneStage | string | Stage name for ongoing zone presence |
| useWildcardDefaults | bool | Use wildcard mapping if no zone-specific config |
| enableVBloodProgress | bool | Enable VBlood state management |

### Zone ID Interpolation
Use `{ZoneId}` placeholder in stage names:
```json
{
  "onEnterStage": "zone.{ZoneId}.onEnter"
}
```
Becomes: `zone.arena_main.onEnter`

### Legacy Mode
Legacy config files are detected automatically:
- `arena_zones.json`
- `arena_territory.json`
- VAuto.ZoneLifecycle.json with `onEnterActions`/`onExitActions`

Warning: Legacy mode is deprecated and will be removed in v2.0

### Migration from Legacy
| Legacy File | Unified Config Location |
|-------------|----------------------|
| arena_zones.json | `zones[]` array |
| arena_territory.json | `zones[].territory` |
| onEnterActions/onExitActions | `zones[].lifecycle.onEnterStage/onExitStage` |

## Trigger Flow: Enter vs Exit

### AUTO Trigger (Boundary Crossing)
**Player enters arena boundary:**
1. ZoneEventBridge scans player positions (every 50-100ms)
2. Detects player crossed into arena territory
3. Calls `DetectAndProcessTransition()`
4. Fires `onEnterArenaZone` stage
5. Executes all registered actions (store inventory, buffs, etc.)

**Player exits arena boundary:**
1. ZoneEventBridge detects position outside territory
2. Calls `DetectAndProcessTransition()`
3. Fires `onExitArenaZone` stage
4. Executes cleanup actions (restore inventory, remove buffs)

### MANUAL Trigger (Commands)
**`.lifecycle enter [zone]`:**
1. Admin executes command
2. Directly calls `ArenaLifecycleManager.OnPlayerEnter()`
3. Fires `onEnterArenaZone` stage
4. Executes actions (same as auto)

**`.lifecycle exit`:**
1. Admin executes command
2. Directly calls `ArenaLifecycleManager.OnPlayerExit()`
3. Fires `onExitArenaZone` stage
4. Executes actions (same as auto)

### Key Difference
| Aspect | AUTO (Boundary) | MANUAL (Command) |
|--------|-----------------|-------------------|
| Trigger | Position check by timer | Direct method call |
| Delay | ~50-100ms | Immediate |
| Zone ID | Auto-detected | Command parameter |
| Position | Actual player pos | float3.zero |
| Logging | Full debug | Minimal |

## Lifecycle Stages Reference

### Built-in Stages
| Stage | Trigger |
|-------|---------|
| onEnterArenaZone | Player enters arena |
| onExitArenaZone | Player exits arena |

### Action Handlers
- **store**: Save player state (inventory, buffs, equipment, blood, spells)
- **message**: Send chat message to player
- **command**: Execute server command
- **config**: Load zone-specific configuration bundle
- **zone**: Zone-specific actions
- **prefix**: Prefix-related actions
- **blood**: Blood type enforcement
- **quality**: Item quality handling

## VBlood Repair System

Provides snapshot-driven unlock mechanism that integrates with V Rising's RepairVBloodProgressionSystem for controlled VBlood management during arena transitions.

### Architecture
```
ZoneLifecycleBridge -> VBloodProgressState -> RepairVBloodProgressionSystem -> VBlood Unlocks
```

### Flow
Player Enters Arena > Capture VBlood Snapshot > Register UnlockVBloods > Arena Active > Player Exits > Restore Original VBlood State

### Core Components
- **VBloodData**: Snapshot data model (UnlockedVBloods, VBloodKills, ProgressPercent)
- **VBloodProgressState**: Tracks zone-specific VBlood state and original progress
- **VBloodRepairCommands**: Test and admin commands for the system

### Commands
| Command | Description | Admin |
|---------|-------------|-------|
| `.vrepair status` | Show repair system status | No |
| `.vrepair force` | Force VBlood repair | Yes |
| `.vrepair reset` | Reset VBlood progression | Yes |
| `.vrepair unlock` | Force unlock all VBloods | Yes |
| `.vrepair enable` | Enable repair system | Yes |
| `.vrepair disable` | Disable repair system | Yes |

### Integration Points

#### On Zone Enter
1. Save current VBlood state to snapshot
2. Initialize zone-specific VBlood state
3. Trigger unlock registration

#### On Zone Exit
1. Save zone-specific progress
2. Restore original VBlood state
3. Clear zone-specific state

### Progress Stages
| Stage | Purpose |
|-------|---------|
| vblood.progressSave | Save VBlood state on entry |
| vblood.progressRestore | Restore original VBlood on exit |
| vblood.isInZone | Maintain VBlood state during arena |
| vblood.unlockCheck | Check and apply unlocks |
| vblood.unlockAll | Force unlock all (testing) |

### Implementation Phases

#### Phase 1: Core Functionality (Completed)
- [x] VBloodRepairCommands.cs - Test commands
- [x] ZoneLifecycleBridge integration
- [x] VBloodProgressState tracking
- [x] VBloodData model

#### Phase 2: ECS Integration (Pending)
- [ ] RepairVBloodProgressionSystemPatch
- [ ] DebugEventSystem integration
- [ ] Actual ECS implementation

#### Phase 3: Production Ready (Pending)
- [ ] Unit tests
- [ ] Performance optimization
- [ ] Error handling and recovery

### Snapshot Pattern
```csharp
// Capture VBlood state
var snapshot = new VBloodData
{
    UnlockedVBloods = GetUnlockedVBloods(),
    VBloodKills = GetVBloodKills(),
    ProgressPercent = GetProgressPercent()
};

// Restore VBlood state
SetVBloodProgress(snapshot);
```

## Debug Commands

### Real-Time Debugging
1. **`.lifecycle status`** - Shows if player is in arena, plugin enabled, initialized
2. **`.lifecycle stages`** - Shows all stages and their action counts
3. **`.lifecycle config`** - Shows save/restore settings for inventory, buffs, equipment, blood, spells
4. **`.lifecycle trigger [stageName]`** - Execute a stage for testing

### Debug Workflow
1. Run `.lifecycle stages` to see registered stages
2. Run `.lifecycle config` to verify settings
3. Test with `.lifecycle trigger onEnterArenaZone`
4. Check server logs for lifecycle debug output
5. Use `.zone status` to verify position/zone detection

## Logging Reference

### Server Log Location
```
BepInEx/logs/*.txt
```

### Log Prefix Patterns
| Component | Prefix | Example |
|-----------|--------|---------|
| ZoneEventBridge | `[ZoneEventBridge]` | `[ZoneEventBridge] Triggered stage 'onEnterArenaZone'` |
| ArenaLifecycleManager | `[ArenaLifecycleManager]` | `[ArenaLifecycleManager] Executing action 'store'` |
| StoreActionHandler | `[StoreActionHandler]` | `[StoreActionHandler] Saved inventory for player X` |
| MessageActionHandler | `[MessageActionHandler]` | `[MessageActionHandler] Sent message to player X` |

### Enter Event Logging
```
[ZoneEventBridge] Player STEAMID entered zone 'arena_main'
[ZoneEventBridge] Triggered stage 'onEnterArenaZone' for player STEAMID
[ArenaLifecycleManager] Executing stage 'onEnterArenaZone'
[StoreActionHandler] SAVED: inventory, buffs, equipment
[MessageActionHandler] SENT: 'Entering arena!'
[ZoneEventBridge] Completed onEnter for STEAMID
```

### Exit Event Logging
```
[ZoneEventBridge] Player STEAMID exited zone 'arena_main'
[ZoneEventBridge] Triggered stage 'onExitArenaZone' for player STEAMID
[ArenaLifecycleManager] Executing stage 'onExitArenaZone'
[StoreActionHandler] RESTORED: inventory, buffs
[MessageActionHandler] SENT: 'Leaving arena!'
[ZoneEventBridge] Completed onExit for STEAMID
```

### What Gets Logged
| Event | Logged | Details |
|-------|--------|---------|
| Stage Trigger | ✅ | Stage name, player ID, zone ID |
| Action Execution | ✅ | Action type, success/failure |
| Action Skip | ✅ | Reason (missing handler, disabled, error) |
| Position Check | ⚠️ | Only if debug enabled |
| State Capture | ⚠️ | Only if verbose logging enabled |

### Skip Reasons (What NOT Executed)
| Reason | Message Example |
|--------|-----------------|
| Disabled | `[ZoneEventBridge] Zone-lifecycle wiring disabled` |
| No Handler | `[ArenaLifecycleManager] No handler for action type: unknown` |
| Unknown Stage | `[ArenaLifecycleManager] Unknown lifecycle stage: unknown` |
| Config Missing | `[ZoneEventBridge] No stages configured for zone X` |

### Enable Verbose Logging
In `VLifecycle.json`:
```json
{
  "lifecycle": {
    "safety": {
      "verboseLogging": true
    }
  }
}
```

### Filter Logs
```bash
# View only lifecycle logs
grep -E "\[ZoneEventBridge\]|\[ArenaLifecycleManager\]" logs/*.txt

# View only enter events
grep "onEnterArenaZone" logs/*.txt

# View only errors
grep -E "ERROR|Warning|Failed" logs/*.txt
```

## Current Status

### Completed
- ZoneEventBridge refactor with three-stage pattern
- UnifiedZoneLifecycleConfig configuration model
- VBlood Repair System
- Comprehensive README documentation

### In Progress
- ZoneLifecycleBridge implementation
- ECS compliance verification

### Pending
- Unit tests for lifecycle stages
- Performance benchmarks
- Hot-reload validation

## V Rising ECS Best Practices
1. Use ToEntityArray(Allocator.Temp) with try-finally
2. Check HasComponent<T> before accessing components
3. Dispose NativeArrays in finally blocks
4. Use Plugin.LogInstance.LogInfo for consistency

## Code Style
- Namespace: VAuto.{PluginName}
- Logger prefix: [{ClassName}]
- Config loading: Use JsonConfigManager
