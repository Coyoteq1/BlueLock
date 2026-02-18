using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using BepInEx;
using Stunlock.Core;
using VAuto.Zone.Models;
using VAutomationCore.Core.Config;
using VAutomationCore.Core.Data;

namespace VAuto.Zone.Core
{
    /// <summary>
    /// Centralized prefab token/alias -> PrefabGUID resolver.
    /// Uses Bluelock/config/Prefabsref.json as the primary catalog, with legacy ability alias support and PrefabsAll fallback.
    /// </summary>
    public static class PrefabResolver
    {
        private static readonly object Sync = new();
        private static bool _loaded;
        private static readonly Dictionary<string, PrefabGUID> ByName = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, PrefabGUID> ByAlias = new(StringComparer.OrdinalIgnoreCase);

        private static string PrefabsRefPath => Path.Combine(Paths.ConfigPath, "Bluelock", "Prefabsref.json");
        private static string AbilityPrefabsPath => Path.Combine(Paths.ConfigPath, "Bluelock", "ability_prefabs.json");

        public static string PrefabsRefConfigPath => PrefabsRefPath;
        public static string AbilityAliasConfigPath => AbilityPrefabsPath;

        public static bool TryResolve(string token, out PrefabGUID guid)
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

            if (ByAlias.TryGetValue(lookup, out guid))
            {
                return true;
            }

            if (ByName.TryGetValue(lookup, out guid))
            {
                return true;
            }

            // Legacy fallback: PrefabsAll catalog.
            if (PrefabsAll.TryGet(lookup, out guid))
            {
                return true;
            }

            return false;
        }

