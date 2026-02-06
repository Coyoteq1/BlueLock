using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using BepInEx;
using ProjectM.Network;
using Stunlock.Core;
using Unity.Entities;
using VAuto.Core;
using VAuto.Services.Interfaces;

namespace VAuto.Core.Lifecycle
{
    public sealed class KitConfigService : IService
    {
        private static readonly string ConfigPath = Path.Combine(Paths.ConfigPath, "EndGameKit.json");
        private readonly Dictionary<ulong, AppliedKitState> _applied = new Dictionary<ulong, AppliedKitState>();
        private KitConfig _config;

        public bool IsInitialized { get; private set; }
        public BepInEx.Logging.ManualLogSource Log { get; private set; }

        public KitConfigService()
        {
            Log = Plugin.Log;
        }

        public void Initialize()
        {
            if (IsInitialized) return;
            LoadOrCreateConfig();
            IsInitialized = true;
        }

        public void Cleanup() { }

        public bool TryApplyKitForZone(Entity user, Entity character, string zoneName)
        {
            if (!IsInitialized) Initialize();
            if (_config == null || string.IsNullOrWhiteSpace(zoneName))
                return false;

            var profile = FindAutoApplyProfile(zoneName);
            if (profile == null)
                return false;

            var platformId = ResolvePlatformId(user, character);
            if (platformId == 0)
                return false;

            var state = new AppliedKitState
            {
                KitName = profile.Name,
                ZoneName = zoneName,
                Character = character,
                User = user,
                PlatformId = platformId,
                AppliedAtUtc = DateTime.UtcNow
            };

            TryCaptureOriginalBlood(character, state);
            _applied[platformId] = state;

            ApplyBlood(character, profile);

            Log?.LogInfo($"[Kit] Applied kit '{profile.Name}' for zone '{zoneName}'");
            KitRecordsService.Record(VRCore.EntityManager, user, character, zoneName, profile.Name);
            return true;
        }

        public bool TryRestoreKitForZone(Entity user, Entity character, string zoneName)
        {
            if (!IsInitialized) Initialize();
            if (_config == null || string.IsNullOrWhiteSpace(zoneName))
                return false;

            var platformId = ResolvePlatformId(user, character);
            if (platformId == 0)
                return false;

            if (!_applied.TryGetValue(platformId, out var state))
                return false;

            if (!string.Equals(state.ZoneName, zoneName, StringComparison.OrdinalIgnoreCase))
                return false;

            var profile = GetProfile(state.KitName);
            if (profile == null || !profile.RestoreOnExit)
                return false;

            _applied.Remove(platformId);

            var em = VRCore.EntityManager;
            var restoreCharacter = character;

            if (state.Character != Entity.Null && em != default && em.Exists(state.Character))
            {
                restoreCharacter = state.Character;
                if (restoreCharacter != character)
                    Log?.LogWarning($"[Kit] Restore called on a different character entity (applied {state.Character}, restore {character}). Restoring to the originally applied entity.");
            }
            else if (state.Character != Entity.Null && state.Character != character)
            {
                Log?.LogWarning($"[Kit] Original character entity no longer exists (applied {state.Character}, restore {character}). Restoring to current entity.");
            }

            TryRestoreOriginalBlood(restoreCharacter, state);
            Log?.LogInfo($"[Kit] Restored kit state for zone '{zoneName}' (kit '{profile.Name}')");
            return true;
        }

        public List<string> GetKitNames()
        {
            return _config?.Profiles?.Select(p => p.Name).ToList() ?? new List<string>();
        }

        private EndGameKitProfile GetProfile(string name)
        {
            return _config?.Profiles?.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        private EndGameKitProfile FindAutoApplyProfile(string zoneName)
        {
            if (_config?.Profiles == null) return null;
            foreach (var profile in _config.Profiles)
            {
                if (!profile.Enabled || !profile.AutoApplyOnZoneEntry)
                    continue;
                if (profile.AutoApplyZones == null || profile.AutoApplyZones.Count == 0)
                    continue;
                if (profile.AutoApplyZones.Any(z => string.Equals(z, zoneName, StringComparison.OrdinalIgnoreCase)))
                    return profile;
            }
            return null;
        }

        private void LoadOrCreateConfig()
        {
            if (!File.Exists(ConfigPath))
            {
                var defaultConfig = new KitConfig
                {
                    Version = "1.0",
                    LastModified = DateTime.UtcNow.ToString("O"),
                    Profiles = new List<EndGameKitProfile>
                    {
                        new EndGameKitProfile
                        {
                            Name = "PvP_Arena",
                            Description = "Default arena kit",
                            Enabled = true,
                            AutoApplyOnZoneEntry = true,
                            AutoApplyZones = new List<string> { "default" },
                            RestoreOnExit = true
                        }
                    }
                };
                WriteConfig(defaultConfig);
            }

            var json = File.ReadAllText(ConfigPath);
            _config = JsonSerializer.Deserialize<KitConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new KitConfig();
        }

        private void WriteConfig(KitConfig cfg)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            var json = JsonSerializer.Serialize(cfg, options);
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
            File.WriteAllText(ConfigPath, json);
        }

