# Flow Comparison Report (Preflow vs Per-Zone)

## Purpose
Provide a single, reliable flow comparison that verifies each zone's resolved flow
against a preflow baseline, then generates a concise report with recommendations.

This document defines the comparison rules and the report format so it works
consistently across zones and future changes.

---

## Inputs
- Preflow baseline: the intended "default" flow steps to compare against.
- Per-zone flow: the resolved flow after applying zone overrides and fallbacks.
- Flow registry: `BepInEx/config/Bluelock/zone.flows.registry.json`
- Zone config: `BepInEx/config/Bluelock/bluelock.domain.json`

---

## Flow Resolution (Per-Zone)
When resolving a zone flow, use the same order as runtime:
1. `{FlowId}.enter` or `{FlowId}.exit`
2. `zone.enter.{ZoneId}` or `zone.exit.{ZoneId}`
3. `zone.enter.default` or `zone.exit.default`

If a flow is missing at all three levels, flag it as `MISSING`.

---

## Comparison Rules
Compare each per-zone resolved flow to the preflow baseline:
- `MATCH`: Steps and order are identical.
- `EXTRA`: Per-zone has additional steps (list them).
- `MISSING`: Per-zone is missing steps present in preflow (list them).
- `REORDERED`: Same steps but in different order.
- `DIFFERENT`: Different steps and order.

Also record:
- Action deltas: added/removed actions.
- Arg deltas: same action but different args.
- Safety notes: actions that change state (teleport, applybuff, removekit, etc.).

---

## Report Output (Send Format)
One report per run, grouped by zone:

```
Report: Flow Comparison
Date: YYYY-MM-DD
Registry: zone.flows.registry.json
Domain: bluelock.domain.json

Zone: <ZoneId>
Enter Flow: <ResolvedFlowId>
Exit Flow: <ResolvedFlowId>
Status: MATCH | EXTRA | MISSING | REORDERED | DIFFERENT

Enter Deltas:
- Added: <action> <args>
- Removed: <action> <args>
- Args Changed: <action> <old> -> <new>

Exit Deltas:
- Added: ...
- Removed: ...
- Args Changed: ...

Recommendations:
- <Short, actionable recommendation>
```

---

## Recommendation Rules
Use these rules to generate a recommendation:
- `MATCH`: "No changes needed."
- `EXTRA`: "Confirm extra steps are intended and documented."
- `MISSING`: "Add missing steps or update baseline to reflect intent."
- `REORDERED`: "Verify order-sensitive steps (teleport, buffs, kits)."
- `DIFFERENT`: "Review flow intent and reconcile with baseline."

If safety-sensitive actions changed, add:
"Validate in staging and ensure rollback path exists."

---

## Minimum Pass Criteria
A zone passes if:
- Both enter and exit flows resolve.
- No `MISSING` or `DIFFERENT` on safety-sensitive actions.
- All deltas are documented and approved.

---

## Quick Checklist
- Baseline preflow updated and versioned
- All zones resolve enter and exit flows
- Report generated and sent
- Recommendations accepted or queued

