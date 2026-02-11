# Cycleborne

![PIC 1](./1.png)

## Vlifecycle

Player lifecycle management system for VRising with arena entry/exit handling, state persistence, and spellbook menu.

## Overview

Vlifecycle manages player state throughout gameplay:

- Arena entry/exit orchestration
- State persistence across sessions
- Auto-kit application on zone enter
- State restoration on zone exit
- Spellbook menu integration

## Commands

| Command | Description |
|---------|-------------|
| `.lifecycle status` | Show current lifecycle status |
| `.lifecycle enter [arenaId]` | Enter arena with ID |
| `.lifecycle exit [arenaId]` | Exit arena with ID |
| `.lifecycle debug` | Enable debug mode |

## Kit Integration

### Auto-Kit System

When entering a zone, Vlifecycle can automatically apply equipment kits:

1. Create `BepInEx/config/EndGameKit.json` (auto-generated)
2. Define kit profiles with equipment items
3. Configure `restoreOnExit` to restore previous state

### Kit Configuration Example

```json
{
  "kits": {
    "PvP": {
      "items": ["Weapon1", "Armor1"],
      "restoreOnExit": true
    }
  }
}
```

## Configuration Files

### PvP Items

`Vlifecycle/Configuration/pvp_item.toml`

Defines PvP-related item configurations.

### Lifecycle Config

`Vlifecycle/Configuration/VLifecycle.json`

Main lifecycle configuration settings.

## Installation

1. Install [VAutomationCore](https://github.com/Coyoteq1/VAutomationCore) first
2. Place `Vlifecycle.dll` in `BepInEx/plugins/`

## Dependencies

- VAutomationCore 1.0.0+
- BepInEx 5.4.2105+
- BepInExPack IL2CPP 6.0.0+
- Vampire Command Framework 0.10.4+

## Project Structure

```
Vlifecycle/
├── Commands/          # Command implementations
│   └── LifecycleCommands.cs
├── Configuration/    # Configuration files
│   ├── pvp_item.toml
│   └── VLifecycle.json
└── Services/         # Service layer
    └── Lifecycle/
        ├── ArenaLifecycleManager.cs
        ├── ConnectionEventPatches.cs
        ├── InputSystemUpdatePatch.cs
        ├── LifecycleActionHandlers.cs
        ├── LifecycleModels.cs
        ├── Singleton.cs
        ├── ZUIInputBlocker.cs
        └── ZUISpellMenu.cs
```

## Features

### Arena Integration

- Seamless arena entry/exit
- Automatic state saving
- Player position tracking

### State Management

- Persistent player data
- Equipment restoration
- Inventory state tracking

### ZUI Integration

- Spellbook menu support
- Input blocking during transitions
- Visual feedback for lifecycle events

## Integration with VAutoZone

Vlifecycle works with VAutoZone for complete zone management:

- Enter arena → Apply kit → Enable PvP
- Exit arena → Restore kit → Disable PvP

## Version

- Version: 1.0.0
- Website: https://github.com/Coyoteq1/Vlifecycle
