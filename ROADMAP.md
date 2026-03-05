# VAutomation Roadmap

Purpose
- Provide a concise, actionable roadmap for all active projects in this repository, aligned with the Flow Stabilization Plan (plan.md).
- Organize work into waves/milestones with clear objectives, deliverables, and acceptance criteria.
- Keep this file high-level; execution details and day-to-day checklists live in plan.md and project-specific docs.

Guiding principles
- Stabilization-first: finish and ship P0/P1 flows behind feature flags before expanding scope.
- Observability-by-default: standardized logging, correlation IDs, and safe rollbacks across all projects.
- Small, verifiable steps: tests before risky changes; canary rollouts with clear rollback procedures.

Milestones (global, cross-project)
- Wave 0: Hardening & Observability Foundations
  - Standard log format + correlation IDs across patches/systems/services.
  - Feature flags (enable/disable + log level) wiring for each flow.
  - CI build/test green on main; minimal release cadence restored.

- Wave 1: P0 Flow Stabilization (highest impact)
  - Server Bootstrap/Startup: deterministic init, idempotency, strict-mode guardrails.
  - Zone Lifecycle: explicit state machine, debouncing, cleanup on exit.
  - Unit Spawning: deterministic prefab selection, validation, bounded retries.

- Wave 2: P1 Flow Stabilization
  - Glow Lifecycle: sync with zone states, ensure cleanup and no residual state.
  - Death Handling: deduplication, idempotency, single-source-of-truth.

- Wave 3: P2 Flow Stabilization
  - Buffs/Effects: mapping validation, non-stack rules, duration clamping.
  - Announcements/Chat: centralized formatting, throttling, disable-on-flag.

- Wave 4: Migrations, Integrations, and Tooling
  - Flow Migrations: forward-only, idempotent, checkpointed.
  - Integrations: Swapkits, CycleBorn/Vlifecycle synchronization.
  - Tooling: Log Viewer App polish (backend/frontend), docs refresh.

Cross-cutting deliverables (apply to all milestones)
- Feature-flagged rollouts with safe defaults and canary plans.
- START/END + branch decision logs with correlation context.
- Minimal snapshot/replay artifacts for regression tests where feasible.
- Docs: flows-compare, operator notes, command cheatsheet as appropriate.

Acceptance criteria (global)
- Unit + integration tests for golden and failure paths in each flow.
- Logs are actionable and consistent (flow, stage, id, ctx fields present).
- Canary validation completed; rollback documented.
- No known double-processing, leaks, or flapping in stabilized flows.


Project roadmaps

1) VAutomationCore (root; Core framework and patches)
- Scope
  - Core patches and systems; unified configuration services; shared logging; flow stabilization (see plan.md).
- Current state
  - Server Bootstrap: correlated logging + feature flags implemented; events wired (ServerStarted, WorldReady, WorldInitialized).
  - Zone Lifecycle: unified config model present; PlayerZoneState extended; mapping helpers and validation in place.
  - Unit Spawning: assessed; disposal issue noted; tests pending.
- Wave targets
  - Wave 1: Finish P0 stabilization (bootstrap, zone lifecycle, spawner) behind flags.
  - Wave 2: Finish glow + death handling guardrails and tests.
  - Wave 3: Buffs/Effects + Announcements tests and observability.
- Key deliverables
  - Completed P0–P2 checklists in plan.md; updated docs (Core-System-Usage.md, flows-compare-report.md).
  - Feature flags across flows with safe defaults.
  - Green CI for build and tests across the solution.
- Risks/mitigations
  - ECS timing/race conditions → debounce + explicit state machine; correlation logs for analysis.
  - Legacy/duplicated code paths → deprecate under flags with tests and docs.

2) Bluelock (zone/glow services and models)
- Scope
  - ZoneDetectionService, SchematicZone/Glow services, SchematicData models supporting zone/glow features.
- Wave targets
  - Wave 1–2: Align ZoneDetection with UnifiedZoneLifecycleConfig; ensure mapping correctness and state debouncing.
  - Wave 2: Glow lifecycle alignment with zone states; cleanup correctness; add tests.
- Deliverables
  - Unit + integration tests: zone enter/exit churn, glow activation/cleanup.
  - Config alignment with unified schema; observability and feature flags.
- Dependencies
  - VAutomationCore unified config and PlayerZoneState life-cycle semantics.

3) CycleBorn/Vlifecycle (stage registry and action execution)
- Scope
  - Receives stage names (onEnter/isInZone/onExit), executes configured actions; owns idempotency of action execution.
