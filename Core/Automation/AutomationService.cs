using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Unity.Entities;
using VAutomationCore.Core.Logging;
using VAutomationCore.Core.Api;
using VampireCommandFramework;
using BepInEx;
using VAutomationCore.Core.Extensions;

namespace VAutomationCore.Core.Automation
{
    /// <summary>
    /// Service for managing dynamic command registration and execution.
    /// Provides runtime command registration and VCF integration.
    /// </summary>
    public class AutomationService
    {
        private static AutomationService _instance;
        private static readonly object _lock = new object();
        
        private readonly ConcurrentDictionary<string, AutomationRule> _rules = new(StringComparer.OrdinalIgnoreCase);
        private readonly CoreLogger _log;
        private readonly string _configPath;
        private readonly JsonSerializerOptions _jsonOptions;

        /// <summary>
        /// Gets the singleton instance of the AutomationService.
        /// </summary>
        public static AutomationService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new AutomationService();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Initializes a new instance of the AutomationService class.
        /// </summary>
        private AutomationService()
        {
            _log = new CoreLogger("AutomationService");
            _configPath = Path.Combine(Paths.ConfigPath, "VAutomationCore", "automation_rules.json");
            
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            LoadRules();
        }

        /// <summary>
        /// Registers a new automation rule.
        /// </summary>
        /// <param name="rule">The rule to register.</param>
        /// <returns>True if registration was successful, false otherwise.</returns>
        public bool RegisterRule(AutomationRule rule)
        {
            if (rule == null)
            {
                _log.Error("Cannot register null rule");
                return false;
            }

            if (string.IsNullOrEmpty(rule.Id))
            {
                _log.Error("Rule ID cannot be null or empty");
                return false;
            }

            if (rule.Trigger == null || string.IsNullOrEmpty(rule.Trigger.Command))
            {
                _log.Error("Rule trigger command cannot be null or empty");
                return false;
            }

            if (rule.Action == null)
            {
                _log.Error("Rule action cannot be null");
                return false;
            }

            // Clean the command name (remove leading dots)
            var cleanCommand = rule.Trigger.Command.TrimStart('.');
            rule.Trigger.Command = cleanCommand;
            rule.LastModified = DateTime.UtcNow;

            if (_rules.TryAdd(cleanCommand, rule))
            {
                _log.Info($"Registered automation rule: {rule.Name} (command: .{cleanCommand})");
                return true;
            }
            else
            {
                _log.Warning($"Failed to register rule: {rule.Name} - command already exists");
                return false;
            }
        }

        /// <summary>
        /// Unregisters an automation rule by command name.
        /// </summary>
        /// <param name="command">The command name to unregister.</param>
        /// <returns>True if unregistration was successful, false otherwise.</returns>
        public bool UnregisterRule(string command)
        {
            var cleanCommand = command.TrimStart('.');
            
            if (_rules.TryRemove(cleanCommand, out var rule))
            {
                _log.Info($"Unregistered automation rule: {rule.Name} (command: .{cleanCommand})");
                return true;
            }
            else
            {
                _log.Warning($"Failed to unregister rule: command .{cleanCommand} not found");
                return false;
            }
        }

        /// <summary>
        /// Gets an automation rule by command name.
        /// </summary>
        /// <param name="command">The command name.</param>
        /// <returns>The automation rule if found, null otherwise.</returns>
        public AutomationRule GetRule(string command)
        {
            var cleanCommand = command.TrimStart('.');
            _rules.TryGetValue(cleanCommand, out var rule);
            return rule;
        }

        /// <summary>
        /// Gets all registered automation rules.
        /// </summary>
        /// <returns>A collection of all registered rules.</returns>
        public IEnumerable<AutomationRule> GetAllRules()
        {
            return _rules.Values.ToList();
        }

        /// <summary>
        /// Checks if a command is registered as an automation rule.
        /// </summary>
        /// <param name="command">The command name to check.</param>
        /// <returns>True if the command is registered, false otherwise.</returns>
        public bool IsRegisteredCommand(string command)
        {
            var cleanCommand = command.TrimStart('.');
            return _rules.ContainsKey(cleanCommand);
        }

