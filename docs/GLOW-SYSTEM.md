# Glow System Documentation

## Overview

The Glow System provides dynamic visual effects for zones in VAutoZone. It creates border glow effects using buff entities that can be customized per zone with various shapes, colors, and rotation patterns.

## Key Features

- **Shape Support**: Circle, Rectangle, and Polygon shapes
- **Auto-Rotation**: Configurable rotation of glow effects
- **Presets**: Pre-defined glow configurations
- **Customization**: Per-zone glow customization
- **Commands**: Comprehensive command system for management
- **Auto-Seed**: Automatic configuration generation

## Configuration Files

### Glow Types (`config/glow/glow_types.json`)

Defines available glow types with their properties:

```json
{
  "inkshadow": {
    "name": "InkShadow",
    "prefabGuid": -1124645803,
    "description": "Dark ink shadow glow effect",
    "color": { "r": 0.1, "g": 0.1, "b": 0.2, "a": 1.0 }
  }
}
```

### Glow Presets (`config/glow/glow_presets.json`)

Pre-defined configurations for common use cases:

```json
{
  "arena": {
    "name": "Arena",
    "description": "High-intensity arena glow preset",
    "spacing": 3.0,
    "heightOffset": 0.5,
    "glowTypes": ["chaos", "emerald", "agony"],
    "autoRotate": true,
    "rotationIntervalMinutes": 3
  }
}
```

### Auto-Rotation (`config/glow/glow_auto_rotation.json`)

Global and per-zone rotation settings:

```json
{
  "globalRotationEnabled": true,
  "globalRotationIntervalMinutes": 5,
  "zoneOverrides": {
    "arena": {
      "enabled": true,
      "intervalMinutes": 3,
      "glowTypes": ["chaos", "emerald", "agony"]
    }
  }
}
```

## Commands

### Zone Glow Commands

- `.zone glow spawn [zoneId]` - Spawn glow effects in current zone
- `.zone glow clear [zoneId]` - Clear glow effects in current zone
- `.zone glow list` - List available glow types and presets

### Glow Management Commands

- `.glow type add <name> <prefabGuid> <description> <r> <g> <b> <a>` - Add new glow type
- `.glow type remove <name>` - Remove glow type
- `.glow preset add <name> <description> <spacing> <heightOffset> <glowTypes> <autoRotate> <rotationInterval>` - Add new preset
- `.glow preset remove <name>` - Remove preset

### Rotation Commands

- `.glow rotate` - Force manual rotation
- `.glow status` - Show glow system status

### Debug Commands

- `.glow debug info` - Show detailed system information
- `.glow entities` - List glow entities in current zone
- `.glow test` - Test glow spawning
- `.glow clear all` - Clear all glow effects
- `.glow performance` - Show performance metrics

## Integration

### Zone Lifecycle Integration

The glow system integrates with zone lifecycle events:

- **On Enter**: `glow_spawn` action spawns glow effects
- **On Exit**: `glow_reset` action clears glow effects

### Auto-Rotation Integration

Auto-rotation is handled by a timer service that rotates glow effects at configured intervals.

## Customization

### Per-Zone Customization

Zones can be customized with:

- **Custom Glow Types**: Use specific glow effects
- **Custom Spacing**: Adjust distance between glow entities
- **Custom Height**: Set vertical offset
- **Rotation Settings**: Enable/disable auto-rotation

### Preset Usage

Presets provide quick configuration for common scenarios:

- **Arena**: High-intensity combat zones
- **Peaceful**: Calm, low-intensity areas
- **Danger**: Warning and hazard zones
- **Mystic**: Magical and mysterious areas

## Performance Considerations

- **Entity Count**: Glow effects create multiple entities per zone
- **Rotation Overhead**: Auto-rotation requires periodic updates
- **Buff Management**: Glow effects use buff entities for visual effects
- **Memory Usage**: Presets and configurations are cached in memory

## Troubleshooting

### Common Issues

- **Glow Not Appearing**: Check if glow system is enabled in config
- **Rotation Not Working**: Verify auto-rotation is enabled and timer is running
- **Performance Issues**: Reduce entity count or disable auto-rotation
- **Invalid Glow Types**: Ensure prefab GUIDs are valid and spawnable

### Debug Commands

Use debug commands to diagnose issues:

- `.glow debug info` - System status
- `.glow entities` - Current entities
- `.glow performance` - Performance metrics

## Configuration Management

### Auto-Seed Service

The system includes an auto-seed service that:

- Creates default configurations if missing
- Validates existing configurations
- Provides backup and restore functionality
- Ensures configuration integrity

### Hot Reload

Configurations can be reloaded without restarting:

- `.glow config reload` - Reload all configurations
- Automatic detection of config file changes

## API Usage

### Services

- **GlowTypeManager**: Manage glow types and presets
- **GlowAutoRotationService**: Control auto-rotation
- **ZoneGlowBorderService**: Handle glow spawning and clearing

### Configuration Access

- **GlowTypeDefinitions**: Access predefined glow types
- **GlowPresets**: Access predefined presets
- **ZoneRotationOverrides**: Access zone-specific rotation settings

## Best Practices

1. **Use Presets**: Start with presets for common scenarios
2. **Test Configurations**: Use test commands before deployment
3. **Monitor Performance**: Watch entity counts in large zones
4. **Backup Configurations**: Keep backups of custom configurations
5. **Use Auto-Seed**: Let the system create default configurations
6. **Validate Changes**: Use schema validation for custom configurations