# BlueLock Plugin

**GUID:** `gg.coyote.BlueLock`  
**Namespace:** `VAuto.Zone`  
**Version:** `1.0.0`

Zone management, PvP arenas, sandbox testing, template-driven structures, and kit system for V Rising dedicated servers.

## Features

| Feature | Description |
|---------|-------------|
| **Zone Management** | Circular zones with enable/disable, teleport, tags, and enter/exit messages |
| **Template System** | Runtime-only structures (border, floor, roof, glow) — disposable and rebuildable |
| **Arena System** | Match lifecycle, death tracking, respawn handling, arena territory grid |
| **Kit System** | Named equipment and spell loadouts applied on zone entry |
| **Ability Overrides** | Per-zone ability slot assignments via prefab alias |
| **Sandbox Zones** | Isolated spell/ability testing with VBlood suppression |
| **Boss Spawning** | Zone-triggered boss encounters |
| **Glow Borders** | Tile-based visual zone borders with color support |
| **Player Tags** | Zone-scoped display name tags |
| **Spawn Commands** | Admin unit and boss spawn by prefab name or GUID |

## Quick Start

1. Install `BlueLock.dll`, `VAutomationCore.dll`, and `lifecycle.dll` in `BepInEx/plugins/`
2. Start server — config files are created in `BepInEx/config/Bluelock/` on first run
3. Create a zone: `.z create 45`
4. Enable it: `.z on 1`
5. Enter it: `.enter 1`

## Configuration Files

All files live under `BepInEx/config/Bluelock/`:

| File | Purpose |
|------|---------|
| `VAuto.Zones.json` | Zone definitions (center, radius, kit, templates, messages) |
| `VAuto.ZoneLifecycle.json` | Ordered enter/exit step sequences per zone ID or wildcard |
| `VAuto.Kits.json` | Equipment and spell kit definitions |
| `ability_prefabs.json` | Friendly name → PrefabGUID aliases for abilities |
| `ability_zones.json` | Ability zone-specific settings |
| `Prefabsref.json` | Full prefab name → GUID catalog for templates and spawns |

## Project Structure

```
Bluelock/
├── Plugin.cs                   # BepInEx entry point, Harmony bootstrap
├── Commands/Core/              # Chat command handlers
│   ├── ZoneCommands.cs         # .zone / .z
│   ├── TemplateCommands.cs     # .template / .tm
│   ├── MatchCommands.cs        # .match / .m
│   ├── QuickZoneCommands.cs    # .enter / .exit
│   ├── SpawnCommands.cs        # .spawn / .sp
│   ├── TagCommands.cs          # .tag
│   └── VBloodUnlockCommands.cs # .unlockprefab
├── Core/
│   ├── ZoneCore.cs             # ECS world access
│   ├── LifecycleCore.cs        # Lifecycle step dispatcher
│   ├── PrefabResolver.cs       # Alias → PrefabGUID resolution
│   └── Arena/                  # Arena lifecycle management
├── Services/                   # Feature service implementations
├── Models/                     # Data model classes
└── config/                     # Source config files (copied to BepInEx on build)
```

## Dependencies

| Plugin | GUID | Role |
|--------|------|------|
| VAutomationCore | `gg.coyote.VAutomationCore` | ECS utilities, config, logging |
| lifecycle (CycleBorn) | `gg.coyote.lifecycle` | Player state snapshot/restore |
| VampireCommandFramework | `gg.deca.VampireCommandFramework` | Chat command registration |

## Template Operations

After any config change or server migration, rebuild templates:

```
.tm clearall <zoneId>
.tm rebuild <zoneId>
```

Templates are runtime-only. They are never persisted and never affect zone logic.

## See Also

- Full documentation and command reference: [`README.md`](../README.md)
- Quick reference: [`BLUELOCK.md`](../BLUELOCK.md)

