using System;
using System.Collections.Generic;
using System.Globalization;
using Stunlock.Core;
using VAutomationCore.Core.Data;

namespace VAuto.Zone.Core
{
    public enum PrefabCatalogDomain
    {
        Glow,
        Ability,
        Spell,
        VBlood,
        Weapon,
        Amulet,
        Armor,
        Trap
    }

    public static class PrefabReferenceCatalog
    {
        private static readonly object Sync = new();
        private static bool _loaded;
        private static readonly Dictionary<string, int> _all = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<PrefabCatalogDomain, Dictionary<string, int>> _byDomain =
            new()
            {
                [PrefabCatalogDomain.Glow] = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                [PrefabCatalogDomain.Ability] = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                [PrefabCatalogDomain.Spell] = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                [PrefabCatalogDomain.VBlood] = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                [PrefabCatalogDomain.Weapon] = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                [PrefabCatalogDomain.Amulet] = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                [PrefabCatalogDomain.Armor] = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                [PrefabCatalogDomain.Trap] = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            };

        public static bool TryResolve(string token, out PrefabGUID guid, params PrefabCatalogDomain[] preferredDomains)
        {
            guid = PrefabGUID.Empty;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            EnsureLoaded();

            var lookup = token.Trim();
            if (int.TryParse(lookup, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric) && numeric != 0)
            {
                guid = new PrefabGUID(numeric);
                return true;
            }

            if (preferredDomains != null)
            {
                for (var i = 0; i < preferredDomains.Length; i++)
                {
                    if (_byDomain.TryGetValue(preferredDomains[i], out var domain) && domain.TryGetValue(lookup, out var hit))
                    {
                        guid = new PrefabGUID(hit);
                        return true;
                    }
                }
            }

            if (_all.TryGetValue(lookup, out var value))
            {
                guid = new PrefabGUID(value);
                return true;
            }

            return false;
        }

        private static void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }

            lock (Sync)
            {
                if (_loaded)
                {
                    return;
                }

                LoadFromPrefabsAll();

                _loaded = true;
            }
        }

        private static void LoadFromPrefabsAll()
        {
            foreach (var kv in PrefabsAll.ByName)
            {
                AddEntry(kv.Key, kv.Value.GuidHash);
            }
        }

        private static void AddEntry(string key, int guid)
        {
            _all[key] = guid;
            foreach (var domain in _byDomain.Keys)
            {
                if (MatchesDomain(key, domain))
                {
                    _byDomain[domain][key] = guid;
                }
            }
        }

        private static bool MatchesDomain(string key, PrefabCatalogDomain domain)
        {
            return domain switch
            {
                PrefabCatalogDomain.Glow =>
                    key.Contains("Glow", StringComparison.OrdinalIgnoreCase) ||
                    key.Contains("Trail", StringComparison.OrdinalIgnoreCase) ||
                    key.Contains("Aura", StringComparison.OrdinalIgnoreCase) ||
                    key.Contains("Light", StringComparison.OrdinalIgnoreCase) ||
                    key.Contains("Effect", StringComparison.OrdinalIgnoreCase),
                PrefabCatalogDomain.Ability =>
                    key.StartsWith("AB_", StringComparison.OrdinalIgnoreCase),
                PrefabCatalogDomain.Spell =>
                    key.StartsWith("Spell_", StringComparison.OrdinalIgnoreCase) ||
                    key.StartsWith("AB_", StringComparison.OrdinalIgnoreCase),
                PrefabCatalogDomain.VBlood =>
                    key.Contains("VBlood", StringComparison.OrdinalIgnoreCase) ||
                    key.StartsWith("VIB_", StringComparison.OrdinalIgnoreCase),
                PrefabCatalogDomain.Weapon =>
                    key.Contains("Weapon", StringComparison.OrdinalIgnoreCase),
                PrefabCatalogDomain.Amulet =>
                    key.Contains("Amulet", StringComparison.OrdinalIgnoreCase) ||
                    key.Contains("MagicSource", StringComparison.OrdinalIgnoreCase),
                PrefabCatalogDomain.Armor =>
                    key.Contains("Armor", StringComparison.OrdinalIgnoreCase),
                PrefabCatalogDomain.Trap =>
                    key.Contains("Trap", StringComparison.OrdinalIgnoreCase) ||
                    key.Contains("Spike", StringComparison.OrdinalIgnoreCase) ||
                    key.Contains("Mine", StringComparison.OrdinalIgnoreCase) ||
                    key.Contains("Bomb", StringComparison.OrdinalIgnoreCase) ||
                    key.Contains("Chest", StringComparison.OrdinalIgnoreCase) ||
                    key.Contains("Container", StringComparison.OrdinalIgnoreCase),
                _ => false
            };
        }

    }
}
