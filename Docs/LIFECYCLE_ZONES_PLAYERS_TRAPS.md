# VAutomationEvents - Lifecycle Zones, Players, and Traps Documentation

## Overview

This document covers the three core systems in VAutomationEvents: Lifecycle Zones, Player Management, and Trap Systems. These systems work together to provide automated arena management, player lifecycle events, and interactive trap mechanics.

---

## 1. Lifecycle Zones

### 1.1 Zone Types

Lifecycle zones are defined by the `ZoneType` enum:

```csharp
public enum ZoneType : byte 
{ 
    World,      // Default world zone
    MainArena,  // Main arena area
    PvPArena,   // PvP-specific arena
    SafeZone,   // Safe zone (no damage)
    GlowZone,   // Visual glow effect zone
    Custom      // Custom-defined zone
}
```

### 1.2 Zone Configuration

Zones are configured through the `LifecycleZone` component:

```csharp
public struct LifecycleZone : IComponentData
{
    public ZoneType Type;                    // Zone classification
    public bool AllowAutoEnter;              // Enable auto enter functionality
    public FixedString32Bytes GearLoadout;   // Kit profile to apply
    public bool AutoRepairOnEntry;           // Repair gear on zone entry
    public bool AutoRepairOnExit;            // Repair gear on zone exit
    public int RepairThreshold;               // Damage threshold for auto-repair
    public bool UnlockVBloods;               // Unlock VBlood abilities
    public bool GrantSpellbooks;             // Grant zone-specific spellbooks
    public float Radius;                     // Zone radius from center
    public float3 Center;                    // Zone center coordinates
}
```

### 1.3 Zone Configuration Example

```json
{
  "Lifecycle": {
    "Zones": [
      {
        "Type": "PvPArena",
        "Radius": 50.0,
        "Center": { "X": 1000, "Y": 0, "Z": 2000 },
        "Automation": {
          "AutoEnter": true,
          "Kit": { "Profile": "PvPLoadout" },
          "Repair": { 
            "OnEntry": true, 
            "OnExit": true, 
            "Threshold": 25 
          },
          "VBlood": { "Enabled": true },
          "Spellbook": { 
            "Enabled": true,
            "ZoneAbilities": [12345, 67890]
          }
        }
      }
    ]
  }
}
```

### 1.4 Zone Lifecycle Events

#### Auto Enter Flow
1. **Detection**: Player enters zone radius
2. **Validation**: Check `AllowAutoEnter` and player state
3. **Snapshot**: Capture current player state
4. **Processing**: Apply kits, repairs, unlocks
5. **Notification**: Trigger enter events

#### Auto Exit Flow
1. **Detection**: Player leaves zone or dies
2. **Validation**: Confirm zone exit conditions
3. **Restoration**: Restore original player state
4. **Cleanup**: Remove zone-specific abilities
5. **Notification**: Trigger exit events

---

## 2. Player Management

### 2.1 Player Data Structure

#### PlayerData Struct
```csharp
public struct PlayerData
{
    public FixedString64Bytes CharacterName;  // Player character name
    public ulong SteamID;                     // Steam platform ID
    public bool IsOnline;                     // Online status
    public Entity UserEntity;                 // User entity reference
    public Entity CharEntity;                 // Character entity reference
}
```

#### Player Class
```csharp
public class Player
{
    public string Name;        // Player name
    public ulong SteamID;      // Steam ID
    public bool IsOnline;      // Online status
    public bool IsAdmin;       // Admin status
    public Entity User;        // User entity
    public Entity Character;   // Character entity
}
```

### 2.2 Player Zone Detection

Players can check their zone status through various methods:

```csharp
// Check if player is in any zone
bool IsInZone()

// Check specific zone types
bool IsInPvPArena()     // PvP arena detection
bool IsInMainArena()     // Main arena detection
bool IsInSafeZone()      // Safe zone detection
bool IsInGlowZone()      // Glow zone detection
bool IsInArena()         // Any arena detection
```

