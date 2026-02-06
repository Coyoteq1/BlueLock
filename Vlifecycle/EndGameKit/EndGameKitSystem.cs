// Restored from Projects_features/EndGameKit/EndGameKitSystem.cs (legacy kit system).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProjectM;
using Stunlock.Core;
using Unity.Entities;
using UnityEngine;
using VAuto.EndGameKit.Configuration;
using VAuto.EndGameKit.Helpers;
using VAuto.EndGameKit.Services;

namespace VAuto.EndGameKit
{
    /// <summary>
    /// Manages EndGameKit profiles and applies them to players entering arenas.
    /// </summary>
    public class EndGameKitSystem
    {
        private readonly EntityManager _entityManager;
        private readonly EndGameKitConfigService _configService;
        private readonly EquipmentService _equipmentService;
        private readonly ConsumableService _consumableService;
        private readonly JewelService _jewelService;
        private readonly StatExtensionService _statService;

        private readonly Dictionary<string, EndGameKitProfile> _kitProfiles = new(StringComparer.OrdinalIgnoreCase);
        private DateTime _lastConfigCheck = DateTime.MinValue;
        public bool IsInitialized { get; private set; }

        public EndGameKitSystem(EntityManager entityManager)
        {
            _entityManager = entityManager ?? throw new ArgumentNullException(nameof(entityManager));

            // Get ServerGameManager from VRCore (already initialized in plugin bootstrap)
            var serverGameManager = VAuto.Core.VRCore.ServerGameManager;

            var configPath = Path.Combine(Paths.ConfigPath, "EndGameKit.json");
            _configService = new EndGameKitConfigService(configPath);
            _equipmentService = new EquipmentService(serverGameManager, entityManager);
            _consumableService = new ConsumableService(entityManager);
            _jewelService = new JewelService(entityManager);
            _statService = new StatExtensionService(serverGameManager, entityManager);

            LoadConfiguration();
        }

        public void Initialize()
        {
            if (IsInitialized) return;
            IsInitialized = true;
        }

        public bool ApplyKit(Entity player, string kitName, out string error)
        {
            return TryApplyKit(player, kitName, out error);
        }

        public bool RemoveKit(Entity player, out string error)
        {
            error = string.Empty;

            if (!PlayerHelper.IsValidPlayer(_entityManager, player))
            {
                error = "Invalid player entity.";
                Plugin.Log.LogWarning($"[EndGameKitSystem] {error}");
                return false;
            }

            // Clear equipment
            _equipmentService.ClearEquipment(player);

            // Clear consumables (if method exists, otherwise log)
            // _consumableService.ClearConsumables(player); // TODO: implement if needed

            // Clear jewels
            _jewelService.ClearJewels(player);

            // Clear stat extensions (if method exists, otherwise log)
            // _statService.ClearStatExtensions(player); // TODO: implement if needed

            Plugin.Log.LogInfo($"[EndGameKitSystem] Cleared kit from player {player.Index}");
            return true;
        }

        public bool TryApplyKit(Entity player, string kitName, out string error)
        {
            error = string.Empty;

            if (!_kitProfiles.TryGetValue(kitName, out var profile))
            {
                error = $"Kit '{kitName}' not found.";
                Plugin.Log.LogWarning($"[EndGameKitSystem] {error}");
                return false;
            }

            // Equip items
            var equipmentApplied = _equipmentService.EquipKit(player, profile.GetEquipmentGuidMap());

            // Apply consumables
            var consumablesApplied = _consumableService.ApplyBatch(player, profile.GetConsumableGuidList());

            // Attach jewels
            var jewelsApplied = _jewelService.AttachJewels(player, profile.GetJewelGuidList());

            // Apply stat extensions
            var statsApplied = _statService.ApplyStatOverrides(player, profile.StatOverrides);

            Plugin.Log.LogInfo($"[EndGameKitSystem] Applied kit '{kitName}' to player {player.Index}: " +
                               $"equipment={equipmentApplied}, consumables={consumablesApplied}, jewels={jewelsApplied}, stats={statsApplied}");

            return true;
        }

        public IEnumerable<string> ListKits() => _kitProfiles.Keys.OrderBy(k => k);

        public List<string> GetKitProfileNames()
        {
            return _kitProfiles.Keys.ToList();
        }

        public EndGameKitProfile GetKitProfile(string kitName)
        {
            return _kitProfiles.TryGetValue(kitName, out var profile) ? profile : null;
        }

        public bool TryApplyKitForZone(Entity user, Entity character, string zoneName, out string error)
        {
            // For now, zone-specific logic is not implemented, so we apply the kit directly
            // TODO: Implement zone-specific kit selection logic
            return TryApplyKit(character, zoneName, out error);
        }

        public bool TryRestoreKitForZone(Entity user, Entity character, string zoneName, out string error)
        {
            // For now, zone-specific logic is not implemented, so we remove the kit directly
            // TODO: Implement zone-specific kit removal logic
            return TryRemoveKit(character, out error);
        }

        public bool HasKitApplied(Entity player, out string error)
        {
            error = string.Empty;

            if (!PlayerHelper.IsValidPlayer(_entityManager, player))
            {
                error = "Invalid player entity.";
                return false;
            }

            // TODO: Implement logic to check if a kit has been applied to the player
            // For now, we return false as the system doesn't track applied kits
            return false;
        }

        public void HotReload()
        {
            var now = DateTime.UtcNow;
            if ((now - _lastConfigCheck).TotalSeconds < 5)
                return; // throttle

            _lastConfigCheck = now;
            if (_configService.TryReloadIfChanged(out var profiles))
            {
                _kitProfiles.Clear();
                foreach (var profile in profiles)
                {
                    _kitProfiles[profile.Name] = profile;
                }
                Plugin.Log.LogInfo($"[EndGameKitSystem] Hot-reloaded {_kitProfiles.Count} kit profiles");
            }
        }

        private void LoadConfiguration()
        {
            if (_configService.TryLoad(out var profiles))
            {
                _kitProfiles.Clear();
                foreach (var profile in profiles)
                {
                    _kitProfiles[profile.Name] = profile;
                }
                Plugin.Log.LogInfo($"[EndGameKitSystem] Loaded {_kitProfiles.Count} kit profiles");
            }
            else
            {
                Plugin.Log.LogWarning("[EndGameKitSystem] Failed to load configuration; no kits available");
            }
        }
    }
}

