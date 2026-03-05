using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using ProjectM;
using VAuto.Services.Interfaces;
using Blueluck.Services;
using Blueluck.Models;

namespace Blueluck.Services
{
    /// <summary>
    /// Validation result for a single flow action.
    /// </summary>
    public class FlowActionValidationResult
    {
        public bool IsValid { get; private set; }
        public bool IsWarning { get; private set; }
        public string ActionType { get; private set; } = string.Empty;
        public string? ErrorMessage { get; private set; }
        public string? ResolvedPrefab { get; private set; }
        public PrefabGUID? ResolvedGuid { get; private set; }

        public static FlowActionValidationResult Success(string actionType, string? resolvedPrefab = null, PrefabGUID? resolvedGuid = null)
        {
            return new FlowActionValidationResult { IsValid = true, ActionType = actionType, ResolvedPrefab = resolvedPrefab, ResolvedGuid = resolvedGuid };
        }

        public static FlowActionValidationResult Failure(string actionType, string error)
        {
            return new FlowActionValidationResult { IsValid = false, ActionType = actionType, ErrorMessage = error };
        }

        public static FlowActionValidationResult Warning(string actionType, string prefab, string warningMessage)
        {
            return new FlowActionValidationResult { IsValid = true, IsWarning = true, ActionType = actionType, ResolvedPrefab = prefab, ErrorMessage = warningMessage };
        }
    }

    /// <summary>
    /// Validation result for an entire flow.
    /// </summary>
    public class FlowValidationResult
    {
        public bool IsValid => Errors.Count == 0;
        public string FlowId { get; }
        public List<FlowActionValidationResult> ActionResults { get; } = new();
        public List<string> Errors => ActionResults.Where(r => !r.IsValid).Select(r => r.ErrorMessage).Where(e => e != null).Cast<string>().ToList();
        public List<string> Warnings { get; } = new();

        public FlowValidationResult(string flowId)
        {
            FlowId = flowId;
        }

        public void AddResult(FlowActionValidationResult result)
        {
            ActionResults.Add(result);
        }

        public string GetSummary()
        {
            if (IsValid)
                return $"Flow '{FlowId}': Valid ({ActionResults.Count} actions)";
            
            return $"Flow '{FlowId}': Invalid - {string.Join("; ", Errors)}";
        }
    }

    /// <summary>
    /// Service for validating flows, actions, and references before execution.
    /// Prevents runtime crashes by validating prefabs, kits, and abilities exist.
    /// </summary>
    public class FlowValidationService : IService
    {
        private static readonly ManualLogSource _log = Logger.CreateLogSource("Blueluck.FlowValidation");
        
        public bool IsInitialized { get; private set; }
        public ManualLogSource Log => _log;

        private PrefabToGuidService? _prefabToGuid;
        private KitService? _kitService;
        private ZoneConfigService? _zoneConfig;
        private FlowRegistryService? _flowRegistry;

        // Known valid VBlood prefabs (built-in validation)
        private static readonly HashSet<string> KnownVBloodPrefabs = new(StringComparer.OrdinalIgnoreCase)
        {
            "CHAR_Gloomrot_Purifier_VBlood",
            "CHAR_Bandit_Tourok_VBlood",
            "CHAR_Bandit_StoneBreaker_VBlood",
            "CHAR_Bandit_Stalker_VBlood",
            "CHAR_Bandit_Foreman_VBlood",
            "CHAR_ArchMage_VBlood",
            "CHAR_Vampire_Dracula_VBlood",
            "CHAR_Vampire_Sun_X_Clone",
            "CHAR_Vampire_Lategame_01",
            "CHAR_Vampire_Lategame_02",
            "CHAR_Vampire_Lategame_03",
            "CHAR_Vampire_Clan_01",
            "CHAR_Vampire_Clan_02",
            "CHAR_Vampire_Clan_03",
            "CHAR_Vampire_Clan_04",
            "CHAR_Vampire_Clan_05"
        };

