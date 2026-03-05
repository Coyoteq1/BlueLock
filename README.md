# Blueluck

Advanced V Rising mod providing enhanced gameplay with custom zones, kit loadouts, and an ECS-powered flow automation system with cross-mod support.

## Features

- **Zone Management** - Custom zone detection with PvP, PvE, sanctuary, raid, and more
- **Kit Commands** - Apply custom loadouts to players
- **Flow Registry** - Game state management with triggers and actions
- **Boss System** - Dynamic boss spawning and encounters
- **Crossmod Automation Controls** - Advanced ECS automation system for complex game mechanics
- **Registry System** - Centralized configuration and state management
- **ECS Automation** - Entity-Component-System architecture for scalable automation
- **Per-Flow, Per-Zone, Per-Time, Per-Entity Controls** - Granular automation management
- **30+ Flow Types** - Coming soon!

## Supported Bosses

| Boss | Prefab |
|------|--------|
| Tourok | `CHAR_Bandit_Tourok_VBlood` |
| Gloomrot | `CHAR_Gloomrot_Purifier_VBlood` |
| StoneBreaker | `CHAR_Bandit_StoneBreaker_VBlood` |
| Stalker | `CHAR_Bandit_Stalker_VBlood` |
| Foreman | `CHAR_Bandit_Foreman_VBlood` |
| ArchMage | `CHAR_ArchMage_VBlood` |
| Dracula | `CHAR_Vampire_Dracula_VBlood` |
| Spider Queen | `CHAR_Spider_Queen_VBlood` |
| Wendigo | `CHAR_Wendigo_VBlood` |
| Winter Yeti | `CHAR_Winter_Yeti_VBlood` |
| Manticore | `CHAR_Manticore_VBlood` |
| Cursed Witch | `CHAR_Cursed_Witch_VBlood` |
| Poloma | `CHAR_Poloma_VBlood` |
| Blood Knight | `CHAR_Vampire_BloodKnight_VBlood` |
| Geomancer | `CHAR_Geomancer_Human_VBlood` |
| Harpy Matriarch | `CHAR_Harpy_Matriarch_VBlood` |
| Jade | `CHAR_VHunter_Jade_VBlood` |
| Bishop of Death | `CHAR_Undead_BishopOfDeath_VBlood` |
| Bishop of Shadows | `CHAR_Undead_BishopOfShadows_VBlood` |

## Current Flows (25 Active)

### Arena Flows
- `arena_enter` - Enable PvP, send arena message
- `arena_exit` - Disable PvP, safe zone message

### Boss Flows
- `boss_enter` - Spawn single boss
- `boss_exit` - Remove boss on defeat
- `boss_enter_multi` - Spawn 3 bosses
- `boss_enter_tourok` - Spawn Tourok
- `boss_enter_gloomrot` - Spawn Gloomrot
- `boss_enter_stonebreaker` - Spawn StoneBreaker
- `boss_enter_stalker` - Spawn Stalker
- `boss_enter_foreman` - Spawn Foreman
- `boss_enter_archmage` - Spawn ArchMage
- `boss_enter_dracula` - Spawn Dracula
- `boss_enter_spider_queen` - Spawn Spider Queen
- `boss_enter_wendigo` - Spawn Wendigo
- `boss_enter_yeti` - Spawn Yeti
- `boss_enter_manticore` - Spawn Manticore
- `boss_enter_cursed_witch` - Spawn Cursed Witch
- `boss_enter_poloma` - Spawn Poloma
- `boss_enter_bloodknight` - Spawn Blood Knight
- `boss_enter_geomancer` - Spawn Geomancer
- `boss_enter_harpy` - Spawn Harpy Matriarch
- `boss_enter_jade` - Spawn Jade
- `boss_enter_bishop_death` - Spawn Bishop of Death
- `boss_enter_bishop_shadows` - Spawn Bishop of Shadows

### Game Mode Flows
- `dungeon_enter` / `dungeon_exit` - Dungeon zones
- `pvp_ffa_enter` / `pvp_ffa_exit` - Free for all PvP
- `sanctuary_enter` / `sanctuary_exit` - Safe trading zones
- `raid_enter` / `raid_exit` - Territory battles
- `pve_arena_enter` / `pve_arena_exit` - PvE wave combat
- `duel_enter` / `duel_exit` - 1v1 duels
- `ctf_enter` / `ctf_exit` - Capture the flag

