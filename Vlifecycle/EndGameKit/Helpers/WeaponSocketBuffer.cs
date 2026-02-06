using Stunlock.Core;
using Unity.Entities;

namespace ProjectM.WeaponComponents
{
    /// <summary>
    /// Placeholder buffer element that represents a weapon socket entry.
    /// This mirrors the structure expected by the kit/jewel services.
    /// </summary>
    public struct WeaponSocketBuffer
    {
        public PrefabGUID Jewel;
    }
}
