using System;
using Stunlock.Core;

namespace VAuto.EndGameKit.Helpers
{
    /// <summary>
    /// Utility class for PrefabGUID operations.
    /// Provides safe GUID generation, parsing, and validation.
    /// </summary>
    public static class GuidHelper
    {
        /// <summary>
        /// Checks if a PrefabGUID is valid (non-zero).
        /// </summary>
        public static bool IsValid(PrefabGUID guid)
        {
            return guid.GuidHash != 0;
        }

        /// <summary>
        /// Checks if a long value represents a valid GUID.
        /// </summary>
        public static bool IsValid(long value)
        {
            return value != 0;
        }

        /// <summary>
        /// Creates a valid PrefabGUID from a long value.
        /// Returns false if the value is invalid.
        /// </summary>
        public static bool Create(long value, out PrefabGUID guid)
        {
            guid = new PrefabGUID((int)value);
            return IsValid(guid);
        }

        /// <summary>
        /// Creates a PrefabGUID from a long value (throws on invalid).
        /// </summary>
        public static PrefabGUID FromLong(long value)
        {
            if (value == 0)
            {
                throw new ArgumentException("GUID value cannot be zero", nameof(value));
            }
            return new PrefabGUID((int)value);
        }

        /// <summary>
        /// Safely tries to parse a string to PrefabGUID.
        /// </summary>
        public static bool TryParse(string input, out PrefabGUID guid)
        {
            guid = PrefabGUID.Empty;

            if (string.IsNullOrEmpty(input))
                return false;

            // Try parsing as long
            if (long.TryParse(input, out var value))
            {
                guid = new PrefabGUID((int)value);
                return true;
            }

            // Try parsing as GUID string (for systems that use GUID format)
            if (Guid.TryParse(input, out var systemGuid))
            {
                // Convert System.Guid to long (using first 8 bytes)
                var bytes = systemGuid.ToByteArray();
                var longValue = BitConverter.ToInt64(bytes, 0);
                guid = new PrefabGUID((int)longValue);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Generates a new unique GUID using timestamp and random.
        /// Useful for runtime-generated items.
        /// </summary>
        public static PrefabGUID GenerateUnique()
        {
            var timestamp = (int)(DateTime.UtcNow.Ticks % int.MaxValue);
            var random = new Random().Next(1, int.MaxValue);
            return new PrefabGUID((int)timestamp ^ random);
        }

        /// <summary>
        /// Creates an empty PrefabGUID.
        /// </summary>
        public static PrefabGUID Empty => PrefabGUID.Empty;

        /// <summary>
        /// Compares two PrefabGUIDs for equality.
        /// </summary>
        public static bool Equals(PrefabGUID a, PrefabGUID b)
        {
            return a.GuidHash == b.GuidHash;
        }

        /// <summary>
        /// Gets the hash code for a PrefabGUID.
        /// </summary>
        public static int GetHashCode(PrefabGUID guid)
        {
            return guid.GuidHash.GetHashCode();
        }

        /// <summary>
        /// Converts PrefabGUID to long value.
        /// </summary>
        public static long ToLong(PrefabGUID guid)
        {
            return guid.GuidHash;
        }

        /// <summary>
        /// Converts PrefabGUID to string representation.
        /// </summary>
        public static string ToString(PrefabGUID guid)
        {
            return guid.GuidHash.ToString();
        }

        /// <summary>
        /// Validates GUID against a list of known valid GUIDs.
        /// </summary>
        public static bool IsInList(PrefabGUID guid, params PrefabGUID[] validGuids)
        {
            foreach (var validGuid in validGuids)
            {
                if (Equals(guid, validGuid))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Validates GUID against a list of known valid long values.
        /// </summary>
        public static bool IsInList(PrefabGUID guid, params long[] validValues)
        {
            foreach (var value in validValues)
            {
                if (guid.GuidHash == value)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Gets a display string for debugging (shows first 8 chars of GUID).
        /// </summary>
        public static string ToDisplayString(PrefabGUID guid)
        {
            return $"[{guid.GuidHash}]";
        }

        /// <summary>
        /// Safely converts an array of long values to PrefabGUIDs.
        /// Filters out invalid (zero) values.
        /// </summary>
        public static PrefabGUID[] ToPrefabGuidArray(long[] values)
        {
            if (values == null)
                return Array.Empty<PrefabGUID>();

            var result = new PrefabGUID[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                result[i] = new PrefabGUID((int)values[i]);
            }
            return result;
        }

        /// <summary>
        /// Safely converts an array of PrefabGUIDs to long values.
        /// </summary>
        public static long[] ToLongArray(PrefabGUID[] guids)
        {
            if (guids == null)
                return Array.Empty<long>();

            var result = new long[guids.Length];
            for (int i = 0; i < guids.Length; i++)
            {
                result[i] = guids[i].GuidHash;
            }
            return result;
        }
    }
}
