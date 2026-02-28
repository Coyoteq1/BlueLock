using System.Collections.Generic;
using Unity.Mathematics;

namespace VAutomationCore.Core.ECS
{
    public static class ZoneHashUtility
    {
        private static readonly Dictionary<string, int> ZoneIdToHash = new();
        private static readonly Dictionary<int, string> HashToZoneId = new();
        private static readonly Dictionary<int, float3> HashToCenter = new();
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
        }

        public static int GetZoneHash(string zoneId)
        {
            if (string.IsNullOrEmpty(zoneId)) return 0;
            if (ZoneIdToHash.TryGetValue(zoneId, out var hash)) return hash;
            hash = zoneId.GetHashCode();
            ZoneIdToHash[zoneId] = hash;
            HashToZoneId[hash] = zoneId;
            return hash;
        }

        public static string GetZoneId(int hash)
        {
            return hash == 0 ? string.Empty : HashToZoneId.GetValueOrDefault(hash, string.Empty);
        }

        public static void CacheZoneCenter(int hash, float3 center)
        {
            if (hash != 0) HashToCenter[hash] = center;
        }

        public static float3 GetZoneCenter(int hash)
        {
            return HashToCenter.GetValueOrDefault(hash, float3.zero);
        }

        public static bool AreZonesSameLocation(int hashA, int hashB)
        {
            if (hashA == 0 || hashB == 0) return false;
            var centerA = GetZoneCenter(hashA);
            var centerB = GetZoneCenter(hashB);
            return math.distancesq(centerA, centerB) < 0.01f;
        }
    }
}