## Coming Soon: 30+ Additional Flows

New flow types in development for enhanced gameplay:
- Blood hunt events
- Territory control
- Tournament modes
- Quest triggers
- Custom win conditions
- And more!

## Commands

### Kit Commands

| Command | Description | Usage | Admin Only |
|---------|-------------|-------|------------|
| `!kit list` | List all available kits with descriptions | `!kit list` | No |
| `!kit <name>` | Apply a specific kit to yourself | `!kit warrior` | Yes |
| `!kit apply <player> <name>` | Apply kit to another player | `!kit apply PlayerName warrior` | Yes |
| `!kit preview <name>` | Preview kit contents without applying | `!kit preview mage` | No |

### Zone Commands

| Command | Shortcut | Description | Usage | Admin Only |
|---------|----------|-------------|-------|------------|
| `!enterarena <zone>` | - | Force-enter an arena zone by name or hash | `!enterarena arena_1` | Yes |
| `!exitarena` | - | Force-exit your current arena zone | `!exitarena` | Yes |
| `!enterboss <zone>` | - | Force-enter a boss zone by name or hash | `!enterboss boss_dracula` | Yes |
| `!exitboss` | - | Force-exit your current boss zone | `!exitboss` | Yes |
| `!zone status` | `!zs` | Show current zone status and active flows | `!zone status` | Yes |
| `!zone list` | `!zl` | List all configured zones with types | `!zone list` | Yes |
| `!zone reload` | `!zr` | Reload zone configuration from disk | `!zone reload` | Yes |
| `!zone debug` | - | Toggle zone detection debug mode | `!zone debug` | Yes |
| `!zone info <zone>` | - | Show detailed zone information | `!zone info arena_1` | Yes |
| `!zone create <name> <type> <x> <y> <z> <radius>` | - | Create new zone (Admin) | `!zone create test pvp 100 0 100 50` | Yes |
| `!zone delete <name>` | - | Delete existing zone (Admin) | `!zone delete test` | Yes |
| `!flow reload` | - | Reload flows.json from disk | `!flow reload` | Yes |
| `!flow validate` | - | Validate all flow configurations | `!flow validate` | Yes |
| `!flow list` | - | List all active flows | `!flow list` | Yes |
| `!flow status <flow>` | - | Show flow status and triggers | `!flow status boss_enter` | Yes |

### Snapshot Commands

| Command | Description | Usage | Admin Only |
|---------|-------------|-------|------------|
| `!snap status` | Show your saved snapshot status | `!snap status` | Yes |
| `!snap save` | Save a snapshot of your progression and buffs | `!snap save` | Yes |
| `!snap apply` | Apply saved snapshot (keeps extra buffs) | `!snap apply` | Yes |
| `!snap restore` | Restore snapshot exactly (removes extra buffs) | `!snap restore` | Yes |
| `!snap clear` | Clear your saved snapshot | `!snap clear` | Yes |
| `!snap backup <name>` | Create named backup snapshot | `!snap backup checkpoint1` | Yes |
| `!snap load <name>` | Load named backup snapshot | `!snap load checkpoint1` | Yes |
| `!snap list` | List all your saved snapshots | `!snap list` | Yes |

### Boss Commands

| Command | Description | Usage | Admin Only |
|---------|-------------|-------|------------|
| `!boss spawn <type> [x] [y] [z]` | Spawn specific boss type | `!boss spawn dracula` | Yes |
| `!boss list` | List all available boss types | `!boss list` | No |
| `!boss kill <boss_id>` | Kill specific boss by ID | `!boss kill 12345` | Yes |
| `!boss killall` | Kill all active bosses | `!boss killall` | Yes |
| `!boss status` | Show boss spawn status and timers | `!boss status` | Yes |
| `!boss config reload` | Reload boss configuration | `!boss config reload` | Yes |

### Flow Commands

| Command | Description | Usage | Admin Only |
|---------|-------------|-------|------------|
| `!flow trigger <flow_name> <player>` | Manually trigger a flow | `!flow trigger boss_enter PlayerName` | Yes |
| `!flow disable <flow_name>` | Disable specific flow | `!flow disable boss_enter` | Yes |
| `!flow enable <flow_name>` | Enable specific flow | `!flow enable boss_enter` | Yes |
| `!flow stats` | Show flow execution statistics | `!flow stats` | Yes |
| `!flow reset <flow_name>` | Reset flow cooldowns | `!flow reset boss_enter` | Yes |

