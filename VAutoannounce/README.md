# VAutoannounce

Announcement system for VRising with customizable broadcast messages and notifications.

## Overview

VAutoannounce provides comprehensive announcement capabilities for VRising servers, including:

- Public broadcasts to all players
- Private messages to specific players
- Alert system with customizable formatting
- Trap alerts for PvP events

## Commands

| Command | Description |
|---------|-------------|
| `.announce [message]` | Broadcast message to all players |
| `.say [player] [message]` | Send private message to player |
| `.alert [message]` | Send alert notification |
| `.trapalert [message]` | Send trap-related alert |
| `.broadcast [message]` | Broadcast with special formatting |

## Installation

1. Install [VAutomationCore](https://github.com/Coyoteq1/VAutomationCore) first
2. Place `VAutoannounce.dll` in `BepInEx/plugins/`

## Dependencies

- VAutomationCore 1.0.0+
- BepInEx 5.4.2105+
- BepInExPack IL2CPP 6.0.0+
- Vampire Command Framework 0.10.4+

## Configuration

No additional configuration required. Uses default BepInEx logging.

## Project Structure

```
VAutoannounce/
├── Commands/           # Command implementations
│   └── Core/          # Core announcement commands
├── Core/              # Core utilities
│   ├── PrefabGuidConverter.cs
│   └── VRCore.cs
└── Services/          # Service layer
    └── AnnouncementService.cs
```

## Usage Examples

```bash
.announce Welcome to the server!
.say PlayerName Here is your private message
.alert Server will restart in 5 minutes
.trapalert Trap activated near the arena!
```

## Version

- Version: 1.0.0
- Website: https://github.com/Coyoteq1/VAutoannounce
