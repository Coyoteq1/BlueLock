# Cross-Services Integration Plan

This plan creates unified cross-service integration between CycleBorn and Bluelock modules using VAutomationCore naming conventions.

## Current State Analysis

### CycleBorn Module
- **LifecycleCommands**: Arena state management with lifecycle transitions
- **AutoCommandComponents**: Command tracking with ECS components
- **Focus**: Arena/PVP gameplay mechanics

### Bluelock Module  
- **GlowCommands**: Zone glow effects with presets
- **GlowService/VisualService/TileService/CastleService**: Visual effects
- **Focus**: Zone management and visual effects

## Integration Strategy

### 1. Unified Command System
**Create: `Services/UnifiedCommandService.cs`**
```csharp
public static class UnifiedCommandService
{
    // Cross-module command registration
    public static void RegisterCrossModuleCommands()
    
    // Command routing between CycleBorn and Bluelock
    public static void RouteCommand(string command, ChatCommandContext ctx, params string[] args)
}
```

### 2. Shared State Management
**Create: `Services/SharedStateService.cs`**
```csharp
public static class SharedStateService
{
    // Unified state tracking across modules
    public static Dictionary<string, CrossModuleState> ModuleStates { get; }
    
    // Arena-Zone interaction tracking
    public static void RegisterArenaZoneInteraction(string arenaId, string zoneId)
    
    // Cross-module event system
    public static void PublishCrossModuleEvent(CrossModuleEvent eventType, object data)
}
```

### 3. Cross-Module Events
**Create: `Events/CrossModuleEvents.cs`**
```csharp
// Events that bridge CycleBorn and Bluelock
public class ArenaZoneActivatedEvent
public class PlayerEnteredArenaWithZoneEffectEvent
public class CrossModuleStateChangedEvent
```

### 4. Enhanced Command Features

#### Arena + Zone Integration
- `.arena zoneglow <zoneId>` - Apply zone glow to arena
- `.arena border <color>` - Set arena border with glow effects
- `.arena preset <preset>` - Apply glow preset to arena

#### Zone + Arena Integration  
- `.zone arena <zoneId>` - Link zone to arena system
- `.zone status <arenaId>` - Show arena status with zone effects
- `.zone trigger <zoneId> <effect>` - Trigger arena effects from zone

#### Unified Cross Commands
- `.cross status` - Show all module states and interactions
- `.cross sync` - Synchronize states between modules
- `.cross reset` - Reset cross-module state

### 5. Service Integration Points

#### GlowService Enhancements
```csharp
// Add arena-aware methods
public static void SetArenaGlow(string arenaId, float3 color, float radius)
public static void LinkZoneToArena(string zoneId, string arenaId)

// Enhanced zone methods with arena integration
public static void SetZoneWithArenaSupport(string zoneId, ZoneConfig config, string linkedArenaId)
```

#### CycleBorn Enhancements
```csharp
// Add zone-aware lifecycle methods
public static void OnZoneActivatedInArena(string zoneId, string arenaId)
public static void ApplyZoneEffectsToLifecycle(string zoneId, LifecycleStage stage)

// Enhanced arena methods with zone support
public static void SetArenaWithZoneEffects(string arenaId, List<string> zoneIds)
```

### 6. Configuration Integration

#### Unified Config Schema
**Create: `Config/unified-cross-config.json`**
```json
{
  "crossModule": {
    "enableArenaZoneIntegration": true,
    "enableSharedStateTracking": true,
    "enableCrossModuleEvents": true
  },
  "arenaZone": {
    "defaultZonePresets": ["chaos", "peaceful", "danger"],
    "autoLinkZones": true,
    "inheritZoneEffects": true
  },
  "zoneArena": {
    "defaultArenaPresets": ["pvp", "peaceful", "training"],
    "autoLinkArenas": true,
    "broadcastZoneEvents": true
  },
  "flows": {
    "enableCrossModuleFlows": true,
    "sharedFlowDefinitions": true,
    "flowStateIntegration": true,
    "arenaZoneFlowTriggers": true
  }
}
```

### 7. Flow System Integration

#### Cross-Module Flow Support
**Create: `Services/CrossModuleFlowService.cs`**
```csharp
public static class CrossModuleFlowService
{
    // Flow integration between CycleBorn and Bluelock
    public static void RegisterCrossModuleFlows()
    
    // Arena-zone flow triggers
    public static void TriggerArenaZoneFlow(string arenaId, string zoneId, string flowType)
    
    // Zone-arena flow execution
    public static void ExecuteZoneArenaFlow(string zoneId, string arenaId, FlowDefinition flow)
    
    // Cross-module flow state management
    public static void UpdateFlowState(string flowId, CrossModuleFlowState state)
}
```

#### Enhanced Flow Commands
- `.flow arena <zoneId> <flowType>` - Trigger flow in arena from zone
- `.flow zone <arenaId> <flowType>` - Trigger flow in zone from arena
- `.flow status` - Show active cross-module flows
- `.flow trigger <flowId>` - Manually trigger any cross-module flow

#### Flow Integration Points
```csharp
// Enhanced GlowService with flow support
public static void SetZoneWithFlowSupport(string zoneId, ZoneConfig config, string linkedArenaId, List<FlowDefinition> flows)

// Enhanced CycleBorn with zone flow integration
public static void OnZoneFlowTriggered(string zoneId, string arenaId, FlowDefinition flow)
```

### 7. Implementation Steps

#### Phase 1: Core Infrastructure (Day 1-2)
1. Create `Services/UnifiedCommandService.cs`
2. Create `Services/SharedStateService.cs` 
3. Create `Events/CrossModuleEvents.cs`
4. Update both command classes to use unified routing

#### Phase 2: Service Integration (Day 3-4)
1. Enhance `GlowService.cs` with arena-aware methods
2. Enhance `CycleBorn` lifecycle with zone integration
3. Add cross-module state synchronization
4. Implement unified configuration system

#### Phase 3: Command Expansion (Day 5-6)
1. Add arena-zone integration commands
2. Add zone-arena integration commands  
3. Add unified cross-module commands
4. Implement preset sharing between modules

#### Phase 4: Testing & Polish (Day 7)
1. Test cross-module interactions
2. Verify command routing works both ways
3. Test unified configuration loading
4. Performance optimization and cleanup

### 8. Benefits of Integration

#### Unified User Experience
- **Single Command Entry**: Players use one interface for all features
- **Consistent Syntax**: Same command patterns across modules
- **Seamless Interaction**: Arena and zone effects work together

#### Developer Benefits  
- **Code Reuse**: Shared services reduce duplication
- **Easier Maintenance**: Unified state management
- **Better Testing**: Integrated test scenarios

#### Player Benefits
- **Rich Interactions**: Arena fights with zone effects
- **Dynamic Content**: Presets work across modules
- **Performance**: Optimized cross-module communication

### 9. Backward Compatibility

#### Migration Strategy
- **Legacy Commands**: Keep existing commands working
- **Gradual Rollout**: Phase in new unified commands
- **Configuration Migration**: Auto-convert existing configs
- **Fallback Support**: Graceful degradation if integration fails

#### API Compatibility
- **Existing Hooks**: Maintain current event system integration
- **Service Interfaces**: Keep current service signatures
- **Plugin Architecture**: Ensure both modules load independently

This integration creates a **unified VAutomationCore architecture** where CycleBorn and Bluelock work together seamlessly while maintaining their individual strengths and providing rich cross-module interactions.