        // Known valid VFX prefabs
        private static readonly HashSet<string> KnownVfxPrefabs = new(StringComparer.OrdinalIgnoreCase)
        {
            "VFX_Zone_Border_01",
            "VFX_Zone_Border_Red_01",
            "VFX_Zone_Border_Blue_01",
            "VFX_Zone_Border_Gold_01"
        };

        // Known buff prefabs
        private static readonly HashSet<string> KnownBuffPrefabs = new(StringComparer.OrdinalIgnoreCase)
        {
            "Buff_PvP_Enabled",
            "Buff_PvE_Enabled"
        };

        public void Initialize()
        {
            _prefabToGuid = Plugin.PrefabToGuid;
            _kitService = Plugin.Kits;
            _zoneConfig = Plugin.ZoneConfig;
            _flowRegistry = Plugin.FlowRegistry;

            IsInitialized = true;
            _log.LogInfo("[FlowValidation] Initialized.");
        }

        public void Cleanup()
        {
            IsInitialized = false;
            _log.LogInfo("[FlowValidation] Cleaned up.");
        }

        /// <summary>
        /// Sets dependencies after all services are initialized.
        /// </summary>
        public void SetDependencies(PrefabToGuidService prefabToGuid, KitService kitService, ZoneConfigService zoneConfig, FlowRegistryService flowRegistry)
        {
            _prefabToGuid = prefabToGuid;
            _kitService = kitService;
            _zoneConfig = zoneConfig;
            _flowRegistry = flowRegistry;
            _log.LogInfo("[FlowValidation] Dependencies set.");
        }

