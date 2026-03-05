using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using BepInEx;
using BepInEx.Logging;
using ProjectM;
using ProjectM.Network;
using Stunlock.Core;
using Unity.Entities;
using VAuto.Services.Interfaces;
using VAuto.Core;
using VAutomationCore.Services;

namespace Blueluck.Services
{
    /// <summary>
    /// Loads and applies item kits defined in config/Blueluck/kits.json.
    /// Uses DebugEventsSystem via reflection for cross-version compatibility.
    /// </summary>
    public sealed class KitService : IService
    {
        private static readonly ManualLogSource _log = Logger.CreateLogSource("Blueluck.Kits");

        public bool IsInitialized { get; private set; }
        public ManualLogSource Log => _log;

        private string _configPath = string.Empty;
        private DebugEventsSystem? _debugEventsSystem;
        private Dictionary<string, List<KitItem>> _kits = new(StringComparer.OrdinalIgnoreCase);
        private Func<FromCharacter, PrefabGUID, int, bool>? _giveItem;

        private sealed class KitConfig
        {
            public Dictionary<string, List<KitItem>> Kits { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        }

        public sealed class KitItem
        {
            public string Prefab { get; set; } = string.Empty;
            public int Qty { get; set; } = 1;
        }

        public void Initialize()
        {
            _configPath = Path.Combine(Paths.ConfigPath, "Blueluck", "kits.json");
            Directory.CreateDirectory(Path.GetDirectoryName(_configPath) ?? Paths.ConfigPath);

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                _log.LogWarning("[Kits] World not ready; kit service not initialized.");
                return;
            }

            _debugEventsSystem = world.GetExistingSystemManaged<DebugEventsSystem>();
            if (_debugEventsSystem == null)
            {
                _log.LogWarning("[Kits] DebugEventsSystem unavailable; kit grants will be disabled.");
            }

            LoadKits();
            _giveItem = BuildGiveItemInvoker(_debugEventsSystem);

            IsInitialized = true;
            _log.LogInfo($"[Kits] Initialized with {_kits.Count} kits.");
        }

        public void Cleanup()
        {
            _debugEventsSystem = null;
            _kits.Clear();
            _giveItem = null;
            IsInitialized = false;
            _log.LogInfo("[Kits] Cleaned up.");
        }

        public void Reload()
        {
            LoadKits();
        }

        public IReadOnlyCollection<string> ListKitNames()
        {
            return _kits.Keys.ToArray();
        }

        public bool ApplyKit(Entity player, string kitName)
        {
            if (!IsInitialized)
            {
                _log.LogWarning("[Kits] ApplyKit called before initialization.");
                return false;
            }

            if (player == Entity.Null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(kitName))
            {
                return false;
            }

            if (!_kits.TryGetValue(kitName.Trim(), out var items) || items.Count == 0)
            {
                _log.LogWarning($"[Kits] Kit not found or empty: {kitName}");
                return false;
            }

            if (!TryGetFromCharacter(player, out var fromCharacter))
            {
                _log.LogWarning($"[Kits] Failed to resolve FromCharacter for player {player.Index}");
                return false;
            }

            if (_giveItem == null)
            {
                NotifyUser(fromCharacter.User, $"[Kits] Cannot grant kit '{kitName}': give-item API unavailable.");
                return false;
            }

            var ok = true;
            foreach (var item in items)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.Prefab))
                {
                    continue;
                }

                var qty = item.Qty <= 0 ? 1 : item.Qty;
                if (!PrefabGuidConverter.TryGetGuid(item.Prefab.Trim(), out var guid) || guid == default)
                {
                    _log.LogWarning($"[Kits] Unknown prefab '{item.Prefab}' in kit '{kitName}'");
                    ok = false;
                    continue;
                }

