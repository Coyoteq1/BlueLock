using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace VAuto.Core.Services
{
    internal sealed class SandboxPendingContext
    {
        public string PlayerKey { get; set; } = string.Empty;
        public string CharacterName { get; set; } = string.Empty;
        public ulong PlatformId { get; set; }
        public string ZoneId { get; set; } = string.Empty;
        public string SnapshotId { get; set; } = string.Empty;
        public DateTime CapturedUtc { get; set; }
        public BaselineRow[] ComponentRows { get; set; } = Array.Empty<BaselineRow>();
        public ZoneEntityEntry[] PreEnterZoneEntities { get; set; } = Array.Empty<ZoneEntityEntry>();
    }

    internal sealed class ZoneEntityEntry
    {
        public int EntityIndex { get; set; }
        public int EntityVersion { get; set; }
        public long PrefabGuidHash { get; set; }
        public string PrefabName { get; set; } = string.Empty;
        public float PosX { get; set; }
        public float PosY { get; set; }
        public float PosZ { get; set; }
    }

    internal sealed class BaselineRow
    {
        public int Version { get; set; } = 1;
        public string SnapshotId { get; set; } = string.Empty;
        public string PlayerKey { get; set; } = string.Empty;
        public string CharacterName { get; set; } = string.Empty;
        public ulong PlatformId { get; set; }
        public string ZoneId { get; set; } = string.Empty;
        public DateTime CapturedUtc { get; set; }
        public string RowType { get; set; } = "component";
        public string ComponentType { get; set; } = string.Empty;
        public string AssemblyQualifiedType { get; set; } = string.Empty;
        public bool Existed { get; set; }
        public string PayloadBase64 { get; set; } = string.Empty;
        public string PayloadHash { get; set; } = string.Empty;
    }

    internal sealed class DeltaRow
    {
        public int Version { get; set; } = 1;
        public string SnapshotId { get; set; } = string.Empty;
        public string PlayerKey { get; set; } = string.Empty;
        public string CharacterName { get; set; } = string.Empty;
        public ulong PlatformId { get; set; }
        public string ZoneId { get; set; } = string.Empty;
        public DateTime CapturedUtc { get; set; }
        public string RowType { get; set; } = string.Empty;
        public string Operation { get; set; } = string.Empty;
        public string ComponentType { get; set; } = string.Empty;
        public string BeforePayloadBase64 { get; set; } = string.Empty;
        public string AfterPayloadBase64 { get; set; } = string.Empty;
        public long TechGuid { get; set; }
        public string TechName { get; set; } = string.Empty;
        public int EntityIndex { get; set; }
        public int EntityVersion { get; set; }
        public long PrefabGuid { get; set; }
        public string PrefabName { get; set; } = string.Empty;
        public float PosX { get; set; }
        public float PosY { get; set; }
        public float PosZ { get; set; }
    }

    internal sealed class FlowSnapshot
    {
        public string FlowId { get; set; } = string.Empty;
        public string ZoneId { get; set; } = string.Empty;
        public string StepId { get; set; } = string.Empty;
        public ulong PlatformId { get; set; }
        public int PlayerEntityIndex { get; set; }
        public DateTime CapturedUtc { get; set; }
        public string Result { get; set; } = string.Empty;
    }

    internal sealed class ActionSnapshot
    {
        public string ActionName { get; set; } = string.Empty;
        public string ZoneId { get; set; } = string.Empty;
        public string StepId { get; set; } = string.Empty;
        public ulong PlatformId { get; set; }
        public int PlayerEntityIndex { get; set; }
        public DateTime CapturedUtc { get; set; }
        public string Result { get; set; } = string.Empty;
    }

    internal sealed class SandboxBaselineSnapshot
    {
        public string PlayerKey { get; set; } = string.Empty;
        public string CharacterName { get; set; } = string.Empty;
        public ulong PlatformId { get; set; }
        public string ZoneId { get; set; } = string.Empty;
        public string SnapshotId { get; set; } = string.Empty;
        public DateTime CapturedUtc { get; set; }
        public BaselineRow[] Rows { get; set; } = Array.Empty<BaselineRow>();
    }

    internal sealed class SandboxDeltaSnapshot
    {
        public string PlayerKey { get; set; } = string.Empty;
        public string CharacterName { get; set; } = string.Empty;
        public ulong PlatformId { get; set; }
        public string ZoneId { get; set; } = string.Empty;
        public string SnapshotId { get; set; } = string.Empty;
        public DateTime CapturedUtc { get; set; }
        public DeltaRow[] Rows { get; set; } = Array.Empty<DeltaRow>();
    }

    internal static class SandboxSnapshotStore
    {
        private static readonly object Sync = new();
        private static readonly Dictionary<string, SandboxPendingContext> PendingContexts = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, SandboxBaselineSnapshot> ActiveBaselines = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, SandboxDeltaSnapshot> ActiveDeltas = new(StringComparer.Ordinal);
        private static readonly List<FlowSnapshot> FlowSnapshots = new();
        private static readonly List<ActionSnapshot> ActionSnapshots = new();
        private static bool _dirty;

        // ============== Diagnostic Methods ==============
        
        /// <summary>
        /// Get snapshot counts for diagnostics.
        /// </summary>
        public static (int activeBaselines, int pendingContexts, int activeDeltas, bool isDirty) GetSnapshotCounts()
        {
            lock (Sync)
            {
                return (ActiveBaselines.Count, PendingContexts.Count, ActiveDeltas.Count, _dirty);
            }
        }
        
        /// <summary>
        /// Get baseline snapshot info for a player.
        /// </summary>
        public static (string? zoneId, int rowCount, DateTime? capturedUtc) GetBaselineInfo(ulong platformId)
        {
            lock (Sync)
            {
                var baseline = ActiveBaselines.Values.FirstOrDefault(b => b.PlatformId == platformId);
                if (baseline != null)
                {
                    return (baseline.ZoneId, baseline.Rows?.Length ?? 0, baseline.CapturedUtc);
                }
                return (null, 0, null);
            }
        }
        
        /// <summary>
        /// Check if player has active baseline.
        /// </summary>
        public static bool HasActiveBaseline(ulong platformId)
        {
            lock (Sync)
            {
                return ActiveBaselines.Values.Any(b => b.PlatformId == platformId);
            }
        }

        public static void ClearAll()
        {
            lock (Sync)
            {
                PendingContexts.Clear();
                ActiveBaselines.Clear();
                ActiveDeltas.Clear();
                FlowSnapshots.Clear();
                ActionSnapshots.Clear();
                _dirty = false;
            }
        }

        public static string UpsertPendingContext(SandboxPendingContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            lock (Sync)
            {
                var key = ResolvePlayerKeyNoLock(context.CharacterName, context.PlatformId, createIfMissing: true);
                context.PlayerKey = key;
                PendingContexts[key] = context;
                return key;
            }
        }

        public static bool TryTakePendingContext(string characterName, ulong platformId, out string playerKey, out SandboxPendingContext? context)
        {
            lock (Sync)
            {
                context = null;
                playerKey = string.Empty;

                if (!TryResolveExistingPlayerKeyNoLock(characterName, platformId, out var key))
                {
                    return false;
                }

                if (!PendingContexts.TryGetValue(key, out context) || context == null)
                {
                    return false;
                }

                playerKey = key;
                PendingContexts.Remove(key);
                return true;
            }
        }

        public static bool TryGetActiveSnapshots(string characterName, ulong platformId, out string playerKey, out SandboxBaselineSnapshot? baseline, out SandboxDeltaSnapshot? delta)
        {
            lock (Sync)
            {
                baseline = null;
                delta = null;
                playerKey = string.Empty;

                if (!TryResolveExistingPlayerKeyNoLock(characterName, platformId, out var key))
                {
                    return false;
                }

                ActiveBaselines.TryGetValue(key, out baseline);
                ActiveDeltas.TryGetValue(key, out delta);
                playerKey = key;
                return baseline != null || delta != null;
            }
        }

        public static void PutActiveSnapshots(string playerKey, SandboxBaselineSnapshot baseline, SandboxDeltaSnapshot delta)
        {
            if (string.IsNullOrWhiteSpace(playerKey))
            {
                throw new ArgumentException("Player key is required.", nameof(playerKey));
            }

            lock (Sync)
            {
                ActiveBaselines[playerKey] = baseline;
                ActiveDeltas[playerKey] = delta;
            }
        }

        public static void RemoveActiveSnapshots(string playerKey)
        {
            if (string.IsNullOrWhiteSpace(playerKey))
            {
                return;
            }

            lock (Sync)
            {
                ActiveBaselines.Remove(playerKey);
                ActiveDeltas.Remove(playerKey);
            }
        }

        public static void ImportActiveSnapshots(IEnumerable<SandboxBaselineSnapshot> baselines, IEnumerable<SandboxDeltaSnapshot> deltas, bool markDirty)
        {
            lock (Sync)
            {
                ActiveBaselines.Clear();
                ActiveDeltas.Clear();
                PendingContexts.Clear();

                foreach (var baseline in baselines ?? Array.Empty<SandboxBaselineSnapshot>())
                {
                    if (baseline == null || string.IsNullOrWhiteSpace(baseline.PlayerKey))
                    {
                        continue;
                    }

                    ActiveBaselines[baseline.PlayerKey] = baseline;
                }

                foreach (var delta in deltas ?? Array.Empty<SandboxDeltaSnapshot>())
                {
                    if (delta == null || string.IsNullOrWhiteSpace(delta.PlayerKey))
                    {
                        continue;
                    }

                    ActiveDeltas[delta.PlayerKey] = delta;
                }

                _dirty = markDirty;
            }
        }

        public static SandboxBaselineSnapshot[] GetActiveBaselines()
        {
            lock (Sync)
            {
                return ActiveBaselines.Values.ToArray();
            }
        }

        public static SandboxDeltaSnapshot[] GetActiveDeltas()
        {
            lock (Sync)
            {
                return ActiveDeltas.Values.ToArray();
            }
        }

        public static FlowSnapshot[] GetFlowSnapshots()
        {
            lock (Sync)
            {
                return FlowSnapshots.ToArray();
            }
        }

        public static ActionSnapshot[] GetActionSnapshots()
        {
            lock (Sync)
            {
                return ActionSnapshots.ToArray();
            }
        }

        public static void RecordFlowSnapshot(FlowSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            lock (Sync)
            {
                FlowSnapshots.Add(snapshot);
                _dirty = true;
            }
        }

        public static void RecordActionSnapshot(ActionSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            lock (Sync)
            {
                ActionSnapshots.Add(snapshot);
                _dirty = true;
            }
        }

        public static void MarkDirty()
        {
            lock (Sync)
            {
                _dirty = true;
            }
        }

        public static void MarkClean()
        {
            lock (Sync)
            {
                _dirty = false;
            }
        }

        public static bool IsDirty
        {
            get
            {
                lock (Sync)
                {
                    return _dirty;
                }
            }
        }

        public static string GetPreferredPlayerKey(string characterName, ulong platformId)
        {
            lock (Sync)
            {
                return ResolvePlayerKeyNoLock(characterName, platformId, createIfMissing: true);
            }
        }

        private static bool TryResolveExistingPlayerKeyNoLock(string characterName, ulong platformId, out string key)
        {
            key = ResolvePlayerKeyNoLock(characterName, platformId, createIfMissing: false);
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            if (PendingContexts.ContainsKey(key) || ActiveBaselines.ContainsKey(key) || ActiveDeltas.ContainsKey(key))
            {
                return true;
            }

            // Fallback: match by platform id when character name changed.
            foreach (var pending in PendingContexts)
            {
                if (pending.Value.PlatformId == platformId)
                {
                    key = pending.Key;
                    return true;
                }
            }

            foreach (var active in ActiveBaselines)
            {
                if (active.Value.PlatformId == platformId)
                {
                    key = active.Key;
                    return true;
                }
            }

            foreach (var active in ActiveDeltas)
            {
                if (active.Value.PlatformId == platformId)
                {
                    key = active.Key;
                    return true;
                }
            }

            return false;
        }

        private static string ResolvePlayerKeyNoLock(string characterName, ulong platformId, bool createIfMissing)
        {
            var normalizedName = NormalizeName(characterName, platformId);
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                return platformId.ToString(CultureInfo.InvariantCulture);
            }

            if (TryTryUsePrimaryNoLock(normalizedName, platformId, out var key))
            {
                return key;
            }

            if (!createIfMissing)
            {
                return key;
            }

            var collisionKey = $"{normalizedName}|{platformId.ToString(CultureInfo.InvariantCulture)}";
            return collisionKey;
        }

        private static bool TryTryUsePrimaryNoLock(string normalizedName, ulong platformId, out string key)
        {
            key = normalizedName;
            if (TryKeyMatchesPlatformNoLock(normalizedName, platformId))
            {
                return true;
            }

            var collisionKey = $"{normalizedName}|{platformId.ToString(CultureInfo.InvariantCulture)}";
            if (TryKeyExistsNoLock(collisionKey))
            {
                key = collisionKey;
                return true;
            }

            if (!TryKeyExistsNoLock(normalizedName))
            {
                key = normalizedName;
                return true;
            }

            return false;
        }

        private static bool TryKeyMatchesPlatformNoLock(string key, ulong platformId)
        {
            if (PendingContexts.TryGetValue(key, out var pending))
            {
                return pending.PlatformId == platformId;
            }

            if (ActiveBaselines.TryGetValue(key, out var baseline))
            {
                return baseline.PlatformId == platformId;
            }

            if (ActiveDeltas.TryGetValue(key, out var delta))
            {
                return delta.PlatformId == platformId;
            }

            return false;
        }

        private static bool TryKeyExistsNoLock(string key)
            => PendingContexts.ContainsKey(key) || ActiveBaselines.ContainsKey(key) || ActiveDeltas.ContainsKey(key);

        private static string NormalizeName(string characterName, ulong platformId)
        {
            var normalized = (characterName ?? string.Empty).Trim();
            return normalized.Length == 0 ? platformId.ToString(CultureInfo.InvariantCulture) : normalized;
        }
    }
}