        /// <summary>
        /// Validates a single flow action.
        /// </summary>
        public FlowActionValidationResult ValidateAction(FlowAction action)
        {
            if (string.IsNullOrEmpty(action.Action))
                return FlowActionValidationResult.Failure("unknown", "Action type is empty");

            var actionType = action.Action.ToLowerInvariant();

            try
            {
                return actionType switch
                {
                    "zone.setpvp" => ValidateSetPvp(action),
                    "zone.sendmessage" => ValidateSendMessage(action),
                    "zone.spawnboss" => ValidateSpawnBoss(action),
                    "zone.removeboss" => ValidateRemoveBoss(action),
                    "zone.applyborderfx" => ValidateApplyBorderFx(action),
                    "zone.removeborderfx" => ValidateRemoveBorderFx(action),
                    "zone.applykit" => ValidateApplyKit(action),
                    "zone.removekit" => ValidateRemoveKit(action),
                    "zone.applyzonebuff" => ValidateApplyZoneBuff(action),
                    "zone.removezonebuff" => ValidateRemoveZoneBuff(action),
                    _ => FlowActionValidationResult.Failure(action.Action, $"Unknown action type: {action.Action}")
                };
            }
            catch (Exception ex)
            {
                _log.LogError($"[FlowValidation] Error validating action {action.Action}: {ex.Message}");
                return FlowActionValidationResult.Failure(action.Action, $"Validation error: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates an entire flow by ID.
        /// </summary>
        public FlowValidationResult ValidateFlow(string flowId)
        {
            var result = new FlowValidationResult(flowId);

            if (_flowRegistry == null || !_flowRegistry.IsInitialized)
            {
                result.Warnings.Add("FlowRegistry not initialized - cannot validate");
                return result;
            }

            // This would require exposing the flows dictionary from FlowRegistryService
            // For now, we provide action-level validation
            result.Warnings.Add("Flow-level validation requires FlowRegistry integration");
            return result;
        }

        /// <summary>
        /// Validates a flow action with full context.
        /// </summary>
        public FlowActionValidationResult ValidateAction(FlowAction action, Entity player, string zoneId, int zoneHash)
        {
            // First validate the action itself
            var result = ValidateAction(action);

            // If valid, also validate zone context
            if (result.IsValid && _zoneConfig != null)
            {
                if (!_zoneConfig.TryGetZoneByHash(zoneHash, out var zone) || zone == null)
                {
                    // Not a hard error - zone might be dynamically created
                    result = FlowActionValidationResult.Success(result.ActionType, result.ResolvedPrefab, result.ResolvedGuid);
                }
            }

            return result;
        }

        private FlowActionValidationResult ValidateSetPvp(FlowAction action)
        {
            // Validate the PvP buff prefab exists
            const string pvpBuff = "Buff_PvP_Enabled";
            
            if (_prefabToGuid != null && _prefabToGuid.IsInitialized)
            {
                if (_prefabToGuid.TryGetGuid(pvpBuff, out var guid))
                {
                    return FlowActionValidationResult.Success("zone.setpvp", pvpBuff, guid);
                }
            }

            // Check known buffs
            if (KnownBuffPrefabs.Contains(pvpBuff))
            {
                return FlowActionValidationResult.Success("zone.setpvp", pvpBuff + " (known)");
            }

            return FlowActionValidationResult.Failure("zone.setpvp", $"PvP buff prefab '{pvpBuff}' not found");
        }

        private FlowActionValidationResult ValidateSendMessage(FlowAction action)
        {
            var message = action.Message ?? action.Value?.ToString();
            if (string.IsNullOrWhiteSpace(message))
            {
                return FlowActionValidationResult.Failure("zone.sendmessage", "Message is empty");
            }

            if (message.Length > 500)
            {
                return FlowActionValidationResult.Failure("zone.sendmessage", $"Message too long ({message.Length} > 500 chars)");
            }

            return FlowActionValidationResult.Success("zone.sendmessage", $"\"{message.Substring(0, Math.Min(50, message.Length))}...\"");
        }

        private FlowActionValidationResult ValidateSpawnBoss(FlowAction action)
        {
            var bossPrefab = action.Prefab?.ToString();
            if (string.IsNullOrWhiteSpace(bossPrefab))
            {
                return FlowActionValidationResult.Failure("zone.spawnboss", "Boss prefab is empty");
            }

            // Check if it's a known VBlood
            if (KnownVBloodPrefabs.Contains(bossPrefab))
            {
                return FlowActionValidationResult.Success("zone.spawnboss", bossPrefab + " (known VBlood)");
            }

            // Try to resolve via PrefabToGuidService
            if (_prefabToGuid != null && _prefabToGuid.IsInitialized)
            {
                if (_prefabToGuid.TryGetGuid(bossPrefab, out var guid))
                {
                    return FlowActionValidationResult.Success("zone.spawnboss", bossPrefab, guid);
                }
            }

            // Check if it's a valid prefab pattern (CHAR_*)
            if (bossPrefab.StartsWith("CHAR_", StringComparison.OrdinalIgnoreCase))
            {
                return FlowActionValidationResult.Warning("zone.spawnboss", bossPrefab, "Prefabricated but not in known list");
            }

            return FlowActionValidationResult.Failure("zone.spawnboss", $"Boss prefab '{bossPrefab}' not found");
        }

        private FlowActionValidationResult ValidateRemoveBoss(FlowAction action)
        {
            // No parameters needed for remove
            return FlowActionValidationResult.Success("zone.removeboss");
        }

        private FlowActionValidationResult ValidateApplyBorderFx(FlowAction action)
        {
            var vfxPrefab = action.VfxPrefab?.ToString();
            if (string.IsNullOrWhiteSpace(vfxPrefab))
            {
                return FlowActionValidationResult.Failure("zone.applyborderfx", "VFX prefab is empty");
            }

            // Check known VFX
            if (KnownVfxPrefabs.Contains(vfxPrefab))
            {
                return FlowActionValidationResult.Success("zone.applyborderfx", vfxPrefab + " (known VFX)");
            }

            // Try to resolve
            if (_prefabToGuid != null && _prefabToGuid.IsInitialized)
            {
                if (_prefabToGuid.TryGetGuid(vfxPrefab, out var guid))
                {
                    return FlowActionValidationResult.Success("zone.applyborderfx", vfxPrefab, guid);
                }
            }

            // VFX might be optional - return warning
            return FlowActionValidationResult.Warning("zone.applyborderfx", vfxPrefab, "VFX prefab not found - will skip silently");
        }

        private FlowActionValidationResult ValidateRemoveBorderFx(FlowAction action)
        {
            return FlowActionValidationResult.Success("zone.removeborderfx");
        }

        private FlowActionValidationResult ValidateApplyKit(FlowAction action)
        {
            var kitName = action.Value?.ToString();
            if (string.IsNullOrWhiteSpace(kitName))
            {
                return FlowActionValidationResult.Failure("zone.applykit", "Kit name is empty");
            }

            // Check if kit exists
            if (_kitService != null && _kitService.IsInitialized)
            {
                if (_kitService.KitExists(kitName))
                {
                    return FlowActionValidationResult.Success("zone.applykit", kitName);
                }
            }

            return FlowActionValidationResult.Failure("zone.applykit", $"Kit '{kitName}' not found");
        }

        private FlowActionValidationResult ValidateRemoveKit(FlowAction action)
        {
            // Kit removal doesn't require the kit to exist
            return FlowActionValidationResult.Success("zone.removekit");
        }

        private FlowActionValidationResult ValidateApplyZoneBuff(FlowAction action)
        {
            var buffPrefab = action.BuffPrefab ?? action.Value?.ToString();
            if (string.IsNullOrWhiteSpace(buffPrefab))
            {
                return FlowActionValidationResult.Failure("zone.applyzonebuff", "Buff prefab is empty");
            }

            // Check known buffs
            if (KnownBuffPrefabs.Contains(buffPrefab))
            {
                return FlowActionValidationResult.Success("zone.applyzonebuff", buffPrefab + " (known)");
            }

            // Try to resolve
            if (_prefabToGuid != null && _prefabToGuid.IsInitialized)
            {
                if (_prefabToGuid.TryGetGuid(buffPrefab, out var guid))
                {
                    return FlowActionValidationResult.Success("zone.applyzonebuff", buffPrefab, guid);
                }
            }

            return FlowActionValidationResult.Failure("zone.applyzonebuff", $"Buff prefab '{buffPrefab}' not found");
        }

        private FlowActionValidationResult ValidateRemoveZoneBuff(FlowAction action)
        {
            return FlowActionValidationResult.Success("zone.removezonebuff");
        }

        /// <summary>
        /// Validates all configured flows at startup.
        /// </summary>
        public List<FlowValidationResult> ValidateAllFlows()
        {
            var results = new List<FlowValidationResult>();

            if (_flowRegistry == null || !_flowRegistry.IsInitialized)
            {
                _log.LogWarning("[FlowValidation] Cannot validate flows - FlowRegistry not initialized");
                return results;
            }

            // This would require exposing flow data from FlowRegistryService
            // Placeholder for integration
            _log.LogInfo("[FlowValidation] Flow validation at startup not yet integrated with FlowRegistry");

            return results;
        }

        /// <summary>
        /// Validates a zone's flow references.
        /// </summary>
        public FlowValidationResult ValidateZoneFlows(ZoneDefinition zone)
        {
            var result = new FlowValidationResult($"zone_{zone.Name}");

            if (zone is ArenaZoneConfig arenaZone && string.IsNullOrWhiteSpace(arenaZone.AbilitySet))
            {
                result.Warnings.Add($"Arena zone '{zone.Name}' should set AbilitySet.");
            }

            if (!string.IsNullOrEmpty(zone.FlowOnEnter))
            {
                result.Warnings.Add($"Flow '{zone.FlowOnEnter}' on enter - validation not yet implemented");
            }

            if (!string.IsNullOrEmpty(zone.FlowOnExit))
            {
                result.Warnings.Add($"Flow '{zone.FlowOnExit}' on exit - validation not yet implemented");
            }

            if (!string.IsNullOrEmpty(zone.KitOnEnter))
            {
                if (_kitService != null && _kitService.IsInitialized && !_kitService.KitExists(zone.KitOnEnter))
                {
                    result.Warnings.Add($"Kit '{zone.KitOnEnter}' referenced but may not exist");
                }
            }

            return result;
        }
    }

    /// <summary>
    /// Extension methods for FlowActionValidationResult.
    /// </summary>
    public static class FlowActionValidationResultExtensions
    {
        public static FlowActionValidationResult Warning(this FlowActionValidationResult result, string actionType, string prefab, string warning)
        {
            return FlowActionValidationResult.Warning(actionType, prefab, $"Warning: {warning}");
        }
    }
}
