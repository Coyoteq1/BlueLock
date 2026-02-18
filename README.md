# Bluelock Plugin

## Purpose
- Zone management, glow borders, kits, ability zone behavior, arena helpers.

## Primary Entry Points
- `Plugin.cs`: plugin bootstrap and runtime orchestration.
- `Commands/Core/*`: admin/user command surface.
- `Services/*`: domain behavior for zones, glow, templates, lifecycle handlers.

## Key Config
- `config/VAuto.Zones.json`
- `config/VAuto.ZoneLifecycle.json`
- `config/ability_zones.json`
- `config/ability_prefabs.json`

## Dependencies
- `VAutomationCore` (shared contracts/services).
- `CycleBorn/Vlifecycle` (temporary project-reference exception).

## Ownership Rule
- Keep Bluelock-specific logic in `Bluelock/*`.
- Move shared abstractions into `Core/*` before reuse by other plugins.
