# Blueluck Migration: Old → New Mapping

## Summary
This document lists what moved or was consolidated for VBlueluck. It includes:
- Service files copied from Bluelock
- Flow registry consolidation
- Zone flow mapping (legacy → two flows)

## Current Status (2026-03-05)
- **Project Location:** `New folder/VBlueluck.csproj`
- **Project Name:** Blueluck (Assembly: Blueluck)
- **Root Namespace:** Blueluck

## Services: Old → New
The following service files were copied into VBlueluck for consolidation work:

### Already Migrated (14 files)
| Old Path | New Path | Status |
|---|---|---|
| `Bluelock/Services/ArenaDeathTracker.cs` | `New folder/Services/ArenaDeathTracker.cs` | ✅ Done |
| `Bluelock/Services/ArenaRespawnHelper.cs` | `New folder/Services/ArenaRespawnHelper.cs` | ✅ Done |
| `Bluelock/Services/BluelockDomainConfigService.cs` | `New folder/Services/BluelockDomainConfigService.cs` | ✅ Done |
| `Bluelock/Services/BuildingService.cs` | `New folder/Services/BuildingService.cs` | ✅ Done |
| `Bluelock/Services/KitService.cs` | `New folder/Services/KitService.cs` | ✅ Done |
| `Bluelock/Services/PlayerSnapshotService.cs` | `New folder/Services/PlayerSnapshotService.cs` | ✅ Done |
| `Bluelock/Services/PrefabRemapService.cs` | `New folder/Services/PrefabRemapService.cs` | ✅ Done |
| `Bluelock/Services/ZoneBossSpawnerService.cs` | `New folder/Services/ZoneBossSpawnerService.cs` | ✅ Done |
| `Bluelock/Services/ZoneDetectionService.cs` | `New folder/Services/ZoneDetectionService.cs` | ✅ Done |
| `Bluelock/Services/ZoneFlowRegistryService.cs` | `New folder/Services/ZoneFlowRegistryService.cs` | ✅ Done |
| `Bluelock/Services/ZoneNoDurabilityService.cs` | `New folder/Services/ZoneNoDurabilityService.cs` | ✅ Done |
| `Bluelock/Services/ZonePlayerTagService.cs` | `New folder/Services/ZonePlayerTagService.cs` | ✅ Done |
| `Bluelock/Services/ZoneTrackingService.cs` | `New folder/Services/ZoneTrackingService.cs` | ✅ Done |
| `Bluelock/Services/AbilityUi.cs` | `New folder/Services/AbilityUi.cs` | ✅ Done |

### Missing Files (Need to be added)
The following files exist in Bluelock but are NOT yet in VBlueluck:

| Old Path | New Path | Status |
|---|---|---|
| `Bluelock/Services/ArenaMatchManager.cs` | `New folder/Services/ArenaMatchManager.cs` | ❌ Missing |
| `Bluelock/Services/ArenaMatchUtilities.cs` | `New folder/Services/ArenaMatchUtilities.cs` | ❌ Missing |
| `Bluelock/Services/ArenaZoneConfigLoader.cs` | `New folder/Services/ArenaZoneConfigLoader.cs` | ❌ Missing |
| `Bluelock/Services/Building.cs` | `New folder/Services/Building.cs` | ❌ Missing |
| `Bluelock/Services/ConfigSeedService.cs` | `New folder/Services/ConfigSeedService.cs` | ❌ Missing |
| `Bluelock/Services/DefaultConfigManifest.cs` | `New folder/Services/DefaultConfigManifest.cs` | ❌ Missing |
| `Bluelock/Services/ProcessConfigService.cs` | `New folder/Services/ProcessConfigService.cs` | ❌ Missing |
| `Bluelock/Services/SchematicZoneService.cs` | `New folder/Services/SchematicZoneService.cs` | ❌ Missing |
| `Bluelock/Services/StaggeredManifestationService.cs` | `New folder/Services/StaggeredManifestationService.cs` | ❌ Missing |
| `Bluelock/Services/TemplateRepository.cs` | `New folder/Services/TemplateRepository.cs` | ❌ Missing |
| `Bluelock/Services/TemplateSnapshotService.cs` | `New folder/Services/TemplateSnapshotService.cs` | ❌ Missing |
| `Bluelock/Services/ZoneConfigService.cs` | `New folder/Services/ZoneConfigService.cs` | ❌ Missing |
| `Bluelock/Services/ZoneLifecycleConfigVersionMigration.cs` | `New folder/Services/ZoneLifecycleConfigVersionMigration.cs` | ❌ Missing |
| `Bluelock/Services/ZoneSchematicLoader.cs` | `New folder/Services/ZoneSchematicLoader.cs` | ❌ Missing |
| `Bluelock/Services/ZoneStructureLoader.cs` | `New folder/Services/ZoneStructureLoader.cs` | ❌ Missing |
| `Bluelock/Services/ZoneTemplateRegistry.cs` | `New folder/Services/ZoneTemplateRegistry.cs` | ❌ Missing |
| `Bluelock/Services/ZoneTemplateService.cs` | `New folder/Services/ZoneTemplateService.cs` | ❌ Missing |
| `Bluelock/Services/Lifecycle/SpellbookLifecycleService.cs` | `New folder/Services/Lifecycle/SpellbookLifecycleService.cs` | ❌ Missing |
| `Bluelock/Services/Lifecycle/VBloodLifecycleService.cs` | `New folder/Services/Lifecycle/VBloodLifecycleService.cs` | ❌ Missing |

