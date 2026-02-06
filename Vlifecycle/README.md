# Vlifecycle

Lifecycle services, arena enter/exit orchestration, and auto kit integration.

## Commands
- `.lifecycle status`
- `.lifecycle enter [arenaId]`
- `.lifecycle exit [arenaId]`
- `.lifecycle debug`

## Kit Integration
- Auto-apply kit on zone enter via `EndGameKit.json` profiles.
- Auto-restore on exit if `restoreOnExit` is true (tracks applied kits).
- Zone name uses `arenaId` from `.lifecycle enter/exit`.

## Config
- `Configuration/pvp_item.toml`
- `BepInEx/config/EndGameKit.json` (created on first run)

## Prefab Conversion
Use `VAuto.Core.PrefabGuidConverter` to convert prefab names to GUIDs and back.