        /// <summary>
        /// Executes an automation rule by command name.
        /// </summary>
        /// <param name="command">The command name to execute.</param>
        /// <param name="ctx">The chat command context.</param>
        /// <returns>True if execution was successful, false otherwise.</returns>
        public async Task<bool> ExecuteRule(string command, ChatCommandContext? ctx = null)
        {
            var cleanCommand = command.TrimStart('.');
            
            if (!_rules.TryGetValue(cleanCommand, out var rule))
            {
                return false;
            }

            if (!rule.Enabled)
            {
                ctx?.Reply($"Command .{cleanCommand} is currently disabled.");
                return false;
            }

            try
            {
                await ExecuteAction(rule.Action, ctx);
                _log.Info($"Executed automation rule: {rule.Name} (command: .{cleanCommand})");
                return true;
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to execute automation rule {rule.Name}: {ex.Message}");
                ctx?.ReplyError($"Failed to execute command: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Executes an action with the given context.
        /// </summary>
        /// <param name="action">The action to execute.</param>
        /// <param name="ctx">The chat command context.</param>
        private async Task ExecuteAction(IAction action, ChatCommandContext? ctx = null)
        {
            switch (action)
            {
                case SequenceAction sequence:
                    await ExecuteSequenceAction(sequence, ctx);
                    break;
                case ConditionalAction conditional:
                    await ExecuteConditionalAction(conditional, ctx);
                    break;
                case DelayAction delay:
                    await ExecuteDelayAction(delay, ctx);
                    break;
                default:
                    throw new NotSupportedException($"Action type {action.ActionType} is not supported");
            }
        }

        /// <summary>
        /// Executes a sequence action.
        /// </summary>
        /// <param name="sequence">The sequence action to execute.</param>
        /// <param name="ctx">The chat command context.</param>
        private async Task ExecuteSequenceAction(SequenceAction sequence, ChatCommandContext? ctx = null)
        {
            foreach (var command in sequence.Commands)
            {
                if (string.IsNullOrEmpty(command))
                    continue;

                try
                {
                    // Execute the command via available runtime executors.
                    await Task.Run(() =>
                    {
                        try
                        {
                            // First try VCF runtime execution path.
                            if (TryExecuteVcfCommandRuntime(command.Trim()))
                            {
                                _log.Info($"Executed VCF command: {command.Trim()}");
                                return;
                            }

                            // Fallback: map common chat-like commands to game actions.
                            if (TryExecuteMappedCommand(command.Trim()))
                            {
                                _log.Info($"Executed mapped command: {command.Trim()}");
                                return;
                            }

                            throw new InvalidOperationException($"Unsupported command syntax: {command}");
                        }
                        catch (Exception ex)
                        {
                            _log.Error($"Failed to execute command '{command}': {ex.Message}");
                            ctx?.ReplyError($"Failed to execute command: {command}");
                        }
                    });

                    // Small delay between commands to prevent overwhelming the server
                    await Task.Delay(100);
                }
                catch (Exception ex)
                {
                    _log.Error($"Error executing command '{command}': {ex.Message}");
                    ctx?.ReplyError($"Error executing command: {command}");
                }
            }
        }

        private static bool TryExecuteMappedCommand(string rawCommand)
        {
            if (string.IsNullOrWhiteSpace(rawCommand))
            {
                return false;
            }

            var trimmed = rawCommand.Trim();
            if (!trimmed.StartsWith(".", StringComparison.Ordinal))
            {
                return false;
            }

            var commandText = trimmed.TrimStart('.');
            var split = commandText.IndexOf(' ');
            var name = split >= 0 ? commandText.Substring(0, split) : commandText;
            var arg = split >= 0 ? commandText.Substring(split + 1).Trim() : string.Empty;

            switch (name.ToLowerInvariant())
            {
                case "announce":
                case "broadcast":
                case "say":
                    if (string.IsNullOrWhiteSpace(arg))
                    {
                        return false;
                    }

                    var message = arg.Trim('"', '\'');
                    return VAutomationCore.Services.GameActionService.InvokeAction("sendmessagetoall", new object[] { message });
                default:
                    return false;
            }
        }

        private bool TryExecuteVcfCommandRuntime(string command)
        {
            try
            {
                if (!UnifiedCore.IsInitialized || UnifiedCore.EntityManager == default || string.IsNullOrWhiteSpace(command))
                {
                    return false;
                }

                var registryType = Type.GetType("VampireCommandFramework.CommandRegistry, VampireCommandFramework");
                if (registryType == null)
                {
                    return false;
                }

                foreach (var method in registryType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static))
                {
                    if (!method.Name.Contains("Handle", StringComparison.OrdinalIgnoreCase) &&
                        !method.Name.Contains("Execute", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var parameters = method.GetParameters();
                    if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string))
                    {
                        method.Invoke(null, new object[] { command });
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Warning($"VCF runtime command execution failed for '{command}': {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Executes a conditional action.
        /// </summary>
        /// <param name="conditional">The conditional action to execute.</param>
        /// <param name="ctx">The chat command context.</param>
        private async Task ExecuteConditionalAction(ConditionalAction conditional, ChatCommandContext? ctx = null)
        {
            // For now, we'll implement a simple condition parser
            // In the future, this could be extended with more complex condition evaluation
            var conditionMet = EvaluateCondition(conditional.Condition, ctx);
            
            var actionToExecute = conditionMet ? conditional.TrueAction : conditional.FalseAction;
            
            if (actionToExecute != null)
            {
                await ExecuteAction(actionToExecute, ctx);
            }
        }

        /// <summary>
        /// Executes a delay action.
        /// </summary>
        /// <param name="delay">The delay action to execute.</param>
        /// <param name="ctx">The chat command context.</param>
        private async Task ExecuteDelayAction(DelayAction delay, ChatCommandContext? ctx = null)
        {
            await Task.Delay(delay.DurationSeconds * 1000);
        }

        /// <summary>
        /// Evaluates a condition string.
        /// </summary>
        /// <param name="condition">The condition to evaluate.</param>
        /// <param name="ctx">The chat command context.</param>
        /// <returns>True if the condition is met, false otherwise.</returns>
        private bool EvaluateCondition(string condition, ChatCommandContext ctx)
        {
            // Simple condition evaluation - could be extended with more complex logic
            if (string.IsNullOrEmpty(condition))
                return false;

            // Example conditions:
            // "admin" - check if user is admin
            // "player_count>5" - check player count
            // "time>18:00" - check time of day
            
            switch (condition.ToLower())
            {
                case "admin":
                    return ctx.IsAdmin;
                default:
                    _log.Warning($"Unknown condition: {condition}");
                    return false;
            }
        }

        /// <summary>
        /// Saves all rules to the JSON configuration file.
        /// </summary>
        public void SaveRules()
        {
            try
            {
                var directory = Path.GetDirectoryName(_configPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var rulesList = _rules.Values.ToList();
                var json = JsonSerializer.Serialize(rulesList, _jsonOptions);
                File.WriteAllText(_configPath, json);
                
                _log.Info($"Saved {_rules.Count} automation rules to {_configPath}");
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to save automation rules: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads rules from the JSON configuration file.
        /// </summary>
        public void LoadRules()
        {
            try
            {
                if (!File.Exists(_configPath))
                {
                    _log.Info("No automation rules file found, starting with empty ruleset");
                    return;
                }

                var json = File.ReadAllText(_configPath);
                var rules = JsonSerializer.Deserialize<List<AutomationRule>>(json, _jsonOptions) ?? new List<AutomationRule>();

                foreach (var rule in rules)
                {
                    RegisterRule(rule);
                }

                _log.Info($"Loaded {_rules.Count} automation rules from {_configPath}");
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to load automation rules: {ex.Message}");
            }
        }

        /// <summary>
        /// Clears all registered rules.
        /// </summary>
        public void ClearRules()
        {
            _rules.Clear();
            _log.Info("Cleared all automation rules");
        }
    }
}
