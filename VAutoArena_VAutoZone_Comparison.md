## VAutoArena vs VAutoZone Comparison

### Overview

| Aspect | VAutoArena | VAutoZone |
|--------|-----------|-----------|
| **Purpose** | Standalone arena plugin with territory management and auto-enter/exit | Generic zone glow border management |
| **Dependencies** | VAutomationCore + **VAutoZone** | VAutomationCore only |
| **Complexity** | Higher (more features) | Lower (single-purpose) |
| **Namespace** | `VAuto` | `VAutoZone` |

### Key Differences

1. **VAutoArena** ([`VAutoArena/Plugin.cs`](VAutoArena/Plugin.cs:1)):
   - Depends on VAutoZone (see [`VAutoArena.csproj:21`](VAutoArena/VAutoArena.csproj:21))
   - Includes **ArenaAutoEnterSystem** - ECS system for auto-entering/exiting arena zones
   - Configurable `AutoEnter` and `AutoExit` settings via config
   - Commands: `.arena`, `.ae`, `.ax`, `.arena glow spawn|clear|test|validate`
   - Territory management with `ArenaTerritory` service
   - Glow border spawning on startup with fallback command

2. **VAutoZone** ([`VAutoZone/Plugin.cs`](VAutoZone/Plugin.cs:1)):
   - Base zone framework (no dependencies on other VAuto plugins)
   - Focuses on **glow border rendering** for zones
   - Supports **rotation** of glow prefabs on configurable intervals
   - Configurable via `glow_zones.json`
   - No auto-enter/exit functionality

### Architecture

- **VAutoArena** is a **higher-level plugin** built on top of VAutoZone
- VAutoZone provides the generic zone glow infrastructure
- VAutoArena adds arena-specific logic (territory, auto-enter, commands)

### Recommendation

**Use VAutoZone** if you need:
- Simple zone glow borders
- Rotating glow prefabs
- No arena-specific features
- add commands

**Use VAutoArena** if you need:
- Full arena territory management
- Auto-enter/exit functionality
- Arena-specific commands

The naming suggests VAutoArena is the **intended replacement/focus**, with VAutoZone serving as the underlying infrastructure layer.
