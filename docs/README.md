# VAutomation Framework Wiki

> Operational reference for VAutomationCore and Blueluck with ECS runtime mode, config governance, and maintenance guardrails.

## Project Boundaries

### VAutomationCore (`v1.0.1`)

- Shared services and API contracts
- ECS utilities and lifecycle contracts
- Config infrastructure and typed loading
- Automation service and sandboxing
- HTTP server and event scheduling

### Blueluck (`v1.0.0`)

- Zone runtime entrypoint and command surface
- ECS bootstrap, detection, and transition routing
- Zone JSON/config validation and migration hooks
- Kit system for equipment loadouts
- Snapshot system for progress save/restore
- Ability loadouts (buff-based)

## Current Versions

| Project | Version | Dependencies |
|---------|---------|--------------|
| VAutomationCore | `1.0.1` | VampireCommandFramework 0.10.4 |
| Blueluck | `1.0.0` | VAutomationCore 1.1.0, VampireCommandFramework 0.10.4 |

## Chat Commands

### Zone Commands

| Command | Shorthand | Description |
|---------|-----------|-------------|
| `zone status` | `zs` | Show zone status |
| `zone list` | `zl` | List all configured zones |
| `zone reload` | `zr` | Reload zone configuration |
| `flow reload` | - | Reload flows.json from disk |
| `zone debug` | - | Toggle zone detection debug mode |

### Kit Commands

| Command | Description |
|---------|-------------|
| `kit list` | List available kits |
| `kit [name]` | Apply a kit to yourself |

### Snapshot Commands

| Command | Description |
|---------|-------------|
| `snap status` | Show snapshot status |
| `snap save [name]` | Save current progress snapshot |
| `snap apply [name]` | Apply a snapshot |
| `snap restore` | Restore last saved snapshot |
| `snap clear` | Clear snapshot data |

## Configuration System

### BepInEx Config Entries (Blueluck)

#### General
- `General.Enabled` - Enable Blueluck functionality (default: true)
- `General.LogLevel` - Logging level: Debug, Info, Warning, Error (default: Info)

#### Detection
- `Detection.CheckIntervalMs` - Zone detection check interval in ms (default: 500)
- `Detection.PositionThreshold` - Position change threshold for detection (default: 1.0)
- `Detection.DebugMode` - Enable debug logging for zone detection (default: false)

#### Feature Toggles
- `Flow.Enabled` - Enable flow system (default: true)
- `Kits.Enabled` - Enable kit system (default: true)
- `Progress.Enabled` - Enable progress save/restore (default: true)
- `Abilities.Enabled` - Enable ability loadouts (default: true)

## Services Architecture

### Zone Services

| Service | Description |
|---------|-------------|
| `ZoneConfigService` | Zone configuration management |
| `ZoneTransitionService` | Zone transition handling |
| `FlowRegistryService` | Flow definitions registry |
| `ProgressService` | Player progress save/restore |
| `AbilityService` | Ability loadout management |
| `BossCoopService` | Boss spawning for co-op |

### Utility Services

| Service | Description |
|---------|-------------|
| `PrefabRemapService` | Prefab alias remapping |
| `PrefabToGuidService` | Prefab GUID resolution |
| `UnlockService` | Technology/ability unlocks |
| `KitService` | Equipment kit system |

## Config Sources

- `Blueluck/config/zones.json` - Zone data, flow IDs, radii, priorities, FX presets
- `Blueluck/config/flows.json` - Flow definitions (zone.setpvp, zone.sendmessage, zone.spawnboss, etc.)
- `Blueluck/config/kits.json` - Kit definitions (equipment loadouts)
- `Blueluck/config/abilities.json` - Ability loadouts (server-side buff application)
- `config/VAuto.unified_config.schema.json` - Schema contract

> Validation entrypoint: config is validated on load; runtime source of truth is the Blueluck config folder.

## Flow Actions

Available flow actions in `flows.json`:

| Action | Parameters | Description |
|--------|------------|-------------|
| `zone.setpvp` | `value: true/false` | Enable/disable PvP for the zone |
| `zone.sendmessage` | `message: string` | Send a chat message to players |
| `zone.spawnboss` | `prefab: string`, `qty: number`, `randomInZone: boolean` | Spawn a VBlood boss |
| `zone.removeboss` | - | Remove spawned boss entities |
| `zone.applykit` | `kit: string` | Apply a kit to entering players |
| `zone.removekit` | - | Remove kit from exiting players |

## Runtime Mode Model

Blueluck runtime behavior is selected by `Runtime.ZoneRuntimeMode`:

- `Legacy` - legacy pipeline only
- `Hybrid` - ECS detection/router plus legacy compatibility path
- `EcsOnly` - ECS pipeline only

The selected mode is locked at bootstrap and treated as immutable until restart.

## Install Channels

- Thunderstore (V Rising): https://thunderstore.io/c/v-rising/
- NuGet package: https://www.nuget.org/packages/VAutomationCore
- NuGet prerelease: https://www.nuget.org/packages/VAutomationCore/1.0.1-beta.3

## Maintenance and Cleanup Discipline

- Use test guardrails to prevent compile/include drift and event ownership regressions.
- Deprecate first, delete later; only remove legacy paths after a full release cycle with rollback available.
- Track cleanup actions in release notes and PR guardrail sections.

> Use the rollback playbook before any destructive cleanup on live servers.

## Community and Auth

- V Rising Mods Discord: https://discord.gg/68JZU5zaq7
- Ownership support Discord: https://discord.gg/Se4wU3s6md
- Auth/Maintainer: `coyoteq1`
- Contributors: https://github.com/Coyoteq1/D-VAutomationCore-VAutomationCore/graphs/contributors
