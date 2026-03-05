V Rising Modding Rules and Best Practices

This document establishes comprehensive guidelines for developing mods for V Rising using the VAutomationCore framework. These rules ensure mod compatibility, stability, and performance in a multiplayer server-authoritative environment.

Summary of Key Do’s and Don’ts



- Always use predefined EntityQueries; never roll your own if one exists.

- Never use Allocator.TempJob or using(...) with NativeArray; always dispose with try/finally.

- All gameplay logic must be server-authoritative—never trust client state for game rules or stats.

- Never use GetAllEntities() in gameplay loops; always filter queries.

- Cache GUID hashes and config lookups; avoid per-frame allocations or reflection in hot paths.

- Only one ECS system should consume each event stream unless registry-dispatched.

- Unique Module IDs are mandatory—no cross-mod duplication.

- CI must hard-fail on critical ECS and deployment violations (TempJob, using, event consumer conflicts, etc.).

- Always validate mods on a clean dedicated server with real multiplayer scenarios.

- Never treat legacy config files as runtime source of truth.



For rationale and code examples, see each section below.Table of Contents

1. Architecture Awareness Rules
2. Component Safety Rules
3. Multiplayer and Authority Rules
4. Performance Rules
5. Harmony Patch Rules
6. Buff and Ability Rules
7. AI Behavior Rules
8. Logging Rules
9. Scalability Rules
10. Stability Rules
11. Multi-Mod Coexistence Rules
12. Testing and Validation Rules
13. Configuration Contract Rules
14. Command Surface Rules
15. Enforcement Rules
16. Gold Standard AI Prompt

---

1. Architecture Awareness Rules

Rule 1: Always Assume Hybrid Architecture

Treat the game as server-authoritative ECS (DOTS) with client-side GameObject/UI layers.

* Never assume pure MonoBehaviour logic
* When modifying gameplay, prioritize server systems
* When modifying visuals/UI, prioritize client systems

V Rising uses a hybrid approach combining Unity’s DOTS/ECS with traditional GameObject paradigms. Understanding which system handles which aspect is crucial for effective modding.

Rule 2: Never Create Custom EntityQueries If One Exists

Always use predefined queries defined in system files:

// Access via the system's query field
_Query
_BuffsQuery
__query_xxxxx

These are defined inside systems and documented in ServerSystemQueries.txt / ClientSystemQueries.txt.

Why this matters:

* Preserves chunk iteration performance
* Maintains system ordering compatibility
* Ensures multiplayer determinism

Custom EntityQueries must never overlap an existing system query’s component filter set unless intentional and documented. Only create custom queries when absolutely required by specific mod functionality.

Rule 3: Use Temp Allocator, Never TempJob

When retrieving entities from queries:

NativeArray<Entity> entities = default;
try
{
    entities = _Query.ToEntityArray(Allocator.Temp);
    // Work with entities here
}
finally
{
    if (entities.IsCreated)
        entities.Dispose();
}

Critical constraints:

Column 1	Column 2
Allowed	Not Allowed
Allocator.Temp	Allocator.TempJob
try/finally blocks	using statements


V Rising uses a custom ECS fork, and improper disposal can cause server crashes.

---

2. Component Safety Rules

Rule 4: Always Validate Component Presence

Before reading a component, always verify its existence:

if (!entityManager.HasComponent<Buff>(entity))
    continue;

When the component references another entity:

// Validate target exists
if (!entityManager.Exists(buff.Target))
    continue;

// Validate target has required components
if (!entityManager.HasComponent<Health>(buff.Target))
    continue;

Never assume:

* Target exists
* Owner exists
* Buff has valid entity reference

Rule 5: Respect SpawnTag / DestroyTag Flow

When working inside spawn/destroy systems:

* Only act on entities with SpawnTag
* Only clean up on DestroyTag
* DestroyEntity must only be issued inside appropriate DestroyTag processing systems or via ECB in lifecycle-aligned contexts.

This ensures proper entity lifecycle management and prevents desync in multiplayer environments.

---

3. Multiplayer & Authority Rules

Rule 6: Gameplay Changes = Server Patch

When modifying gameplay mechanics, patch only server systems:

Column 1	Column 2
Server Systems (Patch These)	Client Systems (Visuals Only)
Damage calculations	UI feedback
Buff behavior	Visual effects
Ability logic	Animations
AI movement	Particle effects
Spawn logic	Audio


Rule 7: Never Trust Client State

* Do not apply gameplay logic inside client systems
* Never calculate damage client-side
* Never modify stats client-side

Server is the authority. Client-side calculations are for visual feedback only.

