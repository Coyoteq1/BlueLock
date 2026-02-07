# Test Glow Border Commands for VAutoZone
# Run this in-game via chat to test glow spawning

# Available glow prefab choices:
# Glow Buffs:
#   InkShadow, Cursed, Howl, Chaos, Emerald, Poison, Agony, Light
# Decorations:
#   Table3x3Cabal, ChairCabal, Barrel01, Crate01, Fireplace, Banner01

# Usage in-game chat:
# .arena glow choices                              # List all available prefabs
# .arena glow testspawn Table3x3Cabal            # Test spawn with table
# .arena glow testspawn InkShadow                # Test spawn with glow buff
# .arena glow test 3                              # Preview point count at 3m spacing
# .arena glow spawn 3 InkShadow                  # Spawn actual border

# Example test sequence:
. arena glow choices                              # Show all prefabs
. arena glow test 3                              # Count points at 3m spacing
. arena glow testspawn Table3x3Cabal             # Spawn table border
. arena glow clear                               # Clear spawned entities
