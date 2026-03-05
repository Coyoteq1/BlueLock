using System;
using VampireCommandFramework;
using VAutomationCore.Core.Logging;
using VAutomationCore.Core.Automation;
using VAutomationCore.Core.Extensions;

namespace VAutomationCore.Core.Automation
{
    /// <summary>
    /// Test commands for the dynamic command registration system.
    /// These commands help verify that the automation system is working correctly.
    /// </summary>
    public static class AutomationTestCommands
    {
        private static readonly CoreLogger _log = new CoreLogger("AutomationTest");

        /// <summary>
        /// Test the dynamic command registration system with a simple example.
        /// </summary>
        [Command("vtestsetup", description: "Setup test commands for dynamic registration", adminOnly: true)]
        public static void SetupTestCommands(ChatCommandContext ctx)
        {
            try
            {
                // Test command 1: Simple announce
                var testRule1 = new AutomationRule
                {
                    Id = "test_announce_1",
                    Name = "Test Announcement",
                    Trigger = new CommandTrigger { Command = "testannounce" },
                    Action = new SequenceAction 
                    { 
                        Commands = new System.Collections.Generic.List<string> 
                        { 
                            ".announce 'This is a test announcement from dynamic commands!'" 
                        } 
                    }
                };

                // Test command 2: Multi-step sequence
                var testRule2 = new AutomationRule
                {
                    Id = "test_sequence_1",
                    Name = "Test Sequence",
                    Trigger = new CommandTrigger { Command = "testsequence" },
                    Action = new SequenceAction 
                    { 
                        Commands = new System.Collections.Generic.List<string> 
                        { 
                            ".announce 'Starting test sequence...'",
                            ".announce 'Step 1: Testing command execution'",
                            ".announce 'Step 2: Multiple commands work'",
                            ".announce 'Test sequence complete!'"
                        } 
                    }
                };

                // Test command 3: Conditional command
                var testRule3 = new AutomationRule
                {
                    Id = "test_conditional_1",
                    Name = "Test Conditional",
                    Trigger = new CommandTrigger { Command = "testconditional" },
                    Action = new ConditionalAction
                    {
                        Condition = "admin",
                        TrueAction = new SequenceAction
                        {
                            Commands = new System.Collections.Generic.List<string>
                            {
                                ".announce 'Admin detected - executing admin sequence'"
                            }
                        },
                        FalseAction = new SequenceAction
                        {
                            Commands = new System.Collections.Generic.List<string>
                            {
                                ".announce 'Non-admin detected - limited functionality'"
                            }
                        }
                    }
                };

                // Register the test commands
                var success1 = AutomationService.Instance.RegisterRule(testRule1);
                var success2 = AutomationService.Instance.RegisterRule(testRule2);
                var success3 = AutomationService.Instance.RegisterRule(testRule3);

                // Save to persistence
                AutomationService.Instance.SaveRules();

                ctx.ReplySuccess($"Setup complete! Registered {CountSuccess(success1, success2, success3)} test commands.");
                ctx.ReplyInfo("Try these commands: .testannounce, .testsequence, .testconditional");
                
                _log.Info($"Admin {ctx.Event.User.CharacterName} setup test commands");
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to setup test commands: {ex.Message}");
                ctx.ReplyError($"Failed to setup test commands: {ex.Message}");
            }
        }

        /// <summary>
        /// Clear all test commands.
        /// </summary>
        [Command("vtestclear", description: "Clear all test commands", adminOnly: true)]
        public static void ClearTestCommands(ChatCommandContext ctx)
        {
            try
            {
                var rules = AutomationService.Instance.GetAllRules().ToList();
                var testRules = rules.Where(r => r.Id.StartsWith("test_")).ToList();
                
                foreach (var rule in testRules)
                {
                    AutomationService.Instance.UnregisterRule(rule.Trigger.Command);
                }
                
                AutomationService.Instance.SaveRules();
                
                ctx.ReplySuccess($"Cleared {testRules.Count} test commands.");
                _log.Info($"Admin {ctx.Event.User.CharacterName} cleared {testRules.Count} test commands");
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to clear test commands: {ex.Message}");
                ctx.ReplyError($"Failed to clear test commands: {ex.Message}");
            }
        }

        /// <summary>
        /// Show system status and statistics.
        /// </summary>
        [Command("vstatus", description: "Show automation system status", adminOnly: true)]
        public static void ShowStatus(ChatCommandContext ctx)
        {
            try
            {
                var rules = AutomationService.Instance.GetAllRules().ToList();
                var enabledRules = rules.Count(r => r.Enabled);
                var disabledRules = rules.Count(r => !r.Enabled);
                
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("=== VAutomationCore Status ===");
                sb.AppendLine($"Total Rules: {rules.Count}");
                sb.AppendLine($"Enabled: {enabledRules}");
                sb.AppendLine($"Disabled: {disabledRules}");
                sb.AppendLine($"System Ready: {VAutomationCore.Core.UnifiedCore.IsInitialized}");
                sb.AppendLine($"VCF Patch Active: CrossModsCommandsPatch");
                
                if (rules.Any())
                {
                    sb.AppendLine("\n=== Active Rules ===");
                    foreach (var rule in rules.Take(10))
                    {
                        var status = rule.Enabled ? "✓" : "✗";
                        sb.AppendLine($"{status} .{rule.Trigger.Command} - {rule.Name}");
                    }
                    
                    if (rules.Count > 10)
                    {
                        sb.AppendLine($"... and {rules.Count - 10} more rules");
                    }
                }
                else
                {
                    sb.AppendLine("\nNo rules registered yet.");
                }

                ctx.Reply(sb.ToString());
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to show status: {ex.Message}");
                ctx.ReplyError($"Failed to show status: {ex.Message}");
            }
        }

        /// <summary>
        /// Test the VCF command execution directly.
        /// </summary>
        [Command("vtestvcf", description: "Test VCF command execution", adminOnly: true)]
        public static void TestVCFExecution(ChatCommandContext ctx)
        {
            try
            {
                // Test a simple VCF command
                // TODO: Implement actual VCF command execution
                // For now, just log the test attempt
                _log.Info("VCF command execution test attempted");
                
                ctx.ReplySuccess("VCF command execution test passed! (Note: Actual execution not implemented yet)");
            }
            catch (Exception ex)
            {
                _log.Error($"VCF test failed: {ex.Message}");
                ctx.ReplyError($"VCF test failed: {ex.Message}");
            }
        }

        private static int CountSuccess(params bool[] results)
        {
            return results.Count(r => r);
        }
    }
}