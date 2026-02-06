# VAutomationEvents — ECS Design Directive

**Status:** Binding  
**Applies To:** All new and refactored code  
**Scope:** Server-side ECS, Harmony integration, automation logic

---

## 1. Core Architectural Principles

### 1.1 ECS Is the Source of Truth

* **All runtime state MUST live in ECS components**
* Static classes may exist **only** as:
  * Configuration readers
  * Harmony patch entry points
* No gameplay, zone, arena, or persistence state may be stored in:
  * Static dictionaries
  * Static lists
  * Singleton C# objects

> If a value changes over time, it belongs in a component.

### 1.2 Systems Are Pure Orchestrators

* Systems:
  * Read component data
  * Detect state transitions
  * Enqueue structural changes
* Systems **must not**:
  * Cache entity references across frames
  * Perform long-lived bookkeeping outside ECS
  * Contain game configuration logic

### 1.3 Data Is Dumb, Systems Are Smart

* Components:
  * No methods
  * No logic
  * No references to services
* All behavior emerges from:
  * System execution order
  * Component presence / absence
  * Component value changes

---

## 2. V Rising–Specific ECS Rules (Non-Negotiable)

### 2.1 Structural Changes

* **Never** call `EntityManager.Add/RemoveComponent` inside a query loop
* **Always** use an `EntityCommandBuffer` (ECB)
* ECB system choice must match intent:
  * Immediate gameplay effects → `BeginSimulationECB`
  * Cleanup / post-processing → `EndSimulationECB`

### 2.2 Event Components (Critical)

* `DamageEvent`, `TeleportRequest`, chat events, etc. are **event entities**
* You **do not** remove these components from the target entity
* Correct handling:
  * Modify the event
  * Destroy the event entity if cancelled

**Failure to follow this causes:**
* Combat desyncs
* Ghost teleports
* Stuck players

### 2.3 Queries and Native Collections

* Always use **system-defined queries** when available
* When enumerating entities:
  * Use `ToEntityArray(Allocator.Temp)`
  * Dispose in a `try/finally` block
* Never use `Allocator.TempJob` in ProjectM ECS

---

## 3. Zone & Arena Design Rules

### 3.1 Zones Are Data, Not Logic

* Every zone is an entity with `ZoneBoundary`
* Arena, PvP, Safe, Glow zones are **ZoneType variants**
* There is no separate "arena system" conceptually—arenas are zones

### 3.2 Zone Detection vs Zone Transition (Mandatory Split)

You **must** separate:

| Responsibility | System Type |
|---------------|-------------|
| Spatial detection | `ZoneDetectionSystem` |
| Entry/exit effects | `ZoneTransitionSystem` |

* Detection answers: *"Which zone am I in right now?"*
* Transition answers: *"Did my zone change since last frame?"*

**No system may apply effects while doing spatial math.**

### 3.3 Zone Membership Rules

* A player may have:
  * Multiple zone memberships **only if types differ**
* Membership is tracked via:
  * `DynamicBuffer<ZoneMembershipElement>`
* Effects are applied **only on transitions**, never per-frame

---

## 4. Combat & Protection Rules

### 4.1 Safe Zones

* Protection is expressed **only** via `SafeZoneProtection`
* Damage prevention:
  * Intercepts `DamageEvent`
  * Zeroes damage
  * Destroys the event entity

**No combat system may check zone position directly.**

### 4.2 Buffs & Automation

* Buffs applied by automation must be:
  * Explicit ECS components
  * Time-bounded via timestamps
* No "fire and forget" buff logic

---

## 5. Teleportation & Portals

### 5.1 Teleport Interception Contract

When cancelling a teleport:

* Remove **all** teleport-related components
* Optionally emit a failure event
* Never leave partial teleport state

Teleport validation systems must run:
* Before `TeleportSystem`
* After zone/arena state is finalized for the frame

---

## 6. Commands & Player Input

### 6.1 Commands Are Events

* Chat commands are intercepted as ECS events
* Command execution:
  * Produces component changes or new event entities
  * Does **not** directly call gameplay systems

### 6.2 Backward Compatibility

* Existing command syntax must remain valid
* Legacy command handlers may be wrapped, but not extended

---

## 7. Persistence & Shutdown

### 7.1 Persistence Is Observable, Not Driven

* Custom persistence systems:
  * Observe save lifecycle
  * Never initiate core saves
* Save state is tracked via ECS singletons only

### 7.2 Harmony Is a Boundary Layer

* Harmony patches:
  * May *signal* ECS (add components, flags)
  * Must not contain gameplay logic
* All decision-making happens in systems

---

## 8. Performance & Scale

### 8.1 Frame Cost Discipline

* No O(players × zones) logic in a single system
* No per-frame allocations in hot paths
* Logs must be:
  * Transition-based
  * Debug-gated where possible

### 8.2 Multiplayer Safety

* All logic must be deterministic server-side
* No reliance on client prediction state
* All authoritative decisions come from ECS state

---

## 9. Enforcement Checklist (PR Gate)

A change **must be rejected** if it:

- [ ] Introduces static gameplay state
- [ ] Modifies entities structurally without an ECB
- [ ] Handles ProjectM event components incorrectly
- [ ] Mixes spatial detection with effect application
- [ ] Duplicates zone/arena logic outside zone systems

---

## 10. Guiding Test Question

> **"If the server restarted right now, could ECS fully reconstruct the game state?"**

If the answer is **no**, the design violates this directive.

---

## Component Reference

### Zone Components

```csharp
public enum ZoneType : byte
{
    None = 0,
    GlowZone = 1,
    PvPArena = 2,
    SafeZone = 3,
    MainArena = 4,
    Portal = 5
}

public struct ZoneBoundary : IComponentData
{
    public int ZoneId;
    public ZoneType ZoneType;
    public float3 Center;
    public float Radius;
    public bool PvPEnabled;
    public bool SafeZone;
}

public struct ZonePlayerTag : IComponentData 
{ 
    public int ZoneId;
    public ZoneType ZoneType;
}

[InternalBufferCapacity(8)]
public struct ZoneMembershipElement : IBufferElementData
{
    public int ZoneId;
    public ZoneType ZoneType;
    public double EntryTime;
}

public struct ZoneDetectionResult : IComponentData
{
    public int DetectedZoneId;
    public ZoneType DetectedZoneType;
    public double DetectionTime;
}
```

### Combat Components

```csharp
public struct SafeZoneProtection : IComponentData
{
    public double ProtectionEndTime;
    public int DamagePrevented;
}
```

---

## Related Documents

* [ECS System Integration Plan](docs/ECS_SYSTEM_INTEGRATION_PLAN.md)
* [Code Review Report](docs/CODE_REVIEW_REPORT.md)
* [Refactoring Plan](docs/REFACTORING_PLAN.md)

---

*Document Version: 1.0*  
*Last Updated: 2026-01-30*
