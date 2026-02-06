# VAutomationEvents Project Analysis & Reorganization Plan

**Date:** 2026-02-05  
**Version:** 1.0  
**Author:** Architect Mode Analysis

--- 

## 1. Executive Summary

- Source tree is fragmented: duplication between `Core/`, `Services/`, and `excluded_files/`, with ~70 orphaned service/system files.
- Build uses explicit `<Compile>` items; only ~45–50 files are active while dozens remain commented out or unused.
- Documentation and planning artifacts are spread across multiple directories and contain stale content.
- Proposal: reorganize into `src/` + feature submodules, collapse service directories, clean doc space, and update `.csproj`.

**Priority Actions**
1. Remove/ archive `excluded_files/`.
2. Consolidate services under a single path (`src/Core/Services`).
3. Update `.csproj` to reflect new layout.
4. Clean docs to critical references.
5. Preserve feature toggles only where currently used.

## 2. Key Decisions & Context

- Decision: keep the current feature-toggle knobs but clearly document their intent and ensure only active paths remain in the `.csproj`.
- Decision: move non-feature-core services under `src/Core/Services` and relocate feature-specific services into `src/Features/[Feature]/` so ownership lines up with functionality.
- Context: multiple language-model summaries already outlined the status of commands, lifecycle services, and VBlood repair flows; this document builds on that by mapping out a concrete reorganization timeline without duplicating the earlier diagnostics.
- Context: we are accountable for telling the dev team what is already cleaned up (analysis done) and what still needs executing, so the sections that follow include both a high-level strategy and explicit next steps.

## 3. Current Build Analysis

- **Compile Flag Overview**: `IncludeLegacyCommands`, `IncludeEndGameKit`, `IncludeKillStreakEcs`, `IncludeEcsDiagnostics` are all `false` by default; only new ECS/lifecycle files compiled when toggled.
- **Actively Compiled**: `Plugin.cs`, `VRCore`, logging, API, select Core services (data/persistence/rules/traps/announcement), lifecycle helpers, Harmony patches, commands in `Commands\Arena` and `Commands\Core`.
- **Deprecated Files**: Many services (communication, map, portal, etc.) are not referenced in `.csproj`.

## 4. Key Structural Issues

| Problem | Impact |
|-|-|
| `Core/Services` vs `Services/` duplication | Confusion on service placement; hard to track which is authoritative. |
| `excluded_files/` folder | Contains outdated copies & docs; bloats repo. |
| Legacy commands & systems commented but still present | Clutters `.csproj` and slows review. |
| Documentation duplication | Multiple docs describe same lifecycle info. |

## 5. Reorganization Strategy

### 4.1 Folder Layout

```
VAutomationEvents/
├── config/
├── src/
│   ├── Core/
│   │   ├── API/
│   │   ├── Harmony/
│   │   ├── Logging/
│   │   ├── Lifecycle/
│   │   └── Services/
│   ├── Features/
│   │   ├── Arena/
│   │   ├── Zones/
│   │   ├── Glow/
│   │   ├── Lifecycle/
│   │   └── PvP/
│   ├── Commands/
│   └── Data/
├── docs/
├── EndGameKit/ (conditional)
├── scripts/
└── README.md
```

### 4.2 .csproj Updates

- Point `Compile Include` entries at `src/...`.
- Remove commented-out paths (legacy commands, ECS diag, etc.).
- Keep feature toggles only if referenced; document meaning in comments.

### 4.3 Services Cleanup

- Move stable services (persistence, traps, announcement) to `src/Core/Services`.
- Feature services (lifecycle, zone, glow) live under `src/Features/[Feature]/`.
- Archive or delete orphaned `Services/**` files not referenced elsewhere.

### 4.4 Commands Cleanup

- Consolidate commands under `src/Commands/`.
- Keep only actively compiled command files (`GlowCommands`, `ZoneCommands`, `KillStreakCommands`, etc.).
- Remove duplicates and truly legacy files (old VCF commands).

### 4.5 Documentation Rationalization

- Retain: `README.md`, `CONFIGURATION_REFERENCE.md`, `ECS_SYSTEM_INTEGRATION_PLAN.md`, `CROSS_MOD_API.md`, `MOD_INTEGRATION_GUIDE.md`, `COMPILE_REQUIREMENTS.md`, `MOD_COMPATIBILITY_MATRIX.md`.
- Merge/rewrite: `BOOKNOTE_VAutoLifecycle.md`, `LifecycleSpellbookSystem.md`, `PVPLifecycleEventSystem.md` into `LIFECYCLE_AUTOMATION_INTEGRATION_PLAN.md`.
- Delete outdated docs (`PREFAB_SEARCH_WORKFLOW.md`, `COMMANDS_LOGGING_CONFIG_PLAN.md`).

## 6. Execution Plan

1. **Preparation**
   - Create `src/` structure and update `.csproj` references.
   - Move configs into `config/` with subfolders (`VAuto.Arena/`).
2. **Move & Consolidate**
   - Relocate `Core`/`Services` sources into `src/Core` plus feature subfolders.
   - Move commands into `src/Commands`.
3. **Cleanup**
   - Remove `excluded_files/` or archive separately.
   - Delete orphaned `Services/` files not referenced post-move.
4. **Doc Refresh**
   - Delete outdated docs and update existing references to reflect the new structure.
5. **Validation**
   - Run `dotnet build`.
   - Verify each command path still compiles.
   - Confirm config assets still copy via `CopyToBepInEx`.

## 7. Recommendations

- Keep `EndGameKit/` and kill-streak ECS modules conditional; document toggles.
- Replace `ServiceContainer` references with `ServiceManager` over time (warnings noted in build).
- Maintain a single source of truth for lifecycle logic (prefer `LifecycleService`/`PVPItemLifecycle`).
- Ensure automation tests target the reorganized structure (update doc references accordingly).

---

## 8. Status & Next Steps

| Item | Status | Next Step |
| --- | --- | --- |
| Baseline analysis of compiled vs orphaned files | ✅ Completed (this document and helper summaries provide the inventory) | None |
| New `src/` structure and feature modules | ✅ Completed | Moved `Core/`, `Commands/`, and `Data/` into `src/` (feature modules scaffolding created) |
| `.csproj` alignment with new layout | ✅ Completed | Updated `<Compile>` entries to `src/...` paths and removed dead `Services/` includes |
| Documentation cleanup/merging | 🟡 In progress | Deleted outdated docs; merge/refresh remaining lifecycle docs next |
| Service and command cleanup | ✅ Completed | Deleted root `Services/` folder after verifying compiled sources now live under `src/` |

Remaining work remains focused on the physical reorganization (file moves, `.csproj` edits, doc cleanup). Once those steps are complete, run `dotnet build` and smoke-test each command path to ensure no regressions. This doc should be updated with status marks after each phase.

**Next Review:** After initial reorganization commits.  
