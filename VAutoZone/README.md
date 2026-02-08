# VAutoZone

Zone management system for VRising with arena territory, glow borders, and map icons.

## Overview

VAutoZone provides comprehensive zone management:

- Arena territory definitions
- Glow border visualization
- Zone-based permissions
- Player tracking within zones

## Commands

| Command | Description |
|---------|-------------|
| `.arena help` | Show arena commands |
| `.arena list` | List all arenas |
| `.arena enter [id]` | Enter arena zone |
| `.arena exit` | Exit current arena |
| `.zone help` | Show zone commands |

## Configuration Files

### Arena Territory

`VAutoZone/arena_territory.json`

Defines arena boundaries and territory settings.

### Arena Zones

`VAutoZone/arena_zones.json`

Zone definitions within arenas.

### Glow Prefabs

`VAutoZone/arena_glow_prefabs.json`

Prefab configurations for glow borders.

## Installation

1. Install [VAutomationCore](https://github.com/Coyoteq1/VAutomationCore) first
2. Install [Vlifecycle](https://github.com/Coyoteq1/Vlifecycle) for full functionality
3. Place `VAutoZone.dll` in `BepInEx/plugins/`

## Dependencies

- VAutomationCore 1.0.0+
- Vlifecycle 1.0.0+
- BepInEx 5.4.2105+
- BepInExPack IL2CPP 6.0.0+
- Vampire Command Framework 0.10.4+

## Project Structure

```
VAutoZone/
├── Core/              # Core implementations
│   ├── Arena/        # Arena functionality
│   └── Zone/         # Zone functionality
├── Models/           # Data models
│   ├── GlowZoneEntry.cs
│   └── GlowZonesConfig.cs
└── Services/         # Service layer
    ├── ArenaCommands.cs
    ├── ArenaGlowBorderService.cs
    ├── ArenaTerritory.cs
    ├── ArenaZoneConfigLoader.cs
    ├── GlowService.cs
    ├── PlayerSnapshotService.cs
    ├── ZoneEventBridge.cs
    ├── ZoneGlowBorderService.cs
    └── ZoneGlowRotationService.cs
```

## Features

### Arena Management

- Define multiple arena zones
- Track player enter/exit events
- Apply zone-specific configurations

### Glow Borders

- Visual indicators for zone boundaries
- Configurable glow prefabs
- Rotation animations

### Zone Configuration

- JSON-based configuration
- Support for multiple zone types
- Customizable glow settings

## Version

- Version: 1.0.0
- Website: https://github.com/Coyoteq1/VAutoZone