        public static void Reload()
        {
            lock (Sync)
            {
                _loaded = false;
                ByName.Clear();
                ByAlias.Clear();
            }
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

                LoadPrefabsRef();
                LoadLegacyAbilityAliases();
                _loaded = true;
            }
        }

        private static void LoadPrefabsRef()
        {
            TypedJsonConfigManager.TryLoadOrCreate(
                PrefabsRefPath,
                CreateDefaultPrefabsRef,
                out PrefabsRefConfig config,
                out _,
                new JsonSerializerOptions { WriteIndented = true },
                ValidatePrefabsRef,
                ZoneCore.LogInfo,
                ZoneCore.LogWarning,
                ZoneCore.LogError);

            if (config?.Choices == null || config.Choices.Count == 0)
            {
                ZoneCore.LogWarning("[PrefabResolver] Prefabsref.json has no entries; defaults injected.");
                config = CreateDefaultPrefabsRef();
            }

            ByName.Clear();
            foreach (var choice in config.Choices)
            {
                if (!TryNormalizeChoice(choice, out var key, out var guid))
                {
                    continue;
                }

                ByName[key] = guid;

                // Store both name and prefab token if different.
                if (!string.IsNullOrWhiteSpace(choice.Prefab) &&
                    !key.Equals(choice.Prefab, StringComparison.OrdinalIgnoreCase))
                {
                    ByName[choice.Prefab.Trim()] = guid;
                }
            }

            ByAlias.Clear();
            foreach (var kvp in config.Aliases ?? Enumerable.Empty<KeyValuePair<string, string>>())
            {
                AddAlias(kvp.Key, kvp.Value, "[Prefabsref.alias]");
            }
        }

        private static void LoadLegacyAbilityAliases()
        {
            try
            {
                if (!File.Exists(AbilityPrefabsPath))
                {
                    return;
                }

                var json = File.ReadAllText(AbilityPrefabsPath);
                var legacy = JsonSerializer.Deserialize<LegacyAbilityAliasConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (legacy?.Aliases == null || legacy.Aliases.Count == 0)
                {
                    return;
                }

                foreach (var kvp in legacy.Aliases)
                {
                    AddAlias(kvp.Key, kvp.Value, "[ability_prefabs]");
                }
            }
            catch (Exception ex)
            {
                ZoneCore.LogWarning($"[PrefabResolver] Failed to load legacy ability aliases: {ex.Message}");
            }
        }

        private static void AddAlias(string alias, string target, string sourceTag)
        {
            if (string.IsNullOrWhiteSpace(alias) || string.IsNullOrWhiteSpace(target))
            {
                return;
            }

            if (ByAlias.ContainsKey(alias))
            {
                ZoneCore.LogWarning($"[PrefabResolver] Alias '{alias}' duplicated from {sourceTag}; keeping first value.");
                return;
            }

            if (!TryResolveTarget(target, out var guid))
            {
                ZoneCore.LogWarning($"[PrefabResolver] Alias '{alias}' points to unknown prefab '{target}' (source {sourceTag}).");
                return;
            }

            ByAlias[alias.Trim()] = guid;
        }

        private static bool TryResolveTarget(string token, out PrefabGUID guid)
        {
            guid = PrefabGUID.Empty;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            // Prefer already loaded name map to avoid recursion.
            if (ByName.TryGetValue(token.Trim(), out guid))
            {
                return true;
            }

            if (PrefabsAll.TryGet(token, out guid))
            {
                return true;
            }

            if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric) && numeric != 0)
            {
                guid = new PrefabGUID(numeric);
                return true;
            }

            return false;
        }

        private static bool TryNormalizeChoice(PrefabChoice choice, out string key, out PrefabGUID guid)
        {
            key = string.Empty;
            guid = PrefabGUID.Empty;

            if (choice == null)
            {
                return false;
            }

            var primary = (choice.Name ?? choice.Prefab ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(primary))
            {
                return false;
            }

            if (choice.PrefabGuid == 0)
            {
                return false;
            }

            key = primary;
            guid = new PrefabGUID(choice.PrefabGuid);
            return true;
        }

        private static PrefabsRefConfig CreateDefaultPrefabsRef()
        {
            var config = new PrefabsRefConfig
            {
                SchemaVersion = 2,
                Source = "PrefabResolver defaults",
                Choices = new List<PrefabChoice>
                {
                    new()
                    {
                        Name = "PurpleCarpetsBuildMenuGroup01",
                        Prefab = "PurpleCarpetsBuildMenuGroup01",
                        PrefabGuid = 1144832236
                    },
                    new()
                    {
                        Name = "TM_Castle_ObjectDecor_TargetDummy_Vampire01",
                        Prefab = "TM_Castle_ObjectDecor_TargetDummy_Vampire01",
                        PrefabGuid = 230163020
                    }
                },
                Aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Spell_VeilOfBlood"] = "AB_Vampire_VeilOfBlood_Group",
                    ["Spell_VeilOfChaos"] = "AB_Vampire_VeilOfChaos_Group",
                    ["Spell_VeilOfFrost"] = "AB_Vampire_VeilOfFrost_Group",
                    ["Spell_VeilOfBones"] = "AB_Vampire_VeilOfBones_AbilityGroup",
                    ["AB_BloodRite_AbilityGroup"] = "AB_Blood_BloodRite_AbilityGroup"
                }
            };

            config.Count = config.Choices.Count;
            config.MaxEntries ??= 200;
            return config;
        }

        private static (bool IsValid, string Error) ValidatePrefabsRef(PrefabsRefConfig config)
        {
            if (config == null)
            {
                return (false, "Config is null.");
            }

            if (config.Choices == null || config.Choices.Count == 0)
            {
                return (false, "No choices present.");
            }

            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenPrefabs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenGuids = new HashSet<int>();

            foreach (var choice in config.Choices)
            {
                if (!TryNormalizeChoice(choice, out var key, out var guid))
                {
                    return (false, "Choice missing name/prefab/guid.");
                }

                if (!seenNames.Add(key))
                {
                    return (false, $"Duplicate choice name '{key}'.");
                }

                if (!string.IsNullOrWhiteSpace(choice.Prefab) && !seenPrefabs.Add(choice.Prefab.Trim()))
                {
                    return (false, $"Duplicate prefab token '{choice.Prefab}'.");
                }

                if (!seenGuids.Add(guid.GuidHash))
                {
                    return (false, $"Duplicate prefab GUID '{guid.GuidHash}'.");
                }
            }

            return (true, string.Empty);
        }

        private sealed class LegacyAbilityAliasConfig
        {
            public Dictionary<string, string> Aliases { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        }
    }
}
