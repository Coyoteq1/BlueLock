# V Rising Mods - Project Checklist

## VAutomationCore

| Category | Item | Status |
|----------|------|--------|
| Build | Build Release | [ ] |
| Build | Dependencies | [ ] |
| Core | Core_ECSHelper | [ ] |
| Core | Core_VRCore | [ ] |
| Chat | ChatService | [ ] |
| Config | VAutoConfigService | [ ] |
| Config | JsonConfigManager | [ ] |
| Config | PluginManifest | [ ] |
| Helpers | PrefabGuidConverter | [ ] |
| Helpers | VAutoLogger | [ ] |
| Extensions | ECSExtensions | [ ] |
| Extensions | PrefabGuidExtensions | [ ] |
| Extensions | VAutoExtensions | [ ] |
| Services | EntitySpawner | [ ] |
| Services | EntitySpawner - Initialize | [ ] |
| Services | EntitySpawner - Batch Spawn | [ ] |
| Services | EntitySpawner - GlowConfig | [ ] |
| Services | EntitySpawner - SpawnConfig | [ ] |
| Services | EntitySpawner - SpawnResult | [ ] |
| Services | EntitySpawner - SpawnGlowBorder | [ ] |
| Services | EntitySpawner - SpawnGlowGrid | [ ] |
| Services | EntitySpawner - DespawnAll | [ ] |

## VAutoannounce

| Category | Item | Status |
|----------|------|--------|
| Build | Build | [ ] |
| Build | Dependencies | [ ] |
| Command | .announce | [ ] |
| Command | .say | [ ] |
| Command | .alert | [ ] |
| Command | .trapalert | [ ] |
| Command | .broadcast | [ ] |
| Service | AnnouncementService | [ ] |
| Core | VRCore | [ ] |
| Core | PrefabGuidConverter | [ ] |

## VAutoTraps

| Category | Item | Status |
|----------|------|--------|
| Build | Build | [ ] |
| Build | Dependencies | [ ] |
| Command | .trap help | [ ] |
| Command | .trap status | [ ] |
| Command | .trap config | [ ] |
| Command | .trap reload | [ ] |
| Command | .trap debug | [ ] |
| Command | .trap set | [ ] |
| Command | .trap remove | [ ] |
| Command | .trap list | [ ] |
| Command | .trap arm | [ ] |
| Command | .trap trigger | [ ] |
| Command | .trap clear | [ ] |
| Command | .trap chest spawn | [ ] |
| Command | .trap chest list | [ ] |
| Command | .trap chest remove | [ ] |
| Command | .trap chest clear | [ ] |
| Command | .trap zone create | [ ] |
| Command | .trap zone delete | [ ] |
| Command | .trap zone list | [ ] |
| Command | .trap zone arm | [ ] |
| Command | .trap zone check | [ ] |
| Command | .trap zone clear | [ ] |
| Command | .trap streak status | [ ] |
| Command | .trap streak reset | [ ] |
| Command | .trap streak config | [ ] |
| Command | .trap streak toggle | [ ] |
| Command | .trap streak test | [ ] |
| Command | .trap streak stats | [ ] |
| Service | ChestSpawnService | [ ] |
| Service | ContainerTrapService | [ ] |
| Service | TrapZoneService | [ ] |
| Service | TrapSpawnRules | [ ] |
| Config | TOML Config | [ ] |
| Config | JSON Config | [ ] |

## VAutoZone

| Category | Item | Status |
|----------|------|--------|
| Build | Build | [ ] |
| Build | Dependencies | [ ] |
| Command | .arena help | [ ] |
| Command | .arena list | [ ] |
| Command | .arena enter | [ ] |
| Command | .arena exit | [ ] |
| Command | .zone help | [ ] |
| Service | ArenaCommands | [ ] |
| Service | ArenaGlowBorderService | [ ] |
| Service | ArenaTerritory | [ ] |
| Service | ArenaZoneConfigLoader | [ ] |
| Service | GlowService | [ ] |
| Service | PlayerSnapshotService | [ ] |
| Service | ZoneEventBridge | [ ] |
| Service | ZoneGlowBorderService | [ ] |
| Service | ZoneGlowRotationService | [ ] |
| Integration | EntitySpawner batch spawn | [ ] |
| Integration | EntitySpawner glow border | [ ] |
| Integration | EntitySpawner glow grid | [ ] |
| Config | arena_territory.json | [ ] |
| Config | arena_zones.json | [ ] |
| Config | glow_zones.json | [ ] |

## Vlifecycle

| Category | Item | Status |
|----------|------|--------|
| Build | Build | [ ] |
| Build | Dependencies | [ ] |
| Command | .lifecycle status | [ ] |
| Command | .lifecycle enter | [ ] |
| Command | .lifecycle exit | [ ] |
| Command | .lifecycle debug | [ ] |
| Service | ArenaLifecycleManager | [ ] |
| Service | ConnectionEventPatches | [ ] |
| Service | InputSystemUpdatePatch | [ ] |
| Service | LifecycleActionHandlers | [ ] |
| Service | LifecycleModels | [ ] |
| Service | Singleton | [ ] |
| Service | ZUIInputBlocker | [ ] |
| Service | ZUISpellMenu | [ ] |
| Config | pvp_item.toml | [ ] |
| Config | VLifecycle.json | [ ] |

## Cross-Project Integration

| Check | Projects | Status |
|-------|----------|--------|
| VAutomationCore loads first | All | [ ] |
| Vlifecycle before VAutoZone | Vlifecycle, VAutoZone | [ ] |
| VAutoannounce works | All | [ ] |
| VAutoTraps zone rules | VAutoTraps, VAutoZone | [ ] |
| Zone detection events | VAutoTraps, Vlifecycle | [ ] |
| Glow borders display | VAutoZone | [ ] |

---

## Quick Reference

### Project Dependencies
```
VAutomationCore (Required by all)
├── VAutoannounce
├── VAutoTraps
├── VAutoZone
└── Vlifecycle
```

### Load Order
1. VAutomationCore
2. Vlifecycle
3. VAutoZone
4. VAutoannounce
5. VAutoTraps

--- /!!

*Last Updated: 2026-02-08*