### Utility Commands

| Command | Description | Usage | Admin Only |
|---------|-------------|-------|------------|
| `!help` | Show all available commands | `!help` | No |
| `!version` | Show mod version information | `!version` | No |
| `!reload` | Reload all configurations | `!reload` | Yes |
| `!debug toggle` | Toggle debug mode | `!debug toggle` | Yes |
| `!coords` | Show your current coordinates | `!coords` | No |
| `!players` | Show all connected players | `!players` | No |

### Command Permissions

- **Player Commands**: Available to all players (marked as "No" in Admin Only column)
- **Admin Commands**: Require administrator privileges (marked as "Yes" in Admin Only column)
- **Zone Admin**: Special permission for zone management commands
- **Flow Admin**: Special permission for flow management commands

### Command Examples

```bash
# Basic usage
!kit list
!zone status
!snap save

# Admin usage
!kit apply PlayerName warrior
!zone create arena_test pvp 100 0 100 50
!boss spawn dracula 200 0 200
!flow trigger boss_enter PlayerName
```

### Command Shortcuts

Many commands support shortcuts for faster usage:

- `!zs` = `!zone status`
- `!zl` = `!zone list` 
- `!zr` = `!zone reload`
- `!snap` = `!snap status`

### Command Help

For detailed help on any command, use:
```bash
!help <command_name>
```

Example: `!help !kit` will show detailed information about kit commands.

## Troubleshooting

### Common Issues

**Commands not working:**
- Ensure you have the required permissions (admin commands need administrator privileges)
- Check if the mod is properly loaded in the server console
- Verify command syntax matches the documentation

**Zone detection not working:**
- Use `!zone debug` to enable debug mode and check zone boundaries
- Verify zone coordinates in `zones.json` are correct
- Ensure players are within the defined zone radius

**Boss spawning issues:**
- Check boss prefab names in configuration files
- Verify spawn coordinates are valid and not obstructed
- Use `!boss status` to check spawn timers and cooldowns

**Flow system problems:**
- Use `!flow validate` to check for configuration errors
- Verify flow triggers are properly configured
- Check server logs for flow execution errors

**Snapshot restoration issues:**
- Ensure you have a valid snapshot saved before attempting restore
- Check that no conflicting buffs are preventing restoration
- Use `!snap status` to verify snapshot integrity

### Configuration Files

**kits.json** - Defines available equipment presets and abilities
**zones.json** - Configures zone boundaries and types  
**flows.json** - Sets up automated game state triggers and actions

### Server Logs

Check server logs for detailed error messages and mod status:
- Look for "Blueluck" entries in the log files
- Enable debug mode with `!debug toggle` for more verbose logging
- Report persistent issues with relevant log sections

### Performance Notes

- Zone detection runs efficiently with optimized algorithms
- Flow system uses minimal server resources
- Boss spawning is designed for smooth gameplay experience
- Snapshot system stores data locally per player

## Installation

1. Build the solution
2. Place the DLL in your V Rising server's BepInEx/plugins folder

## Configuration

Edit the JSON files in the `config` folder:
- `kits.json` - Kit definitions
- `zones.json` - Zone configurations
- `flows.json` - Flow settings

## Technical Architecture

### ECS Automation System

The Blueluck mod implements a sophisticated Entity-Component-System (ECS) architecture for scalable automation:

- **Entities**: Players, bosses, zones, and game objects
- **Components**: Data structures defining state and properties
- **Systems**: Logic processors that operate on entities with specific components

### Registry System

Centralized configuration and state management through:

- **Flow Registry**: Manages all active flows and their triggers
- **Zone Registry**: Tracks zone definitions and player locations
- **Boss Registry**: Handles boss spawning, tracking, and lifecycle
- **Player Registry**: Manages player states, snapshots, and progression

### Automation Layers

**Per-Flow Controls**: Individual flow configuration with custom triggers and actions
**Per-Zone Controls**: Zone-specific automation rules and behaviors  
**Per-Time Controls**: Time-based triggers and cooldown management
**Per-Entity Controls**: Entity-specific automation and state management

### Crossmod Integration

The mod supports crossmod automation through:

- **Event System**: Publish/subscribe architecture for game events
- **API Endpoints**: RESTful interfaces for external mod communication
- **Configuration Hooks**: Extensible configuration system for other mods
- **State Synchronization**: Real-time state sharing between compatible mods

## License

Coyoteq1 2026
