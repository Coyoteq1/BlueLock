using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using VAuto.Zone.Core;
using VAuto.Zone.Models;

namespace VAuto.Zone.Services
{
    public class ArenaMatchManager
    {
        private static ArenaMatchManager _instance;
        public static ArenaMatchManager Instance => _instance ??= new ArenaMatchManager();

        private readonly Dictionary<string, MatchState> _matchStates = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<Entity>> _matchParticipants = new(StringComparer.OrdinalIgnoreCase);

        public MatchStartResult StartMatch(string zoneId, MatchConfig config)
        {
            if (string.IsNullOrWhiteSpace(zoneId))
            {
                return new MatchStartResult { Success = false, Error = "Zone identifier required" };
            }

            if (_matchStates.TryGetValue(zoneId, out var existing) && existing.Phase == MatchPhase.Active)
            {
                return new MatchStartResult { Success = false, Error = "Match already active" };
            }

            var em = ZoneCore.EntityManager;
            var reset = ResetArena(zoneId, em);

            var state = new MatchState
            {
                ZoneId = zoneId,
                Config = config ?? new MatchConfig(),
                Phase = MatchPhase.Active,
                StartedAt = DateTime.UtcNow
            };

            _matchStates[zoneId] = state;
            return new MatchStartResult
            {
                Success = reset.Success,
                ResetResult = reset,
                Error = reset.Success ? string.Empty : reset.Error
            };
        }

        public MatchEndResult EndMatch(string zoneId, MatchEndReason reason)
        {
            if (!_matchStates.TryGetValue(zoneId, out var state))
            {
                return new MatchEndResult { Success = false, Error = "No active match" };
            }

            state.Phase = MatchPhase.Ending;
            state.EndedAt = DateTime.UtcNow;
            state.Phase = MatchPhase.Cooldown;

            return new MatchEndResult { Success = true };
        }

        public ResetResult ResetArena(string zoneId, EntityManager em)
        {
            var result = new ResetResult
            {
                ZoneId = zoneId
            };

            try
            {
                var clearedTemplates = ZoneTemplateService.ClearAllZoneTemplates(zoneId, em);
                var clearedGlow = GlowTileService.ClearGlowTiles(zoneId, em);
                var templateResults = ZoneTemplateService.SpawnAllZoneTemplates(zoneId, em);
                var spawnedTemplates = templateResults.Sum(r => r.EntityCount);
                var glowSpawn = GlowTileService.SpawnGlowTiles(zoneId, em);
                var spawnedGlow = glowSpawn.Success ? glowSpawn.EntityCount : 0;

                result.EntitiesCleared = clearedTemplates + clearedGlow;
                result.EntitiesSpawned = spawnedTemplates + spawnedGlow;
                result.SpawnResults = templateResults;
                result.GlowResult = glowSpawn;
                result.Success = templateResults.Count > 0 || glowSpawn.Success;
                result.Error = glowSpawn.Success ? string.Empty : glowSpawn.Error;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
            }
            finally
            {
                _matchStates.Remove(zoneId);
                _matchParticipants.Remove(zoneId);
            }

            return result;
        }
    }

    public class MatchStartResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public ResetResult ResetResult { get; set; }
    }

    public class MatchEndResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }
    }

    public class ResetResult
    {
        public string ZoneId { get; set; }
        public bool Success { get; set; }
        public string Error { get; set; }
        public int EntitiesCleared { get; set; }
        public int EntitiesSpawned { get; set; }
        public List<TemplateSpawnResult> SpawnResults { get; set; } = new();
        public TemplateSpawnResult GlowResult { get; set; }
    }

    public enum MatchPhase
    {
        Idle,
        Starting,
        Active,
        Ending,
        Cooldown
    }

    public enum MatchEndReason
    {
        AdminEnded,
        TimeExpired,
        ManualReset
    }

    public class MatchState
    {
        public string ZoneId { get; set; }
        public MatchPhase Phase { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime EndedAt { get; set; }
        public MatchConfig Config { get; set; }
    }

    public class MatchConfig
    {
        public int DurationSeconds { get; set; } = 300;
        public int MaxParticipants { get; set; } = 10;
        public bool AutoReset { get; set; } = true;
        public List<string> TemplateTypesToReset { get; set; } = new() { "arenaTM", "trapTM" };
    }
}
