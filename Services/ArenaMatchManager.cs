using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using VAuto.Zone.Core;
using VAuto.Zone.Core.Components;

namespace VAuto.Zone.Services
{
    /// <summary>
    /// Configuration data used when starting a match.
    /// </summary>
    public sealed class MatchConfig
    {
        private int _durationSeconds = DefaultDurationSeconds;

        /// <summary>
        /// Default duration applied when the caller does not override it.
        /// </summary>
        public const int DefaultDurationSeconds = 300;

        /// <summary>
        /// The minimum duration allowed to keep matches from expiring immediately.
        /// </summary>
        public const int MinimumDurationSeconds = 5;

        /// <summary>
        /// Duration of the match in seconds. Values below <see cref="MinimumDurationSeconds"/> are clamped.
        /// </summary>
        public int DurationSeconds
        {
            get => _durationSeconds;
            set => _durationSeconds = Math.Max(MinimumDurationSeconds, value);
        }

        /// <summary>
        /// When true, players may respawn while the match is running.
        /// </summary>
        public bool AllowRespawn { get; set; } = true;

        /// <summary>
        /// Optional human-readable label describing the match.
        /// </summary>
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// Controls why a match was ended.
    /// </summary>
    public enum MatchEndReason
    {
        AdminEnded,
        Timeout,
        ManualReset,
        SystemError,
        Unknown
    }

    /// <summary>
    /// Result returned by most arena match operations.
    /// </summary>
    public class MatchOperationResult
    {
        public bool Success { get; init; }

        public string Error { get; init; } = string.Empty;

        public static MatchOperationResult SuccessResult(string detail = null)
            => new() { Success = true, Error = detail ?? string.Empty };

        public static MatchOperationResult FailureResult(string error)
            => new() { Success = false, Error = error ?? string.Empty };
    }

    /// <summary>
    /// Extended reset information for calls that inspect spawned/cleared counts.
    /// </summary>
    public class ArenaResetResult : MatchOperationResult
    {
        public int MatchesCleared { get; init; }

        public int TemplateEntitiesRemoved { get; init; }

        public int GlowEntitiesRemoved { get; init; }

        public int DamageStatesRemoved { get; init; }

        public string Status { get; init; } = string.Empty;
    }

    /// <summary>
    /// Lightweight service that tracks basic arena match state for admin commands.
    /// </summary>
    public sealed class ArenaMatchManager
    {
        private static readonly Lazy<ArenaMatchManager> LazyInstance = new(() => new ArenaMatchManager());

        public static ArenaMatchManager Instance => LazyInstance.Value;

        private readonly Dictionary<string, ArenaMatchState> _activeMatches = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _sync = new();

        private ArenaMatchManager() { }

        public MatchOperationResult StartMatch(string zoneId, MatchConfig config)
        {
            if (string.IsNullOrWhiteSpace(zoneId))
            {
                return MatchOperationResult.FailureResult("Zone identifier is required.");
            }

            config ??= new MatchConfig();

            lock (_sync)
            {
                if (_activeMatches.TryGetValue(zoneId, out var existing) && existing.IsActive)
                {
                    return MatchOperationResult.FailureResult(
                        $"A match is already running in '{zoneId}' (started at {existing.StartedAt:O}).");
                }

                var state = new ArenaMatchState(zoneId, config);
                _activeMatches[zoneId] = state;
                ZoneCore.LogInfo($"Started arena match in '{zoneId}' for {config.DurationSeconds} seconds.");
                return MatchOperationResult.SuccessResult();
            }
        }

        public MatchOperationResult EndMatch(string zoneId, MatchEndReason reason)
        {
            if (string.IsNullOrWhiteSpace(zoneId))
            {
                return MatchOperationResult.FailureResult("Zone identifier is required.");
            }

            lock (_sync)
            {
                if (!_activeMatches.TryGetValue(zoneId, out var state))
                {
                    return MatchOperationResult.FailureResult($"No active arena match found for '{zoneId}'.");
                }

                state.MarkEnded(reason);
                _activeMatches.Remove(zoneId);
            }

            ZoneCore.LogInfo($"Ended arena match in '{zoneId}' (reason={reason}).");
            return MatchOperationResult.SuccessResult();
        }

