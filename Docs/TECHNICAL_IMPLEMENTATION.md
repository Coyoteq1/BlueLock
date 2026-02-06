# VAutomationEvents Split-Mods Technical Plan

This document covers the standalone plugin set in this workspace:

- `VAutoArena`
- `VAutoTraps`
- `VAutoannounce`
- `Vlifecycle`

It focuses on configuration correctness, command surfaces, and operator experience for a V Rising dedicated server (BepInEx IL2CPP).

## 1) Roadmap (Phase-By-Phase, Weekly Estimates)

All durations are estimates in weeks. Deliverables are tangible artifacts (code, config files, docs, validation scripts).

### Phase 0: Baseline Hardening (1 week)

- Roles
- Lead Engineer: config formats, loaders, command surfaces
- QA/Operator: server verification, config override coverage, regression checklist
- Deliverables
- `scripts/Format-JsonConfigs.ps1`
- `Schemas/*.schema.json` for owned configs
- Per-mod command hub entrypoints
- Dependency checklist
- .NET SDK `6.0.428` installed
- BepInEx IL2CPP server install available
- NuGet offline constraints documented (build with `--no-restore`)

### Phase 1: TOML-First Config Migration (2 weeks)

- Roles
- Engineer A: TOML parsers and migration writers
- Engineer B: config loader integration, validation, error messaging
- Deliverables
- `VAutoArena/config/VAuto.Arena/*.toml`
- `Vlifecycle/Configuration/pvp_item.toml`
- TOML-first loaders with JSON fallback and one-time migration
- Dependency checklist
- Confirm TOML defaults ship with DLL output (`CopyToOutputDirectory`)
- Confirm JSON fallback remains supported for existing servers

### Phase 2: End-to-End Arena Entry/Exit + Glow Border (2 weeks)

- Roles
- Engineer A: arena tracking service + commands integration
- Engineer B: glow border spawn correctness + prefab resolution
- Designer/Operator: command UX, safe defaults, permissions, help text
- Deliverables
- `.arena` command suite with debug/test
- Verified glow border spawn based on territory grid + spacing
- Recovery commands: glow clear, territory reload, status checks
- Dependency checklist
- Prefab GUID source list maintained in config
- Server log review checklist for spawn/despawn entity counts

### Phase 3: Lifecycle Orchestration & Extensions (3 weeks)

- Roles
- Engineer A: lifecycle manager robustness and service APIs
- Engineer B: snapshot service + building service hooks
- QA: UAT scripts for enter/exit, rollback, error handling
- Deliverables
- `.lifecycle` manual commands and status
- Service registration/injection rules
- Failure containment (service isolation) and cleanup path
- Dependency checklist
- Verify lifecycle services remain deterministic server-side
- Verify no unhandled exceptions escape lifecycle entrypoints

### Phase 4: Release Engineering (1 week)

- Roles
- Engineer: packaging, deployment script, documentation polish
- Operator: versioning, update workflow
- Deliverables
- Operator runbook for deploy/rollback
- Config migration notes and compatibility matrix updates
- Dependency checklist
- Confirm deploy path and backups behavior
- Confirm split mods do not double-register commands with monolith

## 2) Last Changes (ISO-8601, Severity, File Paths)

Note: GitHub PR/Issue IDs are not available in this local workspace. Use `N/A` until linked.

### 2026-02-06 (critical)

- N/A: Added unified arena command hub and tracking logic.
- Affected: `VAutoArena/Commands/Arena/ArenaCommands.cs`
- Affected: `VAutoArena/Services/ArenaPlayerService.cs`

### 2026-02-06 (minor)

- N/A: Added glow prefab config support and TOML defaults for arena configs.
- Affected: `VAutoArena/Services/ArenaGlowBorderService.cs`
- Affected: `VAutoArena/Services/ArenaTerritory.cs`
- Affected: `VAutoArena/Services/SimpleToml.cs`
- Affected: `VAutoArena/config/VAuto.Arena/arena_territory.toml`
- Affected: `VAutoArena/config/VAuto.Arena/arena_glow_prefabs.toml`
- Affected: `VAutoArena/VAutoArena.csproj`

### 2026-02-06 (critical)

