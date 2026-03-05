using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using BepInEx;
using BepInEx.Logging;
using ProjectM;
using Stunlock.Core;
using Unity.Entities;
using VAuto.Services.Interfaces;
using VAuto.Core;
using VAutomationCore.Abstractions;
using VAutomationCore.Core;
using VAutomationCore.Services;

namespace Blueluck.Services
{
    /// <summary>
    /// Server-side ability loadout service. Abilities in V Rising are implemented as buffs.
    /// We grant/remove only the abilities we applied (tracked per player) to avoid nuking unrelated buffs.
    /// </summary>
    public sealed class AbilityService : IService
    {
        private static readonly ManualLogSource _log = Logger.CreateLogSource("Blueluck.Abilities");

        public bool IsInitialized { get; private set; }
        public ManualLogSource Log => _log;

        private string _configPath = string.Empty;
        private readonly Dictionary<string, List<string>> _sets = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _aliases = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<Entity, List<PrefabGUID>> _appliedByPlayer = new();

        private sealed class AbilityConfig
        {
            public Dictionary<string, List<string>> Sets { get; set; } = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, string> Aliases { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        }

        public void Initialize()
        {
            _configPath = Path.Combine(Paths.ConfigPath, "Blueluck", "abilities.json");
            Directory.CreateDirectory(Path.GetDirectoryName(_configPath) ?? Paths.ConfigPath);

            LoadConfig();
            IsInitialized = true;
            _log.LogInfo($"[Abilities] Initialized with {_sets.Count} sets.");
        }

        public void Cleanup()
        {
            foreach (var kvp in _appliedByPlayer.ToArray())
            {
                ClearAbilities(kvp.Key);
            }

            _sets.Clear();
            _aliases.Clear();
            _appliedByPlayer.Clear();
            IsInitialized = false;
            _log.LogInfo("[Abilities] Cleaned up.");
        }

        public void Reload()
        {
            LoadConfig();
        }

        public IReadOnlyCollection<string> ListSetNames()
        {
            return _sets.Keys.ToArray();
        }

        public bool ApplySet(Entity player, string setName)
        {
            if (!IsInitialized || player == Entity.Null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(setName))
            {
                return false;
            }

            var key = ResolveAlias(setName.Trim());
            if (!_sets.TryGetValue(key, out var tokens) || tokens.Count == 0)
            {
                _log.LogWarning($"[Abilities] Set not found/empty: {setName}");
                return false;
            }

            if (!TryResolveUserEntity(player, out var userEntity))
            {
                _log.LogWarning($"[Abilities] Cannot resolve user entity for player {player.Index}");
                return false;
            }

            // Remove previously applied abilities for this player before applying a new set.
            ClearAbilities(player);

            var applied = new List<PrefabGUID>();
            foreach (var token in tokens)
            {
                if (string.IsNullOrWhiteSpace(token))
                {
                    continue;
                }

                if (!TryResolvePrefabGuid(token.Trim(), out var guid))
                {
                    _log.LogWarning($"[Abilities] Unresolved ability token '{token}' in set '{key}'");
                    continue;
                }

                if (Abilities.GrantAbility(userEntity, player, guid, level: 1))
                {
                    applied.Add(guid);
                }
            }

            _appliedByPlayer[player] = applied;
            _log.LogInfo($"[Abilities] Applied set '{key}' to player {player.Index} (count={applied.Count})");
            return true;
        }

        public void ClearAbilities(Entity player)
        {
            if (!IsInitialized || player == Entity.Null)
            {
                return;
            }

            if (!_appliedByPlayer.TryGetValue(player, out var list) || list.Count == 0)
            {
                _appliedByPlayer.Remove(player);
                return;
            }

            foreach (var guid in list)
            {
                if (guid == PrefabGUID.Empty) continue;
                Abilities.RemoveAbility(player, guid);
            }

            _appliedByPlayer.Remove(player);
        }

        private void LoadConfig()
        {
            _sets.Clear();
            _aliases.Clear();

            try
            {
                if (!File.Exists(_configPath))
                {
                    CreateDefaultConfig();
                    return;
                }

                var json = File.ReadAllText(_configPath);
                var config = JsonSerializer.Deserialize<AbilityConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true,
                    MaxDepth = 64
                }) ?? new AbilityConfig();

                foreach (var pair in config.Sets ?? new Dictionary<string, List<string>>())
                {
                    var k = (pair.Key ?? string.Empty).Trim();
                    if (k.Length == 0) continue;
                    _sets[k] = (pair.Value ?? new List<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                }

                foreach (var pair in config.Aliases ?? new Dictionary<string, string>())
                {
                    var k = (pair.Key ?? string.Empty).Trim();
                    var v = (pair.Value ?? string.Empty).Trim();
                    if (k.Length == 0 || v.Length == 0) continue;
                    _aliases[k] = v;
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[Abilities] Failed to load abilities: {ex.Message}");
            }
        }

        private void CreateDefaultConfig()
        {
            var config = new AbilityConfig
            {
                Sets = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    // Fill these with real ability/buff prefab tokens for your server version.
                    ["arena"] = new List<string>(),
                    ["boss"] = new List<string>()
                },
                Aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["a"] = "arena",
                    ["b"] = "boss"
                }
            };

            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            File.WriteAllText(_configPath, json);
            _log.LogInfo($"[Abilities] Created default abilities at {_configPath}");

            _sets["arena"] = new List<string>();
            _sets["boss"] = new List<string>();
            _aliases["a"] = "arena";
            _aliases["b"] = "boss";
        }

        private string ResolveAlias(string input)
        {
            return _aliases.TryGetValue(input, out var mapped) ? mapped : input;
        }

        private static bool TryResolveUserEntity(Entity player, out Entity userEntity)
        {
            userEntity = Entity.Null;
            try
            {
                var em = UnifiedCore.EntityManager;
                if (em == default || player == Entity.Null || !em.Exists(player) || !em.HasComponent<PlayerCharacter>(player))
                {
                    return false;
                }

                var pc = em.GetComponentData<PlayerCharacter>(player);
                if (pc.UserEntity == Entity.Null || !em.Exists(pc.UserEntity))
                {
                    return false;
                }

                userEntity = pc.UserEntity;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryResolvePrefabGuid(string prefabName, out PrefabGUID guid)
        {
            guid = PrefabGUID.Empty;
            if (string.IsNullOrWhiteSpace(prefabName))
            {
                return false;
            }

            var token = prefabName.Trim();
            if (Plugin.PrefabToGuid?.IsInitialized == true && Plugin.PrefabToGuid.TryGetGuid(token, out guid))
            {
                return guid.GuidHash != 0;
            }

            return PrefabGuidConverter.TryGetGuid(token, out guid) && guid.GuidHash != 0;
        }
    }
}

