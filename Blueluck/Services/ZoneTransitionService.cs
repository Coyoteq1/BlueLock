using System;
using System.Collections.Generic;
using Unity.Entities;
using VAuto.Services.Interfaces;
using Blueluck.Models;
using VAutomationCore.Core.Lifecycle;

namespace Blueluck.Services
{
    /// <summary>
    /// Service for handling zone enter/exit transitions.
    /// Implements IService from VAutomationCore.
    /// </summary>
    public class ZoneTransitionService : IService
    {
        private static readonly ManualLogSource _log = Logger.CreateLogSource("Blueluck.ZoneTransition");
        
        public bool IsInitialized { get; private set; }
        public ManualLogSource Log => _log;

        private ZoneConfigService? _configService;
        private FlowRegistryService? _flowRegistry;
        private ProgressService? _progressService;

        // Track players currently in zones
        private readonly Dictionary<Entity, int> _playersInZones = new();
        // Track occupancy counts per zone hash so we can apply/remove zone-level effects once.
        private readonly Dictionary<int, int> _zoneOccupancy = new();

        public void Initialize()
        {
            _configService = Plugin.ZoneConfig;
            _flowRegistry = Plugin.FlowRegistry;
            _progressService = Plugin.Progress;

            IsInitialized = true;
            _log.LogInfo("[ZoneTransition] Initialized.");
        }

        public void Cleanup()
        {
            _playersInZones.Clear();
            _zoneOccupancy.Clear();
            IsInitialized = false;
            _log.LogInfo("[ZoneTransition] Cleaned up.");
        }

