using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using VAuto.Zone.Core;
using VAuto.Zone.Models;
using VAutomationCore.Core.Config;

namespace VAuto.Zone.Services
{
    public static class ProcessConfigService
    {
        public class ValidationResult
        {
            public bool Success { get; set; } = true;
            public List<string> Errors { get; set; } = new();
            public List<string> Warnings { get; set; } = new();
            public Dictionary<string, object> ConfigData { get; set; } = new();
        }

        public static ValidationResult ValidateAllConfigs(string baseConfigPath)
        {
            var result = new ValidationResult();
            var validators = new IZoneConfigValidator[]
            {
                new SchemaPresenceValidator(),
                new ZonesConfigValidator(),
                new ZoneLifecycleConfigValidator(),
                new FlowFilesValidator(),
                new DatabaseFilesValidator()
            };

            foreach (var validator in validators)
            {
                try
                {
                    var valid = validator.Validate(baseConfigPath, result.Errors, result.Warnings);
                    if (!valid)
                    {
                        result.Success = false;
                    }
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Errors.Add($"{validator.Name}: validator crashed: {ex.Message}");
                }
            }

            return result;
        }

        private sealed class SchemaPresenceValidator : IZoneConfigValidator
        {
            public string Name => nameof(SchemaPresenceValidator);

            public bool Validate(string baseConfigPath, IList<string> errors, IList<string> warnings)
            {
                var schemaPath = Path.Combine(AppContext.BaseDirectory, "config", "VAuto.unified_config.schema.json");
                if (!File.Exists(schemaPath))
                {
                    warnings.Add("Schema file not found at runtime: config/VAuto.unified_config.schema.json");
                }

                return true;
            }
        }

        private sealed class ZonesConfigValidator : IZoneConfigValidator
        {
            public string Name => nameof(ZonesConfigValidator);

            public bool Validate(string baseConfigPath, IList<string> errors, IList<string> warnings)
            {
                var zonesPath = Path.Combine(baseConfigPath, "VAuto.Zones.json");
                if (!File.Exists(zonesPath))
                {
                    errors.Add($"Missing zones config: {zonesPath}");
                    return false;
                }

                var json = File.ReadAllText(zonesPath);
                var config = JsonSerializer.Deserialize<ZonesConfig>(json, new JsonSerializerOptions(ZoneJsonOptions.WithUnityMathConverters)
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                });

                if (config?.Zones == null)
                {
                    errors.Add("VAuto.Zones.json did not deserialize to a valid zones list.");
                    return false;
                }

                var idSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var zone in config.Zones)
                {
                    var zoneId = zone?.Id?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(zoneId))
                    {
                        errors.Add("Zone id cannot be empty.");
                        continue;
                    }

                    if (!idSet.Add(zoneId))
                    {
                        errors.Add($"Duplicate zone id: {zoneId}");
                    }

                    var entry = zone.EntryRadius > 0 ? zone.EntryRadius : zone.Radius;
                    var exit = zone.ExitRadius > 0 ? zone.ExitRadius : zone.Radius;
                    if (entry > exit)
                    {
                        errors.Add($"Zone '{zoneId}' invalid radii: entryRadius ({entry}) > exitRadius ({exit}).");
                    }

                    if (!string.IsNullOrWhiteSpace(zone.FlowId))
                    {
                        // Flow validity checked in FlowFilesValidator.
                    }
                    else
                    {
                        errors.Add($"Zone '{zoneId}' has empty FlowId.");
                    }
                }

