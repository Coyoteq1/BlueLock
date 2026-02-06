# Mod Compatibility Matrix

## Purpose
Track known interactions between VAutomationEvents and common V Rising mods.

## Matrix

| Mod | Conflicts | Integration Required | Status | Notes |
|-----|-----------|---------------------|--------|-------|
| KindredCommands | None | Player data sharing | ✅ Implemented | Align `PlayerData` lookup patterns and naming conventions. |
| KindredArena | Zone management | Conditional loading | ⚠️ TODO | Detect plugin and disable arena systems if present. |
| VMods | None | Optional config | ⚠️ TODO | Use soft dependency with fallback to JSON persistence. |
| RPGMods | Buff/Stats overlap | Event routing | ⚠️ TODO | Confirm buff component usage to avoid duplication. |
| ClanManager | User/Clan data overlap | Shared lookup | ⚠️ TODO | Reuse user entity queries when possible. |

## Detection Pattern
```csharp
// Example: detect another mod by GUID
public static bool IsModLoaded(string guid)
{
    return BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(guid);
}
```

## Update Workflow
1. Test with mod combinations.
2. Document conflicts and mitigations.
3. Add or update integration notes.
