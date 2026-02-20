using System;
using System.Collections.Generic;
using HarmonyLib;
using VampireCommandFramework;
using VampireCommandFramework.Breadstone;
using ProjectM;
using Unity.Entities;
using UnityEngine;

namespace Bluelock.Patches
{
    /// <summary>
    /// Patch for VampireCommandFramework to enable command execution and logging.
    /// </summary>
    [HarmonyPatch]
    public static class VampireCommandFrameworkPatch
    {
        private static readonly Dictionary<string, Action<ChatCommandContext>> _customCommands = new Dictionary<string, Action<ChatCommandContext>>();
        
        /// <summary>
        /// Register a custom command for execution.
        /// </summary>
        public static void RegisterCommand(string name, Action<ChatCommandContext> handler)
        {
            _customCommands[name.ToLower()] = handler;
        }
        
        /// <summary>
        /// Execute a VCF command programmatically.
        /// </summary>
        public static bool ExecuteCommand(string command, EntityManager em)
        {
            try
            {
                var cmd = command.Trim();
                if (string.IsNullOrEmpty(cmd)) return false;
                
                // Handle registered custom commands
                var cmdLower = cmd.ToLower().Split(' ')[0];
                if (_customCommands.TryGetValue(cmdLower, out var handler))
                {
                    // Create dummy context for execution
                    var ctx = CreateDummyContext(em);
                    handler(ctx);
                    return true;
                }
                
                // Try to execute via VCF CommandRegistry
                return TryExecuteViaRegistry(cmd, em);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VCF] Command execution failed: {command}, Error: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Execute multiple VCF commands.
        /// </summary>
        public static int ExecuteCommands(EntityManager em, params string[] commands)
        {
            int successCount = 0;
            foreach (var cmd in commands)
            {
                if (ExecuteCommand(cmd, em))
                    successCount++;
            }
            return successCount;
        }
        
        /// <summary>
        /// Execute 2 VCF commands (specific method).
        /// </summary>
        public static bool Execute2(string cmd1, string cmd2, EntityManager em)
        {
            var r1 = ExecuteCommand(cmd1, em);
            var r2 = ExecuteCommand(cmd2, em);
            return r1 && r2;
        }
        
        private static bool TryExecuteViaRegistry(string command, EntityManager em)
        {
            // Get CommandRegistry via reflection
            var registryType = typeof(CommandRegistry);
            var handleMethod = registryType.GetMethod("Handle", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            
            if (handleMethod != null)
            {
                try
                {
                    var result = handleMethod.Invoke(null, new object[] { new ChatCommandContext((VChatEvent)null!), command });
                    return result != null;
                }
                catch
                {
                    return false;
                }
            }
            
            return false;
        }
        
        private static ChatCommandContext CreateDummyContext(EntityManager em)
        {
            // Create minimal context for command execution
            return new ChatCommandContext((VChatEvent)null!);
        }
        
        /// <summary>
        /// Prefix patch for CommandRegistry Handle method to log commands.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(CommandRegistry), "Handle")]
        public static void HandlePrefix(ChatCommandContext ctx, string input)
        {
            Debug.Log($"[VCF] Executing command: {input}");
        }
        
        /// <summary>
        /// Postfix patch for CommandRegistry Handle method to log results.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CommandRegistry), "Handle")]
        public static void HandlePostfix(ChatCommandContext ctx, string input)
        {
            Debug.Log($"[VCF] Completed command: {input}");
        }
    }
    
    /// <summary>
    /// Helper class for VCF command execution via LocalExecutor.
    /// </summary>
    public static class VCFCommands
    {
        /// <summary>
        /// Execute command using local executor pattern.
        /// </summary>
        public static void Exec(EntityManager em, string command)
        {
            VampireCommandFrameworkPatch.ExecuteCommand(command, em);
        }
        
        /// <summary>
        /// Execute 2 commands.
        /// </summary>
        public static void Exec2(EntityManager em, string cmd1, string cmd2)
        {
            VampireCommandFrameworkPatch.Execute2(cmd1, cmd2, em);
        }
        
        /// <summary>
        /// Execute multiple commands.
        /// </summary>
        public static int ExecAll(EntityManager em, params string[] commands)
        {
            return VampireCommandFrameworkPatch.ExecuteCommands(em, commands);
        }
    }
    
    /// <summary>
    /// Additional Harmony patches for VCF command system.
    /// </summary>
    [HarmonyPatch]
    public static class VCFCommandPatches
    {
        /// <summary>
        /// Patch to intercept and modify command input.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(CommandRegistry), "Handle")]
        public static bool HandlePrefix(ref bool __result, ChatCommandContext ctx, string input)
        {
            Debug.Log($"[VCF Patch] Command received: {input}");
            
            // Allow original to run
            return true;
        }
        
        /// <summary>
        /// Patch to block certain commands.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(CommandRegistry), "Handle")]
        public static bool BlockCommands(ChatCommandContext ctx, string input)
        {
            // Block dangerous commands
            var blocked = new[] { "crash", "hack", "exploit" };
            foreach (var cmd in blocked)
            {
                if (input.ToLower().Contains(cmd))
                {
                    Debug.LogWarning($"[VCF Patch] Blocked command: {input}");
                    return false;
                }
            }
            return true;
        }
        
        /// <summary>
        /// Patch to log admin-only commands.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CommandRegistry), "Handle")]
        public static void LogAdminCommands(ChatCommandContext ctx, string input)
        {
            if (input.Contains("admin") || input.Contains("kick") || input.Contains("ban"))
            {
                Debug.LogWarning($"[VCF] Admin command executed: {input}");
            }
        }
    }
    
    /// <summary>
    /// VCF Command builder for fluent command creation.
    /// </summary>
    public class VCFCommandBuilder
    {
        private readonly List<string> _commands = new List<string>();
        
        public VCFCommandBuilder Add(string cmd)
        {
            _commands.Add(cmd);
            return this;
        }
        
        public VCFCommandBuilder Add(params string[] cmds)
        {
            _commands.AddRange(cmds);
            return this;
        }
        
        public int Execute(EntityManager em)
        {
            return VCFCommands.ExecAll(em, _commands.ToArray());
        }
        
        public static VCFCommandBuilder Create() => new VCFCommandBuilder();
    }
}
