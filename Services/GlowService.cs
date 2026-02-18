using System;
using VAuto.Zone.Core;

namespace VAuto.Zone.Services
{
    public static class GlowService
    {
        public static int[] GetValidatedGlowBuffHashes()
        {
            return Array.Empty<int>();
        }

        public static bool TryResolve(string token, out int guidHash)
        {
            guidHash = 0;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            var lookup = token.Trim();
            if (int.TryParse(lookup, out var numeric) && numeric != 0)
            {
                guidHash = numeric;
                return true;
            }

            if (PrefabReferenceCatalog.TryResolve(lookup, out var guid))
            {
                guidHash = guid.GuidHash;
                return true;
            }

            return false;
        }
    }
}
