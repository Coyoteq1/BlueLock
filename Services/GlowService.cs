using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BepInEx;
using ProjectM;
using Stunlock.Core;
using Unity.Entities;
using VAuto.Zone.Data.DataType;
using VAuto.Zone.Core;

namespace VAuto.Zone.Services
{
    public static class GlowService
    {
        // Keep one legacy numeric fallback (older configs used this as "the" glow).
        // The full allowed glow catalog lives in Data/datatype/Glows.cs (codegen).
        public static readonly PrefabGUID DefaultGlow = new(-888209286);

        private static readonly ComponentType BuffType = ComponentType.ReadOnly<Buff>();

        // Built-in KindredSchematics-style defaults (safe starters when glowChoices.txt is missing).
        private static readonly (string Name, int GuidHash)[] BuiltInGlowChoices =
        {
            ("InkShadow", -1124645803),
            ("Cursed", 1425734039),
            ("Howl", -91451769),
            ("Chaos", 1163490655),
            ("Emerald", -1559874083),
            ("Poison", -1965215729),
            ("Agony", 1025643444),
            ("Light", 178225731)
        };

        private static readonly object Lock = new();
        private static Dictionary<string, int> _rawChoicesByName = new(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<string, PrefabGUID> _validatedChoicesByName = new(StringComparer.OrdinalIgnoreCase);
        private static PrefabGUID[] _validatedGlowBuffs = Array.Empty<PrefabGUID>();
        private static DateTime _lastRefreshUtc = DateTime.MinValue;

        private static string GlowChoicesPath =>
            Path.Combine(Paths.ConfigPath, "VAuto.Arena", "glowChoices.txt");

        public static void RefreshGlowChoices()
        {
            lock (Lock)
            {
                _rawChoicesByName = LoadRawChoices_NoLock();
                _validatedChoicesByName.Clear();
                _validatedGlowBuffs = Array.Empty<PrefabGUID>();
                _lastRefreshUtc = DateTime.UtcNow;
            }

            ZoneCore.LogInfo($"[GlowService] Loaded {_rawChoicesByName.Count} glow choice names. Validating...");

            // Best-effort validation; if world isn't ready yet, caller can retry later.
            if (!TryValidateNow())
            {
                ZoneCore.LogDebug("[GlowService] Validation deferred (world not ready). Will retry when glow buffs are requested.");
            }
        }

        public static PrefabGUID[] GetValidatedGlowBuffs()
        {
            if (_validatedGlowBuffs.Length > 0)
            {
                return _validatedGlowBuffs;
            }

            // Lazy-load the file and validate once the world is ready.
            if (_lastRefreshUtc == DateTime.MinValue)
            {
                RefreshGlowChoices();
            }

            if (!TryValidateNow())
            {
                ZoneCore.LogWarning("[GlowService] Validation deferred: ECS world not ready. Returning no glow buffs for this request.");
                // Do not force fallback glows here; caller can retry once world is ready.
                return Array.Empty<PrefabGUID>();
            }

            return _validatedGlowBuffs;
        }

        private static bool TryValidateNow()
        {
            try
            {
                var em = ZoneCore.EntityManager;
                if (em == default || em.World == null || !em.World.IsCreated)
                {
                    ZoneCore.LogDebug("[GlowService] Validation skipped: EntityManager world not ready");
                    return false;
                }

                Dictionary<string, int> raw;
                lock (Lock)
                {
                    raw = new Dictionary<string, int>(_rawChoicesByName, StringComparer.OrdinalIgnoreCase);
                }

                var validatedByName = new Dictionary<string, PrefabGUID>(StringComparer.OrdinalIgnoreCase);
                var validatedList = new List<PrefabGUID>(raw.Count);
                var seen = new HashSet<int>();
                var rejectedCount = 0;

                foreach (var kvp in raw)
                {
                    var name = kvp.Key;
                    var hash = kvp.Value;
                    if (hash == 0)
                    {
                        continue;
                    }

                    var guid = new PrefabGUID(hash);
                    
                    // Check if GUID resolves; require a prefab entity but avoid early timing rejections
                    if (!ZoneCore.TryGetPrefabEntity(guid, out var prefabEntity) || prefabEntity == Entity.Null)
                    {
                        ZoneCore.LogWarning($"[GlowService] Rejecting glow '{name}' ({hash}): prefab entity not found.");
                        rejectedCount++;
                        continue;
                    }

                    // Check if it's actually a Buff and currently tracked by the EntityManager
                    if (!em.Exists(prefabEntity) || !em.HasComponent(prefabEntity, BuffType))
                    {
                        ZoneCore.LogWarning($"[GlowService] Rejecting glow '{name}' ({hash}): resolved prefab has no Buff component or entity not tracked.");
                        rejectedCount++;
                        continue;
                    }

                    if (seen.Add(guid.GuidHash))
                    {
                        validatedList.Add(guid);
                    }

                    validatedByName[name] = guid;
                }

                // Deterministic fallback: if no validated glows, try DefaultGlow once
                if (validatedList.Count == 0)
                {
                    try
                    {
                        if (ZoneCore.TryGetPrefabEntity(DefaultGlow, out var defEntity) && defEntity != Entity.Null && em.Exists(defEntity) && em.HasComponent(defEntity, BuffType))
                        {
                            validatedList.Add(DefaultGlow);
                            validatedByName["Default"] = DefaultGlow;
                            ZoneCore.LogWarning($"[GlowService] No valid glow choices found; falling back to DefaultGlow ({DefaultGlow.GuidHash}).");
                        }
                    }
                    catch { /* ignore */ }
                }

                lock (Lock)
                {
                    _validatedChoicesByName = validatedByName;
                    _validatedGlowBuffs = validatedList.ToArray();
                }

                ZoneCore.LogInfo($"[GlowService] Validation complete: {_validatedGlowBuffs.Length} valid glows ({rejectedCount} rejected)");
                return _validatedGlowBuffs.Length > 0;
            }
            catch (Exception ex)
            {
                ZoneCore.LogWarning($"[GlowService] Validation failed: {ex.ToString()}");
                return false;
            }
        }

        private static Dictionary<string, int> LoadRawChoices_NoLock()
        {
            var raw = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            // Built-ins always present.
            for (var i = 0; i < BuiltInGlowChoices.Length; i++)
            {
                var (name, hash) = BuiltInGlowChoices[i];
                if (!string.IsNullOrWhiteSpace(name) && hash != 0)
                {
                    raw[name] = hash;
                }
            }

            var path = GlowChoicesPath;
            if (!File.Exists(path))
            {
                return raw;
            }

            try
            {
                foreach (var line in File.ReadAllLines(path))
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("#", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var p = trimmed.Split('=', 2);
                    if (p.Length != 2)
                    {
                        continue;
                    }

                    var name = p[0].Trim();
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    if (!int.TryParse(p[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var hash) || hash == 0)
                    {
                        continue;
                    }

                    raw[name] = hash;
                }
            }
            catch
            {
                // Ignore IO parse issues; caller still gets built-in defaults.
            }

            return raw;
        }

        public static bool TryResolve(string token, out PrefabGUID guid)
        {
            guid = PrefabGUID.Empty;
            if (string.IsNullOrWhiteSpace(token))
            {
                ZoneCore.LogWarning("[GlowService] TryResolve called with empty glow token.");
                return false;
            }

            token = token.Trim();

            // KindredSchematics glow choice names (from glowChoices.txt + built-ins)
            if (_validatedChoicesByName.TryGetValue(token, out var choiceGuid) && choiceGuid != PrefabGUID.Empty)
            {
                guid = choiceGuid;
                return true;
            }

            // Allow a single explicit numeric fallback (legacy token).
            if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var raw))
            {
                if (raw == DefaultGlow.GuidHash)
                {
                    guid = DefaultGlow;
                    return true;
                }

                ZoneCore.LogWarning($"[GlowService] Numeric glow token '{token}' rejected: only explicit default ({DefaultGlow.GuidHash}) is allowed.");
                return false;
            }

            // Prefer short names (whitelisted by the generated catalog).
            if (Glows.ByShortName.TryGetValue(token, out var prefabName) &&
                PrefabResolver.TryResolve(prefabName, out var resolved))
            {
                guid = resolved;
                return true;
            }

            // Allow full prefab names too, but only if they exist in the resolver.
            if (PrefabResolver.TryResolve(token, out resolved))
            {
                guid = resolved;
                return true;
            }

            ZoneCore.LogWarning($"[GlowService] Could not resolve glow token '{token}' to a valid prefab guid.");
            return false;
        }
    }
}