### 2.3 Player Lifecycle State

The `LifecycleState` component tracks player progression:

```csharp
public struct LifecycleState : IComponentData
{
    public Entity CurrentZone;           // Current zone entity
    public Entity PendingZone;           // Pending zone transition
    public bool InLifecycleZone;         // Zone status flag
    public bool AutoEnterEnabled;        // Auto enter permission
    public bool KitApplied;              // Kit application status
    public bool VBloodsUnlocked;         // VBlood unlock status
    public bool SpellbooksGranted;       // Spellbook grant status
    public float LastTransitionTime;     // Last zone change time
    public int RepairThreshold;          // Personal repair threshold
}
```

### 2.4 Player Request System

Players can trigger lifecycle actions through request components:

```csharp
public struct AutoEnterRequest : IComponentData { }
public struct KitApplyRequest : IComponentData 
{ 
    public FixedString32Bytes KitName; 
}
public struct SpellbookGrantRequest : IComponentData { }
```

---

## 3. Trap System

### 3.1 Trap Configuration

Traps are configured using the `ContainerTrap` component:

```csharp
public struct ContainerTrap : IComponentData
{
    public bool Armed;                    // Trap armed status
    public ulong OwnerPlatformId;         // Trap owner Steam ID
    public int DamageAbilityPrefabId;      // Damage ability prefab
    public int VfxPrefabId;               // Visual effect prefab
    public float DamageRadius;            // Damage area radius
    public float DamageAmount;            // Damage dealt
    public float LifetimeSeconds;          // Trap lifetime
    public int MaxTriggers;               // Maximum trigger count
    public int TriggerCount;              // Current trigger count
    public double LastTriggeredTime;      // Last trigger timestamp
    public float CooldownSeconds;         // Trigger cooldown
}
```

### 3.2 Trap Events

#### Trap Triggered Event
```csharp
public struct TrapTriggeredEvent : IComponentData
{
    public ulong OwnerPlatformId;          // Trap owner
    public ulong IntruderPlatformId;       // Who triggered it
    public float3 Position;                // Trigger location
    public int WaypointId;                 // Location waypoint
    public int RegionId;                   // Region identifier
}
```

#### Trigger History
```csharp
public struct TrapTriggerHistory : IBufferElementData
{
    public double Timestamp;               // Trigger time
    public ulong IntruderPlatformId;       // Intruder ID
    public float3 IntruderPosition;        // Intruder location
}
```

### 3.3 Trap Lifecycle

1. **Creation**: Trap placed on container
2. **Arming**: Trap becomes active
3. **Detection**: Player enters trigger radius
4. **Validation**: Check owner/intruder permissions
5. **Execution**: Apply damage and effects
6. **Logging**: Record trigger event
7. **Cooldown**: Wait before next trigger
8. **Cleanup**: Remove when expired or depleted

### 3.4 Trap Integration with Zones

Traps work seamlessly with lifecycle zones:
- **Zone Awareness**: Traps respect zone rules (safe zones disable traps)
- **Auto Cleanup**: Traps auto-disarm when owners leave zones
- **Event Integration**: Trap triggers generate lifecycle events
- **Performance**: ECS-based processing for high trap counts

---

## 4. System Integration

### 4.1 ECS System Architecture

All three systems use Unity's Entity Component System (ECS):

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   Zone System   │    │  Player System   │    │   Trap System   │
│                 │    │                  │    │                 │
│ • Zone Tracking │◄──►│ • State Mgmt     │◄──►│ • Trap Logic    │
│ • Auto Enter    │    │ • Kit Application│    │ • Damage Calc   │
│ • Visual Effects│    │ • Zone Detection │    │ • Event Logging │
└─────────────────┘    └──────────────────┘    └─────────────────┘
         │                       │                       │
         └───────────────────────┼───────────────────────┘
                                 │
                    ┌──────────────────┐
                    │  Event System    │
                    │                  │
                    │ • Enter/Exit     │
                    │ • Trap Triggered │
                    │ • State Changes  │
                    └──────────────────┘
