# Cross-Mod API Usage

## Purpose
Document the public API surface intended for other mods to integrate with VAutomationEvents.

## Public API Namespaces
- `VAuto.API.ZoneAPI`
- `VAuto.API.LifecycleAPI`
- `VAuto.API.VAutoEvents`

## Example: Subscribe to VAuto Events
```csharp
using VAuto.API;
using Unity.Entities;

public class MyModIntegration
{
    public void Initialize()
    {
        VAutoEvents.OnPlayerEnteredZone += HandleZoneEnter;
        VAutoEvents.OnKillStreakMilestone += HandleKillStreak;
    }

    private void HandleZoneEnter(Entity player, string zoneName)
    {
        // Custom logic for zone entry
    }

    private void HandleKillStreak(Entity player, int streak)
    {
        // Custom logic for streak milestones
    }
}
```

## Example: Query Zone/Lifecycle State
```csharp
using VAuto.API;
using Unity.Entities;

public static class MyZoneChecks
{
    public static bool IsPlayerInMainArena(Entity player)
    {
        return ZoneAPI.IsPlayerInZone(player, "MainArena");
    }

    public static bool IsPlayerInLifecycle(Entity player)
    {
        return LifecycleAPI.IsInLifecycleZone(player);
    }
}
```

## Notes
- These APIs are scaffolds and will return default values until ECS systems are fully enabled.
- Prefer soft dependencies and runtime detection for compatibility.

## Arena Auto-Enter / Auto-Exit API
The modular arena lifecycle exposes Auto-Enter and Auto-Exit workflows via services, lifecycle events, and commands. Consumers should rely on the published services/events rather than touching IL2CPP entities directly.

### Auto-Enter Flow (entry points)
1. **Triggering** – `Services/Systems/ZoneTrackingService`, `KillStreakTrackingSystem`, `LifecycleAutoEnterService`, or crash-recovery logic detects entry intent.
2. **Validation** – `LifecycleAutoEnterService` confirms the player is valid, tracked, alive, and not already inside an arena.
3. **Snapshot capture** – `EnhancedArenaSnapshotService.CreateSnapshot()` captures inventory/position/abilities.
4. **Lifecycle registration** – `LifecycleService.RegisterPlayerInArena()` stores timestamps/flags; `LifecycleAPI.EnterArena` exposes the same guard rails.
5. **VBlood preparation** – `Core/Harmony/RepairVBloodProgressionSystemPatch` gates normal repairs and applies snapshot-driven unlocks.
6. **Loadout/teleport/ui** – `ArenaBuildService`, `WorldSpawnService`, `ArenaUIManager`, and allied services teleport the player, apply gear, and show the arena HUD.
7. **Event broadcast** – `LifecycleAutoEnterService` raises `OnPlayerEnterArena`, which traps, kill streaks, notifications, and unlocking flows subscribe to.

### Auto-Exit Flow (exit points)
1. **Triggering** – zone leave, death, timer, trap, or admin command calls `LifecycleAutoEnterService.ExitPlayer` or `LifecycleAPI.ExitArena`.
2. **Repair + lock** – `RepairVBloodProgressionSystemPatch` allows a single repair tick, applies pending snapshot unlocks, and then blocks additional repairs while exit finishes.
3. **Snapshot restoration** – `EnhancedArenaSnapshotService.RestoreSnapshot()` reverts inventory, abilities, and position.
4. **Cleanup** – `AbilityOverrideService`, `ArenaUIManager`, and `LifecycleService` remove arena-specific state; kill streaks and traps are notified via exit events.
5. **Event broadcast** – `OnPlayerExitArena` signals other mods/services to run cleanup logic.

### Debug & Control access
- `Commands/Core/VBloodRepairCommands.cs` exposes `.debug vblood status|phases|reset|pending` to inspect and reset the phase machine.
- `Core/Harmony/RepairVBloodProgressionSystemPatch.ResetPhaseTracking()` and `ArenaUnlockService.ResetArenaFlags()` can be invoked when a crash leaves the lifecycle stuck.
- `Core/RepairGate.cs` offers an ECS-safe guard for manual repair operations triggered during auto-exit.
