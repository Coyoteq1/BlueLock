# VAutomationCore Patches

Ready-to-use Harmony patches for V Rising mod development.

## Usage

### 1. Register Patches in Your Plugin

```csharp
using HarmonyLib;
using VAutomationCore.Patches;

public class MyPlugin : BasePlugin
{
    private Harmony _harmony;
    
    public override void Load()
    {
        _harmony = new Harmony("com.example.mypatch");
        _harmony.PatchAll(typeof(DeathEventSystemPatch));
        _harmony.PatchAll(typeof(BuffSpawnServerPatch));
        _harmony.PatchAll(typeof(ServerBootstrapSystemPatch));
        _harmony.PatchAll(typeof(UnitSpawnerSystemPatch));
        
        // Subscribe to events
        DeathEventSystemPatch.OnDeathEvent += OnDeathEventHandler;
        BuffSpawnServerPatch.OnBuffInitialized += OnBuffInitializedHandler;
        ServerBootstrapSystemPatch.OnWorldReady += OnWorldReadyHandler;
    }
}
```

## Available Patches

### DeathEventSystemPatch
Tracks death events in the game world.

```csharp
DeathEventSystemPatch.OnDeathEvent += (sender, args) =>
{
    var killer = args.Killer;
    var victim = args.Victim;
    var reason = args.Reason;
    var isPlayerKill = args.IsPlayerKill;
    var isVBlood = args.IsVBlood;
};
```

### BuffSpawnServerPatch
Tracks when buffs are applied and removed.

```csharp
// Track buff application
BuffSpawnServerPatch.OnBuffInitialized += (sender, args) =>
{
    var owner = args.Owner;
    var buffGuid = args.BuffGuid;
    var duration = args.Duration;
};

// Track buff removal
BuffSpawnServerPatch.OnBuffDestroyed += (sender, args) =>
{
    var owner = args.Owner;
    var buffGuid = args.BuffGuid;
};
```

### ServerBootstrapSystemPatch
Tracks server initialization state.

```csharp
ServerBootstrapSystemPatch.OnServerStarted += (sender, args) =>
{
    // Server has started
};

ServerBootstrapSystemPatch.OnWorldReady += (sender, args) =>
{
    // World is ready, all systems initialized
    // Safe to access EntityManager and other systems
};

// Check initialization state
bool isReady = ServerBootstrapSystemPatch.IsWorldReady;
```

### UnitSpawnerSystemPatch
Tracks unit spawning events.

```csharp
UnitSpawnerSystemPatch.OnUnitSpawned += (sender, args) =>
{
    var spawner = args.Spawner;
    var spawnedUnit = args.SpawnedUnit;
    var prefabGuid = args.PrefabGuid;
    var position = args.Position;
    var level = args.Level;
    var isNightSpawn = args.IsNightSpawn;
};
```

### SpawnTravelBuffSystemPatch
Tracks spawn travel buff application.

```csharp
SpawnTravelBuffSystemPatch.OnSpawnTravelBuffApplied += (sender, args) =>
{
    var unit = args.Unit;
    var prefabGuid = args.PrefabGuid;
    var position = args.Position;
    var isMoving = args.IsMoving;
};
```

## Best Practices

1. **Unsubscribe from events** in `OnDestroy()` to prevent memory leaks
2. **Check CoreLogger.IsInitialized** before accessing core systems
3. **Handle exceptions** in event handlers to prevent patch failures
4. **Use prefix patches** when you need to modify behavior
5. **Use postfix patches** when you need to react to events

## Example: Complete Integration

```csharp
public class MyPlugin : BasePlugin
{
    private Harmony _harmony;
    
    public override void Load()
    {
        _harmony = new Harmony("com.example.mypatch");
        _harmony.PatchAll(typeof(DeathEventSystemPatch));
        _harmony.PatchAll(typeof(ServerBootstrapSystemPatch));
        
        // Subscribe to events
        ServerBootstrapSystemPatch.OnWorldReady += InitializeMod;
        DeathEventSystemPatch.OnDeathEvent += HandleDeath;
    }
    
    private void InitializeMod(object sender, EventArgs e)
    {
        // World is ready, initialize your mod
        CoreLogger.LogInfo("MyMod initialized");
    }
    
    private void HandleDeath(object sender, DeathEventSystemPatch.DeathEventArgs e)
    {
        // Handle death event
        if (e.IsPlayerKill)
        {
            CoreLogger.LogInfo($"Player killed an entity");
        }
    }
    
    public override void OnDestroy()
    {
        // Unsubscribe from events
        ServerBootstrapSystemPatch.OnWorldReady -= InitializeMod;
        DeathEventSystemPatch.OnDeathEvent -= HandleDeath;
        
        _harmony?.UnpatchSelf();
    }
}
```
