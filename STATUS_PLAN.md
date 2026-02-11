# Domain Ascend (VAutoZone / BlueLock) - Status Plan

## Scope
- Zone detection and transitions
- Tile/template-driven event zones (10x10, 50x50)
- Event entry/exit integration with lifecycle, score, and announcements

## Current Readiness
- **Zone core + transitions:** 80% ready
- **Template/schematics integration path:** 75% ready
- **Event wiring to lifecycle/announce:** 75% ready
- **System access reliability:** 65% ready

## What Is Ready
- Zone enter/exit handling pipeline
- Zone message, kit, ability UI, and boss-spawner hooks
- In-zone lifecycle trigger bridge (`OnPlayerIsInZone`)
- Zone-based player messaging works in dedicated server runtime
- Default zone priority selection via command (`.z default [name]`) with list/status visibility
- Admin fast commands added: `.enter [zoneId optional]` and `.exit`
- `.enter` with empty argument now resolves to configured default zone (no JSON editing required)

## In Progress
- Full schematics reference flow for tile-based zone templates
- Event role swap logic for `Last Raid` (10v10)
- Cross-check of reflection calls after lifecycle assembly rename
- Prevent action conflicts when zone exit and lifecycle restore fire close together

## Next Implementation Plan
1. Finalize event registry mapping zone IDs -> event definitions.
2. Implement 10v10 phase swap (`7 min`) and winner resolution.
3. Complete score in-zone/out-zone bonus validation.
4. Add markdown specs for each live zone event.

## Risks
- Zone naming mismatch can break PVE/PVP routing.
- Runtime reflection dependency can fail silently without diagnostics.
- Zone/lifecycle data timing conflicts can produce inconsistent action execution.

## Latest Update (Current Sprint)
- Added default-zone-first behavior in runtime zone resolution.
- Added command-side default zone management (`.z default`, `[DEFAULT]` marker in `.z list`).
- Added force transition command flow:
  - `.enter` teleports to zone center and runs standard enter pipeline.
  - `.exit` runs standard exit pipeline and clears tracked zone state.
