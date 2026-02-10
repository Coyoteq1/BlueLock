using VampireCommandFramework;
using VAutomationCore.Core.Commands;
using VAutomationCore.Core.Logging;
using System.Collections.Generic;
using System.Text.Json;
using System;

namespace VAuto.Commands.Core
{
    /// <summary>
    /// Commands for managing automation rules.
    /// Uses CommandBase for VCF integration, logging, and feedback.
    /// </summary>
    public static class AutomationCommands : CommandBase
    {
        private const string CommandName = "automation";
        
        [Command("automation", shortHand: "auto", description: "Manage automation rules", adminOnly: true)]
        public static void Automation(ChatCommandContext ctx)
        {
            ExecuteSafely(ctx, CommandName, () =>
            {
                RequirePermission(ctx, PermissionLevel.Admin);
                
                SendInfo(ctx, "Usage: .automation list|reload|addboss <containerPrefabId> <bossPrefabId> <bossName>|info <ruleId>");
            });
        }
        
        [Command("automation list", shortHand: "auto list", description: "List all automation rules")]
        public static void AutomationList(ChatCommandContext ctx)
        {
            ExecuteSafely(ctx, "automation list", () =>
            {
                RequirePermission(ctx, PermissionLevel.Admin);
                
                Log.Info($"Automation list requested by {GetPlayerInfo(ctx).Name}");
                
                SendInfo(ctx, "=== Automation Rules ===");
                SendInfo(ctx, "Run '.automation reload' to refresh from config file.");
            });
        }
        
        [Command("automation reload", shortHand: "auto reload", description: "Reload automation rules from config")]
        public static void AutomationReload(ChatCommandContext ctx)
        {
            ExecuteSafely(ctx, "automation reload", () =>
            {
                RequirePermission(ctx, PermissionLevel.Admin);
                
                try
                {
                    Log.Info($"Automation rules reloaded by {GetPlayerInfo(ctx).Name}");
                    SendSuccess(ctx, "Rules reloaded successfully.");
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, "AutomationReload");
                    SendError(ctx, "Error reloading rules", ex.Message);
                }
            });
        }
        
        [Command("automation addboss", shortHand: "auto addboss", 
                 description: "Add a boss spawn rule: .automation addboss <containerPrefabId> <bossPrefabId> <bossName> [offsetX] [offsetY] [offsetZ]")]
        public static void AutomationAddBoss(
            ChatCommandContext ctx,
            int containerPrefabId,
            int bossPrefabId,
            string bossName,
            float offsetX = 0,
            float offsetY = 0,
            float offsetZ = 5)
        {
            ExecuteSafely(ctx, "automation addboss", () =>
            {
                RequirePermission(ctx, PermissionLevel.Admin);
                
                try
                {
                    var configPath = System.IO.Path.Combine(BepInEx.Paths.ConfigPath, "VAuto", "automation_rules.json");
                    
                    // Load existing config
                    var config = new AutomationConfig { Rules = new List<AutomationRuleConfig>() };
                    
                    if (System.IO.File.Exists(configPath))
                    {
                        var json = System.IO.File.ReadAllText(configPath);
                        config = JsonSerializer.Deserialize<AutomationConfig>(json) ?? config;
                    }
                    
                    // Add new rule
                    var newRule = new AutomationRuleConfig
                    {
                        Id = Guid.NewGuid().ToString("N")[..8],
                        Name = $"Boss {bossName} from container {containerPrefabId}",
                        ContainerPrefabId = containerPrefabId,
                        Enabled = true,
                        MaxUses = 1,
                        CooldownSeconds = 300f,
                        ArenaOnly = true,
                        Actions = new List<AutomationActionConfig>
                        {
                            new AutomationActionConfig
                            {
                                Type = "SpawnBoss",
                                IntParam = bossPrefabId,
                                StringParam = bossName,
                                PositionOffset = new float[] { offsetX, offsetY, offsetZ }
                            }
                        }
                    };
                    
                    config.Rules.Add(newRule);
                    
                    // Save
                    var options = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                    var outputJson = JsonSerializer.Serialize(config, options);
                    System.IO.File.WriteAllText(configPath, outputJson);
                    
                    var playerInfo = GetPlayerInfo(ctx);
                    Log.Info($"Boss rule added by {playerInfo.Name}: {bossName} (ID: {bossPrefabId}) for container {containerPrefabId}");
                    
                    SendSuccess(ctx, $"Added boss rule: {bossName}", $"ID: {bossPrefabId}");
                    SendInfo(ctx, $"Saved to: {configPath}");
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, "AutomationAddBoss");
                    SendError(ctx, "Error adding rule", ex.Message);
                }
            });
        }
        
        [Command("automation info", shortHand: "auto info", description: "Show info about an automation rule")]
        public static void AutomationInfo(ChatCommandContext ctx, string ruleId)
        {
            ExecuteSafely(ctx, "automation info", () =>
            {
                RequirePermission(ctx, PermissionLevel.Admin);
                
                // TODO: Integrate with AutomationService
                // var rule = AutomationService.Instance.GetRule(ruleId);
                // if (rule == null)
                // {
                //     SendError(ctx, $"Rule '{ruleId}' not found.");
                //     return;
                // }
                
                Log.Info($"Automation info requested for rule: {ruleId} by {GetPlayerInfo(ctx).Name}");
                
                SendInfo(ctx, $"=== Rule: {ruleId} ===");
                SendCount(ctx, "ID", 0);
                SendInfo(ctx, "Rule details not available - service not initialized");
            });
        }
        
        [Command("automation queuestatus", shortHand: "auto qs", description: "Show queue service status")]
        public static void AutomationQueueStatus(ChatCommandContext ctx)
        {
            ExecuteSafely(ctx, "automation queuestatus", () =>
            {
                RequirePermission(ctx, PermissionLevel.Admin);
                
                try
                {
                    Log.Info($"Queue status requested by {GetPlayerInfo(ctx).Name}");
                    SendCount(ctx, "Queue count", 0);
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, "AutomationQueueStatus");
                    SendError(ctx, "Error getting queue status", ex.Message);
                }
            });
        }
    }
    
    #region Configuration Models
    
    public class AutomationConfig
    {
        public List<AutomationRuleConfig> Rules { get; set; } = new();
    }
    
    public class AutomationRuleConfig
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int ContainerPrefabId { get; set; }
        public bool Enabled { get; set; }
        public int MaxUses { get; set; }
        public float CooldownSeconds { get; set; }
        public bool ArenaOnly { get; set; }
        public List<AutomationActionConfig> Actions { get; set; } = new();
    }
    
    public class AutomationActionConfig
    {
        public string Type { get; set; } = string.Empty;
        public int IntParam { get; set; }
        public string StringParam { get; set; } = string.Empty;
        public float[] PositionOffset { get; set; } = Array.Empty<float>();
    }
    
    #endregion
}
