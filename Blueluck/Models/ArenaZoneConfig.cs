using System.Text.Json.Serialization;

namespace Blueluck.Models
{
    /// <summary>
    /// ArenaZone-specific configuration extending base ZoneDefinition.
    /// </summary>
    public class ArenaZoneConfig : ZoneDefinition
    {
        public ArenaZoneConfig()
        {
            Type = "ArenaZone";
        }

        [JsonPropertyName("pvpEnabled")]
        public bool PvpEnabled { get; set; } = true;

        [JsonPropertyName("allowEscape")]
        public bool AllowEscape { get; set; } = false;

        [JsonPropertyName("saveProgress")]
        public bool SaveProgress { get; set; } = true;

        [JsonPropertyName("cleanOnEnter")]
        public bool CleanOnEnter { get; set; } = true;

        [JsonPropertyName("restoreOnExit")]
        public bool RestoreOnExit { get; set; } = true;

        [JsonPropertyName("respawnEnabled")]
        public bool RespawnEnabled { get; set; } = true;

        [JsonPropertyName("respawnPoint")]
        public float[]? RespawnPoint { get; set; }

        [JsonPropertyName("showBorder")]
        public bool ShowBorder { get; set; } = true;

        [JsonPropertyName("borderColor")]
        public string BorderColor { get; set; } = "red";

        [JsonPropertyName("maxPlayers")]
        public int MaxPlayers { get; set; } = 0; // 0 = unlimited

        [JsonPropertyName("minPlayers")]
        public int MinPlayers { get; set; } = 0;

        [JsonPropertyName("matchDurationMinutes")]
        public int MatchDurationMinutes { get; set; } = 0; // 0 = unlimited
    }
}