                if (!_giveItem(fromCharacter, guid, qty))
                {
                    _log.LogWarning($"[Kits] Failed to give '{item.Prefab}' x{qty} to player {player.Index} (kit '{kitName}')");
                    ok = false;
                }
            }

            if (ok)
            {
                NotifyUser(fromCharacter.User, $"[Kits] Applied kit '{kitName}'.");
            }
            else
            {
                NotifyUser(fromCharacter.User, $"[Kits] Kit '{kitName}' applied with errors. Check server logs.");
            }

            return ok;
        }

        private void LoadKits()
        {
            _kits.Clear();

            try
            {
                if (!File.Exists(_configPath))
                {
                    CreateDefaultKits();
                    return;
                }

                var json = File.ReadAllText(_configPath);
                var config = JsonSerializer.Deserialize<KitConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true,
                    MaxDepth = 64
                }) ?? new KitConfig();

                if (config.Kits != null)
                {
                    foreach (var pair in config.Kits)
                    {
                        var key = (pair.Key ?? string.Empty).Trim();
                        if (key.Length == 0)
                        {
                            continue;
                        }

                        var list = pair.Value ?? new List<KitItem>();
                        _kits[key] = list.Where(x => x != null && !string.IsNullOrWhiteSpace(x.Prefab)).ToList();
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[Kits] Failed to load kits: {ex.Message}");
            }
        }

        private void CreateDefaultKits()
        {
            var defaultConfig = new KitConfig
            {
                Kits = new Dictionary<string, List<KitItem>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["arena_enter"] = new List<KitItem>
                    {
                        new KitItem { Prefab = "Item_Weapon_Sword_T05_Iron", Qty = 1 },
                        new KitItem { Prefab = "Item_Consumable_HealingSalve_T01", Qty = 10 }
                    },
                    ["arena_exit"] = new List<KitItem>
                    {
                        // Optional: grant a small reward on exit.
                        new KitItem { Prefab = "ItemResource_GoldCoin", Qty = 10 }
                    }
                }
            };

            var json = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            File.WriteAllText(_configPath, json);

            _kits = defaultConfig.Kits;
            _log.LogInfo($"[Kits] Created default kits at {_configPath}");
        }

        private static Func<FromCharacter, PrefabGUID, int, bool>? BuildGiveItemInvoker(DebugEventsSystem? debugEventsSystem)
        {
            if (debugEventsSystem == null)
            {
                return null;
            }

            try
            {
                var systemType = debugEventsSystem.GetType();
                var candidates = systemType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(m =>
                    {
                        var n = m.Name;
                        return n.IndexOf("GiveItem", StringComparison.OrdinalIgnoreCase) >= 0
                            || n.IndexOf("GrantItem", StringComparison.OrdinalIgnoreCase) >= 0
                            || n.IndexOf("AddItem", StringComparison.OrdinalIgnoreCase) >= 0;
                    })
                    .ToArray();

                MethodInfo? best = null;
                GiveItemSignature signature = GiveItemSignature.None;

                foreach (var method in candidates)
                {
                    var p = method.GetParameters();
                    if (p.Length == 3 &&
                        p[0].ParameterType == typeof(FromCharacter) &&
                        p[1].ParameterType == typeof(PrefabGUID) &&
                        p[2].ParameterType == typeof(int))
                    {
                        best = method;
                        signature = GiveItemSignature.FromCharacterPrefabGuidInt;
                        break;
                    }

                    if (p.Length == 3 &&
                        p[0].ParameterType == typeof(FromCharacter) &&
                        p[1].ParameterType == typeof(int) &&
                        p[2].ParameterType == typeof(int))
                    {
                        best = method;
                        signature = GiveItemSignature.FromCharacterHashInt;
                    }

                    if (p.Length == 3 &&
                        p[0].ParameterType == typeof(Entity) &&
                        p[1].ParameterType == typeof(PrefabGUID) &&
                        p[2].ParameterType == typeof(int))
                    {
                        best = method;
                        signature = GiveItemSignature.UserEntityPrefabGuidInt;
                    }
                }

                if (best == null || signature == GiveItemSignature.None)
                {
                    _log.LogWarning("[Kits] No compatible GiveItem method found on DebugEventsSystem.");
                    return null;
                }

                return (fromCharacter, prefabGuid, qty) =>
                {
                    try
                    {
                        switch (signature)
                        {
                            case GiveItemSignature.FromCharacterPrefabGuidInt:
                                best.Invoke(debugEventsSystem, new object[] { fromCharacter, prefabGuid, qty });
                                return true;
                            case GiveItemSignature.FromCharacterHashInt:
                                best.Invoke(debugEventsSystem, new object[] { fromCharacter, prefabGuid.GetHashCode(), qty });
                                return true;
                            case GiveItemSignature.UserEntityPrefabGuidInt:
                                best.Invoke(debugEventsSystem, new object[] { fromCharacter.User, prefabGuid, qty });
                                return true;
                            default:
                                return false;
                        }
                    }
                    catch (Exception ex)
                    {
                        _log.LogDebug($"[Kits] GiveItem invocation failed: {ex.Message}");
                        return false;
                    }
                };
            }
            catch (Exception ex)
            {
                _log.LogWarning($"[Kits] Failed to build GiveItem invoker: {ex.Message}");
                return null;
            }
        }

        private enum GiveItemSignature
        {
            None = 0,
            FromCharacterPrefabGuidInt = 1,
            FromCharacterHashInt = 2,
            UserEntityPrefabGuidInt = 3
        }

        private static bool TryGetFromCharacter(Entity player, out FromCharacter fromCharacter)
        {
            fromCharacter = default;

            try
            {
                var world = World.DefaultGameObjectInjectionWorld;
                if (world == null)
                {
                    return false;
                }

                var em = world.EntityManager;
                if (!em.Exists(player) || !em.HasComponent<PlayerCharacter>(player))
                {
                    return false;
                }

                var playerCharacter = em.GetComponentData<PlayerCharacter>(player);
                if (!em.Exists(playerCharacter.UserEntity) || !em.HasComponent<User>(playerCharacter.UserEntity))
                {
                    return false;
                }

                fromCharacter = new FromCharacter
                {
                    User = playerCharacter.UserEntity,
                    Character = player
                };

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void NotifyUser(Entity userEntity, string message)
        {
            if (userEntity == Entity.Null || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            // Best-effort: do not throw if chat fails on this runtime.
            try
            {
                GameActionService.InvokeAction("sendmessagetouser", new object[] { userEntity, message });
            }
            catch
            {
                // ignored
            }
        }
    }
}
