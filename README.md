# Bluelock Plugin

A V Rising server plugin for zone management, arenas, and custom gameplay features.

## Features

| Feature | Description |
|---------|-------------|
| **Zone Management** | Create and manage custom zones with configurable behaviors |
| **Glow Borders** | Visual borders around zones with customizable effects |
| **Arena System** | PvP arenas with death tracking, respawns, and match management |
| **Kits** | Pre-configured loadouts for players |
| **Ability Zones** | Zones with special ability interactions |
| **Boss Spawning** | Automated boss encounters in designated areas |
| **Walls & Structures** | Dynamic building placement within zones |

## Quick Start

1. Install the plugin DLL in your BepInEx plugins folder
2. Configure zones in `config/VAuto.Zones.json`
3. Set up lifecycle events in `config/VAuto.ZoneLifecycle.json`

## Configuration Files

| File | Purpose |
|------|---------|
| `config/VAuto.Zones.json` | Main zone definitions |
| `config/VAuto.ZoneLifecycle.json` | Zone enter/exit behaviors |
| `config/ability_zones.json` | Ability zone settings |
| `config/ability_prefabs.json` | Ability prefab mappings |
| `config/VAuto.Kits.json` | Kit definitions |

## Project Structure

```
Bluelock/
├── Plugin.cs              # Main entry point
├── Commands/Core/         # Chat commands
├── Services/              # Core gameplay services
├── Core/                  # Zone and lifecycle logic
├── Models/                # Data models
└── config/                # JSON configuration files
```

## Dependencies

- **VAutomationCore** - Core framework and shared services
- **CycleBorn/Vlifecycle** - Lifecycle management (temporary)

## Support

- Check `Bluelock/docs/` for detailed documentation
- Review config files for all available options
