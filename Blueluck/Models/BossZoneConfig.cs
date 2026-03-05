using System.Text.Json.Serialization;

namespace Blueluck.Models
{
    /// <summary>
    /// BossZone-specific configuration extending base ZoneDefinition.
    /// </summary>
    public class BossZoneConfig : ZoneDefinition
    {
        public BossZoneConfig()
        {
            Type = "BossZone";
        }

        [JsonPropertyName("bossPrefab")]
        public string BossPrefab { get; set; } = string.Empty;

        [JsonPropertyName("bossQuantity")]
        public int BossQuantity { get; set; } = 1;

        [JsonPropertyName("respawnTimeMinutes")]
        public int RespawnTimeMinutes { get; set; } = 30;

        [JsonPropertyName("enableSubclanCoop")]
        public bool EnableSubclanCoop { get; set; } = true;

        [JsonPropertyName("bossHealthMultiplier")]
        public float BossHealthMultiplier { get; set; } = 1.0f;

        [JsonPropertyName("bossDamageMultiplier")]
        public float BossDamageMultiplier { get; set; } = 1.0f;

        [JsonPropertyName("announceSpawn")]
        public bool AnnounceSpawn { get; set; } = true;

        [JsonPropertyName("announceDeath")]
        public bool AnnounceDeath { get; set; } = true;

        [JsonPropertyName("randomSpawn")]
        public bool RandomSpawn { get; set; } = false;

        [JsonPropertyName("spawnPositions")]
        public float[][]? SpawnPositions { get; set; }

        /// <summary>
        /// If true, do not save/restore player progress (sanctuary mode).
        /// </summary>
        [JsonPropertyName("noProgress")]
        public bool NoProgress { get; set; } = true;

        /// <summary>
        /// List of ability sets players can choose from in this boss zone.
        /// </summary>
        [JsonPropertyName("abilitySets")]
        public string[]? AbilitySets { get; set; }

        /// <summary>
        /// Allow multiple boss zones in the same area.
        /// </summary>
        [JsonPropertyName("allowMultipleZones")]
        public bool AllowMultipleZones { get; set; } = true;
    }
}
