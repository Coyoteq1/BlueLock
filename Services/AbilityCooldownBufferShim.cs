using Unity.Entities;
using Stunlock.Core;

namespace ProjectM
{
    /// <summary>
    /// Shim for AbilityCooldownBuffer so AbilityUi can compile.
    /// In environments where the real ProjectM.AbilityCooldownBuffer exists,
    /// this file should be removed.
    /// Note: defined without implementing IBufferElementData so it compiles
    /// even if the ECS package defines that type differently.
    /// </summary>
    public struct AbilityCooldownBuffer
    {
        public PrefabGUID Ability;
        public float RemainingSeconds;
    }
}
