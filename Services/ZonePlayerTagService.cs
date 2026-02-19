using System;
using System.Collections.Generic;
using System.Linq;
using ProjectM;
using Stunlock.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace VAuto.Zone.Services;

/// <summary>
/// Service for managing player tags based on zone IDs.
/// Allows easy renaming of player tags while maintaining zone association.
/// </summary>
public static class ZonePlayerTagService
{
    private static Action<string> _logInfo;
    private static Action<string> _logWarning;

    private static readonly Dictionary<ulong, PlayerTagInfo> _playerTags = new();

    public static void Initialize(Action<string> logInfo, Action<string> logWarning)
    {
        _logInfo = logInfo;
        _logWarning = logWarning;
        _logInfo?.Invoke("[ZonePlayerTagService] Initialized");
    }

    /// <summary>
    /// Information about a player's zone tag.
    /// </summary>
    public class PlayerTagInfo
    {
        public ulong SteamId { get; set; }
        public string ZoneId { get; set; }
        public string CurrentTag { get; set; }
        public string OriginalName { get; set; }
        public DateTime TaggedAt { get; set; }
    }

    /// <summary>
    /// Apply a zone-based tag to a player.
    /// </summary>
    public static void ApplyTag(ulong steamId, string zoneId, string playerName, Entity characterEntity, EntityManager em)
    {
        // Create tag based on zone ID
        var tag = $"[{zoneId}]";

        // Store original name and tag info
        var tagInfo = new PlayerTagInfo
        {
            SteamId = steamId,
            ZoneId = zoneId,
            CurrentTag = tag,
            OriginalName = playerName,
            TaggedAt = DateTime.UtcNow
        };

        _playerTags[steamId] = tagInfo;
        _logInfo?.Invoke($"[ZonePlayerTagService] Applied tag '{tag}' to player {playerName} (steamId: {steamId}) for zone '{zoneId}'");
    }

    /// <summary>
    /// Remove the zone-based tag from a player.
    /// </summary>
    public static void RemoveTag(ulong steamId, Entity characterEntity, EntityManager em)
    {
        if (_playerTags.TryGetValue(steamId, out var tagInfo))
        {
            _logInfo?.Invoke($"[ZonePlayerTagService] Removed tag '{tagInfo.CurrentTag}' from player {tagInfo.OriginalName} (steamId: {steamId})");
            _playerTags.Remove(steamId);
        }
    }

    /// <summary>
    /// Rename a player's tag easily while keeping the same name.
    /// </summary>
    public static bool RenameTag(ulong steamId, string newTag)
    {
        if (_playerTags.TryGetValue(steamId, out var tagInfo))
        {
            var oldTag = tagInfo.CurrentTag;
            tagInfo.CurrentTag = newTag;
            _logInfo?.Invoke($"[ZonePlayerTagService] Renamed tag for {tagInfo.OriginalName} from '{oldTag}' to '{newTag}'");
            return true;
        }
        _logWarning?.Invoke($"[ZonePlayerTagService] Cannot rename tag - player {steamId} has no active tag");
        return false;
    }

    /// <summary>
    /// Get the current tag for a player.
    /// </summary>
    public static string GetTag(ulong steamId)
    {
        return _playerTags.TryGetValue(steamId, out var tagInfo) ? tagInfo.CurrentTag : null;
    }

    /// <summary>
    /// Get the zone ID associated with a player's tag.
    /// </summary>
    public static string GetZoneId(ulong steamId)
    {
        return _playerTags.TryGetValue(steamId, out var tagInfo) ? tagInfo.ZoneId : null;
    }

    /// <summary>
    /// Check if a player has an active zone tag.
    /// </summary>
    public static bool HasTag(ulong steamId)
    {
        return _playerTags.ContainsKey(steamId);
    }

    /// <summary>
    /// Get all active player tags.
    /// </summary>
    public static IReadOnlyDictionary<ulong, PlayerTagInfo> GetAllTags()
    {
        return _playerTags;
    }

    /// <summary>
    /// Clear all tags (used on plugin shutdown).
    /// </summary>
    public static void ClearAll()
    {
        _playerTags.Clear();
        _logInfo?.Invoke("[ZonePlayerTagService] Cleared all player tags");
    }
}