---

4. Performance Rules (Critical for Open World)

V Rising handles:

* Thousands of entities
* Persistent world state
* Multiplayer replication

Your mod must scale accordingly.

Rule 8: Never Loop Over All Entities Without Query Filtering

// BAD - iterates everything
foreach (var entity in entityManager.GetAllEntities())

// GOOD - uses filtered query
foreach (var entity in entities)
{
    if (!entityManager.HasComponent<PlayerCharacter>(entity))
        continue;
    // Process only players
}

Rule 9: Use PrefabGUID Instead of String Matching

When identifying prefabs:

if (entityManager.GetComponentData<PrefabGUID>(entity).GuidHash == targetGuid)
{
    // Match found
}

Use known GUID hashes from:

* PrefabIndex.json
* PrefabGUID dumps in the framework

Never compare prefab names at runtime.

Rule 9b: Cache PrefabGUID Hashes

Avoid recomputing GUID hashes in hot paths:

// BAD - recomputes hash every check
if (guid.GuidHash == new PrefabGUID(123).GuidHash)

// GOOD - cached static hash
private static readonly int MyGuidHash = new PrefabGUID(123).GuidHash;
if (guid.GuidHash == MyGuidHash)

Rule 9c: Avoid Per-Frame Query Materialization

Avoid ToEntityArray inside Update unless the query is event-filtered. High-frequency polling queries will degrade large servers.

* Only materialize when event-driven
* Or cache for frame-local processing
* Prefer singleton access patterns for configuration data

---

5. Harmony Patch Rules

Rule 10: HarmonyPrefix Should Return void (Unless Controlling Execution)

[HarmonyPatch(typeof(SomeSystem), nameof(SomeSystem.OnUpdate))]
public static class Patch
{
    static void Prefix(SomeSystem __instance)
    {
        // Safe logic here
    }
}

Only return bool if you explicitly want to skip the original method execution.

Rule 11: Do Not Break System Ordering

Avoid:

* Injecting logic that delays structural changes
* Adding structural changes mid-iteration
* Spawning entities outside Spawn systems

Respect the system lifecycle:

Spawn → Modify → Destroy

---

6. Buff & Ability Rules

Rule 12: Modify Through Existing Systems

When adjusting gameplay mechanics, patch the appropriate existing systems:

Column 1	Column 2
Target	Patch These Systems
Ability damage	Apply_BuffModificationsSystem_Server
Buff duration	AbilityCastStarted_*
Targeting	BuffSystem_Spawn_Server


Do not create parallel systems unless absolutely necessary.

Rule 13: Always Handle Event-Based Systems Properly

Many systems rely on single-frame event entities:

* AbilityCastStartedEvent
* AbilityInterruptedEvent
* AbilityCastEndedEvent

If you miss the frame, your logic fails. Process events immediately within the same system update.

---

7. AI Behavior Rules

Rule 14: Modify AI via Buffs or Events

Instead of directly altering movement:

* Apply behavior-modifying buffs
* Inject into AI state transitions
* Patch AI spawn systems

Directly overriding AI movement often breaks pathfinding synchronization with the server.

---

8. Logging Rules

Rule 15: Always Use Centralized Logger

Plugin.LogInstance.LogInfo($"[MyMod] Entity {entity.Index} modified.");

Include in logs:

* System name
* Entity index
* Relevant component values

Never spam logs inside large loops. Use conditional logging or sampling for high-frequency operations.

---

9. Scalability Rules

Rule 16: Assume 100+ Players and 10,000+ Entities

Your mod must:

* Avoid per-frame heavy operations
* Avoid structural changes in large loops
* Avoid reflection inside update loops
* Avoid LINQ queries

Use efficient data structures and pre-computed lookups.

---

10. Stability Rules

Rule 17: Never Use using With NativeArray

V Rising’s ECS fork sometimes fails with using statements. Always use try/finally:

NativeArray<Entity> entities = _Query.ToEntityArray(Allocator.Temp);
try
{
    // Process entities
}
finally
{
    entities.Dispose();
}

Rule 18: Never Assume Unity Standard DOTS Behavior

V Rising uses:

* Custom ECS behavior
* Custom job scheduling
* Custom system order

Always test on:

* Dedicated server
* Multiplayer environment
* Long-running sessions

Rule 19: Use EntityCommandBuffer for Structural Changes

V Rising’s ECS fork is sensitive to structural changes during iteration.

Never call directly inside an entity iteration loop:

* AddComponent
* RemoveComponent
* DestroyEntity
* Instantiate

Preferred pattern using ECB:

