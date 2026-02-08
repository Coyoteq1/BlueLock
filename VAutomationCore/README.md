# VAutomationCore

Core library for Vauto mods providing shared utilities, ECS helpers, and configuration management.

## Overview

VAutomationCore is the foundational library that all Vauto plugins depend on. It provides common functionality used across all mods including:

- Shared configuration management
- Entity Component System (ECS) helpers
- Prefab GUID conversion utilities
- Chat service integration
- Logging infrastructure
- JSON serialization support

## Dependencies

- BepInEx 5.4.2105+
- BepInExPack IL2CPP 6.0.0+
- Vampire Command Framework 0.10.4+

## Installation

Place `VAutomationCore.dll` in your `BepInEx/plugins/` folder. All other Vauto mods require this core library.

## Features

### Configuration Management

```csharp
// Load JSON configuration
var config = VAutoConfigService.Load<MyConfig>();

// Save configuration
VAutoConfigService.Save(config);
```

### ECS Helper

```csharp
// Query entities efficiently
var query = ECSHelper.Query<MyComponent>();

// Get singleton component
var singleton = ECSHelper.GetSingleton<MyComponent>();
```

### Prefab GUID Conversion

```csharp
// Convert prefab name to GUID
var guid = PrefabGuidConverter.NameToGuid("PrefabName");

// Convert GUID to prefab name
var name = PrefabGuidConverter.GuidToName(guid);
```

### Chat Service

```csharp
// Send message to player
ChatService.Send(player, "Hello!");

// Broadcast to all players
ChatService.Broadcast("Server announcement!");
```

### Logger

```csharp
VAutoLogger.LogInfo("Information message");
VAutoLogger.LogError("Error message");
VAutoLogger.LogDebug("Debug message");
```

## Project Structure

```
VAutomationCore/
├── Chat/                 # Chat service integration
├── Configuration/        # Configuration management
├── Extensions/          # Extension methods
├── Helpers/             # Utility helpers
├── Json/               # JSON serialization
└── libs/              # External libraries
```

## Compatibility

- VRising version: 1.0+
- .NET Standard 2.1

## Version

- Version: 1.0.0
- Website: https://github.com/Coyoteq1/VAutomationCore