## Patches: Migration Required
The following patch files exist in Bluelock but are NOT yet in VBlueluck:

| Old Path | New Path | Status |
|---|---|---|
| `Bluelock/Patches/ArenaManagementPatch.cs` | `New folder/Patches/ArenaManagementPatch.cs` | ❌ Missing |
| `Bluelock/Patches/DropInventorySystemPatch.cs` | `New folder/Patches/DropInventorySystemPatch.cs` | ❌ Missing |
| `Bluelock/Patches/VampireCommandFrameworkPatch.cs` | `New folder/Patches/VampireCommandFrameworkPatch.cs` | ❌ Missing |

## Systems: Migration Required
The following system files exist in Bluelock but are NOT yet in VBlueluck:

| Old Path | New Path | Status |
|---|---|---|
| `Bluelock/Systems/FlowExecutionSystem.cs` | `New folder/Systems/FlowExecutionSystem.cs` | ❌ Missing |
| `Bluelock/Systems/ZoneBootstrapSystem.cs` | `New folder/Systems/ZoneBootstrapSystem.cs` | ❌ Missing |
| `Bluelock/Systems/ZoneDetectionSystem.cs` | `New folder/Systems/ZoneDetectionSystem.cs` | ❌ Missing |
| `Bluelock/Systems/ZoneTemplateLifecycleSystem.cs` | `New folder/Systems/ZoneTemplateLifecycleSystem.cs` | ❌ Missing |
| `Bluelock/Systems/ZoneTransitionRouterSystem.cs` | `New folder/Systems/ZoneTransitionRouterSystem.cs` | ❌ Missing |

## Commands: Migration Required
The following command files exist in Bluelock but are NOT yet in VBlueluck:

| Old Path | New Path | Status |
|---|---|---|
| `Bluelock/Commands/GlowCommands.cs` | `New folder/Commands/GlowCommands.cs` | ❌ Missing |
| `Bluelock/Commands/GlowDebugCommands.cs` | `New folder/Commands/GlowDebugCommands.cs` | ❌ Missing |
| `Bluelock/Commands/Converters/FoundMapIconConverter.cs` | `New folder/Commands/Converters/FoundMapIconConverter.cs` | ❌ Missing |
| `Bluelock/Commands/Core/FlowCommands.cs` | `New folder/Commands/Core/FlowCommands.cs` | ❌ Missing |
| `Bluelock/Commands/Core/MatchCommands.cs` | `New folder/Commands/Core/MatchCommands.cs` | ❌ Missing |
| `Bluelock/Commands/Core/QuickZoneCommands.cs` | `New folder/Commands/Core/QuickZoneCommands.cs` | ❌ Missing |
| `Bluelock/Commands/Core/SpawnCommands.cs` | `New folder/Commands/Core/SpawnCommands.cs` | ❌ Missing |
| `Bluelock/Commands/Core/TagCommands.cs` | `New folder/Commands/Core/TagCommands.cs` | ❌ Missing |
| `Bluelock/Commands/Core/TemplateCommands.cs` | `New folder/Commands/Core/TemplateCommands.cs` | ❌ Missing |
| `Bluelock/Commands/Core/VBloodUnlockCommands.cs` | `New folder/Commands/Core/VBloodUnlockCommands.cs` | ❌ Missing |
| `Bluelock/Commands/Core/ZoneCommands.cs` | `New folder/Commands/Core/ZoneCommands.cs` | ❌ Missing |

## Flow Registry: Old → New
Legacy flow registries:
- `CycleBorn/Configuration/flows.registry.json` (currently empty)
- `Bluelock/config/zone.flows.registry.json`

New unified registry target:
- `New folder/config/flows.registry.json`

## Flow Mapping: Legacy → Two Flows
All legacy flows are collapsed into two flow sets per zone type:

