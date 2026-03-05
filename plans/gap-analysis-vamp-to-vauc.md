# VAutomationCore & VAuto.Zone Gap Analysis
## Comparing with Temp/VAMP (Reference Implementation)

**Date**: 2026-03-05  
**Purpose**: Identify missing APIs, Services, Utilities, Patches, and Systems that need to be migrated or created in VAutomationCore/VAuto.Zone

---

## Executive Summary

The Temp folder contains the VAMP (Versatile API for Modding ProjectM) reference implementation with ~24 public static classes. The current VAutomationCore + Bluelock (VAuto.Zone) has ~144 public static classes combined. The gap analysis reveals significant functionality in VAMP that has either been migrated, partially implemented, or is completely missing.

---

## 1. SERVICES COMPARISON

### ✅ FULLY MIGRATED (Match or Exceed VAMP)
| VAMP (Temp) | VAutomationCore/Bluelock | Status |
|-------------|--------------------------|--------|
| `PlayerService` | `PlayerApi` (Core/Api) | ✅ Migrated - different approach using EntityMap |
| `CastleHeartService` | `CastleApi` (Core/Api) | ✅ Migrated |
| `CastleTerritoryService` | `CastleApi` (Core/Api) | ✅ Migrated |
| `SpawnService` | `EntitySpawner` (Core/Services) | ✅ Migrated with enhancements |
| - | `EntitySpawnerIntegrationExamples` | ✅ Additional examples |

### ⚠️ PARTIALLY MIGRATED
| VAMP (Temp) | Current Implementation | Gap |
|-------------|----------------------|-----|
| `EventScheduler` | Not found | Missing event scheduling system |
| `ClanService` | Not found | Missing clan management utilities |
| `SystemService` | `Sys` (Core/ECS/JobSystemExtensions.cs) | Partial - needs full system access wrapper |

### ❌ MISSING FROM VAMP
| Missing Service | Location | Notes |
|-----------------|----------|-------|
| `EmailWebhookService` | Core/Services | ✅ Exists in Core |
| `HttpServer` | Core/Services | ✅ Exists in Core |
| `TileService` | Core/Services | ✅ Exists in Core |
| `VisualService` | Core/Services | ✅ Exists in Core |
| `GlowService` | Core/Services | ✅ Exists in Core |
| `ModCommunicationService` | Core/Services | ✅ Exists in Core |

---

## 2. UTILITIES COMPARISON

### ✅ FULLY MIGRATED
| VAMP (Temp) | VAutomationCore | Status |
|-------------|-----------------|--------|
| `EntityUtil` | `ECSExtensions` (Extensions/) | ✅ Migrated - similar Read/Write methods |
| `BuffUtil` | `Buffs` (Core/Abstractions) | ✅ Migrated - Abilities static class |
| - | `PrefabGUIDExtensions` | ✅ Additional in Core |

### ⚠️ PARTIALLY MIGRATED
| VAMP (Temp) | Current Implementation | Gap |
|-------------|----------------------|-----|
| `ChatUtil` | `ChatColor` (Core/Commands) | Partial - needs full chat utilities |
| `ItemUtil` | `GameActionService` | Partial - item manipulation in GameActionService |
| `UnitUtil` | `EntityExtensions` | Partial - unit utilities in EntityExtensions |
| `SettingsUtil` | `ConfigService` | Partial - settings in ConfigService |
| `JsonUtil` | `TypedJsonConfigManager` | Partial - JSON in Core/Config |
| `DevUtil` | Not found | Missing developer utilities |

### ❌ MISSING
| Missing Utility | Notes |
|-----------------|-------|
| `DevUtil` | Debug utilities not migrated |
| Event voting utilities | EventScheduler-related utilities |

---

## 3. PATCHES COMPARISON

### ✅ FULLY MIGRATED
| VAMP (Temp) | VAutomationCore | Status |
|-------------|-----------------|--------|
| `InitializationPatch` | `ServerReadySystem` | ✅ Server readiness handling |
| `ConnectivityPatchs` | `VAutoPatches.PlayerConnectivityPatches` | ✅ User connect/disconnect |
| `ChatMessagePatch` | Not found | ✅ Needs migration |
| `UnitSpawnerPatch` | `UnitSpawnerSystemPatch` (Patches/) | ✅ Migrated |

