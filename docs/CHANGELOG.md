# Changelog

All notable changes to the VAutomation framework are documented here.

## [Current] - Work in Progress

### Added
- **Blueluck v1.0.0** - Zone management system with BossZone and ArenaZone support
  - ECS-based zone detection system
  - Flow system for zone enter/exit actions
  - Kit system for equipment loadouts
  - Snapshot system for progress save/restore
  - Ability loadouts (server-side buff application)
  - Boss co-op spawning service
  - Full chat command interface

- **VAutomationCore v1.0.1** - Core library updates
  - Automation service and sandboxing
  - HTTP server and event scheduling
  - Enhanced ECS utilities

### Chat Commands (Blueluck)
- `zone status` / `zs` - Show zone status
- `zone list` / `zl` - List all configured zones
- `zone reload` / `zr` - Reload zone configuration
- `flow reload` - Reload flows.json from disk
- `zone debug` - Toggle zone detection debug mode
- `flow validate` - Validate flow configurations
- `kit list` - List available kits
- `kit [name]` - Apply a kit to yourself
- `snap status` - Show snapshot status
- `snap save [name]` - Save current progress snapshot
- `snap apply [name]` - Apply a snapshot
- `snap restore` - Restore last saved snapshot
- `snap clear` - Clear snapshot data

### Configuration (Blueluck)
- General.Enabled - Enable/disable plugin
- General.LogLevel - Logging level (Debug, Info, Warning, Error)
- Detection.CheckIntervalMs - Zone detection check interval
- Detection.PositionThreshold - Position change threshold
- Detection.DebugMode - Debug logging toggle
- Flow.Enabled - Flow system toggle
- Kits.Enabled - Kit system toggle
- Progress.Enabled - Progress save/restore toggle
- Abilities.Enabled - Ability loadouts toggle

### Flow Actions
- `zone.setpvp` - Enable/disable PvP
- `zone.sendmessage` - Send chat message
- `zone.spawnboss` - Spawn VBlood boss
- `zone.removeboss` - Remove boss entities
- `zone.applykit` - Apply kit to players
- `zone.removekit` - Remove kit from players

### Flow Validation Service (NEW)
- Validates prefabs before execution to prevent runtime crashes
- Validates boss prefabs against known VBlood list
- Validates VFX prefabs
- Validates buff prefabs
- Validates kit references
- Exposes `flow validate` command for server operators
- Includes known prefab lists for early error detection