- Wave targets
  - Wave 1: Ensure idempotent stage execution and clear state transitions.
  - Wave 3–4: Config bundles, must-flow support, replay-friendly action inputs.
- Deliverables
  - Stage execution logs with correlation; tests covering re-entrance and retries.
- Dependencies
  - Inputs from VAutomationCore (zone mapping, flow IDs, config bundles).

4) Swapkits (integration set)
- Scope
  - Optional integrations; ensure they are feature-flagged and failure-isolated.
- Wave targets
  - Wave 4: Audit integration points; add flags and guardrails; basic end-to-end tests.

5) VAutoannounce (announcements/chat)
- Scope
  - Chat/announcement features and commands.
- Wave targets
  - Wave 3: Centralized formatting, throttling, localization hooks; tests for rate limits and disabled state.
- Deliverables
  - Updated docs/command-cheatsheet.md; feature flag announce.safeMode.

6) VAutoTraps (traps and configuration)
- Scope
  - Traps logic and converters; ensure deterministic and safe behavior.
- Wave targets
  - Wave 3: Tests for trap activation rules and conversions; guardrails for misconfigurations.

7) VAuto.Extensions (.NET helper library)
- Scope
  - Shared extension methods (collections, strings, datetime, exceptions).
- Wave targets
  - Wave 0: API stability and documentation; ensure no breaking changes for dependent projects.

8) Vexil (plugin/library)
- Scope
  - Core services and shared infrastructure used by various mods/plugins.
- Wave targets
  - Wave 0–1: Stabilize public surfaces; add defensive checks; document usage patterns.

9) Log Viewer App (frontend/backend)
- Scope
  - Developer/operator tooling to visualize correlated logs and flow health.
- Wave targets
  - Wave 4: Baseline UX, query filters for flow/stage/id; packaging and quickstart scripts.
- Deliverables
  - Indexed correlated logs; dashboards for START/END/ERROR counts by flow.

10) Packaging & Release Engineering
- Scope
  - Build, sign, and ship artifacts; scripts in tools/ and packaging/.
- Wave targets
  - Wave 0: Reproducible builds; signed artifacts; automated deploy scripts.
  - Wave 4: Channel-based releases (canary/beta/stable) with rollbacks.


Milestone backlog and sequencing
- Wave 0 (Foundations)
  - [ ] Standardize log format and correlation in all projects.
  - [ ] Wire feature flags per flow across projects where applicable.
  - [ ] CI build/test green; signing pipeline validated.

- Wave 1 (P0 Flows)
  - [ ] Server Bootstrap hardening complete; strict mode default ON.
  - [ ] Zone Lifecycle state machine + debouncing implemented and tested.
  - [ ] Unit Spawning deterministic selection + validation + bounded retries.

- Wave 2 (P1 Flows)
  - [ ] Glow Lifecycle synchronized with zone states; cleanup proved via tests.
  - [ ] Death Handling idempotent; duplicate suppression tested.

- Wave 3 (P2 Flows)
  - [ ] Buffs/Effects stacking and duration rules tested; mapping validations.
  - [ ] Announcements throttling and formatting centralized; feature flag default safe.

- Wave 4 (Migrations/Integrations/Tooling)
  - [ ] Flow Migrations forward-only with checkpoints; re-run/partial recovery tests.
  - [ ] Swapkits and Vlifecycle integration hardening; flags and isolation.
  - [ ] Log Viewer packaged; docs and quickstart.


Metrics & reporting
- Build health: CI status green; test pass count; coverage trend for critical flows.
- Flow health: START/END/ERROR counts by flow; duplicate/rollback incidence.
- Rollout health: canary duration, incident counts, time-to-rollback.

Risks & mitigations
- Race conditions in ECS lifecycles → explicit state machines, debouncing, idempotency.
- Config drift across projects → unified schemas, validation, and canonical config services.
- Integration fragility → feature flags per integration and clear isolation boundaries.

Operating cadence
- Weekly sync: review plan.md status tracker, update ROADMAP.md deltas.
- Fortnightly release window: ship flagged improvements post-canary.
- Document changes: update docs and ADRs as architectural decisions evolve.

References
- plan.md (Flow Stabilization Plan)
- docs/Core-System-Usage.md
- docs/flows-compare-report.md
- docs/GLOW-SYSTEM.md
- docs/command-cheatsheet.md
- tools/*.ps1 (deployment and packaging)
- packaging/*
