# PVPLifecycleEventSystem Documentation

## Overview

`PVPLifecycleEventSystem` is a core system in VAutoLifecycle that manages player lifecycle events in PVP arenas. It handles entity position tracking, lifecycle action execution, and configuration management for arena-based interactions.

## Namespace

`VAutomation.Services.Systems`

## Class Definition

```csharp
public class PVPLifecycleEventSystem : MonoBehaviour
```

## Key Properties

| Property | Type | Description |
|----------|------|-------------|
| `_logPrefix` | `string` | Prefix for all log messages, typically `"[PVPLifecycle]"` |
| `Log` | `ILogger` | Logger instance for diagnostic output |
| `_playerLifecycleData` | `Dictionary<Entity, PlayerLifecycleData>` | Stores lifecycle state for each player entity |
| `_configLoader` | `ConfigLoader` | Handles configuration file loading |
| `_zonesService` | `ZonesService` | Manages zone definitions and configurations |
| `_lifecycleSystem` | `LifecycleSystem` | Core lifecycle processing system |
| `HasEnterZoneEvent` | `bool` | Indicates if enter zone events are configured |
| `MaxDistanceThreshold` | `float` | Maximum distance for entity position queries |

## Public Methods

### GetEntityPosition

Retrieves the world position of an entity with robust error handling for cross-world entity issues.

```csharp
public float3 GetEntityPosition(Entity entity)
```

**Parameters:**
- `entity` (`Entity`) - The entity to get the position for

**Returns:**
- `float3` - The entity's world position, or `float3.zero` if the entity is invalid or an error occurs

**Error Handling:**
- Catches `ArgumentException` for cross-world entity errors
- Returns `float3.zero` on any exception to prevent system crashes

**Example:**
```csharp
var position = _pvPEventSystem.GetEntityPosition(playerEntity);
if (position != float3.zero)
{
    // Process position
}
```

---

## Private Methods

### LoadConfigurations

Loads all configuration files required by the system.

```csharp
private void LoadConfigurations()
```

**Configuration Files Loaded:**
1. `pvp_item.json` - PVP item configurations
2. `glow_zones.json` - Zone definitions and glow settings
3. Lifecycle actions from the lifecycle system

**Behavior:**
- Creates sample configurations if files don't exist
- Logs warnings for missing configurations
- Loads lifecycle actions into the lifecycle system

---

### LoadLifecycleActions

Loads lifecycle action configurations from the config loader into the lifecycle system.

```csharp
private void LoadLifecycleActions()
```

**Lifecycle Stages Loaded:**
- `onUse` - Actions when items are used
- `onEnterArenaZone` - Actions when entering arena zones
- `onExitArenaZone` - Actions when exiting arena zones

**Example Configuration:**
```json
{
  "onEnterArenaZone": {
    "Actions": ["grant_spellbook", "buff_player"]
  }
}
```

---

### CheckEnterZoneEventConfiguration

Determines if the `onEnterArenaZone` lifecycle stage is configured and sets `HasEnterZoneEvent` accordingly.

```csharp
private void CheckEnterZoneEventConfiguration()
```

**Behavior:**
- Checks if `onEnterArenaZone` has any actions defined
- Sets `HasEnterZoneEvent` to enable/disable enter zone event processing
- Falls back to `onUse` events if enter zone is not configured

---

## Nested Class: PlayerLifecycleData

Stores lifecycle data for a single player.

```csharp
public class PlayerLifecycleData
```

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `UserEntity` | `Entity` | The player's user entity |
| `CharacterEntity` | `Entity` | The player's character entity |
| `LastItemUsed` | `Entity` | The last item the player used |
| `LastUseTime` | `DateTime` | Timestamp of the last item use |
| `StoredData` | `Dictionary<string, object>` | Custom data storage for lifecycle events |
| `CurrentZone` | `string` | The current zone the player is in |

### Constructor

```csharp
public PlayerLifecycleData()
```

Initializes:
- `StoredData` as an empty dictionary
- `LastUseTime` as `DateTime.MinValue`

---

## Usage Example

```csharp
// Get the system instance
var pvPSystem = Plugin.GetSystem<PVPLifecycleEventSystem>();

// Check if a player is in an arena zone
var position = pvPSystem.GetEntityPosition(playerEntity);
if (position != float3.zero)
{
    // Process position for zone detection
}

// Access player lifecycle data
if (pvPSystem._playerLifecycleData.TryGetValue(playerEntity, out var data))
{
    var currentZone = data.CurrentZone;
    var lastUseTime = data.LastUseTime;
}
```

---

## Error Handling

The system implements robust error handling for common ECS issues:

1. **Cross-World Entity Errors**: Catches `ArgumentException` when entities from different worlds are accessed
2. **Position Retrieval Failures**: Returns `float3.zero` instead of throwing
3. **Configuration Errors**: Logs warnings and creates sample configs

---

## Integration Points

- **ConfigLoader**: Loads and parses JSON configuration files
- **ZonesService**: Provides zone definitions and boundaries
- **LifecycleSystem**: Executes lifecycle actions based on events
- **Unity ECS**: Accesses entity positions and manages entity data
