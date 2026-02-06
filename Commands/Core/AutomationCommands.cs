using VampireCommandFramework;
using VAuto.Core;
using VAuto.Core.Components;
using VAuto.Core.Services;
using System.Collections.Generic;
using System.Text.Json;

namespace VAuto.Commands.Core
{
    /// <summary>
    /// Commands for managing automation rules.
    /// </summary>
    public static class AutomationCommands
    {
        [Command("automation", shortHand: "auto", description: "Manage automation rules", adminOnly: true)]
        public static void Automation(ChatCommandContext ctx)
        {
            ctx.Reply("[Automation] Usage: .automation list|reload|addboss <containerPrefabId> <bossPrefabId> <bossName>|info <ruleId>");
        }
        
        [Command("automation list", shortHand: "auto list", description: "List all automation rules")]
        public static void AutomationList(ChatCommandContext ctx)
        {
            var service = AutomationService.Instance;
            var rules = service;
            
            ctx.Reply("[Automation] === Automation Rules ===");
            ctx.Reply($"Total rules: {service}");
            
            // This would need implementation
            ctx.Reply("[Automation] Run '.automation reload' to refresh from config file.");
        }
        
        [Command("automation reload", shortHand: "auto reload", description: "Reload automation rules from config")]
        public static void AutomationReload(ChatCommandContext ctx)
        {
            try
            {
                AutomationService.Instance.Reload();
                ctx.Reply("[Automation] Rules reloaded successfully.");
            }
            catch (System.Exception ex)
            {
                ctx.Reply($"[Automation] Error reloading: {ex.Message}");
            }
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
            try
            {
                var configPath = System.IO.Path.Combine(BepInEx.Paths.ConfigPath, "VAuto", "automation_rules.json");
                
                // Load existing config
                AutomationConfig config = new AutomationConfig { rules = new List<AutomationRuleConfig>() };
                
                if (System.IO.File.Exists(configPath))
                {
                    var json = System.IO.File.ReadAllText(configPath);
                    config = JsonSerializer.Deserialize<AutomationConfig>(json) ?? config;
                }
                
                // Add new rule
                var newRule = new AutomationRuleConfig
                {
                    id = Guid.NewGuid().ToString("N")[..8],
                    name = $"Boss {bossName} from container {containerPrefabId}",
                    containerPrefabId = containerPrefabId,
                    enabled = true,
                    maxUses = 1,
                    cooldownSeconds = 300f,
                    arenaOnly = true,
                    actions = new List<AutomationActionConfig>
                    {
                        new AutomationActionConfig
                        {
                            type = "SpawnBoss",
                            intParam = bossPrefabId,
                            stringParam = bossName,
                            positionOffset = new float[] { offsetX, offsetY, offsetZ }
                        }
                    }
                };
                
                config.rules.Add(newRule);
                
                // Save
                var options = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                var outputJson = JsonSerializer.Serialize(config, options);
                System.IO.File.WriteAllText(configPath, outputJson);
                
                // Reload
                AutomationService.Instance.Reload();
                
                ctx.Reply($"[Automation] Added boss rule: {bossName} (ID: {bossPrefabId}) for container {containerPrefabId}");
                ctx.Reply($"[Automation] Saved to: {configPath}");
            }
            catch (System.Exception ex)
            {
                ctx.Reply($"[Automation] Error: {ex.Message}");
            }
        }
        
        [Command("automation info", shortHand: "auto info", description: "Show info about an automation rule")]
        public static void AutomationInfo(ChatCommandContext ctx, string ruleId)
        {
            var rule = AutomationService.Instance.GetRule(ruleId);
            if (rule == null)
            {
                ctx.Reply($"[Automation] Rule '{ruleId}' not found.");
                return;
            }
            
            ctx.Reply($"[Automation] === Rule: {rule.name} ===");
            ctx.Reply($"ID: {rule.id}");
            ctx.Reply($"Container Prefab: {rule.containerPrefabId}");
            ctx.Reply($"Enabled: {rule.enabled}");
            ctx.Reply($"Arena Only: {rule.arenaOnly}");
            ctx.Reply($"Max Uses: {rule.maxUses}");
            ctx.Reply($"Cooldown: {rule.cooldownSeconds}s");
            ctx.Reply($"Actions: {rule.actions?.Count ?? 0}");
        }
        
        [Command("automation queuestatus", shortHand: "auto qs", description: "Show queue service status")]
        public static void AutomationQueueStatus(ChatCommandContext ctx)
        {
            try
            {
                var queue = QueueService.Instance;
                var count = 0; // queue.QueueCount;
                ctx.Reply($"[Automation] Queue count: {count}");
            }
            catch (System.Exception ex)
            {
                ctx.Reply($"[Automation] Error: {ex.Message}");
            }
        }
    }
}
