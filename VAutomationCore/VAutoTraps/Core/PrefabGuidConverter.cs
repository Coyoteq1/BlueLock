using System;
using System.Reflection;
using ProjectM;
using Stunlock.Core;
using Unity.Entities;

namespace VAuto.Core
{
    internal static class PrefabGuidConverter
    {
        public static bool TryGetGuid(string prefabName, out PrefabGUID guid)
        {
            guid = default;
            if (string.IsNullOrWhiteSpace(prefabName))
                return false;

            var system = VRCore.ServerWorld?.GetExistingSystemManaged<PrefabCollectionSystem>();
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

            var system = VRCore.ServerWorld?.GetExistingSystemManaged<PrefabCollectionSystem>();
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

                if (dictValue is int intGuid)
                {
                    guid = new PrefabGUID((int)intGuid);
                    return true;
                }
            }

            foreach (var key in dict.Keys)
            {
                if (key is not string keyStr) continue;
                if (!keyStr.Equals(prefabName, StringComparison.OrdinalIgnoreCase)) continue;

                var dictValue = dict[key];
                if (dictValue is PrefabGUID pg)
                {
                    guid = pg;
                    return true;
                }

                if (dictValue is int intGuid)
                {
                    guid = new PrefabGUID((int)intGuid);
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
                if (key is not string keyStr) continue;

                var dictValue = dict[key];
                if (dictValue is PrefabGUID pg && pg.GuidHash == guid.GuidHash)
                {
                    prefabName = keyStr;
                    return true;
                }

                if (dictValue is int intGuid && (long)intGuid == guid.GuidHash)
                {
                    prefabName = keyStr;
                    return true;
                }
            }

            return false;
        }
    }
}