- N/A: Tightened PvP lifecycle config JSON parsing to strict RFC 8259 (no trailing commas/comments) and added TOML support.
- Affected: `Vlifecycle/Core/Configuration/PVPLifecycleConfigLoader.cs`
- Affected: `Vlifecycle/Core/Configuration/SimpleToml.cs`
- Affected: `Vlifecycle/Configuration/pvp_item.toml`
- Affected: `Vlifecycle/Vlifecycle.csproj`

### 2026-02-06 (minor)

- N/A: Consolidated trap commands under a single `.trap` group with status/debug/test plus sub-areas.
- Affected: `VAutoTraps/Commands/Core/TrapCommands.cs`

### 2026-02-06 (minor)

- N/A: Standardized announcement commands under `.announce` and added test/debug helpers.
- Affected: `VAutoannounce/Commands/Core/AnnounceCommands.cs`

### 2026-02-06 (minor)

- N/A: Added JSON schemas and formatter to reduce config drift and serialization issues.
- Affected: `Schemas/VAutoArena.arena_territory.schema.json`
- Affected: `Schemas/VAutoArena.arena_glow_prefabs.schema.json`
- Affected: `Schemas/Vlifecycle.pvp_item.schema.json`
- Affected: `scripts/Format-JsonConfigs.ps1`

## 3) Enhancements (Implementation Details, Benefits, Testing, Future Notes)

### Enhancement: TOML-First Config Loading With Safe Fallback

- Implementation details
- VAutoArena: `ArenaTerritory` and glow prefab config now prefer `.toml`, fall back to `.json`, and write `.toml` once when loading JSON.
- Vlifecycle: `PVPLifecycleConfigLoader` prefers `.toml` and can migrate from `.json`.
- VAutoTraps: `TrapSpawnRules` prefers `.toml` and can migrate from `.json`.
- Leveraged APIs/libraries
- `System.IO` for config discovery and one-time migration writes
- Minimal internal TOML parsers (`SimpleToml`) scoped to required features
- Measurable benefits
- Reduced config ambiguity and stricter parsing by default (goal: eliminate trailing comma/comment drift)
- Operator consistency (single default format across mods)
- Testing scope
- Unit: parser tests for supported TOML subset (recommended next addition)
- Integration: load TOML then JSON fallback on a live server
- User acceptance: edit config, hot reload (if enabled later), confirm behavior matches prior JSON
- Future notes
- Replace `SimpleToml` with a full TOML 1.0 parser when offline NuGet constraints are resolved
- Add explicit schema-driven validation before accepting config at runtime

### Enhancement: Standardized TOML Sections

- Implementation details
- Added `[metadata]`, `[core]`, `[dependencies]`, `[optionalFeatures]` sections to shipped TOML configs.
- Loaders accept both root-level keys and `[core]` for backward compatibility.
- Measurable benefits
- Clearer operator ownership and consistent config structure across mods.
- Testing scope
- Integration: load TOML with and without sections, verify defaults and overrides.
- User acceptance: open TOML in editor and verify discoverability.
- Future notes
- Add automated TOML schema checks when CI is available.

### Enhancement: Unified Command Surfaces With Debug/Test Helpers

- Implementation details
- `.arena` command hub provides status/debug/test plus glow and territory operations.
- `.trap` command group consolidates container/zone/chest/streak commands under one namespace.
- `.announce` provides admin and test commands to exercise output paths.
- `.lifecycle` provides manual enter/exit plus status.
- Measurable benefits
- Faster operator diagnosis (single entry point per mod)
- Reduced duplicate command collisions and accidental shorthand overlaps
- Testing scope
- Integration: in-game command invocation for each area (arena enter/exit, glow spawn/clear, trap set/list, announce test, lifecycle enter/exit)
- User acceptance: help output readability and admin gating correctness
- Future notes
- Add `--dry-run` style flags (where applicable) and structured replies for automation

### Enhancement: Strict JSON Formatting + RFC 8259 Compliance Gate

- Implementation details
- `scripts/Format-JsonConfigs.ps1` parses and re-emits source configs with consistent indentation.
- PvP lifecycle JSON parsing is now strict (no comments/trailing commas) to align with RFC 8259.
- Measurable benefits
- Lower config parsing variance between environments
- Less “works on my server” due to permissive parsing
- Testing scope
- Unit: script should reject invalid JSON
- Integration: load configs on server post-formatting
- Future notes
- Add JSON Schema validation as a CI step (or pre-build script) once CI exists
