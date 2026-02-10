using System;
using Stunlock.Core;
using Unity.Entities;
using VAutomationCore.Core.Logging;

namespace VAutomationCore.Core.ECS
{
    /// <summary>
    /// Extension methods for PrefabGUID operations.
    /// Provides safe access to PrefabGUID data including name resolution.
    /// </summary>
    public static class PrefabGUIDExtensions
    {
        private static readonly CoreLogger _log = new CoreLogger("PrefabGUIDExtensions");
        
        /// <summary>
        /// Gets the prefab name from a PrefabGUID using cached lookup.
        /// Falls back to empty string if not found.
        /// </summary>
        public static string Name(this PrefabGUID prefabGuid)
        {
            try
            {
                var hash = prefabGuid.GuidHash;
                if (hash == 0) return string.Empty;
                
                // Try to resolve name from prefab registry
                return GetPrefabName(hash);
            }
            catch (Exception ex)
            {
                _log.Exception(ex);
                return string.Empty;
            }
        }
        
        /// <summary>
        /// Gets the prefab name from a GUID hash.
        /// </summary>
        private static string GetPrefabName(int guidHash)
        {
            try
            {
                // This would typically use a prefab registry or database lookup
                // For now, return empty string as placeholder
                return string.Empty;
            }
            catch (Exception ex)
            {
                _log.Exception(ex);
                return string.Empty;
            }
        }
        
        /// <summary>
        /// Checks if a PrefabGUID is valid (non-zero hash).
        /// </summary>
        public static bool IsValid(this PrefabGUID prefabGuid)
        {
            return prefabGuid.GuidHash != 0;
        }
        
        /// <summary>
        /// Gets the GUID hash as a string for display/logging.
        /// </summary>
        public static string ToHexString(this PrefabGUID prefabGuid)
        {
            return $"0x{prefabGuid.GuidHash:X8}";
        }
    }
}
