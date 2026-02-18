using System.Collections.Generic;
using Stunlock.Core;
using VAuto.Zone.Data;

namespace VAutomationCore.Core.Data
{
    public static class PrefabsAll
    {
        public static readonly Dictionary<string, PrefabGUID> ByName = PrefabsCatalog.ByName;

        public static bool TryGet(string name, out PrefabGUID guid)
        {
            guid = PrefabGUID.Empty;
            return !string.IsNullOrWhiteSpace(name) && ByName.TryGetValue(name, out guid);
        }
    }
}