var ecb = new EntityCommandBuffer(Allocator.Temp);
try
{
    var entities = _Query.ToEntityArray(Allocator.Temp);
    try
    {
        foreach (var entity in entities)
        {
            if (!entityManager.Exists(entity))
                continue;

            ecb.AddComponent<SomeTag>(entity);
        }
    }
    finally
    {
        entities.Dispose();
    }
}
finally
{
    ecb.Playback(entityManager);
    ecb.Dispose();
}

Structural changes mid-iteration can cause:

* Invalid chunk state
* Desync
* Silent server instability

This is critical for lifecycle patches and buff spawn handling.

Rule 20: Validate Singleton Existence Before Access

In V Rising, some +Singleton components are:

* Recreated on reload
* Temporarily lost during world load

Never assume a singleton exists. Always check:

if (!_Query.TryGetSingleton(out SomeSingleton singleton))
    return;

Dedicated server reloads can temporarily invalidate singleton assumptions.

---

11. Multi-Mod Coexistence Rules

Rule 21: Never Share Module IDs Across Mods

Each mod must have a distinct module ID. If two mods share a module ID:

* Event consumers may collide
* Flow resolution becomes nondeterministic
* Replace=true registration can mask race conditions

Current module ownership:

Column 1	Column 2
Mod	Module ID
Bluelock	bluelock.zones
CycleBorn	cycleborn.lifecycle


BlueLock and CycleBorn must enforce symmetric ID correction. All new mods must declare a unique module ID in their configuration.

Rule 22: Guard Hot Reload Against Partial Bootstrap

Hot reload is dangerous in multiplayer ECS.

Never enable file watchers before:

* Config migration completes
* JSON seeding completes
* Registry registration completes

Correct bootstrap sequence:

Disable watcher → Migrate → Seed → Register → Enable watcher

This prevents reload during partial state.

Rule 23: No Cross-Mod Double Event Consumption

ZoneTransitionEvent, Ability events, and Buff spawn events must have:

* One authoritative consumer
* No competing systems from multiple mods

If multiple mods need behavior:

* Use registry dispatch
* Do not create parallel ECS systems consuming same event entity

---

12. Testing and Validation Rules

Rule 24: Dedicated Server Validation Required

All gameplay-affecting mods must be validated on:

* Clean dedicated server
* 30+ minute runtime
* At least one zone transition
* At least one player reconnect
* At least one world save cycle

Many ECS issues only appear:

* After reconnect
* After long runtime
* After reload

---

13. Configuration Contract Rules

Rule 25: Use Canonical Config Roots Only

Do not use legacy root config locations for runtime behavior.

Canonical runtime roots:

Column 1	Column 2
Mod	Canonical Root
VAutomationCore	BepInEx/config
BlueLock	BepInEx/config/Bluelock
CycleBorn	BepInEx/config/CycleBorn


Important canonical files:

* Core: gg.coyote.VAutomationCore.cfg
* BlueLock: VAuto.Zone.cfg, bluelock.domain.json, zone.flows.registry.json
* CycleBorn: VAuto.Lifecycle.cfg, VAuto.Lifecycle.json, flows.registry.json, lifecycle.policy.json

Rule 26: BlueLock Domain + Registry Only

BlueLock runtime must never load or validate legacy JSONs:

* VAuto.ZoneLifecycle.json
* VAuto.Zones.json
* VAuto.Kits.json

Lifecycle mappings are in-memory defaults, and kits/zones flow through the canonical domain.

Legacy files may exist on disk from old installs, but they must not be treated as runtime sources of truth.

Rule 27: Validation Must Follow Runtime Sources

Validation must mirror runtime loading order and ownership:

* Zone definitions validate from canonical domain source first
* Flow resolvability validates against flow registries (not legacy per-flow files)
* Missing optional schema artifacts should not fail startup on dedicated servers

If validation and runtime source differ, multiplayer behavior becomes nondeterministic.

Rule 28: Deploy Path Must Normalize to BepInEx Root

Deployment tooling must support both user inputs:

* .../BepInEx
* .../BepInEx/plugins

And always resolve:

* plugins: .../BepInEx/plugins
* BlueLock config: .../BepInEx/config/Bluelock
* CycleBorn config: .../BepInEx/config/CycleBorn

---

14. Command Surface Rules

Rule 29: Canonical BlueLock Command Roots Only

Canonical BlueLock roots:

* zone
* match
* spawn
* template
* tag
* glow
* vblood

Deprecated or removed:

* enter
* exit
* seed
* zone glow aliases

If a legacy command is found, remove it and document the canonical replacement.