### ❌ MISSING
| Missing Patch | Notes |
|---------------|-------|
| `ChatMessagePatch` | Chat command handling needs migration |
| `EquipmentPatches` | Equipment handling not migrated |

---

## 4. SYSTEMS COMPARISON

### ✅ FULLY MIGRATED
| VAMP (Temp) | Bluelock/VAutomationCore | Status |
|-------------|--------------------------|--------|
| - | `FlowExecutionSystem` (Bluelock/Systems) | Zone flow execution |
| - | `ZoneDetectionSystem` (Bluelock/Systems) | Zone detection |
| - | `ZoneTransitionRouterSystem` (Bluelock/Systems) | Zone transitions |
| - | `ZoneBootstrapSystem` (Bluelock/Systems) | Zone bootstrap |
| - | `LifecycleEventBridgeSystem` (Core/Systems) | Core lifecycle |

### ⚠️ PARTIALLY MIGRATED
| VAMP (Temp) | Current Implementation | Gap |
|-------------|----------------------|-----|
| `FileWatcherSystem` | `LifecycleConfigWatcher` (Core/Config) | Partial - config file watching only |
| `ModSystem` | Not found | Mod enumeration needs migration |
| `ModTalk` | `ModCommunicationService` | Partial - mod communication |
| `ModProfiler` | Not found | Missing mod profiling |
| `RecordLevelSystem` | Not found | Missing |
| `ServerWipe` | Not found | Missing |

---

## 5. DATA CLASSES COMPARISON

### ✅ FULLY MIGRATED
| VAMP (Temp) | Bluelock/VAutomationCore | Status |
|-------------|--------------------------|--------|
| `Prefabs` | `PrefabsCatalog`, `PrefabReferenceCatalog` | ✅ Enhanced |
| `VBloods` | `VBloods` (Bluelock/Data) | ✅ Migrated |
| `WorldRegions` | Not found (may not be needed) | ⚠️ Optional |
| `Territory` | Not found (may not be needed) | ⚠️ Optional |

---

## 6. CORE INFRASTRUCTURE

### ✅ FULLY MIGRATED
| VAMP (Temp) | VAutomationCore | Status |
|-------------|-----------------|--------|
| `Core` (static class) | `UnifiedCore` (Core/) | ✅ Migrated |

---

## PRIORITY MIGRATION ITEMS

### HIGH PRIORITY
1. **EventScheduler** - Event scheduling system with voting
2. **ChatMessagePatch** - Chat command handling
3. **DevUtil** - Developer utilities for debugging
4. **ModSystem** - Mod enumeration and info

### MEDIUM PRIORITY
5. **ModProfiler** - Mod performance profiling
6. **ClanService** - Clan management utilities
7. **EquipmentPatches** - Equipment handling patches
8. **FileWatcherSystem** - Enhanced file watching

### LOW PRIORITY
9. **RecordLevelSystem** - Level recording
10. **ServerWipe** - Server wipe utilities

---

## ARCHITECTURAL DIFFERENCES

### VAMP Approach
- Static service classes with internal state
- Heavy use of caching (dictionaries)
- Central Core class for all access

### VAutomationCore Approach
- More modular with separate concerns
- EntityMap instead of direct caching
- TypedEventBus for events
- FlowService for flow execution

---

## RECOMMENDATIONS

1. **Migrate EventScheduler** to Bluelock/Services as it's zone/event specific
2. **Migrate ChatMessagePatch** to Core/Patches or Bluelock/Patches
3. **Create DevUtil** in Core/Utilities or Core/Services
4. **Create ModSystem** in Core/Services with mod enumeration
5. **Review FileWatcherSystem** - current LifecycleConfigWatcher may be sufficient

---

## FILES TO CREATE/MODIFY

### New Files
- `Core/Services/EventScheduler.cs` - Event scheduling
- `Core/Services/ModSystem.cs` - Mod enumeration
- `Core/Utilities/DevUtil.cs` - Developer utilities
- `Core/Patches/ChatMessagePatch.cs` - Chat handling

### Modified Files
- `Core/ECS/JobSystemExtensions.cs` - Enhance Sys class
- `Core/Commands/ChatColor.cs` - Expand chat utilities
- `Bluelock/Systems/` - Add missing systems

---

*End of Gap Analysis*
