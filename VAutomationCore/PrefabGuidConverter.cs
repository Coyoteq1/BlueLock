using System;
using System.Reflection;
using ProjectM;
using Stunlock.Core;
using Unity.Entities;
using VAutomationCore.Core;

namespace VAuto.Core
{
    internal static class PrefabGuidConverter
    {
        public static bool TryGetGuid(string prefabName, out PrefabGUID guid)
        {
            guid = default;
            if (string.IsNullOrWhiteSpace(prefabName))
                return false;

            if (UnifiedCore.Server == null)
                return false;

            var system = UnifiedCore.Server.GetExistingSystemManaged<PrefabCollectionSystem>();
            if (system == null)
                return false;

            var members = system.GetType().GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var member in members)
            {
                object value = member switch
                {
                    FieldInfo f => f.GetValue(system),
                    PropertyInfo p => p.GetValue(system),
                    _ => null
                };

                if (value == null) continue;
                if (TryGetGuidFromDictionary(value, prefabName, out guid))
                    return true;
            }

            return false;
        }

        public static bool TryGetName(PrefabGUID guid, out string prefabName)
        {
            prefabName = string.Empty;
            if (guid.GuidHash == 0L)
                return false;

            if (UnifiedCore.Server == null)
                return false;

            var system = UnifiedCore.Server.GetExistingSystemManaged<PrefabCollectionSystem>();
            if (system == null)
                return false;

            var members = system.GetType().GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var member in members)
            {
                object value = member switch
                {
                    FieldInfo f => f.GetValue(system),
                    PropertyInfo p => p.GetValue(system),
                    _ => null
                };

                if (value == null) continue;
                if (TryGetNameFromDictionary(value, guid, out prefabName))
                    return true;
            }

            return false;
        }

        private static bool TryGetGuidFromDictionary(object value, string prefabName, out PrefabGUID guid)
        {
            guid = default;
            if (value is not System.Collections.IDictionary dict)
                return false;

            if (dict.Contains(prefabName))
            {
                var dictValue = dict[prefabName];
                if (dictValue is PrefabGUID pg)
                {
                    guid = pg;
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetNameFromDictionary(object value, PrefabGUID guid, out string prefabName)
        {
            prefabName = string.Empty;
            if (value is not System.Collections.IDictionary dict)
                return false;

            foreach (var key in dict.Keys)
            {
                if (dict[key] is PrefabGUID pg && pg.GuidHash == guid.GuidHash)
                {
                    prefabName = key.ToString() ?? string.Empty;
                    return true;
                }
            }

            return false;
        }
    }
}
