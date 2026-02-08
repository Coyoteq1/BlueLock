# VAutoTraps Troubleshooting Guide

This guide covers common issues and their solutions for VAutoTraps.

## Table of Contents

- [Installation Issues](#installation-issues)
- [Configuration Issues](#configuration-issues)
- [Command Issues](#command-issues)
- [Gameplay Issues](#gameplay-issues)
- [Performance Issues](#performance-issues)
- [Log Analysis](#log-analysis)
- [Getting Help](#getting-help)

---

## Installation Issues

### Plugin Not Loading

**Symptoms:**
- No `[VAutoTraps]` messages in server log
- Commands not recognized
- Plugin not in BepInEx plugin list

**Solutions:**

1. **Check dependencies**
   ```
   Ensure VAutomationCore.dll and VampireCommandFramework.dll are in BepInEx/plugins/
   ```

2. **Verify file placement**
   ```
   BepInEx/plugins/VAutoTraps.dll
   BepInEx/plugins/VAutomationCore.dll
   BepInEx/plugins/VampireCommandFramework.dll
   ```

3. **Check BepInEx version**
   ```
   Ensure BepInEx 5.4.2105+ is installed
   For IL2CPP servers, use BepInExPack IL2CPP 6.0.0+
   ```

4. **Verify .NET version**
   ```
   Project requires .NET 6.0 or higher
   ```

### Dependency Errors

**Symptoms:**
```
[Error] Could not load dependency: gg.coyote.VAutomationCore
[Error] Could not load dependency: gg.deca.VampireCommandFramework
```

**Solutions:**

1. **Download missing dependencies**
   - [VAutomationCore](https://github.com/Coyoteq1/VAutomationCore)
   - [VampireCommandFramework](https://github.com/deca-voxel/VampireCommandFramework)

2. **Place in correct folder**
   ```
   BepInEx/plugins/ (not subfolders)
   ```

3. **Check version compatibility**
   ```
   VAutomationCore 1.0.0+
   VampireCommandFramework 0.10.4+
   ```

---

## Configuration Issues

### Config Files Not Loading

**Symptoms:**
- Default values always used
- Changes to config not taking effect
- `[VAutoTraps] Created new JSON configuration` in logs

**Solutions:**

1. **Check config file location**
   ```
   BepInEx/config/VAuto.Traps.cfg
   BepInEx/config/VAuto.Traps.json
   ```

2. **Verify JSON syntax**
   ```json
   {
       "Traps": {
           "Enabled": true
       }
   }
   ```

3. **Use hot-reload**
   ```
   Set HotReload = true in config
   Use .trap reload command
   ```

4. **Check file permissions**
   ```
   Server must have write access to config folder
   ```

### TOML Config Not Applied

**Symptoms:**
- Kill streak thresholds not working
- Custom rules not loading

**Solutions:**

1. **Verify TOML location**
   ```
   BepInEx/config/VAutoTraps/killstreak_trap_config.toml
   ```

2. **Check TOML syntax**
   ```toml
   [Streaks]
   bronze_threshold = 3
   silver_threshold = 5
   gold_threshold = 10
   ```

3. **Reload configuration**
   ```
   Use .trap reload command
   Restart server
   ```

---

## Command Issues

### Commands Not Recognized

**Symptoms:**
```
Unknown command: .trap
Unknown command: .spawnchest
```

**Solutions:**

1. **Check plugin loading**
   ```
   Look for [VAutoTraps] Loaded successfully in logs
   ```

2. **Verify Vampire Command Framework**
   ```
   VCF commands should work
   Test with .help (VCF built-in)
   ```

3. **Check admin permissions**
   ```
   Some commands require admin
   Verify .trap debug works
   ```

### Admin Commands Not Working

**Symptoms:**
```
Admin only: .trap set
Admin only: .trap reload
```

**Solutions:**

1. **Set admin flag**
   ```
   Commands with adminOnly: true require admin
   Check server configuration for admin list
   ```

2. **Use in-game admin**
   ```
   !admin add <playername>
   !admin reload
   ```

3. **Check permission level**
   ```
   Some commands require specific permission levels
   ```

---

## Gameplay Issues

### Chests Not Spawning

**Symptoms:**
- `.trap chest spawn` shows success but no chest
- Kill streak reward not appearing
- No chest entity in world

**Solutions:**

1. **Check chest spawns enabled**
   ```
   ChestSpawns.Enabled = true in config
   Plugin.ChestSpawnsActive = true
   ```

2. **Verify spawn position**
   ```
   Ensure valid coordinates
   Check not in restricted zone
   ```

3. **Check max chest count**
   ```
   ChestMaxCount may be reached
   Remove existing chests first
   ```

4. **Verify reward types**
   ```
   ChestRewards contains valid types
   Valid: relic, shard, gem
   ```

### Traps Not Dealing Damage

**Symptoms:**
- Trap triggered but no damage
- PvP combat not affected
- No damage messages

**Solutions:**

1. **Check PvP enabled**
   ```
   Server PvP must be enabled
   Check player PvP status
   ```

2. **Verify trap settings**
   ```
   ContainerTraps.Enabled = true
   TrapDamageAmount > 0
   ```

3. **Check zone rules**
   ```
   AllowInsideZones = true (for indoor traps)
   AllowOutsideZones = true (for outdoor traps)
   ```

4. **Verify trap activation**
   ```
   Use .trap status to see trap state
   Check trap is armed
   ```

### Kill Streak Not Tracking

**Symptoms:**
- Kill streak always 0
- No rewards at thresholds
- `.trap streak status` shows 0

**Solutions:**

1. **Check kill streak enabled**
   ```
   KillStreak.Enabled = true
   Plugin.KillStreakActive = true
   ```

2. **Verify threshold**
   ```
   KillStreak.Threshold = 5 (default)
   Player needs 5+ kills
   ```

3. **Check kill detection**
   ```
   Ensure kills are being registered
   Check server events are firing
   ```

4. **Restart streak tracking**
   ```
   Use .trap reload
   Or restart server
   ```

### Cannot Loot Chests

**Symptoms:**
- "No chest nearby" message
- Cannot open spawned chests
- Chest exists but inaccessible

**Solutions:**

1. **Check distance**
   ```
   Loot range is 2 meters
   Get closer to chest position
   ```

2. **Verify kill streak**
   ```
   Need KillThreshold kills
   Use .trap streak status to check
   ```

3. **Check if already looted**
   ```
   Each player can loot once
   Chest locks after looting
   ```

4. **Remove and respawn**
   ```
   .trap chest remove
   .trap chest spawn
   ```

---

## Performance Issues

### High CPU Usage

**Symptoms:**
- Server lag
- High CPU percentage
- Slow command responses

**Solutions:**

1. **Reduce update interval**
   ```
   TrapSystem.UpdateInterval = 10 (instead of 5)
   ```

2. **Limit entity counts**
   ```
   ChestMaxCount = 5 (instead of 10)
   ContainerMaxCount = 3 (instead of 5)
   ```

3. **Disable debug mode**
   ```
   DebugMode = false
   TrapDebugMode = false
   ```

4. **Disable hot-reload**
   ```
   HotReload = false
   Reduces timer overhead
   ```

### Memory Usage

**Symptoms:**
- Increasing memory usage
- Memory leaks suspected
- Server crashes after long uptime

**Solutions:**

1. **Limit chest lifetime**
   ```
   Add chest expiration
   Remove old chests periodically
   ```

2. **Clear inactive traps**
   ```
   Use .trap clear regularly
   Remove unused zones
   ```

3. **Restart periodically**
   ```
   Schedule server restarts
   Every 24-48 hours
   ```

---

## Log Analysis

### Reading Server Logs

**Log Location:**
```
BepInEx/logs/Unity.log
BepInEx/logs/OutputLog.txt
```

### Important Log Messages

| Message | Meaning |
|---------|---------|
| `[VAutoTraps] Loading v1.0.0...` | Plugin starting |
| `[VAutoTraps] Loaded successfully.` | Plugin loaded |
| `[VAutoTraps] Unloaded.` | Plugin unloaded |
| `[ChestSpawnService] Spawned chest` | Chest spawned |
| `[ChestSpawnService] Player looted chest` | Chest looted |
| `[ContainerTrapService] Trap set` | Trap placed |
| `[VAutoTraps] Error:` | Something wrong |

### Debug Logging

Enable debug mode for detailed logs:

```toml
[TrapSystem]
DebugMode = true

[Debug]
DebugMode = true
```

---

## Getting Help

### Before Asking

1. **Check this guide** - Solution may already be here
2. **Check server logs** - Look for error messages
3. **Try basic fixes** - Restart server, reload config
4. **Search existing issues** - Others may have same problem

### Collecting Information

When reporting an issue, include:

1. **Server logs** (relevant sections)
2. **Configuration files** (VAuto.Traps.cfg, VAuto.Traps.json)
3. **Steps to reproduce**
4. **Expected vs actual behavior**
5. **Server version** (V Rising, BepInEx, etc.)

### Reporting Issues

- **GitHub Issues**: [VAutoTraps Issues](https://github.com/Coyoteq1/VAutoTraps/issues)
- **Discord**: V Rising Modding community
- **Provide**: Log files, config, reproduction steps

---

## Quick Fix Checklist

- [ ] Restart server
- [ ] Reload config (`.trap reload`)
- [ ] Verify dependencies installed
- [ ] Check config file locations
- [ ] Enable debug mode for more logs
- [ ] Verify admin permissions
- [ ] Check PvP status
- [ ] Clear and respawn entities
- [ ] Update to latest versions

---

## Common Error Messages

| Error | Solution |
|-------|----------|
| `Failed to load JSON configuration` | Check JSON syntax, recreate file |
| `Could not load dependency` | Install missing dependencies |
| `Admin only` | Get admin permissions |
| `No chest nearby` | Get within 2m of chest |
| `No such command` | Plugin not loaded |
| `Config hot-reload failed` | Check file permissions |