---

15. Enforcement Rules

Rule 30: CI Hard-Fail Checks Are Mandatory

The following violations must fail CI immediately:

* Allocator.TempJob usage in runtime code
* using (...) around ToEntityArray(...) or other NativeArray ECS materialization
* GetAllEntities() usage in gameplay loops
* Module ID collision between known modules (bluelock.zones, cycleborn.lifecycle)
* Additional ECS systems that consume single-owner event streams (for example ZoneTransitionEvent)

Recommended checks:

* rg "Allocator\\.TempJob"
* rg "using\\s*\\(.*ToEntityArray"
* rg "GetAllEntities\\("
* Reflection/unit test for single-consumer event ownership

Rule 29: Single Event Consumer Contract

Some event streams are single-owner and must have exactly one ECS consumer.

Current mandatory single-owner stream:

* ZoneTransitionEvent -> ZoneTransitionRouterSystem only

If new behavior is needed:

* Extend router dispatch
* Or dispatch through registry/services
* Do not introduce a second ECS consumer system for the same event stream

Rule 30: Registry Ownership Matrix Is Authoritative

Flow key ownership is partitioned by module.

Column 1	Column 2	Column 3
Owner	Module ID	Reserved Keys
BlueLock	bluelock.zones	zone.enter.*, zone.exit.*, A1.*, B1.*, T3.*, ZoneDefault.*
CycleBorn	cycleborn.lifecycle	lifecycle-owned keys outside BlueLock reserved prefixes


Conflict policy:

* If a module loads keys owned by another module, skip and warn.
* If configured module ID collides with another owner, force-correct to owner default and warn.

Rule 31: Startup Log Acceptance Checklist

A startup is accepted only when logs show:

* Canonical config paths for all loaded mods
* Active module IDs for all flow registries
* No legacy root config paths presented as runtime sources
* No false validation errors that rely on removed legacy file layouts

Rule 32: Rule-to-Test Mapping Is Required

Each critical rule must map to at least one automated guard (unit test, reflection test, or static grep check).

Minimum mapping set:

Column 1	Column 2
Rule Area	Guard Type
Single event consumer	reflection/unit test
Allocator/disposal safety	static grep CI check
Module ID uniqueness	unit test + runtime correction
Registry ownership partition	unit test
Canonical config paths	startup smoke test


Rule 33: Deprecation Lifecycle Is Time-Boxed

Deprecated behavior must follow this lifecycle:

1. Mark as deprecated and emit warning.
2. Keep compatibility for one planned release window.
3. Remove legacy behavior and warnings in the next planned cleanup release.

Do not keep indefinite compatibility branches in runtime paths.

Rule 34: Hot-Reload Safety Sequence Is Mandatory

Hot reload must never run during partial bootstrap.

Required sequence:

1. Stop watcher
2. Seed and validate files
3. Register/reload module flows atomically
4. Start watcher

When flow registries are reloaded, unregister old module entries before re-registering.

Rule 35: Operator Deploy SOP

Standard operator command:

powershell -ExecutionPolicy Bypass -File .\tools\deploy-all.ps1 -Configuration Release -ServerBepInExPath "C:\Program Files (x86)\Steam\steamapps\common\VRising\VRising_Server\BepInEx\plugins"

Accepted -ServerBepInExPath forms:

* ...\BepInEx
* ...\BepInEx\plugins

Post-deploy checks:

* Deploy output prints normalized BepInEx root, plugins path, and both config paths
* Server restart completed
* Startup log acceptance checklist (Rule 31) passes

---

🏆 Gold Standard AI Prompt for V Rising Modding

If you want AI to generate safe mods, use this instruction:

“Generate Harmony patches for V Rising using predefined EntityQueries, Allocator.Temp with try/finally disposal, EntityCommandBuffer for structural changes, server-authoritative logic, safe component validation (HasComponent + Exists), singleton existence checks (TryGetSingleton), no TempJob, no using statements, no LINQ, no custom EntityQueries unless required, distinct module IDs for multi-mod environments, and scalable for multiplayer open-world performance.”

---

Related Documentation

* ServerSystemQueries.txt — Predefined server-side entity queries
* ClientSystemQueries.txt — Predefined client-side entity queries
* PrefabIndex.json — Known prefab GUID mappings
* VAutomationCore Framework Wiki — Framework documentation
* Bluelock Zone Flow Registry — Zone lifecycle management
* CycleBorn Flow Registry — Lifecycle event dispatching
* Deploy Script — Unified build/deploy path normalization

---

Last updated: 2026-03-05