```

### 4.2 Configuration Management

All systems use the unified configuration system:
- **Hot Reload**: Configuration changes apply without restart
- **JSON Format**: Human-readable configuration files
- **Validation**: Automatic config validation on load
- **Fallbacks**: Default values for missing settings

### 4.3 Performance Considerations

- **Batch Processing**: Entity queries process multiple entities
- **Memory Management**: Native collections with proper disposal
- **Update Groups**: Systems run in optimized order
- **Culling**: Inactive entities excluded from processing

---

## 5. Usage Examples

### 5.1 Creating a PvP Arena Zone

```csharp
// Create zone entity
var zoneEntity = entityManager.CreateEntity();

// Add zone component
entityManager.AddComponentData(zoneEntity, new LifecycleZone
{
    Type = ZoneType.PvPArena,
    AllowAutoEnter = true,
    GearLoadout = "PvPLoadout",
    AutoRepairOnEntry = true,
    AutoRepairOnExit = true,
    RepairThreshold = 25,
    UnlockVBloods = true,
    GrantSpellbooks = true,
    Radius = 50.0f,
    Center = new float3(1000, 0, 2000)
});

// Add transform for positioning
entityManager.AddComponentData(zoneEntity, LocalTransform.FromPosition(
    new float3(1000, 0, 2000)
));
```

### 5.2 Setting Up a Container Trap

```csharp
// Add trap to container entity
entityManager.AddComponentData(containerEntity, new ContainerTrap
{
    Armed = true,
    OwnerPlatformId = playerSteamId,
    DamageAbilityPrefabId = 12345,
    VfxPrefabId = 67890,
    DamageRadius = 5.0f,
    DamageAmount = 100.0f,
    LifetimeSeconds = 300.0f,
    MaxTriggers = 5,
    CooldownSeconds = 2.0f
});
```

### 5.3 Checking Player Zone Status

```csharp
// Get player entity
var playerEntity = GetPlayerEntity(steamId);

