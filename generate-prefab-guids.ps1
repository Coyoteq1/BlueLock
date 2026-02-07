# Generate Prefab GUIDs Script
# Usage: Run this in-game via chat command to generate GUIDs for prefabs

Write-Host @"
===============================================
Prefab GUID Generation Tool
===============================================

IN-GAME COMMANDS:
1. .arena glow choices              - List registered glow prefabs with GUIDs
2. .arena glow testspawn <name>    - Get GUID for specific prefab
3. .dump prefabs                   - Dump ALL prefab GUIDs to C:/PrefabsDump.txt

AFTER DUMP:
- Check C:/PrefabsDump.txt for all prefab GUIDs
- Find your prefab and note the GUID
- Add to GlowService.cs:
  _glowChoices["PrefabName"] = new PrefabGUID(123456789);

- Or update glowChoices.txt:
  PrefabName=123456789

===============================================
"@
