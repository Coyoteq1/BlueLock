using System;

namespace VAuto.Zone.Services
{
    internal static class ArenaMatchUtilities
    {
        /// <summary>
        /// Stable hash that matches the one previously embedded in <see cref="Plugin"/>.
        /// Used to correlate arena zone identifiers with stored damage state hashes.
        /// </summary>
        public static int StableHash(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return 0;
            }

            unchecked
            {
                var hash = 2166136261u;
                foreach (var @char in value)
                {
                    hash ^= char.ToLowerInvariant(@char);
                    hash *= 16777619;
                }

                return (int)hash;
            }
        }
    }
}
