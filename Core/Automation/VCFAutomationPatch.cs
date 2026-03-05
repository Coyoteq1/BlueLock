using System;
using VampireCommandFramework;
using VAutomationCore.Core.Logging;
using VAutomationCore.Core.Automation;
using VAutomationCore.Core;
using VAutomationCore.Core.Contracts;
using VAutomationCore.Core.Services;
using VAutomationCore.Core.Extensions;

namespace VAutomationCore.Core.Automation
{
    /// <summary>
    /// ModCommunication-based automation system for dynamic command registration and execution.
    /// This approach is cleaner, safer, and more scalable than Harmony patching.
    /// 
    /// Architecture:
    /// - Static commands (.vreg, .vlist, etc.) registered via CommandRegistry
    /// - Dynamic commands stored as JSON rules with ModCommunication identifiers
    /// - ModCommunicationService for inter-mod communication
    /// - No Harmony patching required
    /// </summary>
    public static class CrossModsCommandsPatch
    {
        private static readonly CoreLogger _log = new CoreLogger("CrossModsCommandsPatch");
        private static bool _subscribed;

        private sealed class DynamicCommandMessage
        {
            public string Command { get; set; } = string.Empty;
            public object[]? Args { get; set; }
        }

        /// <summary>
        /// Initialize ModCommunication automation system.
        /// This should be called during plugin initialization.
        /// </summary>
        public static void Initialize()
        {
            try
            {
                // Initialize the communication service
                ModCommunicationService.Instance.Initialize();
                if (!_subscribed)
                {
                    SubscribeToCommands((fromMod, payload) =>
                    {
                        try
                        {
                            switch (payload)
                            {
                                case DynamicCommandMessage envelope when !string.IsNullOrWhiteSpace(envelope.Command):
                                    _ = AutomationService.Instance.ExecuteRule(envelope.Command);
                                    break;
                                case string cmd when !string.IsNullOrWhiteSpace(cmd):
                                    _ = AutomationService.Instance.ExecuteRule(cmd);
                                    break;
                            }
                        }
                        catch (Exception ex)
                        {
                            _log.Error($"Failed to process dynamic command message from '{fromMod}': {ex.Message}");
                        }
                    });
                    _subscribed = true;
                }
                
                _log.Info("ModCommunication automation system initialized");
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to initialize ModCommunication automation: {ex.Message}");
            }
        }

        /// <summary>
        /// Register a dynamic command for ModCommunication execution.
        /// </summary>
        public static bool RegisterModCommunicationCommand(string commandName, AutomationRule rule)
        {
            try
            {
                // Store the rule in AutomationService
                AutomationService.Instance.RegisterRule(rule);
                
                _log.Info($"ModCommunication command '{commandName}' registered");
                return true;
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to register ModCommunication command '{commandName}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Execute a dynamic command via ModCommunication.
        /// </summary>
        public static void ExecuteCommand(string commandName, object[] args = null)
        {
            try
            {
                // Send the command to other mods via ModCommunication.
                ModCommunicationService.Instance.SendToMod("VAutomationCore", "automation.command", new DynamicCommandMessage
                {
                    Command = commandName,
                    Args = args
                });
                
                _log.Info($"Command '{commandName}' executed via ModCommunication");
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to execute ModCommunication command '{commandName}': {ex.Message}");
            }
        }

        /// <summary>
        /// Subscribe to dynamic command execution requests.
        /// </summary>
        public static void SubscribeToCommands(Action<string, object> handler)
        {
            try
            {
                ModCommunicationService.Instance.Subscribe("VAutomationCore", "automation.command", handler);
                _log.Info("Subscribed to dynamic command execution requests");
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to subscribe to dynamic commands: {ex.Message}");
            }
        }
    }
}
