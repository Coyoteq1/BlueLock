# Zone + Buff + Equipment + Inventory System Visual Plan

## Overview
This document provides a visual representation of how zones, buffs, equipment, inventory, and auto-enter/exit work together in VAutomationCore/Blueluck plugin.

---

## Complete System Architecture

```mermaid
flowchart TD
    VRising["V Rising Game\n(ECS World)"]
    DebugEvents["DebugEventsSystem\n(ECS)"]
    
    ZD[ZoneDetectionService] --> ZDS[ZoneDetectionSystem]
    ZDS --> ZTS[ZoneTransitionService]
    ZTS --> ZC[ZoneConfigService]
    ZC --> ZDef[ZoneDefinition]
    ZC --> UZLC[UnifiedZoneLifecycleConfig]
    
    ZTS --> LC[Lifecycle System]
    ZTS --> PS[ProgressService]
    PS --> INV[Inventory]
    PS --> EQ[Equipment]
    PS --> Buffs[Buff]
    PS --> HP[Health/Blood]
    
    ZTS --> FR[FlowRegistryService]
    FR --> US[UnlockService]
    US --> DebugEvents
    DebugEvents --> VRising
    
    JSON["JSON Config"] --> ZC
    CFG["settings.cfg"] --> Plugins[Plugins]
```

---

## Zone Entry → Buff Sequence

```mermaid
sequenceDiagram
    participant P as Player
    participant ZDS as ZoneDetectionSystem
    participant ZTS as ZoneTransitionService
    participant FR as FlowRegistryService
    participant US as UnlockService
    participant DE as DebugEventsSystem
    
    P->>ZDS: Enters Zone Area
    ZDS->>ZTS: OnZoneEnter(player, zone)
    
    alt Zone Type Handling
        ZTS->>ZTS: HandleBossZoneEnter()
        ZTS->>ZTS: HandleArenaZoneEnter()
    end
    
    alt Flow Execution
        ZTS->>FR: ExecuteFlow(FlowOnEnter, player)
        FR->>FR: Process FlowActions
        
        alt SetPvP Action
            FR->>US: ApplyBuff(player, PvPBuffGUID)
            US->>DE: ApplyBuff(fromCharacter, buffEvent)
            DE->>P: Apply Buff to Player
        end
    end
```

---

## Three-Stage Lifecycle: Auto Enter/Exit

```mermaid
stateDiagram-v2
    [*] --> OutsideZone
    
    OutsideZone --> Entering: Player enters zone
    Entering --> InsideZone: onEnter executes
    InsideZone --> Exiting: Player exits zone
    Exiting --> OutsideZone: onExit executes
    
    InsideZone --> InsideZone: isInZone repeats
```

---

## Progress Save/Restore: Inventory & Equipment

```mermaid
flowchart LR
    subgraph EnterZone["onEnter - SAVE"]
        P1[Player] --> INV1[Save Inventory]
        INV1 --> EQ1[Save Equipment]
        EQ1 --> Buffs1[Save Buffs]
        Buffs1 --> HP1[Save Health/Blood]
        HP1 --> Pos1[Save Position]
    end
    
    subgraph ExitZone["onExit - RESTORE"]
        Clean2[Clean State] --> Pos2[Restore Position]
        Pos2 --> HP2[Restore Health/Blood]
        HP2 --> Buffs2[Restore Buffs]
        Buffs2 --> EQ2[Restore Equipment]
        EQ2 --> INV2[Restore Inventory]
    end
```

---

## Configuration: JSON vs Settings.cfg

**JSON Files (config/):**
- zones.json - Zone definitions
- flows.json - Flow actions  
- VAuto.unified_config.json - Unified settings

**Settings.cfg (BepInEx):**
- enableLogging
- logLevel
- apiPort
- Plugin-specific toggles

---

## ECS Zone Event Systems

| System | File | Purpose |
|--------|------|---------|
| ZoneBuffSpawnSystem | Blueluck/ECS/Systems/ | Processes ZoneApplyBuffEvent, ZoneRemoveBuffEvent, ZoneSendMessageEvent |
| ZoneBossSystem | Blueluck/ECS/Systems/ | Processes ZoneSpawnBossEvent, ZoneRemoveBossEvent |
| ZoneDetectionSystem | Blueluck/Systems/ | Monitors player positions for zone transitions |

## ECS Event Components

- ZoneApplyBuffEvent - triggers buff application
- ZoneRemoveBuffEvent - triggers buff removal
- ZoneSendMessageEvent - triggers chat message
- ZoneSpawnBossEvent - triggers boss spawn
- ZoneRemoveBossEvent - triggers boss removal

---

## Zone Configuration JSON Structure

```json
{
  "zones": [
    {
      "type": "ArenaZone",
      "name": "PvPArena",
      "hash": 12345678,
      "center": [100.0, 50.0, -200.0],
      "entryRadius": 50.0,
      "exitRadius": 60.0,
      "enabled": true,
      "flowOnEnter": "arena_enter",
      "flowOnExit": "arena_exit"
    }
  ]
}
```

## Flow Configuration (flows.json)

```json
{
  "flows": {
    "arena_enter": [
      { "action": "zone.setpvp", "value": true },
      { "action": "zone.apply_buff", "prefab": "VIB_Generic_Buff" }
    ]
  }
}
```

## Unified Config (vlifecycle)

```json
{
  "vlifecycle": {
    "arena": {
      "saveInventory": true,
      "restoreInventory": true,
      "saveBuffs": true,
      "clearArenaBuffsOnExit": true
    },
    "playerState": {
      "saveEquipment": true,
      "saveBlood": true,
      "saveHealth": true
    }
  }
}
```

---

## Implementation Checklist

- [x] Zone Detection & Lifecycle
- [x] ProgressService - save/restore inventory, equipment, buffs, health, blood
- [x] Buff Application via DebugEventsSystem
- [x] Flow system with zone.setpvp, zone.apply_buff
- [x] JSON configuration for zones and flows
- [x] Auto-save on enter, auto-restore on exit

---

*Generated: 2026-03-05*