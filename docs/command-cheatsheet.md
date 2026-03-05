# Command Cheatsheet

Last updated: 2026-03-05

## Core

- `.coreauth help`
- `.coreauth status`
- `.jobs help`
- `.jobs flow list`

## CycleBorn

- `.lifecycle help`
- `.lifecycle status`
- `.lifecycle config`

## BlueLock

- `.zone help`
- `.zone enter <zoneId>`
- `.zone exit [zoneId]`
- `.zone seed [flows|database] [force]`
- `.match start <zoneId>`
- `.spawn help`
- `.template list <zoneId>`
- `.tag list`
- `.glow help`
- `.glow status`
- `.glow debug info`
- `.vblood unlockprefab [vbloodName]`

## Deprecated Aliases (Transition Window)

- `.enter` -> use `.zone enter`
- `.exit` -> use `.zone exit`
- `.seed` -> use `.zone seed`

These aliases still work for one release window and emit a deprecation warning.
