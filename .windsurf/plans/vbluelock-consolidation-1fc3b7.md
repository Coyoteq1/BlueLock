# VBluelock Consolidation Plan

This plan consolidates CycleBorn and Bluelock into a unified VBluelock project using VAutomationCore architecture principles.

## Current State Analysis

### CycleBorn Module
- **LifecycleCommands**: Arena state management with lifecycle transitions
- **AutoCommandComponents**: Command tracking with ECS components
- **Configuration**: `lifecycle.policy.json`, flows registry
- **Focus**: Arena/PVP gameplay mechanics

### Bluelock Module  
- **GlowCommands**: Zone glow effects with presets
- **GlowService/VisualService/TileService/CastleService**: Visual effects
- **Configuration**: Multiple JSON configs for glow types/presets
- **Focus**: Zone management and visual effects

## Consolidation Strategy

### 1. Project Structure
**Create: `VBluelock/` (New unified project)**
```
VBluelock/
├── Core/
│   ├── Commands/
│   │   ├── UnifiedCommands.cs
│   │   ├── ArenaCommands.cs
│   │   ├── ZoneCommands.cs
│   │   └── FlowCommands.cs
│   ├── Services/
│   │   ├── UnifiedStateService.cs
│   │   ├── GlowService.cs
│   │   ├── VisualService.cs
│   │   ├── TileService.cs
│   │   ├── CastleService.cs
│   │   └── FlowService.cs
│   ├── Events/
│   │   ├── CrossModuleEvents.cs
│   │   ├── ArenaEvents.cs
│   │   └── ZoneEvents.cs
│   ├── Components/
│   │   ├── AutoCommandComponents.cs
│   │   └── StateComponents.cs
│   └── Configuration/
│       ├── UnifiedConfig.json
│       ├── FlowDefinitions.json
│       └── Presets.json
├── Data/
│   ├── DataTypes/
│   │   ├── ArenaTypes.cs
│   │   ├── ZoneTypes.cs
│   │   └── FlowTypes.cs
│   └── Presets/
│       ├── ArenaPresets.json
│       ├── ZonePresets.json
│       └── GlowPresets.json
├── Tests/
│   ├── Integration/
│   └── Unit/
└── VBluelock.csproj
```

### 2. Unified Command System
**Create: `Core/Commands/UnifiedCommands.cs`**
```csharp
[CommandGroup("vbluelock", "vbl")]
public static class UnifiedCommands
{
    // Unified command registration for all modules
    public static void RegisterAllCommands()
    
    // Command routing with module context awareness
    public static void RouteCommand(string command, ChatCommandContext ctx, params string[] args)
    
    // Module-specific command handlers
    public static void HandleArenaCommand(string subCommand, ChatCommandContext ctx, params string[] args)
    public static void HandleZoneCommand(string subCommand, ChatCommandContext ctx, params string[] args)
    public static void HandleFlowCommand(string subCommand, ChatCommandContext ctx, params string[] args)
}
```

### 3. Unified State Management
**Create: `Core/Services/UnifiedStateService.cs`**
```csharp
public static class UnifiedStateService
{
    // Unified state tracking across all modules
    public static Dictionary<string, UnifiedModuleState> ModuleStates { get; }
    
    // Arena-zone interaction tracking
    public static void RegisterArenaZoneInteraction(string arenaId, string zoneId)
    
    // Cross-module event system
    public static void PublishUnifiedEvent(UnifiedEventType eventType, object data)
    
    // State synchronization
    public static void SynchronizeModuleStates()
    
    // Persistence
    public static void SaveUnifiedState()
    public static void LoadUnifiedState()
}
```

### 4. Enhanced Service Integration

#### Unified GlowService
```csharp
public static class GlowService
{
    // Arena-aware glow methods
    public static void SetArenaGlow(string arenaId, float3 color, float radius)
    public static void SetArenaBorder(string arenaId, float3 color, float intensity)
    
    // Zone methods with arena integration
    public static void SetZoneWithArenaSupport(string zoneId, ZoneConfig config, string linkedArenaId)
    
    // Flow-aware glow effects
    public static void SetZoneWithFlowSupport(string zoneId, List<FlowDefinition> flows)
    
    // Unified preset system
    public static void ApplyUnifiedPreset(string presetId, string targetId)
}
```

#### Enhanced Arena Integration
```csharp
// Merge CycleBorn lifecycle with zone support
public static class ArenaLifecycleService
{
    public static void OnZoneActivated(string zoneId, string arenaId)
    public static void ApplyZoneEffectsToArena(string zoneId, LifecycleStage stage)
    public static void TriggerArenaFlowFromZone(string zoneId, string flowType)
}
```

### 5. Unified Configuration System