// Check if in lifecycle zone
if (entityManager.HasComponent<LifecycleState>(playerEntity))
{
    var state = entityManager.GetComponentData<LifecycleState>(playerEntity);
    
    if (state.InLifecycleZone)
    {
        // Player is in a zone
        var zone = entityManager.GetComponentData<LifecycleZone>(state.CurrentZone);
        
        // Check zone type
        if (zone.Type == ZoneType.PvPArena)
        {
            // Handle PvP arena logic
        }
    }
}
```

---

## 6. Troubleshooting

### 6.1 Common Issues

**Zone Not Detecting Players**
- Check zone radius and center coordinates
- Verify `AllowAutoEnter` is enabled
- Ensure player has `LifecycleState` component

**Traps Not Triggering**
- Verify trap is armed (`Armed = true`)
- Check damage radius and cooldown
- Ensure intruder is not trap owner

**Performance Issues**
- Monitor entity count in queries
- Check for memory leaks in native collections
- Verify system update order

### 6.2 Debug Commands

Use these commands for debugging:
- `.zone list` - List all active zones
- `.zone info <zoneId>` - Get zone details
- `.trap list` - List active traps
- `.player status <name>` - Check player zone status

---

## 9. Kit System Integration

### 9.1 Kit Overview

The Kit System provides comprehensive equipment and consumable management that integrates seamlessly with the lifecycle zone system. Kits can be automatically applied when players enter specific zones and restored when they exit.

### 9.2 Kit Configuration Structure

#### Kit Profile Configuration
```csharp
public class EndGameKitProfile
{
    public string Name { get; set; }                    // Unique kit identifier
    public string Description { get; set; }             // Human-readable description
    public bool Enabled { get; set; }                   // Enable/disable kit
    public bool AutoApplyOnZoneEntry { get; set; }      // Auto-apply on zone entry
    public List<string> AutoApplyZones { get; set; }    // Zones for auto-application
    public bool RestoreOnExit { get; set; }            // Restore gear on exit
    public int MinimumGearScore { get; set; }          // Gear score requirement
    public bool AllowInPvP { get; set; }               // PvP zone permission
    public Dictionary<string, long> Equipment { get; set; }  // Equipment mapping
    public List<ConsumableItem> Consumables { get; set; }    // Consumables with quantities
    public List<long> Jewels { get; set; }             // Jewel socketing
    public StatOverrideConfig StatOverrides { get; set; }    // Stat bonuses
}
```

#### Equipment Slot Mapping
```csharp
// Supported equipment slots
"MainHand", "OffHand", "Head", "Chest", "Legs", "Feet", 
"Hands", "Neck", "Finger1", "Finger2", "Cloak", "Mount"
```

#### Stat Override Configuration
```csharp
public class StatOverrideConfig
{
    public float BonusPower { get; set; }              // Physical power %
    public float BonusMaxHealth { get; set; }          // Flat health bonus
    public float BonusSpellPower { get; set; }         // Spell power %
    public float BonusMoveSpeed { get; set; }          // Movement speed %
    public float BonusPhysicalResistance { get; set; } // Physical resistance %
    public float BonusSpellResistance { get; set; }    // Spell resistance %
    public float BonusArmor { get; set; }              // Flat armor bonus
    public float BonusMaxStamina { get; set; }         // Flat stamina bonus
}
```

### 9.3 Kit Lifecycle Integration

#### Zone Entry Integration
```json
{
  "Lifecycle": {
    "Zones": [
      {
        "Type": "PvPArena",
        "Automation": {
          "Kit": { 
            "Profile": "PvP_Arena" 
          }
        }
      }
    ]
  }
}
```

#### Auto-Application Flow
1. **Zone Detection**: Player enters configured zone
2. **Kit Lookup**: Find matching kit profile for zone
3. **Validation**: Check gear score, PvP permissions, kit enabled status
4. **Gear Backup**: Save current equipment to buffer
5. **Kit Application**: Apply new equipment, consumables, jewels, stats
6. **State Update**: Mark kit as applied with timestamp

#### Zone Exit Integration
1. **Exit Detection**: Player leaves zone or dies
2. **Kit Verification**: Confirm kit was applied to this player
3. **Gear Restoration**: Restore saved equipment from buffer
4. **Stat Cleanup**: Remove kit-specific stat buffs
5. **State Reset**: Clear kit application flags

### 9.4 Kit Types and Use Cases

#### PvP Arena Kits
```json
{
  "name": "PvP_Arena",
  "description": "Optimized PvP arena kit with balanced stats",
  "autoApplyOnZoneEntry": true,
  "autoApplyZones": ["PvPArena", "ArenaZone1"],
  "allowInPvP": true,
  "minimumGearScore": 90,
  "statOverrides": {
    "bonusPower": 30.0,
    "bonusMaxHealth": 400.0,
    "bonusMoveSpeed": 0.08,
    "bonusPhysicalResistance": 15.0,
    "bonusSpellResistance": 15.0
  }
}
```

#### PvE End-Game Kits
```json
{
  "name": "PvE_EndGame",
  "description": "End-game kit for PvE zones only",
  "autoApplyOnZoneEntry": true,
  "autoApplyZones": ["EndGameZone1", "EndGameZone2"],
  "allowInPvP": false,
  "minimumGearScore": 85,
  "statOverrides": {
    "bonusPower": 20.0,
    "bonusMaxHealth": 250.0,
    "bonusSpellPower": 12.0
  }
}
```

#### Role-Specific Kits

**Healer Support Kit:**
```json
{
  "name": "Healer_Support",
  "description": "Support-focused kit with enhanced healing",
  "statOverrides": {
    "bonusSpellPower": 35.0,
    "bonusMaxStamina": 75.0,
    "bonusSpellResistance": 20.0
  },
  "consumables": [
    { "guid": -1464869976, "quantity": 10 },  // Healing potions
    { "guid": -1464869977, "quantity": 10 }   // Mana potions
  ]
}
```

**Tank Bruiser Kit:**
```json
{
  "name": "Tank_Bruiser",
  "description": "High-defense tank kit with maximum survivability",
  "statOverrides": {
    "bonusMaxHealth": 600.0,
    "bonusPhysicalResistance": 25.0,
    "bonusArmor": 100.0,
    "bonusMaxStamina": 100.0
  }
}
```

**Speed Demon Kit:**
```json
{
  "name": "Speed_Demon",
  "description": "High-mobility kit focused on speed",
  "statOverrides": {
    "bonusMoveSpeed": 0.15,
    "bonusMaxStamina": 150.0,
    "bonusPower": 25.0
  }
}
```

### 9.5 Kit Configuration Examples

#### Complete Kit Profile
```json
{
  "name": "GS91_Standard",
  "description": "Standard GS91 end-game kit with Greatsword",
  "enabled": true,
  "autoApplyOnZoneEntry": false,
  "autoApplyZones": [],
  "restoreOnExit": true,
  "minimumGearScore": 0,
  "allowInPvP": false,
  "equipment": {
    "MainHand": -1234567890,
    "Head": -1234567900,
    "Chest": -1234567901,
    "Legs": -1234567902,
    "Feet": -1234567903,
    "Hands": -1234567904,
    "Neck": -1234567910,
    "Finger1": -1234567911,
    "Finger2": -1234567912
  },
  "consumables": [
    { "guid": -1464869972, "quantity": 10 },  // Health potions
    { "guid": 1977859216, "quantity": 5 },    // Blood potions
    { "guid": -1858380711, "quantity": 3 },  // Garlic coating
    { "guid": -1446898756, "quantity": 20 }  // Vermin coating
  ],
  "jewels": [-987654321, 123456789],
  "statOverrides": {
    "bonusPower": 25.0,
    "bonusMaxHealth": 300.0,
    "bonusSpellPower": 15.0,
    "bonusMoveSpeed": 0.05
  }
}
```

### 9.6 Kit System Components

#### Player Kit State Tracking
```csharp
public struct PlayerEndGameKitState : IComponentData
{
    public bool KitApplied;                    // Kit application status
    public FixedString64Bytes AppliedKitName;  // Applied kit name
    public double AppliedTimestamp;           // Application time
    public bool PreviousGearSaved;             // Backup status
}
```

#### Equipment Backup Buffer
```csharp
[InternalBufferCapacity(12)]
public struct SavedEquipmentBuffer : IBufferElementData
{
    public EquipmentSlot Slot;     // Equipment slot
    public PrefabGUID ItemGuid;    // Original item GUID
}
```

#### Zone Kit Integration Tag
```csharp
public struct InEndGameZoneTag : IComponentData
{
    public FixedString64Bytes ZoneName;        // Current zone
    public FixedString64Bytes KitProfileName;  // Kit to apply
}
```

### 9.7 Kit System Services

#### EndGameKitService
- **Kit Loading**: Load and validate kit configurations
- **Auto-Application**: Handle zone-based kit application
- **Gear Management**: Backup and restore player equipment
- **Stat Application**: Apply/remove stat override buffs
- **Consumable Management**: Add consumables to inventory

#### EquipmentService
- **Slot Management**: Handle equipment slot operations
- **Item Validation**: Verify item GUIDs and compatibility
- **Inventory Operations**: Safe inventory modifications
- **Durability Handling**: Manage item durability during swaps

#### StatExtensionService
- **Buff Application**: Apply stat override buffs
- **Buff Removal**: Clean up kit-specific buffs
- **Stat Calculation**: Calculate final stat values
- **Conflict Resolution**: Handle multiple stat sources

### 9.8 Kit System Commands

#### Manual Kit Management
```
.kit list                    # List available kits
.kit apply <kitName>         # Manually apply kit
.kit restore                 # Restore original gear
.kit info <kitName>          # Show kit details
.kit reload                  # Reload kit configurations
```

#### Administrative Commands
```
.kit admin create <name>     # Create new kit profile
.kit admin edit <name>       # Edit existing kit
.kit admin delete <name>     # Delete kit profile
.kit admin toggle <name>     # Enable/disable kit
.kit admin test <name>       # Test kit application
```

### 9.9 Kit System Best Practices

#### Configuration Management
1. **Version Control**: Maintain kit configuration versions
2. **Validation**: Validate all item GUIDs on load
3. **Backup**: Always backup configurations before changes
4. **Testing**: Test kits in safe zones first

#### Performance Optimization
1. **Batch Operations**: Apply equipment changes in batches
2. **Entity Culling**: Exclude inactive players from processing
3. **Memory Management**: Dispose native collections properly
4. **Update Frequency**: Limit kit system update rate

#### Balance Considerations
1. **Gear Score Requirements**: Set appropriate minimum thresholds
2. **Stat Balance**: Keep stat bonuses within reasonable ranges
3. **PvP Restrictions**: Carefully consider PvP kit permissions
4. **Zone Matching**: Match kit power to zone difficulty

#### User Experience
1. **Clear Feedback**: Notify players of kit changes
2. **Graceful Failures**: Handle kit application failures gracefully
3. **Restore Safety**: Ensure gear restoration always works
4. **Cooldown Management**: Prevent kit application spam

### 9.10 Troubleshooting Kit Issues

#### Common Kit Problems

**Kit Not Applying**
- Check if kit is enabled in configuration
- Verify zone is in auto-apply zones list
- Confirm player meets gear score requirement
- Check PvP restrictions for current zone

**Gear Not Restoring**
- Verify `restoreOnExit` is enabled for kit
- Check if gear backup was successful
- Ensure player inventory has space
- Look for equipment slot conflicts

**Stat Overrides Not Working**
- Verify stat override configuration
- Check for conflicting buffs from other sources
- Ensure buff system is running
- Validate stat value ranges

**Performance Issues**
- Monitor kit system update frequency
- Check for excessive entity queries
- Verify proper disposal of native collections
- Look for memory leaks in equipment buffers

#### Debug Commands
```
.kit debug player <name>      # Show player kit state
.kit debug zone <zoneName>    # Show zone kit configuration
.kit debug kit <kitName>      # Show kit details
.kit debug stats              # Show stat override status
.kit debug performance       # Show system performance metrics
```

---

## 10. Best Practices

1. **Zone Design**: Keep zones focused on single purposes
2. **Trap Balance**: Limit trap density and damage
3. **Performance**: Use batch operations for entity modifications
4. **Configuration**: Use descriptive names and comments
5. **Testing**: Test zone transitions thoroughly
6. **Logging**: Enable debug logging during development
7. **Memory**: Always dispose native collections properly

---

## 8. API Reference

### 8.1 Key Services

- `LifecycleService` - Zone lifecycle management
- `AutoEnterService` - Auto enter/exit processing
- `ArenaPlayerService` - Player arena management
- `TrapSystemService` - Trap creation and management

### 8.2 Core Components

- `LifecycleZone` - Zone configuration
- `LifecycleState` - Player lifecycle state
- `ContainerTrap` - Trap configuration
- `PlayerData` - Player information storage

### 8.3 Event Types

- `AutoEnterRequest` - Zone entry request
- `TrapTriggeredEvent` - Trap activation event
- `KitApplyRequest` - Equipment kit request
- `SpellbookGrantRequest` - Ability grant request

---

*This documentation covers the core functionality of lifecycle zones, player management, and trap systems in VAutomationEvents. For specific implementation details, refer to the source code in the respective modules.*
