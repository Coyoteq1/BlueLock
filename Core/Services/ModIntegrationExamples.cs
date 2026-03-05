using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using VampireCommandFramework;
using VAutomationCore.Core.Services;

namespace VAutomationCore.Examples
{
    /// <summary>
    /// Examples for wiring commands/services from other mods.
    /// </summary>
    public class ModIntegrationExamples
    {
        /// <summary>
        /// Example 1: Execute VCF command from another mod.
        /// </summary>
        public static void ExecuteVCFCommand(EntityManager em, string command)
        {
            // Use the VCF patch we created
            VCFCommands.Exec(em, command);
            
            // Execute 2 commands
            VCFCommands.Exec2(em, ".z list", ".tm status arena1");
            
            // Execute multiple
            VCFCommands.ExecAll(em, ".z list", ".tm status arena1", ".match start arena1");
        }
        
        /// <summary>
        /// Example 2: Interact with another mod's service.
        /// </summary>
        public static void CallModService(string modName, string method, object[] args)
        {
            // Send via our inter-mod communication
            ModAPI.SendToMod(modName, method, args);
            
            // Get result
            var result = ModAPI.GetFromMod<object>(modName, method);
        }
        
        /// <summary>
        /// Example 3: Subscribe to another mod's events.
        /// </summary>
        public static void SubscribeToModEvents()
        {
            // Subscribe to Blueluck events
            ModAPI.SendToMod("Blueluck", "subscribe", new object[] { "onPlayerEnter" });
            
            // Register handler
            ModAPI.Subscribe("Blueluck", "onPlayerEnter", (key, data) => 
            {
                Debug.Log($"Player entered: {data}");
            });
        }
        
        /// <summary>
        /// Example 4: Common mod integrations.
        /// </summary>
        public static void IntegrateWithCommonMods(EntityManager em)
        {
            // VCF Commands - execute game commands
            ExecuteVCFCommand(em, ".giveitem Player Gold 100");
            
            // Schedule commands via time rules
            em.Exec()
                .Job1(new DelayedCommandJob { Command = ".z list" })
                .WithRule(JobTimeRule.Slow)
                .Execute();
        }
        
        /// <summary>
        /// Example 5: Send notification to another mod.
        /// </summary>
        public static void NotifyOtherMod(string modId, string eventName, object data)
        {
            var payload = new { Event = eventName, Data = data, Timestamp = DateTime.UtcNow };
            ModAPI.SendToMod(modId, eventName, payload);
        }
        
        /// <summary>
        /// Example 6: Request data from another mod.
        /// </summary>
        public static T RequestModData<T>(string modId, string key)
        {
            ModAPI.SendToMod(modId, $"request:{key}", null);
            return ModAPI.GetFromMod<T>(modId, key);
        }
        
        /// <summary>
        /// Example 7: Create a mod bridge for two-way communication.
        /// </summary>
        public static void CreateModBridge(string myModId, string targetModId)
        {
            // Register as available for communication
            ModAPI.SendToMod(targetModId, "register_bridge", myModId);
            
            // Listen for responses
            ModAPI.Subscribe(targetModId, "bridge_response", (key, data) => 
            {
                Debug.Log($"Bridge response: {data}");
            });
        }
        
        /// <summary>
        /// Example 8: Sync state across mods after restart.
        /// </summary>
        public static void SyncAfterRestart()
        {
            // Request state from each registered mod
            var mods = new[] { "Blueluck", "VCFCommands" };
            
            foreach (var mod in mods)
            {
                var state = RequestModData<Dictionary<string, object>>(mod, "state");
                if (state != null)
                {
                    Debug.Log($"Restored state from {mod}: {state.Count} entries");
                }
            }
        }
    }
    
    /// <summary>
    /// Job that executes a command with delay.
    /// </summary>
    public struct DelayedCommandJob : IVAutoJob
    {
        public string Command;
        
        public void Execute(Entity entity)
        {
            // Get EntityManager from somewhere (passed externally in real use)
            Debug.Log($"Executing delayed command: {Command}");
        }
    }
    
    /// <summary>
    /// Helper for common mod integrations.
    /// </summary>
    public static class ModHelper
    {
        /// <summary>Check if a mod is loaded.</summary>
        public static bool IsModLoaded(string modId) => true; // Check BepInEx
        
        /// <summary>Get mod version.</summary>
        public static string GetModVersion(string modId) => Core.MyPluginInfo.VERSION;
        
        /// <summary>Send command to VCF.</summary>
        public static void SendVCF(string cmd, EntityManager em) => VCFCommands.Exec(em, cmd);
    }
}
