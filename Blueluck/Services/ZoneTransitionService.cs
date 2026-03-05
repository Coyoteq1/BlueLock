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

        // Track players currently in zones
        private readonly Dictionary<Entity, int> _playersInZones = new();
        // Track occupancy counts per zone hash so we can apply/remove zone-level effects once.
        private readonly Dictionary<int, int> _zoneOccupancy = new();

        private static ZoneConfigService? ZoneConfig => Plugin.ZoneConfig?.IsInitialized == true ? Plugin.ZoneConfig : null;
        private static FlowRegistryService? FlowRegistry => Plugin.FlowRegistry?.IsInitialized == true ? Plugin.FlowRegistry : null;
        private static ProgressService? Progress => Plugin.Progress?.IsInitialized == true ? Plugin.Progress : null;

        public void Initialize()
        {
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
                var flowRegistry = FlowRegistry;
                if (!string.IsNullOrEmpty(zone.FlowOnEnter) && flowRegistry != null)
                {
                    flowRegistry.ExecuteFlow(zone.FlowOnEnter, player, zone.Name, zone.Hash);
                }

                // Zone message (player-scoped; "broadcast" is treated as a label, not a server-wide broadcast)
                if (zone.OnEnter != null)
                {
                    var message = !string.IsNullOrEmpty(zone.OnEnter.Message) 
                        ? zone.OnEnter.Message 
                        : zone.OnEnter.Broadcast;
                    if (!string.IsNullOrEmpty(message))
                    {
                        if (flowRegistry != null)
                        {
                            flowRegistry.SendMessage(player, message, zone.Hash);
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
                var flowRegistry = FlowRegistry;
                if (!string.IsNullOrEmpty(zone.FlowOnExit) && flowRegistry != null)
                {
                    flowRegistry.ExecuteFlow(zone.FlowOnExit, player, zone.Name, zone.Hash);
                }

                // Zone message (player-scoped)
                if (zone.OnExit != null)
                {
                    var message = !string.IsNullOrEmpty(zone.OnExit.Message) 
                        ? zone.OnExit.Message 
                        : zone.OnExit.Broadcast;
                    if (!string.IsNullOrEmpty(message))
                    {
                        if (flowRegistry != null)
                        {
                            flowRegistry.SendMessage(player, message, zone.Hash);
                        }
                        else
                        {
                            _log.LogInfo($"[ZoneTransition] Message: {message}");
                        }
                    }
                }

                if (!_zoneOccupancy.ContainsKey(zone.Hash))
                {
                    ZoneConfig?.ReleaseRetiredZone(zone.Hash);
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[ZoneTransition] Error on zone exit: {ex.Message}");
            }
        }

        private void HandleBossZoneEnter(Entity player, ZoneDefinition zone)
        {
            if (zone is BossZoneConfig bossCfgCoop && bossCfgCoop.EnableSubclanCoop && Plugin.BossCoop?.IsInitialized == true)
            {
                Plugin.BossCoop.OnBossZoneEnter(player, zone.Hash, bossCfgCoop.ForceJoinClan, bossCfgCoop.ShuffleClan);
            }

            // Boss zones skip progress saving by default (NoProgress = true)
            // This preserves player progression
            if (zone is BossZoneConfig bossConfig && !bossConfig.NoProgress)
            {
                var progress = Progress;
                if (progress != null)
                {
                    progress.SaveProgress(player);
                }
            }

            // Fallback gameplay logic when no explicit flow is configured.
            var flowRegistry = FlowRegistry;
            if (zone is BossZoneConfig bossCfg && string.IsNullOrEmpty(zone.FlowOnEnter) && flowRegistry != null)
            {
                var isFirstPlayer = _zoneOccupancy.TryGetValue(zone.Hash, out var count) && count == 1;
                if (isFirstPlayer)
                {
                    // Spawn boss once per zone occupancy (re-entering players won't re-spawn).
                    if (!string.IsNullOrWhiteSpace(bossCfg.BossPrefab))
                    {
                        flowRegistry.EnsureBosses(player, zone.Hash, bossCfg.BossPrefab, bossCfg.BossQuantity, randomInZone: bossCfg.RandomSpawn);
                    }
                }
            }
        }

        private void HandleBossZoneExit(Entity player, ZoneDefinition zone)
        {
            if (zone is BossZoneConfig bossCfgCoop && bossCfgCoop.EnableSubclanCoop && Plugin.BossCoop?.IsInitialized == true)
            {
                Plugin.BossCoop.OnBossZoneExit(player, zone.Hash);
            }

            // Boss zones skip progress restoration by default (NoProgress = true)
            // This preserves player progression
            if (zone is BossZoneConfig bossConfig && !bossConfig.NoProgress)
            {
                var progress = Progress;
                if (progress != null)
                {
                    progress.RestoreProgress(player, clearAfter: true);
                }
            }

            var flowRegistry = FlowRegistry;
            if (zone is BossZoneConfig && string.IsNullOrEmpty(zone.FlowOnExit) && flowRegistry != null)
            {
                // Remove zone-level effects when last player leaves.
                var isLastPlayer = !_zoneOccupancy.ContainsKey(zone.Hash);
                if (isLastPlayer)
                {
                    flowRegistry.RemoveBosses(zone.Hash);
                }
            }
        }

        private void HandleArenaZoneEnter(Entity player, ZoneDefinition zone)
        {
            if (zone is not ArenaZoneConfig arenaConfig)
            {
                return;
            }

            if (arenaConfig.SaveProgress)
            {
                var progress = Progress;
                if (progress != null)
                {
                    progress.SaveProgress(player);
                }
            }

            // Arena ability set is required for deterministic arena loadouts.
            if (string.IsNullOrWhiteSpace(arenaConfig.AbilitySet))
            {
                _log.LogWarning($"[ZoneTransition] Arena '{zone.Name}' is missing required AbilitySet.");
            }

            // Back-compat: if "abilitySet" is present but Ability UI is removed, treat it as a kit name.
            if (Plugin.Kits?.IsInitialized == true && !string.IsNullOrWhiteSpace(arenaConfig.AbilitySet))
            {
                Plugin.Kits.ApplyKit(player, arenaConfig.AbilitySet);
            }

            // Execute explicit flow when configured; otherwise apply PvP fallback from arena config.
            if (!string.IsNullOrWhiteSpace(zone.FlowOnEnter))
            {
                _log.LogInfo($"[ZoneTransition] Arena '{zone.Name}' enter flow configured: {zone.FlowOnEnter}");
                return;
            }

            var flowRegistry = FlowRegistry;
            if (flowRegistry != null)
            {
                flowRegistry.SetPvp(player, arenaConfig.PvpEnabled, zone.Hash);
                _log.LogInfo($"[ZoneTransition] Arena '{zone.Name}' fallback PvP applied: {arenaConfig.PvpEnabled}");
            }
        }

        private void HandleArenaZoneExit(Entity player, ZoneDefinition zone)
        {
            // Restore progress if enabled
            if (zone is ArenaZoneConfig arenaConfig && arenaConfig.RestoreOnExit)
            {
                var progress = Progress;
                if (progress != null)
                {
                    progress.RestoreProgress(player, clearAfter: true);
                }
            }

            var flowRegistry = FlowRegistry;
            if (zone is ArenaZoneConfig && string.IsNullOrEmpty(zone.FlowOnExit) && flowRegistry != null)
            {
                flowRegistry.SetPvp(player, enabled: false, zone.Hash);
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

            if (ZoneConfig?.TryGetZoneByHash(hash, out var zone) == true)
                return zone.Type == zoneType;

            return false;
        }
    }
}
