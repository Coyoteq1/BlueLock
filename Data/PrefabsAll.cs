using System.Collections.Generic;
using Stunlock.Core;
using VAuto.Zone.Data;

namespace VAutomationCore.Core.Data
{
    /// <summary>
    /// Backward-compatible shim that keeps existing call sites while BlueLock owns prefab metadata.
    /// </summary>
    public static class PrefabsAll
    {
        public static IReadOnlyDictionary<string, PrefabGUID> ByName => PrefabsCatalog.ByName;

        public static bool TryGet(string name, out PrefabGUID guid)
        {
            guid = PrefabGUID.Empty;
            return !string.IsNullOrWhiteSpace(name) && PrefabsCatalog.ByName.TryGetValue(name, out guid);
        }
    }
}