#### Single Config Schema
**Create: `Core/Configuration/UnifiedConfig.json`**
```json
{
  "modules": {
    "arena": {
      "enabled": true,
      "defaultPresets": ["pvp", "peaceful", "training"],
      "autoZoneIntegration": true
    },
    "zone": {
      "enabled": true,
      "defaultPresets": ["chaos", "peaceful", "danger"],
      "autoArenaIntegration": true
    },
    "glow": {
      "enabled": true,
      "defaultPresets": ["chaos", "peaceful", "danger"],
      "sharedPresets": true
    },
    "flows": {
      "enabled": true,
      "crossModuleTriggers": true,
      "stateIntegration": true
    }
  },
  "integration": {
    "crossModuleEvents": true,
    "unifiedCommands": true,
    "sharedState": true,
    "synchronizedPresets": true
  }
}
```

#### Unified Preset System
**Create: `Data/Presets/UnifiedPresets.json`**
```json
{
  "arena": [
    {
      "id": "pvp_intense",
      "name": "Intense PVP",
      "description": "High-intensity arena with aggressive effects",
      "settings": {
        "glowIntensity": 1.5,
        "borderEffects": true,
        "flowTriggers": ["combat_start", "player_eliminated"]
      }
    }
  ],
  "zone": [
    {
      "id": "danger_zone",
      "name": "Danger Zone",
      "description": "Hostile area with warning effects",
      "settings": {
        "glowColor": "red",
        "visualEffects": ["lightning", "explosions"],
        "flowTriggers": ["zone_entered", "hostile_detected"]
      }
    }
  ],
  "glow": [
    {
      "id": "chaos_inferno",
      "name": "Chaos Inferno",
      "description": "Intense chaotic effects",
      "settings": {
        "glowTypes": ["chaos", "emerald", "agony"],
        "rotationSpeed": 2.0,
        "particleDensity": "high"
      }
    }
  ]
}
```

### 6. Data Type Unification

#### Unified Data Types
**Create: `Core/DataTypes/UnifiedTypes.cs`**
```csharp
// Unified data types for all modules
public enum UnifiedModuleType
{
    Arena,
    Zone,
    Glow,
    Flow
}

public enum UnifiedCommandType
{
    ArenaManagement,
    ZoneManagement,
    GlowManagement,
    FlowManagement,
    CrossModule
}

public struct UnifiedCommandContext
{
    public UnifiedModuleType ModuleType;
    public string SubCommand;
    public ChatCommandContext ChatContext;
    public string[] Args;
}
```

### 7. Migration Strategy

#### Phase 1: Project Setup (Day 1-2)
1. Create VBluelock project structure
2. Set up build configuration and dependencies
3. Create unified base classes and interfaces

#### Phase 2: Core Services (Day 3-5)
1. Implement UnifiedStateService
2. Create UnifiedCommands with routing
3. Implement enhanced GlowService with arena integration
4. Create unified configuration system

#### Phase 3: Module Integration (Day 6-8)
1. Migrate and enhance arena lifecycle system
2. Integrate zone system with unified state
3. Implement flow system with cross-module triggers
4. Create unified preset system

#### Phase 4: Command Migration (Day 9-10)
1. Update existing commands to use unified routing
2. Create backward compatibility layer
3. Implement command aliases for smooth transition
4. Add deprecation warnings for old commands

#### Phase 5: Testing & Polish (Day 11-12)
1. Create comprehensive integration tests
2. Test cross-module interactions
3. Performance optimization and cleanup
4. Documentation and deployment preparation

### 8. Benefits of Consolidation

#### Development Benefits
- **Single Codebase**: One project instead of two separate modules
- **Unified Architecture**: Consistent patterns across all features
- **Easier Maintenance**: Single build system and deployment
- **Better Testing**: Integrated test scenarios across modules

#### User Experience Benefits
- **Seamless Integration**: Arena and zone features work together naturally
- **Rich Interactions**: Complex cross-module scenarios (arena fights with zone effects)
- **Consistent Interface**: Unified command structure and behavior
- **Powerful Presets**: Shared preset system across all modules

#### Technical Benefits
- **Reduced Dependencies**: Eliminate duplicate services and configurations
- **Performance**: Optimized cross-module communication
- **Scalability**: Unified architecture supports future expansion
- **Maintainability**: Clear separation of concerns with unified interfaces

### 9. Implementation Priority

#### High Priority
1. **Unified State Service** - Core of the new architecture
2. **Command Routing System** - Essential for user interaction
3. **Configuration System** - Replaces multiple JSON files with single system

#### Medium Priority
1. **Enhanced Glow Service** - Arena-aware visual effects
2. **Flow Integration** - Cross-module trigger system
3. **Preset System** - Unified preset management

#### Low Priority
1. **Legacy Compatibility** - Smooth migration from existing systems
2. **Advanced Features** - Enhanced interactions and effects
3. **Performance Optimization** - Post-launch optimizations

This consolidation creates a **unified VBluelock project** that combines the strengths of both CycleBorn and Bluelock while providing a seamless, integrated experience with rich cross-module interactions and unified configuration management.
