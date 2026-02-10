# VAutoTraps API Documentation

This document provides detailed API documentation for developers who want to extend or integrate with VAutoTraps.

## Table of Contents

- [Core Services](#core-services)
- [Data Structures](#data-structures)
- [Configuration Classes](#configuration-classes)
- [Plugin Static Accessors](#plugin-static-accessors)
- [Command Framework](#command-framework)

---

## Core Services

### ChestSpawnService

Manages reward chest spawning and looting.

#### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Initialize()` | `void` | Initialize the chest spawn service |
| `SpawnChest(position, spawnedBy, type)` | `EntityReference` | Spawn a chest at position |
| `AttemptLoot(playerPosition, playerId)` | `LootResult` | Attempt to loot a nearby chest |
| `CanAccessChest(playerPosition, playerKillStreak)` | `bool` | Check if player can access chest |
| `RemoveNearestChest(position, maxRadius)` | `bool` | Remove nearest chest |
| `RemoveChest(position)` | `bool` | Remove chest at exact position |
| `GetChestCount()` | `int` | Get total spawned chest count |

#### Usage Example

```csharp
using VAuto.Core.Services;

// Spawn a relic chest at player position
var entity = ChestSpawnService.SpawnChest(
    new float3(100, 50, 200),
    playerPlatformId,
    ChestRewardType.Relic
);

// Attempt to loot
var result = ChestSpawnService.AttemptLoot(playerPosition, playerId);
if (result.Success)
{
    Console.WriteLine($"Looted: {result.Contents}");
}

// Check access
var canAccess = ChestSpawnService.CanAccessChest(playerPosition, 5);
```

---

### ContainerTrapService

Manages container trap placement and PvP damage.

#### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Initialize()` | `void` | Initialize container traps |
| `SetTrap(position, ownerId, trapType)` | `void` | Set a trap at position |
| `GetTrapCount()` | `int` | Get active trap count |
| `RemoveTrap(position)` | `bool` | Remove trap at position |
| `GetTrapAt(position)` | `TrapData` | Get trap data at position |

#### Usage Example

```csharp
using VAuto.Core.Services;

// Set a container trap
ContainerTrapService.SetTrap(
    new float3(150, 60, 250),
    playerPlatformId,
    "container"
);

// Check trap count
var count = ContainerTrapService.GetTrapCount();
Console.WriteLine($"Active traps: {count}");
```

---

### TrapZoneService

Manages trap zones with custom rules.

#### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Initialize()` | `void` | Initialize trap zones |
| `CreateZone(position, radius, rules)` | `ZoneData` | Create a new zone |
| `GetZoneCount()` | `int` | Get active zone count |
| `DeleteZone(zoneId)` | `bool` | Delete a zone |
| `IsInZone(position)` | `ZoneData` | Check if position is in zone |

#### Usage Example

```csharp
using VAuto.Core.Services;

// Create a trap zone
var zone = TrapZoneService.CreateZone(
    new float3(200, 70, 300),
    50f,
    new ZoneRules { PvPEnabled = true }
);

// Check zone count
var count = TrapZoneService.GetZoneCount();
```

---

### TrapSpawnRules

Configures trap spawning rules and thresholds.

#### Static Properties

| Property | Type | Description |
|----------|------|-------------|
| `Config` | `TrapSpawnConfig` | Current spawn configuration |
| `KillThreshold` | `int` | Kills required for chest spawn |
| `ChestsPerSpawn` | `int` | Chests to spawn per trigger |

#### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Initialize()` | `void` | Initialize rules from config |
| `GetAllStreaks()` | `List<StreakData>` | Get all player streaks |
| `GetStreak(playerId)` | `StreakData` | Get player's streak |
| `IncrementStreak(playerId)` | `int` | Increment player's streak |
| `ResetStreak(playerId)` | `void` | Reset player's streak |

#### Usage Example

```csharp
using VAuto.Core.Services;

// Initialize rules
TrapSpawnRules.Initialize();

// Get config
var threshold = TrapSpawnRules.Config.KillThreshold;
var damage = TrapSpawnRules.Config.TrapDamageAmount;

// Get player streak
var streak = TrapSpawnRules.GetStreak(playerPlatformId);
Console.WriteLine($"Current streak: {streak.Kills}");

// Increment streak
var newStreak = TrapSpawnRules.IncrementStreak(playerPlatformId);
```

---

## Data Structures

### ChestRewardType

Enum defining chest reward types.

```csharp
public enum ChestRewardType
{
    Relic,   // Relic rewards
    Shard,   // Shard rewards
    Gem,     // Gem rewards
    Mixed    // Mixed rewards
}
```

### ChestData

Chest state information.

```csharp
public class ChestData
{
    public float3 Position { get; set; }
    public ulong SpawnedByPlatformId { get; set; }
    public ChestRewardType ChestType { get; set; }
    public DateTime SpawnedTime { get; set; }
    public string Contents { get; set; }
    public bool IsLocked { get; set; }
    public List<ulong> LooterPlatformIds { get; set; }
}
```

### LootResult

Result of a loot attempt.

```csharp
public class LootResult
{
    public bool Success { get; set; }
    public string LootType { get; set; }
    public string Contents { get; set; }
    public string Message { get; set; }
}
```

### EntityReference

Reference to a spawned entity.

```csharp
public class EntityReference
{
    public float3 Position { get; set; }
    public Entity Entity { get; set; }
}
```

### TrapSpawnConfig

Configuration for trap spawning.

```csharp
public class TrapSpawnConfig
{
    public int KillThreshold { get; set; } = 5;
    public int ChestsPerSpawn { get; set; } = 1;
    public float ContainerGlowRadius { get; set; } = 10f;
    public int WaypointTrapThreshold { get; set; } = 3;
    public float WaypointTrapGlowRadius { get; set; } = 15f;
    public bool NotificationEnabled { get; set; } = true;
    public int TrapDamageAmount { get; set; } = 100;
    public int TrapDuration { get; set; } = 60;
}
```

---

## Configuration Classes

### TrapsJsonConfig

Root JSON configuration class.

```csharp
public class TrapsJsonConfig
{
    public TrapsConfigSection Traps { get; set; } = new();
}
```

### TrapsConfigSection

Main configuration section.

```csharp
public class TrapsConfigSection
{
    public bool Enabled { get; set; } = true;
    public TrapSystemConfig TrapSystem { get; set; } = new();
    public ChestSpawnsConfig ChestSpawns { get; set; } = new();
    public ContainerTrapsConfig ContainerTraps { get; set; } = new();
    public KillStreakConfig KillStreak { get; set; } = new();
    public ZoneRulesConfig ZoneRules { get; set; } = new();
}
```

### TrapSystemConfig

Trap system configuration.

```csharp
public class TrapSystemConfig
{
    public bool Enabled { get; set; } = true;
    public bool DebugMode { get; set; } = false;
    public int UpdateInterval { get; set; } = 5;
}
```

### ChestSpawnsConfig

Chest spawning configuration.

```csharp
public class ChestSpawnsConfig
{
    public bool Enabled { get; set; } = true;
    public float Radius { get; set; } = 30f;
    public int MaxCount { get; set; } = 10;
    public int SpawnInterval { get; set; } = 60;
    public string Rewards { get; set; } = "relic,shard,gem";
}
```

### ContainerTrapsConfig

Container trap configuration.

```csharp
public class ContainerTrapsConfig
{
    public bool Enabled { get; set; } = true;
    public float Radius { get; set; } = 20f;
    public int MaxCount { get; set; } = 5;
}
```

### KillStreakConfig

Kill streak configuration.

```csharp
public class KillStreakConfig
{
    public bool Enabled { get; set; } = true;
    public int Threshold { get; set; } = 5;
    public string RewardPrefab { get; set; } = "Inventory_Kill_Count_Ticket_01";
}
```

### ZoneRulesConfig

Zone rules configuration.

```csharp
public class ZoneRulesConfig
{
    public bool AllowInsideZones { get; set; } = true;
    public bool AllowOutsideZones { get; set; } = true;
}
```

---

## Plugin Static Accessors

Access plugin configuration and state.

| Property | Type | Description |
|----------|------|-------------|
| `Plugin.Instance` | `Plugin` | Plugin singleton instance |
| `Plugin.Log` | `ManualLogSource` | Logging instance |
| `Plugin.IsEnabled` | `bool` | Plugin enabled state |
| `Plugin.TrapSystemActive` | `bool` | Trap system enabled |
| `Plugin.TrapDebugActive` | `bool` | Debug mode active |
| `Plugin.ChestSpawnsActive` | `bool` | Chest spawns enabled |
| `Plugin.ChestSpawnRadiusMeters` | `float` | Chest spawn radius |
| `Plugin.ChestMaxActive` | `int` | Max chest count |
| `Plugin.ChestSpawnIntervalSeconds` | `int` | Spawn interval |
| `Plugin.ContainerTrapsActive` | `bool` | Container traps enabled |
| `Plugin.ContainerTrapRadius` | `float` | Container trap radius |
| `Plugin.ContainerMaxActive` | `int` | Max container count |
| `Plugin.KillStreakActive` | `bool` | Kill streak enabled |
| `Plugin.KillStreakMinKills` | `int` | Min kills for reward |
| `Plugin.KillStreakReward` | `string` | Reward prefab name |
| `Plugin.AllowTrapsInsideZones` | `bool` | Allow in zones |
| `Plugin.AllowTrapsOutsideZones` | `bool` | Allow outside zones |
| `Plugin.DebugModeEnabled` | `bool` | Debug mode |

---

## Command Framework

### Command Group: `trap`

Main trap command group.

#### Command: `help`

Show trap command help.

```csharp
[Command("help", shortHand: "h", description: "Show trap commands")]
public static void Help(ChatCommandContext ctx)
```

#### Command: `status`

Show trap system status.

```csharp
[Command("status", shortHand: "s", description: "Show trap system status")]
public static void Status(ChatCommandContext ctx)
```

#### Command: `config`

Show trap configuration.

```csharp
[Command("config", shortHand: "c", description: "Show trap configuration")]
public static void Config(ChatCommandContext ctx)
```

#### Command: `reload`

Reload trap configuration (Admin only).

```csharp
[Command("reload", shortHand: "r", description: "Reload trap configuration")]
public static void Reload(ChatCommandContext ctx)
```

#### Command: `debug`

Show diagnostics and counts.

```csharp
[Command("debug", shortHand: "d", description: "Diagnostics and counts")]
public static void Debug(ChatCommandContext ctx)
```

#### Command: `set`

Set a container trap at your location (Admin only).

```csharp
[Command("set", shortHand: "ts", description: "Set a container trap")]
public static void TrapSet(ChatCommandContext ctx)
```

#### Command: `chest spawn`

Spawn a reward chest (Admin only).

```csharp
[Command("chest spawn", shortHand: "cs", description: "Spawn a chest")]
public static void ChestSpawn(ChatCommandContext ctx)
```

#### Command: `streak status`

Show your kill streak.

```csharp
[Command("streak status", shortHand: "ss", description: "Show kill streak")]
public static void StreakStatus(ChatCommandContext ctx)
```

---

## Extending the Plugin

### Adding New Trap Types

1. Create a new service class in `Services/Traps/`
2. Implement initialization and lifecycle methods
3. Register in `Plugin.cs` Load method
4. Add command handlers in `Commands/Core/`

### Example: Custom Trap Service

```csharp
namespace VAuto.Core.Services
{
    public class CustomTrapService
    {
        public static void Initialize()
        {
            // Initialization logic
        }
        
        public static void ActivateTrap(float3 position)
        {
            // Trap activation logic
        }
    }
}
```

### Adding New Commands

1. Add command method to `TrapCommands.cs`
2. Use `[Command]` attribute
3. Handle admin checks if needed
4. Test with `.trap help`

```csharp
[Command("custom", shortHand: "cu", description: "Custom command")]
public static void CustomCommand(ChatCommandContext ctx)
{
    ctx.Reply("[Custom] Command executed!");
}
```

---

## Best Practices

1. **Use Locking**: Use `lock` when modifying shared data
2. **Check Config**: Always check `Plugin.IsEnabled` before operations
3. **Log Actions**: Use `Plugin.Log.LogInfo()` for significant actions
4. **Handle Errors**: Wrap operations in try-catch blocks
5. **Dispose Resources**: Clean up timers and resources in `Unload()`

---

## Version

API Version: 1.0.0
Last Updated: 2024