### PVP Zone
- **Enter flow:** `zone.enter.pvpzone`
  - Merges: `zone.enter.1`, `zone.enter.2`, `zone.enter.3`, `zone.enter.arena`, `zone.enter.default`
- **Exit flow:** `zone.exit.pvpzone`
  - Merges: `zone.exit.1`, `zone.exit.2`, `zone.exit.3`, `zone.exit.arena`, `zone.exit.default`

### Boss Zone
- **Enter flow:** `zone.enter.bosszone`
  - Based on `zone.enter.default` (plus boss message)
- **Exit flow:** `zone.exit.bosszone`
  - Based on `zone.exit.default`

## Zone Handlers (2 Total)
Each zone type has its own dedicated handler:
- `PvpZoneHandler` → handles `pvpzone` enter/exit flow dispatch.
- `BossZoneHandler` → handles `bosszone` enter/exit flow dispatch.

## Zones Config (Simplified)
New zones config target:
- `New folder/config/zones.json`

Required fields per zone:
- `id`
- `type` (only `pvpzone` or `bosszone`)
- `shape` (Circle/Rectangle)
- position fields matching the current ZoneDefinition usage

## Notes
- Snapshots for flows/actions must use `Core/Services/SandboxSnapshotStore.cs`.
- GameAction handlers should remain registered (legacy actions preserved).

## Migration Action Plan
To complete the migration, the following files need to be copied/moved from Bluelock to VBlueluck:

### Priority 1: Essential Services (Copy first)
1. `Bluelock/Services/ZoneConfigService.cs` → `New folder/Services/ZoneConfigService.cs`
2. `Bluelock/Services/ZoneTemplateService.cs` → `New folder/Services/ZoneTemplateService.cs`
3. `Bluelock/Services/ProcessConfigService.cs` → `New folder/Services/ProcessConfigService.cs`
4. `Bluelock/Services/ConfigSeedService.cs` → `New folder/Services/ConfigSeedService.cs`

### Priority 2: Lifecycle Services
5. `Bluelock/Services/Lifecycle/SpellbookLifecycleService.cs` → `New folder/Services/Lifecycle/SpellbookLifecycleService.cs`
6. `Bluelock/Services/Lifecycle/VBloodLifecycleService.cs` → `New folder/Services/Lifecycle/VBloodLifecycleService.cs`

### Priority 3: Template & Snapshot Services
7. `Bluelock/Services/TemplateSnapshotService.cs` → `New folder/Services/TemplateSnapshotService.cs`
8. `Bluelock/Services/TemplateRepository.cs` → `New folder/Services/TemplateRepository.cs`
9. `Bluelock/Services/SchematicZoneService.cs` → `New folder/Services/SchematicZoneService.cs`

### Priority 4: Systems
10. `Bluelock/Systems/ZoneDetectionSystem.cs` → `New folder/Systems/ZoneDetectionSystem.cs`
11. `Bluelock/Systems/ZoneTransitionRouterSystem.cs` → `New folder/Systems/ZoneTransitionRouterSystem.cs`
12. `Bluelock/Systems/FlowExecutionSystem.cs` → `New folder/Systems/FlowExecutionSystem.cs`
13. `Bluelock/Systems/ZoneBootstrapSystem.cs` → `New folder/Systems/ZoneBootstrapSystem.cs`

### Priority 5: Patches
14. `Bluelock/Patches/ArenaManagementPatch.cs` → `New folder/Patches/ArenaManagementPatch.cs`
15. `Bluelock/Patches/VampireCommandFrameworkPatch.cs` → `New folder/Patches/VampireCommandFrameworkPatch.cs`

### Priority 6: Commands (Optional - may be consolidated)
16. All command files from `Bluelock/Commands/` → `New folder/Commands/`

## Config Files to Migrate
- `Bluelock/config/zone.flows.registry.json` → `New folder/config/flows.registry.json`
- `Bluelock/config/Bluelock.cfg` → `New folder/config/Blueluck.cfg`

## Project File Verification
The current `VBlueluck.csproj` has the following includes:

```xml
<Compile Include="Core\**\*.cs" />
<Compile Include="Services\**\*.cs" />  <!-- ✅ Added during migration -->
<Compile Include="Plugin.cs" />
```

Also need to add if migrating:
- `Systems\**\*.cs`
- `Patches\**\*.cs`
- `Commands\**\*.cs`

## Verification Steps
1. ✅ Ensure all services compile with the new namespace (`Blueluck`)
2. ✅ Updated project file to include Services folder
3. Verify all dependencies are resolved
4. Test zone detection and flow execution
