# VAutoTraps

[![V Rising Version](https://img.shields.io/badge/V%20Rising-1.0-blue)](https://store.steampowered.com/app/1604030/V_Rising/)
[![License](https://img.shields.io/badge/License-GPL--3.0-yellow)](LICENSE)
[![Version](https://img.shields.io/badge/Version-1.0.0-green)](manifest.json)

Advanced trap system for V Rising with chest spawns, container traps, and kill streak tracking.

## Table of Contents

- [Overview](#overview)
- [Features](#features)
- [Commands](#commands)
- [Configuration](#configuration)
- [Installation](#installation)
- [Dependencies](#dependencies)
- [Project Structure](#project-structure)
- [API Documentation](docs/API.md)
- [Troubleshooting](docs/TROUBLESHOOTING.md)
- [Contributing](#contributing)
- [Support](#support)

---

## Overview

VAutoTraps enhances V Rising gameplay with an advanced trap system that rewards skilled players through:

- **Chest Spawn Systems** - Spawn loot chests at waypoints based on kill streaks
- **Container Traps** - PvP-enabled traps inside containers
- **Kill Streak Tracking** - Track and reward consecutive kills
- **Customizable Configurations** - Full control via TOML and JSON configs

---

## Features

### Chest Spawn System
- Spawns reward chests at player waypoints
- Configurable chest types (relic, shard, gem)
- Per-player loot locking (one-time loot per chest)
- Distance-based loot validation (2m range)
- Spawn timer controls

### Container Trap System
- Place traps at player locations
- PvP damage dealing
- Configurable damage amount
- Trap duration limits
- Owner-based tracking

### Kill Streak System
- Track player kill streaks
- Bronze (3 kills), Silver (5 kills), Gold (10 kills) thresholds
- Chest spawn rewards at thresholds
- Real-time streak status
- Admin streak management

### Zone Rules
- Allow/deny traps inside gameplay zones
- Allow/deny traps outside zones
- Configurable per-zone permissions

---

## Commands

### Main Trap Commands

| Command | Short | Description | Admin Only |
|---------|-------|-------------|------------|
| `.trap help` | `.trap h` | Show trap command help | ❌ |
| `.trap status` | `.trap s` | Show trap system status | ❌ |
| `.trap config` | `.trap c` | Show trap configuration | ❌ |
| `.trap reload` | `.trap r` | Reload trap configuration | ✅ |
| `.trap debug` | `.trap d` | Diagnostics and counts | ❌ |
| `.trap test` | `.trap t` | Basic trap system tests | ❌ |

### Trap Management

| Command | Short | Description | Admin Only |
|---------|-------|-------------|------------|
| `.trap set` | `.trap ts` | Set container trap at your location | ✅ |
| `.trap remove` | `.trap tr` | Remove nearest container trap | ✅ |
| `.trap list` | `.trap tl` | List all active container traps | ❌ |
| `.trap arm` | `.trap ta` | Arm a specific trap | ✅ |
| `.trap trigger` | `.trap tt` | Manually trigger a trap | ✅ |
| `.trap clear` | `.trap tc` | Clear all container traps | ✅ |

### Chest Commands

| Command | Short | Description | Admin Only |
|---------|-------|-------------|------------|
| `.trap chest spawn` | `.trap cs` | Spawn a reward chest | ✅ |
| `.trap chest list` | `.trap cl` | List spawned chests | ❌ |
| `.trap chest remove` | `.trap cr` | Remove nearest chest | ✅ |
| `.trap chest clear` | `.trap cc` | Clear all spawned chests | ✅ |

### Zone Commands

| Command | Short | Description | Admin Only |
|---------|-------|-------------|------------|
| `.trap zone create` | `.trap zc` | Create a trap zone | ✅ |
| `.trap zone delete` | `.trap zd` | Delete a trap zone | ✅ |
| `.trap zone list` | `.trap zl` | List all trap zones | ❌ |
| `.trap zone arm` | `.trap za` | Arm a zone | ✅ |
| `.trap zone check` | `.trap zck` | Check zone status | ❌ |
| `.trap zone clear` | `.trap zcl` | Clear all zones | ✅ |

### Kill Streak Commands

| Command | Short | Description | Admin Only |
|---------|-------|-------------|------------|
| `.trap streak status` | `.trap ss` | Show your kill streak | ❌ |
| `.trap streak reset` | `.trap sr` | Reset a player's streak | ✅ |
| `.trap streak config` | `.trap sc` | Show streak configuration | ❌ |
| `.trap streak toggle` | `.trap st` | Toggle streak system | ✅ |
| `.trap streak test` | `.trap stest` | Test streak rewards | ❌ |
| `.trap streak stats` | `.trap sstats` | Show streak statistics | ❌ |

---

## Configuration

### Configuration Files

VAutoTraps uses two configuration methods:

1. **TOML Config** - `BepInEx/config/VAuto.Traps.cfg`
2. **JSON Config** - `BepInEx/config/VAuto.Traps.json`

### TOML Configuration

Location: `BepInEx/config/VAuto.Traps.cfg`

```toml
[General]
Enabled = true
LogLevel = Info

[TrapSystem]
Enabled = true
DebugMode = false
UpdateInterval = 5

[ChestSpawns]
Enabled = true
Radius = 30.0
MaxCount = 10
SpawnInterval = 60
Rewards = "relic,shard,gem"

[ContainerTraps]
Enabled = true
Radius = 20.0
MaxCount = 5

[KillStreak]
Enabled = true
Threshold = 5
RewardPrefab = "Inventory_Kill_Count_Ticket_01"

[ZoneRules]
AllowInsideZones = true
AllowOutsideZones = true

[Debug]
DebugMode = false
HotReload = true
```

### Kill Streak TOML Config

Location: `BepInEx/config/VAutoTraps/killstreak_trap_config.toml`

```toml
# Kill streak thresholds for rewards
[Streaks]
bronze_threshold = 3
silver_threshold = 5
gold_threshold = 10
```

### Configuration Options

#### General
| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `Enabled` | bool | `true` | Enable or disable the plugin |
| `LogLevel` | string | `Info` | Log level (Debug, Info, Warning, Error) |

#### Trap System
| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `Enabled` | bool | `true` | Enable trap system |
| `DebugMode` | bool | `false` | Enable debug logging |
| `UpdateInterval` | int | `5` | Trap update interval in seconds |

#### Chest Spawns
| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `Enabled` | bool | `true` | Enable chest spawns |
| `Radius` | float | `30.0` | Chest spawn radius |
| `MaxCount` | int | `10` | Maximum chest count |
| `SpawnInterval` | int | `60` | Spawn interval in seconds |
| `Rewards` | string | `relic,shard,gem` | Comma-separated reward types |

#### Container Traps
| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `Enabled` | bool | `true` | Enable container traps |
| `Radius` | float | `20.0` | Container trap radius |
| `MaxCount` | int | `5` | Maximum container trap count |

#### Kill Streak
| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `Enabled` | bool | `true` | Enable kill streak rewards |
| `Threshold` | int | `5` | Kill streak threshold |
| `RewardPrefab` | string | `Inventory_Kill_Count_Ticket_01` | Reward prefab name |

#### Zone Rules
| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `AllowInsideZones` | bool | `true` | Allow traps inside zones |
| `AllowOutsideZones` | bool | `true` | Allow traps outside zones |

---

## Installation

### Prerequisites

1. **V Rising Server** - Installed and running
2. **BepInEx 5.4.2105+** - For IL2CPP servers
3. **VAutomationCore 1.0.0+** - Required dependency
4. **Vampire Command Framework 0.10.4+** - Required dependency

### Installation Steps

1. **Install BepInEx**
   ```
   Download BepInExPack IL2CPP and extract to your V Rising server directory
   ```

2. **Install Dependencies**
   ```
   Place VAutomationCore.dll in BepInEx/plugins/
   Place VampireCommandFramework.dll in BepInEx/plugins/
   ```

3. **Install VAutoTraps**
   ```
   Place VAutoTraps.dll in BepInEx/plugins/
   ```

4. **Configure (Optional)**
   ```
   Edit BepInEx/config/VAuto.Traps.cfg
   Edit BepInEx/config/VAuto.Traps.json
   ```

5. **Restart Server**
   ```
   Restart your V Rising server to load the plugin
   ```

### Verification

After installation, check your server logs for:
```
[VAutoTraps] Loading v1.0.0...
[VAutoTraps] Loaded successfully.
```

---

## Dependencies

| Dependency | Version | Required | Link |
|------------|---------|----------|------|
| BepInEx | 5.4.2105+ | ✅ | [GitHub](https://github.com/BepInEx/BepInEx) |
| BepInExPack IL2CPP | 6.0.0+ | ✅ | [GitHub](https://github.com/BepInEx/BepInEx) |
| VAutomationCore | 1.0.0+ | ✅ | [GitHub](https://github.com/Coyoteq1/VAutomationCore) |
| Vampire Command Framework | 0.10.4+ | ✅ | [GitHub](https://github.com/deca-voxel/VampireCommandFramework) |

---

## Project Structure

```
VAutoTraps/
├── Commands/                      # Command implementations
│   └── Core/                      # Trap command handlers
│       └── TrapCommands.cs        # Main command class
├── Configuration/                 # TOML configurations
│   └── killstreak_trap_config.toml
├── Convertor/                     # Data converters
│   └── JSONConverters.cs
├── Core/                          # Core utilities
│   ├── PrefabGuidConverter.cs    # Prefab GUID handling
│   └── VRCore.cs                  # V Rising core utilities
├── Data/                          # Data definitions
│   └── ChestRewardTypes.cs        # Chest reward enumerations
├── Services/                      # Service layer
│   ├── Rules/                     # Spawn rules
│   │   └── TrapSpawnRules.cs     # Trap spawning logic
│   └── Traps/                     # Trap implementations
│       ├── ChestSpawnService.cs   # Chest spawning
│       ├── ContainerTrapService.cs # Container traps
│       └── TrapZoneService.cs     # Trap zones
├── Docs/                          # Documentation
│   ├── API.md                     # API reference
│   └── TROUBLESHOOTING.md         # Common issues
├── GlobalUsings.cs
├── manifest.json                  # Mod manifest
├── MyPluginInfo.cs                # Plugin metadata
├── Plugin.cs                      # Main plugin class
├── README.md                       # This file
└── VAutoTraps.csproj             # Project file
```

---

## API Documentation

For detailed API documentation, see [API.md](docs/API.md).

### Quick Service Access

```csharp
// Get trap system status
var trapCount = ContainerTrapService.GetTrapCount();
var zoneCount = TrapZoneService.GetZoneCount();
var chestCount = ChestSpawnService.GetChestCount();

// Spawn a chest
var entity = ChestSpawnService.SpawnChest(position, playerId, ChestRewardType.Relic);

// Attempt to loot
var result = ChestSpawnService.AttemptLoot(playerPosition, playerId);
```

---

## Troubleshooting

For common issues and solutions, see [TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md).

### Common Issues

| Issue | Solution |
|-------|----------|
| Plugin not loading | Check dependencies are installed |
| Commands not working | Verify Vampire Command Framework is loaded |
| Chests not spawning | Check ChestSpawns.Enabled config |
| Traps not dealing damage | Verify PvP is enabled |

---

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit changes (`git commit -m 'Add amazing feature'`)
4. Push to branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

### Development Setup

```bash
# Clone the repository
git clone https://github.com/Coyoteq1/VAutoTraps.git

# Open in Visual Studio
# Ensure .NET 6.0 and Unity references are configured

# Build
dotnet build
```

---

## Support

- **Issues**: [GitHub Issues](https://github.com/Coyoteq1/VAutoTraps/issues)
- **Discord**: Join the V Rising Modding community
- **Wiki**: [V Rising Modding Wiki](https://github.com/Coyoteq1/VAutomationCore/wiki)

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2024 | Initial release |

---

## License

This project is licensed under the GPL-3.0 License - see the [LICENSE](LICENSE) file for details.

---

**Built with ❤️ for the V Rising community**
