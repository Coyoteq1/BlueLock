using Unity.Entities;
using Stunlock.Core;

namespace ProjectM
{
    /// <summary>
    /// Shim for AbilitySlotBuffer so AbilityUi can compile.
    /// Remove this if the real ProjectM.AbilitySlotBuffer is available in the game assemblies.
    /// </summary>
    public struct AbilitySlotBuffer
    {
        public PrefabGUID Ability;
    }
}