        private void ApplyBlood(Entity character, EndGameKitProfile profile)
        {
            if (string.IsNullOrWhiteSpace(profile.BloodType)) return;
            try
            {
                // Best-effort: attempt to find a blood component and set quality
                if (!VRCore.EntityManager.HasComponent<ProjectM.Blood>(character)) return;
                var blood = VRCore.EntityManager.GetComponentData<ProjectM.Blood>(character);
                blood.BloodType = new PrefabGUID(ResolveBloodGuid(profile.BloodType));
                if (profile.BloodQuality > 0)
                {
                    blood.Quality = (float)Math.Clamp(profile.BloodQuality, 0, 100);
                }
                VRCore.EntityManager.SetComponentData(character, blood);
            }
            catch
            {
                // ignore if not supported
            }
        }

        private static ulong ResolvePlatformId(Entity user, Entity character)
        {
            try
            {
                var em = VRCore.EntityManager;
                if (em == default)
                    return 0;

                if (user != Entity.Null && em.Exists(user) && em.HasComponent<User>(user))
                    return em.GetComponentData<User>(user).PlatformId;

                // Fallback: some entities may have User directly attached (or other mods may pass the user as character).
                if (character != Entity.Null && em.Exists(character) && em.HasComponent<User>(character))
                    return em.GetComponentData<User>(character).PlatformId;
            }
            catch
            {
                return 0;
            }

            return 0;
        }

        private static void TryCaptureOriginalBlood(Entity character, AppliedKitState state)
        {
            try
            {
                var em = VRCore.EntityManager;
                if (em == default || character == Entity.Null || !em.Exists(character))
                    return;

                if (!em.HasComponent<ProjectM.Blood>(character))
                    return;

                var blood = em.GetComponentData<ProjectM.Blood>(character);
                state.OriginalBloodType = blood.BloodType;
                state.OriginalBloodQuality = blood.Quality;
                state.HasOriginalBlood = true;
            }
            catch
            {
                // ignore
            }
        }

        private static void TryRestoreOriginalBlood(Entity character, AppliedKitState state)
        {
            if (!state.HasOriginalBlood)
                return;

            try
            {
                var em = VRCore.EntityManager;
                if (em == default || character == Entity.Null || !em.Exists(character))
                    return;

                if (!em.HasComponent<ProjectM.Blood>(character))
                    return;

                var blood = em.GetComponentData<ProjectM.Blood>(character);
                blood.BloodType = state.OriginalBloodType;
                blood.Quality = state.OriginalBloodQuality;
                em.SetComponentData(character, blood);
            }
            catch
            {
                // ignore
            }
        }

        private int ResolveBloodGuid(string name)
        {
            // Parse int hash if provided; otherwise default to 0 (unknown).
            if (int.TryParse(name, out var val)) return val;
            return 0;
        }

        private sealed class AppliedKitState
        {
            public string KitName { get; set; } = string.Empty;
            public string ZoneName { get; set; } = string.Empty;
            public Entity User { get; set; }
            public ulong PlatformId { get; set; }
            public Entity Character { get; set; }
            public DateTime AppliedAtUtc { get; set; }
            public bool HasOriginalBlood { get; set; }
            public PrefabGUID OriginalBloodType { get; set; }
            public float OriginalBloodQuality { get; set; }
        }
    }

    public sealed class KitConfig
    {
        public string Version { get; set; } = "1.0";
        public string LastModified { get; set; } = string.Empty;
        public List<EndGameKitProfile> Profiles { get; set; } = new List<EndGameKitProfile>();
    }

    public sealed class EndGameKitProfile
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
        public bool AutoApplyOnZoneEntry { get; set; }
        public List<string> AutoApplyZones { get; set; } = new List<string>();
        public bool RestoreOnExit { get; set; } = true;
        public int MinimumGearScore { get; set; }
        public bool AllowInPvP { get; set; }
        public Dictionary<string, long> Equipment { get; set; } = new Dictionary<string, long>();
        public List<ConsumableItem> Consumables { get; set; } = new List<ConsumableItem>();
        public List<long> Jewels { get; set; } = new List<long>();
        public StatOverrideConfig StatOverrides { get; set; } = new StatOverrideConfig();
        public string BloodType { get; set; } = string.Empty;
        public int BloodQuality { get; set; }
    }

    public sealed class ConsumableItem
    {
        public long Guid { get; set; }
        public int Quantity { get; set; } = 1;
    }

    public sealed class StatOverrideConfig
    {
        public float BonusPower { get; set; }
        public float BonusMaxHealth { get; set; }
        public float BonusSpellPower { get; set; }
        public float BonusMoveSpeed { get; set; }
        public float BonusPhysicalResistance { get; set; }
        public float BonusSpellResistance { get; set; }
        public float BonusArmor { get; set; }
        public float BonusMaxStamina { get; set; }
    }
}
