using System;
using System.Collections.Generic;
using System.Linq;
using VampireCommandFramework;
using VAutomationCore.Core.Logging;
using VAutomationCore.Core.Automation;
using VAutomationCore.Core.Contracts;
using VAutomationCore.Core.Services;
using VAutomationCore.Core.Extensions;

namespace VAutomationCore.Core.Automation
{
    /// <summary>
    /// Provides commands for dynamic command registration and management using ModTalk.
    /// This approach is cleaner, safer, and more scalable than Harmony patching.
    /// 
    /// Commands:
    /// - .vreg &lt;new_cmd&gt; &lt;act1&gt; &lt;act2&gt; &lt;act3&gt; &lt;act4&gt; &lt;act5&gt; - Register new online command
    /// - .vlist - List all registered dynamic commands
    /// - .vremove &lt;command&gt; - Remove a registered dynamic command
    /// - .venable &lt;command&gt; - Enable a disabled dynamic command
    /// - .vdisable &lt;command&gt; - Disable a dynamic command
    /// - .vinfo &lt;command&gt; - Show details of a specific dynamic command
    /// - .vclear - Clear all dynamic commands
    /// - .vtest &lt;command&gt; - Test a dynamic command without executing it
    /// </summary>
    public static class DynamicCommandRegistration
    {
        private static readonly CoreLogger _log = new CoreLogger("DynamicCommands");

        /// <summary>
        /// Initialize the dynamic command system.
        /// This should be called during plugin initialization.
        /// </summary>
        public static void Initialize()
        {
            try
            {
                // Initialize dynamic cross-mod command bridge
                CrossModsCommandsPatch.Initialize();
                
                _log.Info("Dynamic command registration system initialized");
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to initialize dynamic command system: {ex.Message}");
            }
        }

        /// <summary>
        /// Register new online command: .vreg &lt;new_cmd&gt; &lt;act1&gt; &lt;act2&gt; &lt;act3&gt; &lt;act4&gt; &lt;act5&gt;
        /// </summary>
        [Command("vreg", "vr", description: "Register new online command: .vreg <new_cmd> <act1> <act2> <act3> <act4> <act5>", adminOnly: true)]
        public static void RegisterOnlineCommand(
            ChatCommandContext ctx, 
            string newCommandName, 
            string act1, 
            string act2 = null, 
            string act3 = null, 
            string act4 = null, 
            string act5 = null)
        {
            try
            {
                // 1. Clean the command name (remove dots if user added them)
                var triggerCmd = newCommandName.TrimStart('.');

                // 2. Create the Sequence Action
                var commandsList = new List<string> { act1 };
                if (act2 != null) commandsList.Add(act2);
                if (act3 != null) commandsList.Add(act3);
                if (act4 != null) commandsList.Add(act4);
                if (act5 != null) commandsList.Add(act5);

                // 3. Build the Rule
                var newRule = new AutomationRule
                {
                    Id = $"Dynamic_{triggerCmd}_{DateTime.UtcNow.Ticks}",
                    Name = $"Online Registered: {triggerCmd}",
                    Trigger = new CommandTrigger { Command = triggerCmd },
                    Action = new SequenceAction { Commands = commandsList }
                };

                // 4. Register in the Live Service
                if (AutomationService.Instance.RegisterRule(newRule))
                {
                    // 5. Persist to JSON immediately so it's not lost on restart
                    AutomationService.Instance.SaveRules();
                    
                    ctx.ReplySuccess($"Registered '.{triggerCmd}' with {commandsList.Count} actions. Execute via '.crossmodscommands {triggerCmd}'.");
                    _log.Info($"Admin {ctx.Event.User.CharacterName} registered command .{triggerCmd} with {commandsList.Count} actions");
                }
                else
                {
                    ctx.ReplyError($"Failed to register command .{triggerCmd}. It may already exist.");
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Error registering dynamic command: {ex.Message}");
                ctx.ReplyError($"Failed to register command: {ex.Message}");
            }
        }

        /// <summary>
        /// List all registered dynamic commands.
        /// </summary>
        [Command("vlist", "vl", description: "List all registered dynamic commands", adminOnly: true)]
        public static void ListDynamicCommands(ChatCommandContext ctx)
        {
            try
            {
                var rules = AutomationService.Instance.GetAllRules().ToList();
                
                if (!rules.Any())
                {
                    ctx.Reply("No dynamic commands registered.");
                    return;
                }

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"=== Dynamic Commands ({rules.Count}) ===");

                foreach (var rule in rules.Take(20)) // Limit output to prevent spam
                {
                    var status = rule.Enabled ? "enabled" : "disabled";
                    var actionType = rule.Action?.ActionType ?? "unknown";
                    sb.AppendLine($".{rule.Trigger.Command} - {rule.Name} [{status}] ({actionType})");
                }

                if (rules.Count > 20)
                {
                    sb.AppendLine($"... and {rules.Count - 20} more commands");
                }

                ctx.Reply(sb.ToString());
            }
            catch (Exception ex)
            {
                _log.Error($"Error listing dynamic commands: {ex.Message}");
                ctx.ReplyError($"Failed to list commands: {ex.Message}");
            }
        }

