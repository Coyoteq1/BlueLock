# VAutomationCore API Reference

> Generated documentation for VAutomationCore and Blueluck APIs

## Core APIs

### FlowService (`Core/Api/FlowService.cs`)

Main flow execution engine for the automation system.

**Key Methods:**
- `RegisterActionAlias(string alias, string action, bool replace)` - Register action aliases
- `ExecuteFlow(string flowId, Entity player, Dictionary<string, object> context)` - Execute a flow

**Usage:**
```csharp
FlowService.ExecuteFlow("arena_enter", playerEntity, new Dictionary<string, object>
{
    { "zoneId", "arena_01" },
    { "zoneHash", 12345 }
});
```

---

### PlayerApi (`Core/Api/PlayerApi.cs`)

Player entity management and interaction.

**Key Methods:**
- `GetPlayerByName(string name)` - Get player entity by name
- `GetPlayerByUserId(ulong userId)` - Get player entity by user ID
- `TeleportPlayer(Entity player, float3 position)` - Teleport player

---

### ServerApi (`Core/Api/ServerApi.cs`)

Server-level operations and state management.

**Key Methods:**
- `GetServerTime()` - Get current server time
- `BroadcastMessage(string message)` - Broadcast message to all players

---

### EntityMap (`Core/Api/EntityMap.cs`)

Entity to user mapping with validation.

**Key Methods:**
- `TryGetUserEntity(Entity character, out Entity userEntity)` - Get user entity from character
- `ValidateEntityComponents(Entity entity, Type[] requiredComponents)` - Validate entity has required components

---

### ConsoleRoleAuthService (`Core/Api/ConsoleRoleAuthService.cs`)

Role-based command authorization.

**Key Methods:**
- `Authorize(ChatCommandContext ctx, string requiredRole)` - Check if user has required role
- `GetUserRole(Entity user)` - Get user's current role

---

## Blueluck Services

### FlowRegistryService (`Blueluck/Services/FlowRegistryService.cs`)

Manages zone transition flows.

**Flow Actions:**
- `zone.setpvp` - Enable/disable PvP
- `zone.sendmessage` - Send chat message
- `zone.spawnboss` - Spawn VBlood boss
- `zone.removeboss` - Remove spawned boss
- `zone.applyborderfx` - Apply visual border effect
- `zone.applykit` - Apply equipment kit

**Key Methods:**
- `ExecuteFlow(string flowId, Entity player, string zoneId, int zoneHash)` - Execute flow
- `LoadFlows()` - Load flows from config

---

### ZoneConfigService (`Blueluck/Services/ZoneConfigService.cs`)

Zone configuration management.

**Key Methods:**
- `GetZones()` - Get all configured zones
- `TryGetZoneByHash(int hash, out ZoneDefinition zone)` - Get zone by hash
- `Reload()` - Reload configuration

---

### KitService (`Blueluck/Services/KitService.cs`)

Equipment loadout system.

**Key Methods:**
- `ApplyKit(Entity player, string kitName)` - Apply kit to player
- `KitExists(string kitName)` - Check if kit exists
- `ListKitNames()` - List all available kits

---

### PrefabToGuidService (`Blueluck/Services/PrefabToGuidService.cs`)

Prefab name to GUID resolution.

**Key Methods:**
- `TryGetGuid(string prefabName, out PrefabGUID guid)` - Resolve prefab name to GUID
- `TryGetName(PrefabGUID guid, out string name)` - Resolve GUID to name

---

### FlowValidationService (`Blueluck/Services/FlowValidationService.cs`)

Validates flows before execution.

**Key Methods:**
- `ValidateAction(FlowAction action)` - Validate single action
- `ValidateZoneFlows(ZoneDefinition zone)` - Validate zone flow references

---

## Configuration

### Config Entries (BepInEx)

**Blueluck:**
- `General.Enabled` - Enable/disable plugin
- `General.LogLevel` - Logging level (Debug, Info, Warning, Error)
- `Detection.CheckIntervalMs` - Zone detection interval
- `Detection.PositionThreshold` - Position threshold
- `Detection.DebugMode` - Debug mode
- `Flow.Enabled` - Flow system toggle
- `Kits.Enabled` - Kit system toggle
- `Progress.Enabled` - Progress system toggle
- `Abilities.Enabled` - Abilities toggle

---

## Chat Commands

### Zone Commands
- `zone status` / `zs` - Show zone status
- `zone list` / `zl` - List all zones
- `zone reload` / `zr` - Reload configuration
- `zone debug` - Toggle debug mode
- `flow validate` - Validate flows
- `flow reload` - Reload flows

### Kit Commands
- `kit list` - List available kits
- `kit [name]` - Apply kit

### Snapshot Commands
- `snap status` - Show snapshot status
- `snap save [name]` - Save snapshot
- `snap apply [name]` - Apply snapshot
- `snap restore` - Restore last snapshot
- `snap clear` - Clear snapshots