        public ArenaResetResult ResetArena(string zoneId, EntityManager entityManager)
        {
            if (entityManager == default)
            {
                ZoneCore.LogWarning($"Cannot reset arena '{zoneId}': EntityManager is unavailable.");
                return new ArenaResetResult
                {
                    Success = false,
                    Error = "EntityManager is not available.",
                    MatchesCleared = 0,
                    TemplateEntitiesRemoved = 0,
                    GlowEntitiesRemoved = 0,
                    DamageStatesRemoved = 0,
                    Status = "EntityManager is not available."
                };
            }

            var cleared = 0;
            var targetDescription = string.IsNullOrWhiteSpace(zoneId) ? "all arenas" : $"arena '{zoneId}'";

            lock (_sync)
            {
                if (string.IsNullOrWhiteSpace(zoneId))
                {
                    cleared = _activeMatches.Count;
                    _activeMatches.Clear();
                }
                else if (_activeMatches.Remove(zoneId))
                {
                    cleared = 1;
                }
            }

            ZoneCore.LogInfo($"Reset {targetDescription}. Cleared {cleared} tracked match(es).");
            var templateEntitiesRemoved = ZoneTemplateService.ClearAllZoneTemplates(zoneId, entityManager);
            var glowEntitiesRemoved = GlowTileService.ClearGlowTiles(zoneId, entityManager);
            var damageStatesRemoved = CleanupArenaDamageStates(zoneId, entityManager);
            var status = $"Reset {targetDescription}: cleared {cleared} active match(es), removed {templateEntitiesRemoved} template entity(ies), cleaned {glowEntitiesRemoved} glow tiles, reset {damageStatesRemoved} damage states.";

            return new ArenaResetResult
            {
                Success = true,
                Error = string.Empty,
                MatchesCleared = cleared,
                TemplateEntitiesRemoved = templateEntitiesRemoved,
                GlowEntitiesRemoved = glowEntitiesRemoved,
                DamageStatesRemoved = damageStatesRemoved,
                Status = status
            };
        }

        internal IReadOnlyCollection<string> GetActiveArenaIds()
        {
            lock (_sync)
            {
                return new List<string>(_activeMatches.Keys);
            }
        }

        private static int CleanupArenaDamageStates(string zoneId, EntityManager entityManager)
        {
            if (entityManager == default)
            {
                return 0;
            }

            var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<ArenaDamageState>());
            if (query.IsEmptyIgnoreFilter)
            {
                query.Dispose();
                return 0;
            }

            var targetHash = string.IsNullOrWhiteSpace(zoneId) ? null : (int?)ArenaMatchUtilities.StableHash(zoneId);
            var removed = 0;
            var entities = query.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
            {
                if (!entityManager.Exists(entity))
                {
                    continue;
                }

                var damageState = entityManager.GetComponentData<ArenaDamageState>(entity);
                if (targetHash.HasValue && damageState.ZoneIdHash != targetHash.Value)
                {
                    continue;
                }

                entityManager.RemoveComponent<ArenaDamageState>(entity);
                removed++;
            }

            entities.Dispose();
            query.Dispose();
            return removed;
        }
    }

    internal sealed class ArenaMatchState
    {
        public string ZoneId { get; }

        public MatchConfig Config { get; }

        public DateTime StartedAt { get; }

        public DateTime EndsAt { get; }

        public bool IsActive { get; private set; } = true;

        public MatchEndReason EndReason { get; private set; }

        public ArenaMatchState(string zoneId, MatchConfig config)
        {
            ZoneId = zoneId;
            Config = config;
            StartedAt = DateTime.UtcNow;
            EndsAt = StartedAt.AddSeconds(Math.Max(MatchConfig.MinimumDurationSeconds, config.DurationSeconds));
        }

        public void MarkEnded(MatchEndReason reason)
        {
            if (!IsActive)
            {
                return;
            }

            IsActive = false;
            EndReason = reason;
        }
    }
}