        /// <summary>
        /// Remove a registered dynamic command.
        /// </summary>
        [Command("vremove", "vrm", description: "Remove a registered dynamic command: .vremove <command>", adminOnly: true)]
        public static void RemoveDynamicCommand(ChatCommandContext ctx, string commandName)
        {
            try
            {
                var cleanCommand = commandName.TrimStart('.');
                
                if (AutomationService.Instance.UnregisterRule(cleanCommand))
                {
                    AutomationService.Instance.SaveRules();
                    ctx.ReplySuccess($"Command .{cleanCommand} has been removed.");
                    _log.Info($"Admin {ctx.Event.User.CharacterName} removed command .{cleanCommand}");
                }
                else
                {
                    ctx.ReplyError($"Command .{cleanCommand} not found or could not be removed.");
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Error removing dynamic command: {ex.Message}");
                ctx.ReplyError($"Failed to remove command: {ex.Message}");
            }
        }

        /// <summary>
        /// Enable a disabled dynamic command.
        /// </summary>
        [Command("venable", description: "Enable a disabled dynamic command: .venable <command>", adminOnly: true)]
        public static void EnableDynamicCommand(ChatCommandContext ctx, string commandName)
        {
            try
            {
                var cleanCommand = commandName.TrimStart('.');
                var rule = AutomationService.Instance.GetRule(cleanCommand);
                
                if (rule != null)
                {
                    rule.Enabled = true;
                    rule.LastModified = DateTime.UtcNow;
                    AutomationService.Instance.SaveRules();
                    ctx.ReplySuccess($"Command .{cleanCommand} has been enabled.");
                    _log.Info($"Admin {ctx.Event.User.CharacterName} enabled command .{cleanCommand}");
                }
                else
                {
                    ctx.ReplyError($"Command .{cleanCommand} not found.");
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Error enabling dynamic command: {ex.Message}");
                ctx.ReplyError($"Failed to enable command: {ex.Message}");
            }
        }

        /// <summary>
        /// Disable a dynamic command.
        /// </summary>
        [Command("vdisable", description: "Disable a dynamic command: .vdisable <command>", adminOnly: true)]
        public static void DisableDynamicCommand(ChatCommandContext ctx, string commandName)
        {
            try
            {
                var cleanCommand = commandName.TrimStart('.');
                var rule = AutomationService.Instance.GetRule(cleanCommand);
                
                if (rule != null)
                {
                    rule.Enabled = false;
                    rule.LastModified = DateTime.UtcNow;
                    AutomationService.Instance.SaveRules();
                    ctx.ReplySuccess($"Command .{cleanCommand} has been disabled.");
                    _log.Info($"Admin {ctx.Event.User.CharacterName} disabled command .{cleanCommand}");
                }
                else
                {
                    ctx.ReplyError($"Command .{cleanCommand} not found.");
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Error disabling dynamic command: {ex.Message}");
                ctx.ReplyError($"Failed to disable command: {ex.Message}");
            }
        }

        /// <summary>
        /// Show details of a specific dynamic command.
        /// </summary>
        [Command("vinfo", description: "Show details of a dynamic command: .vinfo <command>", adminOnly: true)]
        public static void ShowCommandInfo(ChatCommandContext ctx, string commandName)
        {
            try
            {
                var cleanCommand = commandName.TrimStart('.');
                var rule = AutomationService.Instance.GetRule(cleanCommand);
                
                if (rule != null)
                {
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine($"=== Command Info: .{cleanCommand} ===");
                    sb.AppendLine($"Name: {rule.Name}");
                    sb.AppendLine($"Status: {(rule.Enabled ? "enabled" : "disabled")}");
                    sb.AppendLine($"Created: {rule.CreatedAt}");
                    sb.AppendLine($"Last Modified: {rule.LastModified}");
                    sb.AppendLine($"Action Type: {rule.Action?.ActionType}");
                    
                    if (rule.Action is SequenceAction sequence)
                    {
                        sb.AppendLine($"Commands ({sequence.Commands.Count}):");
                        for (int i = 0; i < sequence.Commands.Count; i++)
                        {
                            sb.AppendLine($"  {i + 1}. {sequence.Commands[i]}");
                        }
                    }

                    ctx.Reply(sb.ToString());
                }
                else
                {
                    ctx.ReplyError($"Command .{cleanCommand} not found.");
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Error showing command info: {ex.Message}");
                ctx.ReplyError($"Failed to show command info: {ex.Message}");
            }
        }

        /// <summary>
        /// Clear all dynamic commands.
        /// </summary>
        [Command("vclear", description: "Clear all dynamic commands", adminOnly: true)]
        public static void ClearAllCommands(ChatCommandContext ctx)
        {
            try
            {
                var count = AutomationService.Instance.GetAllRules().Count();
                AutomationService.Instance.ClearRules();
                AutomationService.Instance.SaveRules();
                
                ctx.ReplySuccess($"Cleared {count} dynamic commands.");
                _log.Info($"Admin {ctx.Event.User.CharacterName} cleared all {count} dynamic commands");
            }
            catch (Exception ex)
            {
                _log.Error($"Error clearing dynamic commands: {ex.Message}");
                ctx.ReplyError($"Failed to clear commands: {ex.Message}");
            }
        }

        /// <summary>
        /// Test a dynamic command without actually executing it.
        /// </summary>
        [Command("vtest", description: "Test a dynamic command without executing: .vtest <command>", adminOnly: true)]
        public static void TestCommand(ChatCommandContext ctx, string commandName)
        {
            try
            {
                var cleanCommand = commandName.TrimStart('.');
                var rule = AutomationService.Instance.GetRule(cleanCommand);
                
                if (rule != null)
                {
                    if (!rule.Enabled)
                    {
                        ctx.ReplyWarning($"Command .{cleanCommand} is disabled.");
                        return;
                    }

                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine($"=== Test Preview: .{cleanCommand} ===");
                    sb.AppendLine($"Name: {rule.Name}");
                    sb.AppendLine($"Status: enabled");
                    
                    if (rule.Action is SequenceAction sequence)
                    {
                        sb.AppendLine($"Would execute {sequence.Commands.Count} commands:");
                        for (int i = 0; i < sequence.Commands.Count; i++)
                        {
                            sb.AppendLine($"  {i + 1}. {sequence.Commands[i]}");
                        }
                    }

                    ctx.Reply(sb.ToString());
                }
                else
                {
                    ctx.ReplyError($"Command .{cleanCommand} not found.");
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Error testing dynamic command: {ex.Message}");
                ctx.ReplyError($"Failed to test command: {ex.Message}");
            }
        }

        /// <summary>
        /// Execute a registered dynamic command by name.
        /// Usage: .crossmodscommands &lt;command&gt;
        /// </summary>
        [Command("crossmodscommands", "cmc", description: "Execute a registered dynamic command: .crossmodscommands <command>", adminOnly: false)]
        public static void ExecuteDynamicCommand(ChatCommandContext ctx, string commandName)
        {
            try
            {
                var cleanCommand = commandName.TrimStart('.');
                var ok = AutomationService.Instance.ExecuteRule(cleanCommand, ctx).GetAwaiter().GetResult();
                if (!ok)
                {
                    ctx.ReplyError($"Command .{cleanCommand} not found or failed.");
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Error executing dynamic command '{commandName}': {ex.Message}");
                ctx.ReplyError($"Failed to execute command: {ex.Message}");
            }
        }
    }
}
