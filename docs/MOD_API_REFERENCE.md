# V Rising Automation Mods - API Reference

## Table of Contents
- [VAutomationCore](#vautomationcore)
- [VAutoZone](#vautozone)
- [VAutoTraps](#vautotraps)
- [Vlifecycle](#vlifecycle)

---

## VAutomationCore

**Prefix:** `VAuto.Core`

### Core Classes

#### `VRCore` (`Core_VRCore.cs`)
Static accessor for game server integration.

```csharp
public static class VRCore
{
    public static ServerGameManager? Server { get; }
    public static EntityManager EntityManager { get; }
    public static World World { get; }
    public static PrefabCollectionSystem? PrefabCollection { get; }
    
    public static void Initialize();
    public static bool TryGetPrefabEntity(PrefabGUID guid, out Entity entity);
    public static Entity CreateEntity(params ComponentType[] componentTypes);
    public static void DestroyEntity(Entity entity);
}
```

#### `ChatService` (`Chat/ChatService.cs`)
Manages in-game chat notifications.

```csharp
public static class ChatService
{
    public static void Broadcast(string message, ChatType type = ChatType.Global);
    public static void SendTo(Player player, string message, ChatType type = ChatType.Whisper);
}
```

#### `PrefabGuidConverter` (`PrefabGuidConverter.cs`)
JSON converter for PrefabGUID serialization.

```csharp
public class PrefabGuidJsonConverter : JsonConverter<PrefabGUID>
{
    public override PrefabGUID Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options);
    public override void Write(Utf8JsonWriter writer, PrefabGUID value, JsonSerializerOptions options);
}
```

---

## VAutoZone

**Prefix:** `VAuto.Zone`

### Core Classes

#### `ArenaVRCore` (`Core/Arena/VRCore.cs`)
Zone-specific game server accessor.

```csharp
public static class ArenaVRCore
{
    public static ServerGameManager? Server { get; }
    public static EntityManager EntityManager { get; }
    public static World World { get; }
    public static PrefabCollectionSystem? PrefabCollection { get; }
    
    public static void Initialize();
    public static bool TryGetPrefabEntity(PrefabGUID guid, out Entity entity);
}
```

#### `ZoneGlowBorderService` (`Services/ZoneGlowBorderService.cs`)
Manages zone glow border spawning and rotation.

```csharp
public static class ZoneGlowBorderService
{
    public static void BuildAll(bool rebuild = false);
    public static void ClearAll();
    public static void RotateAll();
    public static void RotateDueZones();
    public static IEnumerable<string> Status();
}
```

#### `ArenaGlowBorderService` (`Services/ArenaGlowBorderService.cs`)
Manments arena-specific glow effects.

```csharp
public static class ArenaGlowBorderService
{
    public static void BuildAll(bool rebuild = false);
    public static void ClearAll();
    public static void RotateAll();
    public static IEnumerable<string> Status();
}
```

#### `GlowService` (`Services/GlowService.cs`)
Provides glow prefab and configuration management.

```csharp
public static class GlowService
{
    public static PrefabGUID GetGlowPrefab(string theme);
    public static float3 GetGlowColor(string theme);
    public static float GetGlowIntensity(string theme);
}
```

#### `ArenaZoneConfigLoader` (`Services/ArenaZoneConfigLoader.cs`)
Loads zone configurations from JSON files.

```csharp
public static class ArenaZoneConfigLoader
{
    public static ArenaZonesConfig LoadConfig(string configPath);
    public static void SaveConfig(ArenaZonesConfig config, string configPath);
}
```

#### `PlayerSnapshotService` (`Services/PlayerSnapshotService.cs`)
Captures and manages player position snapshots.

```csharp
public static class PlayerSnapshotService
{
    public static void TakeSnapshot(Player player);
    public static void RestoreSnapshot(Player player);
    public static void ClearSnapshots();
}
```

#### `ZoneEventBridge` (`Services/ZoneEventBridge.cs`)
Bridges zone events to handlers.

```csharp
public static class ZoneEventBridge
{
    public static void OnPlayerEnterZone(Player player, string zoneId);
    public static void OnPlayerExitZone(Player player, string zoneId);
}
```

### Command Classes

#### `VAutoZoneCommands` (`Core/Arena/VAutoZoneCommands.cs`)
Admin commands for zone management.

```csharp
public static class VAutoZoneCommands
{
    [Command("zone_glow_build", "Rebuild all zone glows")]
    public static void CmdZoneGlowBuild(CommandContext ctx, bool rebuild = false);
    
    [Command("zone_glow_clear", "Clear all zone glows")]
    public static void CmdZoneGlowClear(CommandContext ctx);
    
    [Command("zone_glow_status", "Show zone glow status")]
    public static void CmdZoneGlowStatus(CommandContext ctx);
    
    [Command("zone_glow_rotate", "Rotate all zone glow prefabs")]
    public static void CmdZoneGlowRotate(CommandContext ctx);
}
```

#### `ArenaCommands` (`Services/ArenaCommands.cs`)
Admin commands for arena management.

```csharp
public static class ArenaCommands
{
    [Command("arena_glow_build", "Build arena glows")]
    public static void CmdArenaGlowBuild(CommandContext ctx, bool rebuild = false);
    
    [Command("arena_glow_clear", "Clear arena glows")]
    public static void CmdArenaGlowClear(CommandContext ctx);
}
```

### Models

#### `GlowZoneEntry` (`Models/GlowZoneEntry.cs`)
Configuration for a single glow zone.

```csharp
public class GlowZoneEntry
{
    public string Id { get; set; }
    public string Name { get; set; }
    public bool Enabled { get; set; }
    public float3 Center { get; set; }
    public float? Radius { get; set; }
    public float2? HalfExtents { get; set; }
    public float BorderSpacing { get; set; }
    public float3? GlowColor { get; set; }
    public float? GlowIntensity { get; set; }
    public float? GlowRadius { get; set; }
    public float? GlowDuration { get; set; }
    public int? BuffId { get; set; }
    public bool SpawnEmptyMarkers { get; set; }
    public RotationConfig Rotation { get; set; }
}

public class RotationConfig
{
    public bool Enabled { get; set; }
    public int IntervalSeconds { get; set; }
}
```

#### `GlowZonesConfig` (`Models/GlowZonesConfig.cs`)
Container for multiple glow zones.

```csharp
public class GlowZonesConfig
{
    public List<GlowZoneEntry> Zones { get; set; }
}
```

---

## VAutoTraps

**Prefix:** `VAuto.Traps`

### Core Classes

#### `VRCore` (`Core/VRCore.cs`)
Trap-specific game server accessor.

```csharp
public static class VRCore
{
    public static ServerGameManager? Server { get; }
    public static EntityManager EntityManager { get; }
    
    public static void Initialize();
    public static bool TryGetPrefabEntity(PrefabGUID guid, out Entity entity);
}
```

#### `ContainerTrapService` (`Services/Traps/ContainerTrapService.cs`)
Manages container trap spawning and management.

```csharp
public static class ContainerTrapService
{
    public static void BuildAll(bool rebuild = false);
    public static void ClearAll();
    public static void SpawnTrap(string trapId, float3 position);
    public static void RemoveTrap(string trapId);
    public static IEnumerable<string> Status();
}
```

#### `TrapZoneService` (`Services/Traps/TrapZoneService.cs`)
Manages trap zone configurations and spawning.

```csharp
public static class TrapZoneService
{
    public static void Initialize();
    public static void RegisterZone(TrapZoneConfig config);
    public static void UnregisterZone(string zoneId);
    public static IEnumerable<string> GetActiveZones();
}
```

#### `ChestSpawnService` (`Services/Traps/ChestSpawnService.cs`)
Manages chest spawning within traps.

```csharp
public static class ChestSpawnService
{
    public static void SpawnChest(float3 position, ChestRewardType rewardType);
    public static void ConfigureChest(Entity chest, ChestConfig config);
}
```

### Command Classes

#### `TrapCommands` (`Commands/Core/TrapCommands.cs`)
Admin commands for trap management.

```csharp
public static class TrapCommands
{
    [Command("trap_build", "Build all traps")]
    public static void CmdTrapBuild(CommandContext ctx, bool rebuild = false);
    
    [Command("trap_clear", "Clear all traps")]
    public static void CmdTrapClear(CommandContext ctx);
    
    [Command("trap_spawn", "Spawn a trap at location")]
    public static void CmdTrapSpawn(CommandContext ctx, string trapType, [RemainingText] string config);
    
    [Command("trap_status", "Show trap status")]
    public static void CmdTrapStatus(CommandContext ctx);
    
    [Command("chest_spawn", "Spawn a chest")]
    public static void CmdChestSpawn(CommandContext ctx, string rewardType);
}
```

### Data Classes

#### `ChestRewardTypes` (`Data/ChestRewardTypes.cs`)
Enumeration of chest reward types.

```csharp
public enum ChestRewardType
{
    None,
    Vampire,
    Gear,
    Consumable,
    Material,
    Resource
}
```

---

## Vlifecycle

**Prefix:** `VAuto.Lifecycle`

### Core Classes

#### `Singleton` (`Services/Lifecycle/Singleton.cs`)
Global state manager for lifecycle events.

```csharp
public class Singleton : MonoBehaviour
{
    public static Singleton Instance { get; }
    
    public bool IsInitialized { get; }
    public GameState CurrentState { get; }
    
    public void Initialize();
    public void Shutdown();
}
```

#### `ArenaLifecycleManager` (`Services/Lifecycle/ArenaLifecycleManager.cs`)
Manages arena lifecycle (spawn/despawn on player count).

```csharp
public static class ArenaLifecycleManager
{
    public static void Initialize();
    public static void RegisterArena(ArenaConfig config);
    public static void UnregisterArena(string arenaId);
    public static void SetActivePlayerThreshold(int minPlayers, int maxPlayers);
}
```

#### `ConnectionEventPatches` (`Services/Lifecycle/ConnectionEventPatches.cs`)
Patches for player connection/disconnection events.

```csharp
public static class ConnectionEventPatches
{
    // Harmony patches applied automatically
    // OnPlayerConnected(Player player)
    // OnPlayerDisconnected(Player player)
}
```

#### `LifecycleActionHandlers` (`Services/Lifecycle/LifecycleActionHandlers.cs`)
Handles lifecycle actions based on game events.

```csharp
public static class LifecycleActionHandlers
{
    public static void OnPlayerJoin(Player player);
    public static void OnPlayerLeave(Player player);
    public static void OnPvPStateChanged(bool isPvP);
    public static void OnTimeOfDayChanged(TimeSpan newTime);
}
```

### Command Classes

#### `LifecycleCommands` (`Commands/LifecycleCommands.cs`)
Admin commands for lifecycle management.

```csharp
public static class LifecycleCommands
{
    [Command("lifecycle_status", "Show lifecycle status")]
    public static void CmdLifecycleStatus(CommandContext ctx);
    
    [Command("lifecycle_pause", "Pause lifecycle events")]
    public static void CmdLifecyclePause(CommandContext ctx);
    
    [Command("lifecycle_resume", "Resume lifecycle events")]
    public static void CmdLifecycleResume(CommandContext ctx);
}
```

### Models

#### `LifecycleModels` (`Services/Lifecycle/LifecycleModels.cs`)
Data models for lifecycle configuration.

```csharp
public class ArenaConfig
{
    public string Id { get; set; }
    public float3 Position { get; set; }
    public float Radius { get; set; }
    public int MinPlayers { get; set; }
    public int MaxPlayers { get; set; }
    public List<string> SpawnOnActivate { get; set; }
    public List<string> DespawnOnDeactivate { get; set; }
}

public class GameState
{
    public int ActivePlayerCount { get; set; }
    public bool IsPvPEnabled { get; set; }
    public TimeSpan TimeOfDay { get; set; }
    public List<string> ActiveArenas { get; set; }
}
```

---

## Common Utilities

### JsonConverters (`JsonConverters.cs` - VAutoZone)
JSON serialization helpers.

```csharp
public class Float3JsonConverter : JsonConverter<float3>
{
    public override float3 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options);
    public override void Write(Utf8JsonWriter writer, float3 value, JsonSerializerOptions options);
}
```

### SimpleToml (`Services/SimpleToml.cs` - VAutoTraps/VAutoZone)
TOML configuration parsing.

```csharp
public static class SimpleToml
{
    public static T Load<T>(string filePath) where T : new();
    public static void Save<T>(T obj, string filePath);
    public static Dictionary<string, object> Parse(string tomlContent);
}
```

---

## Event System Integration

### Harmony Patches Applied

All mods use Harmony for IL weaving. Key patches include:

#### VAutoZone Patches
- `PrefabCollectionSystem.OnUpdate` - For glow prefab tracking
- `PlayerZoneSystem.OnUpdate` - For zone enter/exit events

#### VAutoTraps Patches
- `ContainerSystem.OnUpdate` - For trap triggering
- `LootContainerSystem.OnUpdate` - For chest spawning

#### Vlifecycle Patches
- `ServerGameManager.OnPlayerConnected` - Player join detection
- `ServerGameManager.OnPlayerDisconnected` - Player leave detection
- `PvPSystem.OnUpdate` - PvP state changes

---

## Version Information

| Mod | Version | Dependencies |
|-----|---------|--------------|
| VAutomationCore | 1.0.0 | BepInEx 6.0, RisingV.Shared, VCF 0.10.4 |
| VAutoZone | 1.0.0 | VAutomationCore, Unity.Entities |
| VAutoTraps | 1.0.0 | VAutomationCore, Unity.Entities |
| Vlifecycle | 1.0.0 | VAutomationCore, Stunlock.Core |