        /// <summary>
        /// Called when a player enters a zone.
        /// </summary>
        public void OnZoneEnter(Entity player, ZoneDefinition zone)
        {
            try
            {
                _log.LogInfo($"[ZoneTransition] Player {player.Index} entering zone: {zone.Name} ({zone.Type})");

                // Track player in zone
                _playersInZones[player] = zone.Hash;
                _zoneOccupancy[zone.Hash] = _zoneOccupancy.TryGetValue(zone.Hash, out var count) ? count + 1 : 1;

                // Handle zone-specific enter logic
                switch (zone.Type)
                {
                    case "BossZone":
                        HandleBossZoneEnter(player, zone);
                        break;
                    case "ArenaZone":
                        HandleArenaZoneEnter(player, zone);
                        break;
                }

                // Apply kit on enter for any zone type (after SaveProgress so snapshot isn't polluted by kit grants).
                if (Plugin.Kits?.IsInitialized == true && !string.IsNullOrWhiteSpace(zone.KitOnEnter))
                {
                    Plugin.Kits.ApplyKit(player, zone.KitOnEnter);
                }

                // Apply ability set on enter for any zone type.
                if (Plugin.Abilities?.IsInitialized == true)
                {
                    // Boss zones may specify an array; if so, use first as default.
                    var bossDefault = zone is BossZoneConfig bossCfg && bossCfg.AbilitySets != null && bossCfg.AbilitySets.Length > 0
                        ? bossCfg.AbilitySets[0]
                        : null;

                    var setName = !string.IsNullOrWhiteSpace(bossDefault) ? bossDefault : zone.AbilitySet;
                    if (!string.IsNullOrWhiteSpace(setName))
                    {
                        Plugin.Abilities.ApplySet(player, setName);
                    }
                }

                // Execute flow if configured
                if (!string.IsNullOrEmpty(zone.FlowOnEnter) && _flowRegistry?.IsInitialized == true)
                {
                    _flowRegistry.ExecuteFlow(zone.FlowOnEnter, player, zone.Name, zone.Hash);
                }

                // Zone message (player-scoped; "broadcast" is treated as a label, not a server-wide broadcast)
                if (zone.OnEnter != null)
                {
                    var message = !string.IsNullOrEmpty(zone.OnEnter.Message) 
                        ? zone.OnEnter.Message 
                        : zone.OnEnter.Broadcast;
                    if (!string.IsNullOrEmpty(message))
                    {
                        if (_flowRegistry?.IsInitialized == true)
                        {
                            _flowRegistry.SendMessage(player, message, zone.Hash);
                        }
                        else
                        {
                            _log.LogInfo($"[ZoneTransition] Message: {message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[ZoneTransition] Error on zone enter: {ex.Message}");
            }
        }

        /// <summary>
        /// Called when a player exits a zone.
        /// </summary>
        public void OnZoneExit(Entity player, ZoneDefinition zone)
        {
            try
            {
                _log.LogInfo($"[ZoneTransition] Player {player.Index} exiting zone: {zone.Name} ({zone.Type})");

                // Remove from tracking
                _playersInZones.Remove(player);
                if (_zoneOccupancy.TryGetValue(zone.Hash, out var count))
                {
                    count--;
                    if (count <= 0) _zoneOccupancy.Remove(zone.Hash);
                    else _zoneOccupancy[zone.Hash] = count;
                }

                // Handle zone-specific exit logic
                switch (zone.Type)
                {
                    case "BossZone":
                        HandleBossZoneExit(player, zone);
                        break;
                    case "ArenaZone":
                        HandleArenaZoneExit(player, zone);
                        break;
                }

                // Apply kit on exit for any zone type (after RestoreProgress so restore wins).
                if (Plugin.Kits?.IsInitialized == true && !string.IsNullOrWhiteSpace(zone.KitOnExit))
                {
                    Plugin.Kits.ApplyKit(player, zone.KitOnExit);
                }

                // Clear ability loadout on exit.
                if (Plugin.Abilities?.IsInitialized == true)
                {
                    Plugin.Abilities.ClearAbilities(player);
                }

                // Execute flow if configured
                if (!string.IsNullOrEmpty(zone.FlowOnExit) && _flowRegistry?.IsInitialized == true)
                {
                    _flowRegistry.ExecuteFlow(zone.FlowOnExit, player, zone.Name, zone.Hash);
                }

                // Zone message (player-scoped)
                if (zone.OnExit != null)
                {
                    var message = !string.IsNullOrEmpty(zone.OnExit.Message) 
                        ? zone.OnExit.Message 
                        : zone.OnExit.Broadcast;
                    if (!string.IsNullOrEmpty(message))
                    {
                        if (_flowRegistry?.IsInitialized == true)
                        {
                            _flowRegistry.SendMessage(player, message, zone.Hash);
                        }
                        else
                        {
                            _log.LogInfo($"[ZoneTransition] Message: {message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[ZoneTransition] Error on zone exit: {ex.Message}");
            }
        }

        private void HandleBossZoneEnter(Entity player, ZoneDefinition zone)
        {
            // Boss zones skip progress saving by default (NoProgress = true)
            // This preserves player progression
            if (zone is BossZoneConfig bossConfig && !bossConfig.NoProgress)
            {
                if (_progressService?.IsInitialized == true)
                {
                    _progressService.SaveProgress(player);
                }
            }

            // Fallback gameplay logic when no explicit flow is configured.
            if (zone is BossZoneConfig bossCfg && string.IsNullOrEmpty(zone.FlowOnEnter) && _flowRegistry?.IsInitialized == true)
            {
                var isFirstPlayer = _zoneOccupancy.TryGetValue(zone.Hash, out var count) && count == 1;
                if (isFirstPlayer)
                {
                    // Spawn boss once per zone occupancy (re-entering players won't re-spawn).
                    if (!string.IsNullOrWhiteSpace(bossCfg.BossPrefab))
                    {
                        _flowRegistry.EnsureBosses(player, zone.Hash, bossCfg.BossPrefab, bossCfg.BossQuantity, randomInZone: bossCfg.RandomSpawn);
                    }
                }
            }
        }

        private void HandleBossZoneExit(Entity player, ZoneDefinition zone)
        {
            // Boss zones skip progress restoration by default (NoProgress = true)
            // This preserves player progression
            if (zone is BossZoneConfig bossConfig && !bossConfig.NoProgress)
            {
                if (_progressService?.IsInitialized == true)
                {
                    _progressService.RestoreProgress(player, clearAfter: true);
                }
            }

            if (zone is BossZoneConfig && string.IsNullOrEmpty(zone.FlowOnExit) && _flowRegistry?.IsInitialized == true)
            {
                // Remove zone-level effects when last player leaves.
                var isLastPlayer = !_zoneOccupancy.ContainsKey(zone.Hash);
                if (isLastPlayer)
                {
                    _flowRegistry.RemoveBosses(zone.Hash);
                }
            }
        }

        private void HandleArenaZoneEnter(Entity player, ZoneDefinition zone)
        {
            // Save progress if enabled
            if (_progressService?.IsInitialized == true && zone is ArenaZoneConfig arenaConfig && arenaConfig.SaveProgress)
            {
                _progressService.SaveProgress(player);
            }

            // Back-compat: if "abilitySet" is present but Ability UI is removed, treat it as a kit name.
            if (Plugin.Kits?.IsInitialized == true && zone is ArenaZoneConfig arenaConfig3 && !string.IsNullOrWhiteSpace(arenaConfig3.AbilitySet))
            {
                Plugin.Kits.ApplyKit(player, arenaConfig3.AbilitySet);
            }

            // Fallback gameplay logic when no explicit flow is configured.
            if (zone is ArenaZoneConfig arenaCfg && string.IsNullOrEmpty(zone.FlowOnEnter) && _flowRegistry?.IsInitialized == true)
            {
                _flowRegistry.SetPvp(player, arenaCfg.PvpEnabled, zone.Hash);
            }
        }

        private void HandleArenaZoneExit(Entity player, ZoneDefinition zone)
        {
            // Restore progress if enabled
            if (_progressService?.IsInitialized == true && zone is ArenaZoneConfig arenaConfig && arenaConfig.RestoreOnExit)
            {
                _progressService.RestoreProgress(player, clearAfter: true);
            }

            if (zone is ArenaZoneConfig arenaCfg && string.IsNullOrEmpty(zone.FlowOnExit) && _flowRegistry?.IsInitialized == true)
            {
                _flowRegistry.SetPvp(player, enabled: false, zone.Hash);
            }
        }

        /// <summary>
        /// Gets the current zone hash for a player.
        /// </summary>
        public int GetPlayerZone(Entity player)
        {
            return _playersInZones.TryGetValue(player, out var hash) ? hash : 0;
        }

        /// <summary>
        /// Checks if a player is in a specific zone type.
        /// </summary>
        public bool IsPlayerInZoneType(Entity player, string zoneType)
        {
            if (!_playersInZones.TryGetValue(player, out var hash))
                return false;

            if (_configService?.TryGetZoneByHash(hash, out var zone) == true)
                return zone.Type == zoneType;

            return false;
        }
    }
}
