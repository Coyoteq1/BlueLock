# Command Alignment Map (Core + BlueLock + CycleBorn)

Last updated: 2026-03-05

## Canonical Command Roots

| Module | Canonical Roots |
|---|---|
| VAutomationCore | `coreauth`, `jobs` |
| BlueLock | `zone`, `match`, `spawn`, `template`, `tag`, `glow`, `vblood` |
| CycleBorn | `lifecycle` |

## Legacy to Canonical Mapping

| Current / Legacy Token | Canonical Token | Status |
|---|---|---|
| `.enter <zone>` | `.zone enter <zone>` | Deprecated alias retained (warning emitted) |
| `.exit [zone]` | `.zone exit [zone]` | Deprecated alias retained (warning emitted) |
| `.seed [flows|database] [force]` | `.zone seed [flows|database] [force]` | Deprecated alias retained (warning emitted) |
| `.zone glow ...` | `.glow ...` | Removed from command surface |
| `.glow debug info` | `.glow debug info` | Canonical (grouped under `glow`) |
| `.glow entities` | `.glow debug entities` | Canonical renamed |
| `.glow test` | `.glow debug test` | Canonical renamed |
| `.glow clear all` | `.glow debug clearall` | Canonical renamed |
| `.glow performance` | `.glow debug performance` | Canonical renamed |
| `.unlockprefab` (bare root) | `.vblood unlockprefab` | Removed as primary root |

## Command Root Ownership Notes

- BlueLock startup summary must only print canonical roots.
- CycleBorn root remains `lifecycle`.
- Core roots remain `coreauth` and `jobs`.
- No new top-level root should be introduced for a feature already covered by an existing root.
