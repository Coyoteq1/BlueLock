using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Unity.Mathematics;

namespace VAuto.Zone.Models
{
    /// <summary>
    /// Represents a zone definition for JSON configuration.
    /// </summary>
    public class ZoneDefinition
    {
        /// <summary>
        /// Unique identifier for the zone.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Optional tags used to drive behavior (e.g. "sandbox").
        /// </summary>
        public List<string> Tags { get; set; } = new List<string>();

        /// <summary>
        /// Display name for the zone.
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Shape of the zone (e.g., "Circle").
        /// </summary>
        public string Shape { get; set; } = "Circle";

        /// <summary>
        /// Center X coordinate of the zone.
        /// </summary>
        public float CenterX { get; set; }

        /// <summary>
        /// Center Y coordinate of the zone (used for terrain-aware spawns).
        /// </summary>
        public float CenterY { get; set; }

        /// <summary>
        /// Center Z coordinate of the zone.
        /// </summary>
        public float CenterZ { get; set; }

        /// <summary>
        /// Radius of the zone (for circular zones).
        /// </summary>
        public float Radius { get; set; }

        /// <summary>
        /// Minimum X boundary for rectangular zones.
        /// </summary>
        public float MinX { get; set; }

        /// <summary>
        /// Maximum X boundary for rectangular zones.
        /// </summary>
        public float MaxX { get; set; }

        /// <summary>
        /// Minimum Z boundary for rectangular zones.
        /// </summary>
        public float MinZ { get; set; }

        /// <summary>
        /// Maximum Z boundary for rectangular zones.
        /// </summary>
        public float MaxZ { get; set; }

        /// <summary>
        /// Kit ID to apply when entering this zone (legacy name).
        /// </summary>
        public string KitToApplyId { get; set; }

        /// <summary>
        /// Kit ID to apply when entering this zone (preferred).
        /// </summary>
        public string KitId { get; set; }

        /// <summary>
        /// Optional ability preset slots for this zone (T, C, R, SPACE).
        /// Values may be prefab names or numeric GuidHash strings.
        /// </summary>
        public string[] AbilityPresetSlots { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Optional per-zone override for sandbox progression unlock behavior.
        /// Null means use plugin-level default.
        /// </summary>
        public bool? SandboxUnlockEnabled { get; set; }

        /// <summary>
        /// Glow effect color in hex format (e.g., "#FF0000").
        /// </summary>
        public string GlowEffectColorHex { get; set; }

        /// <summary>
        /// Prefab ID for the glow effect.
        /// </summary>
        public int GlowPrefabId { get; set; }

        /// <summary>
        /// Prefab name for the glow effect.
        /// </summary>
        public string GlowPrefab { get; set; }

        /// <summary>
        /// Prefab ID for the border glow effect.
        /// </summary>
        public int BorderGlowPrefabId { get; set; }

        /// <summary>
        /// Prefab name for the border glow effect.
        /// </summary>
        public string BorderGlowPrefab { get; set; }

        /// <summary>
        /// Height at which to spawn glow effects.
        /// </summary>
        public float GlowSpawnHeight { get; set; }

        /// <summary>
        /// Whether to automatically apply glow with zone.
        /// </summary>
        public bool AutoGlowWithZone { get; set; }

        /// <summary>
        /// Optional prefab name for the glow tile grid (per-zone override).
        /// </summary>
        public string GlowTilePrefab { get; set; } = string.Empty;

        /// <summary>
        /// Optional prefab GUID hash for the glow tile grid (numeric fallback).
        /// </summary>
        public int GlowTilePrefabId { get; set; }

        /// <summary>
        /// Spacing in meters between glow tiles along the zone border/grid.
        /// </summary>
        public float GlowTileSpacing { get; set; } = 3f;

        /// <summary>
        /// Height offset applied to glow tiles above the zone center/terrain.
        /// </summary>
        public float GlowTileHeightOffset { get; set; } = 0.3f;

        /// <summary>
        /// Rotation offset in degrees applied when distributing glow tiles.
        /// </summary>
        public float GlowTileRotationDegrees { get; set; }

        /// <summary>
        /// Whether glow tiles should spawn for this zone.
        /// </summary>
        public bool GlowTileEnabled { get; set; } = true;

        /// <summary>
        /// Should glow tiles auto spawn when the zone is activated (first player enters).
        /// </summary>
        public bool GlowTileAutoSpawnOnEnter { get; set; } = true;

        /// <summary>
        /// Should glow tiles automatically clear/reset when the zone becomes empty.
        /// </summary>
        public bool GlowTileAutoSpawnOnReset { get; set; } = true;

        /// <summary>
        /// Message to display when entering the zone.
        /// </summary>
        public string EnterMessage { get; set; }

        /// <summary>
        /// Message to display when exiting the zone.
        /// </summary>
        public string ExitMessage { get; set; }

        /// <summary>
        /// Whether to teleport player on zone enter.
        /// </summary>
        public bool TeleportOnEnter { get; set; }

        /// <summary>
        /// X coordinate for teleport destination.
        /// </summary>
        public float TeleportX { get; set; }

        /// <summary>
        /// Y coordinate for teleport destination.
        /// </summary>
        public float TeleportY { get; set; }

        /// <summary>
        /// Z coordinate for teleport destination.
        /// </summary>
        public float TeleportZ { get; set; }

        /// <summary>
        /// Whether to return player to original position on zone exit.
        /// </summary>
        public bool ReturnOnExit { get; set; }

        /// <summary>
        /// Border spawning configuration (authoritative for zone borders).
        /// </summary>
        public ZoneBorderConfig Border { get; set; }

        /// <summary>
        /// List of schematic IDs granted in this zone.
        /// </summary>
        public List<string> Schematics { get; set; } = new List<string>();

        /// <summary>
        /// Legacy template list for backward compatibility.
        /// </summary>
        [JsonPropertyName("templateList")]
        public List<string> LegacyTemplates { get; set; } = new List<string>();

        /// <summary>
        /// Typed template map for the zone (e.g. arenaTM, trapTM, bossTM).
        /// </summary>
        [JsonPropertyName("templates")]
        public Dictionary<string, string> Templates { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Character name of the zone holder (for arena damage immunity).
        /// Empty string means no holder (everyone takes damage).
        /// </summary>
        public string HolderName { get; set; } = string.Empty;

        /// <summary>
        /// Whether this zone should apply damage-based arena mechanics.
        /// When true, players in this zone take damage based on zone configuration.
        /// Holders are immune. Damage decays over time.
        /// </summary>
        public bool IsArenaZone { get; set; }

        /// <summary>
        /// Check if a position is inside this zone.
        /// </summary>
        public bool IsInside(float x, float z)
        {
            if (Shape.Equals("Circle", StringComparison.OrdinalIgnoreCase))
            {
                var dx = x - CenterX;
                var dz = z - CenterZ;
                return (dx * dx + dz * dz) <= (Radius * Radius);
            }
            // Default to circle for unknown shapes
            var dist = math.distance(new float2(x, z), new float2(CenterX, CenterZ));
            return dist <= Radius;
        }
    }
}
