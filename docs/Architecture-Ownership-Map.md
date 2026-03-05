# Zone Transition Ownership Map

- Runtime engine owner: `VAutomationCore`
- Owns: shared contracts, ECS utilities/components used across modules, lifecycle/runtime option contracts, API primitives.
- World-state owner: `Bluelock`
- Owns: zones, castles/building restrictions, kits, abilities, glow/tile world-state behavior, zone runtime execution and plugin bootstrap/patch wiring.
- Policy/content owner: `CycleBorn`
- Owns: lifecycle content, flow registry definitions, snapshot orchestration policy, and headless operator command surface (`.lifecycle`).
- Combat map-icon owner: `Vexil`
- Owns: PvP combat tracking and global combat map-icon lifecycle (spawn/refresh/expire), with no ownership of zone/building domain data.

## Minimal mode guardrails
- `Bluelock/Plugin.cs` runtime lifecycle JSON models (`ZoneJsonConfig`, `ZoneMapping`, `ZoneMustFlow`) are authoritative in minimal mode.
- Legacy duplicate lifecycle model files in `Bluelock/Models` are removed when unused.
- Command family ownership:
- `Bluelock`: `.zone .match .spawn .template .tag .enter .exit .unlockprefab`
- `VAutomationCore`: `.coreauth .jobs`
- `CycleBorn`: `.lifecycle`
- `Bluelock/Commands/Core/ArenaEcsCommands.cs` remains excluded by csproj and is not an active command surface.

## Runtime modes
- Effective runtime mode: `EcsOnly` (forced).
- Compatibility API: `Runtime.ZoneRuntimeMode` and `ZoneRuntimeModeOptions.FromMode(...)` remain available for developers in Core.
- Boot mode lock: preserved for startup diagnostics and compatibility logging.
- ECS detection tick: `Runtime.EcsDetectionTickSeconds`
- ECS ops warning threshold: `Runtime.ZoneDetectionOpsWarningThreshold`

## Zone transition event flow
1. `ZoneDetectionSystem` emits `ZoneTransitionEvent`.
2. `ZoneTransitionRouterSystem` is the single event owner.
3. Router dispatches to:
   - `FlowExecutionSystem.ApplyTransition(...)`
   - `ZoneTemplateLifecycleSystem.ApplyTransition(...)`
4. Router destroys transition event entity.

## Config governance ownership
- BlueLock canonical domain: `Bluelock/config/bluelock.domain.json`
- CycleBorn flow registry: `CycleBorn/Configuration/flows.registry.json`
- CycleBorn lifecycle policy: `CycleBorn/Configuration/lifecycle.policy.json`
- Vexil combat icons: `Vexil/Configuration/combat.mapicons.json`

## Validation entrypoint
- `Bluelock/Services/ProcessConfigService.ValidateAllConfigs(...)`