                return errors.Count == 0;
            }
        }

        private sealed class ZoneLifecycleConfigValidator : IZoneConfigValidator
        {
            public string Name => nameof(ZoneLifecycleConfigValidator);

            private static readonly HashSet<string> KnownActions = new(StringComparer.OrdinalIgnoreCase)
            {
                "capture_return_position",
                "snapshot_save",
                "zone_enter_message",
                "apply_kit",
                "teleport_enter",
                "apply_templates",
                "apply_abilities",
                "integration_events_enter",
                "announce_enter",
                "zone_exit_message",
                "restore_kit_snapshot",
                "restore_abilities",
                "teleport_return",
                "integration_events_exit",
                "boss_enter",
                "boss_exit",
                "clear_template",
                "apply_template",
                "player_tag",
                "glow_spawn",
                "glow_reset"
            };

            public bool Validate(string baseConfigPath, IList<string> errors, IList<string> warnings)
            {
                var lifecyclePath = Path.Combine(baseConfigPath, "VAuto.ZoneLifecycle.json");
                if (!File.Exists(lifecyclePath))
                {
                    errors.Add($"Missing lifecycle config: {lifecyclePath}");
                    return false;
                }

                var json = File.ReadAllText(lifecyclePath);
                var config = JsonSerializer.Deserialize<ZoneLifecycleConfigModel>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                });

                if (config?.Mappings == null || config.Mappings.Count == 0)
                {
                    errors.Add("VAuto.ZoneLifecycle.json has no mappings.");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(config.SchemaVersion))
                {
                    warnings.Add("VAuto.ZoneLifecycle.json is missing schemaVersion; migration should backfill this.");
                }
                else if (!string.Equals(config.SchemaVersion, ZoneJsonConfig.CurrentConfigVersion, StringComparison.Ordinal))
                {
                    warnings.Add($"VAuto.ZoneLifecycle.json schemaVersion '{config.SchemaVersion}' differs from runtime '{ZoneJsonConfig.CurrentConfigVersion}'.");
                }

                foreach (var kvp in config.Mappings)
                {
                    var mappingKey = kvp.Key;
                    var mapping = kvp.Value;
                    if (mapping == null)
                    {
                        errors.Add($"Zone lifecycle mapping '{mappingKey}' is null.");
                        continue;
                    }

                    ValidateActionList(mappingKey, "onEnter", mapping.OnEnter, errors, warnings);
                    ValidateActionList(mappingKey, "onExit", mapping.OnExit, errors, warnings);
                }

                return errors.Count == 0;
            }

            private static void ValidateActionList(string zone, string phase, string[] actions, IList<string> errors, IList<string> warnings)
            {
                if (actions == null || actions.Length == 0)
                {
                    warnings.Add($"Zone lifecycle mapping '{zone}' has empty {phase} actions.");
                    return;
                }

                foreach (var action in actions)
                {
                    var token = action?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(token))
                    {
                        errors.Add($"Zone lifecycle mapping '{zone}' contains empty action token in {phase}.");
                        continue;
                    }

                    var knownToken = token;
                    if (LifecycleActionToken.TryParse(token, out var baseAction, out _))
                    {
                        knownToken = baseAction;
                    }

                    if (!KnownActions.Contains(knownToken))
                    {
                        warnings.Add($"Zone lifecycle mapping '{zone}' uses unknown action token '{token}' in {phase}.");
                    }
                }
            }

            private sealed class ZoneLifecycleConfigModel
            {
                public string SchemaVersion { get; set; } = string.Empty;
                public Dictionary<string, ZoneLifecycleMapping> Mappings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
            }

            private sealed class ZoneLifecycleMapping
            {
                public string[] OnEnter { get; set; } = Array.Empty<string>();
                public string[] OnExit { get; set; } = Array.Empty<string>();
            }
        }

        private sealed class FlowFilesValidator : IZoneConfigValidator
        {
            public string Name => nameof(FlowFilesValidator);

            public bool Validate(string baseConfigPath, IList<string> errors, IList<string> warnings)
            {
                var zonesPath = Path.Combine(baseConfigPath, "VAuto.Zones.json");
                if (!File.Exists(zonesPath))
                {
                    return false;
                }

                var zonesJson = File.ReadAllText(zonesPath);
                var zones = JsonSerializer.Deserialize<ZonesConfig>(zonesJson, new JsonSerializerOptions(ZoneJsonOptions.WithUnityMathConverters)
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                });

                if (zones?.Zones == null)
                {
                    return false;
                }

                var flowDir = Path.Combine(baseConfigPath, "flows");
                if (!Directory.Exists(flowDir))
                {
                    if (Plugin.ConfigRequireZoneFlowFiles?.Value == true)
                    {
                        errors.Add($"Missing flows folder: {flowDir}");
                    }
                    else
                    {
                        warnings.Add($"Flows folder not found: {flowDir}");
                    }

                    return errors.Count == 0;
                }

                var autoParseFolder = Plugin.ConfigAutoParseFlowFolder?.Value ?? true;
                var requireZoneFlowFiles = Plugin.ConfigRequireZoneFlowFiles?.Value ?? false;
                var discoveredFlowIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                if (autoParseFolder)
                {
                    foreach (var flowPath in Directory.GetFiles(flowDir, "*.json", SearchOption.TopDirectoryOnly))
                    {
                        try
                        {
                            var text = File.ReadAllText(flowPath);
                            _ = JsonDocument.Parse(text);
                            discoveredFlowIds.Add(Path.GetFileNameWithoutExtension(flowPath));
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"Invalid flow JSON '{flowPath}': {ex.Message}");
                        }
                    }
                }

                foreach (var zone in zones.Zones)
                {
                    var flowId = zone?.FlowId?.Trim();
                    if (string.IsNullOrWhiteSpace(flowId))
                    {
                        continue;
                    }

                    var flowPath = Path.Combine(flowDir, flowId + ".json");
                    var exists = File.Exists(flowPath) || discoveredFlowIds.Contains(flowId);
                    if (exists)
                    {
                        continue;
                    }

                    if (requireZoneFlowFiles)
                    {
                        errors.Add($"Missing flow file for zone '{zone.Id}': {flowPath}");
                    }
                    else
                    {
                        warnings.Add($"Zone '{zone.Id}' references missing flow file: {flowPath}");
                    }
                }

                return errors.Count == 0;
            }
        }

        private sealed class DatabaseFilesValidator : IZoneConfigValidator
        {
            public string Name => nameof(DatabaseFilesValidator);

            public bool Validate(string baseConfigPath, IList<string> errors, IList<string> warnings)
            {
                var autoParseFolder = Plugin.ConfigAutoParseDatabaseFolder?.Value ?? true;
                if (!autoParseFolder)
                {
                    return true;
                }

                var databaseDir = Path.Combine(baseConfigPath, "database");
                if (!Directory.Exists(databaseDir))
                {
                    warnings.Add($"Database folder not found: {databaseDir}");
                    return true;
                }

                foreach (var dbPath in Directory.GetFiles(databaseDir, "*.json", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        var text = File.ReadAllText(dbPath);
                        _ = JsonDocument.Parse(text);
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Invalid database JSON '{dbPath}': {ex.Message}");
                    }
                }

                return errors.Count == 0;
            }
        }
    }
}
