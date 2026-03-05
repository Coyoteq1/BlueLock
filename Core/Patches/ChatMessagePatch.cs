using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using ProjectM;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using VAutomationCore.Core.Patches;

namespace VAutomationCore.Core.Patches
{
    /// <summary>
    /// Chat message handling patch for V Rising.
    /// Provides chat command parsing and execution framework.
    /// </summary>
    public static class ChatMessagePatch
    {
        private static bool _initialized;
        private static readonly object _initLock = new object();
        private static readonly Dictionary<string, ChatCommand> _commands = new Dictionary<string, ChatCommand>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> _commandAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        #region Command Configuration

        /// <summary>
        /// Chat command configuration.
        /// </summary>
        public class ChatCommand
        {
            public string Name;
            public string Description;
            public string[] Aliases;
            public string Permission;
            public bool AllowConsole;
            public bool AllowChat;
            public int MinArgs;
            public int MaxArgs;
            public Action<ChatContext> Handler;
            public string Usage;
            public string[] Examples;
            public string Category;
            public bool Enabled = true;
        }

        /// <summary>
        /// Context passed to chat command handlers.
        /// </summary>
        public class ChatContext
        {
            public string Command;
            public string[] Args;
            public Entity Sender;
            public string SenderName;
            public bool IsConsole;
            public bool IsAdmin;
            public bool Canceled;
            public string CancelReason;
            public Dictionary<string, object> Data;
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initialize the chat message patch.
        /// </summary>
        public static void Initialize()
        {
            lock (_initLock)
            {
                if (_initialized) return;

                try
                {
                    // Register built-in commands
                    RegisterCommand(new ChatCommand
                    {
                        Name = "help",
                        Description = "Show available commands",
                        Aliases = new[] { "?" },
                        Handler = ctx => ShowHelp(ctx),
                        Usage = "/help [category]",
                        Examples = new[] { "/help", "/help admin" },
                        Category = "General"
                    });

                    RegisterCommand(new ChatCommand
                    {
                        Name = "echo",
                        Description = "Echo back a message",
                        Handler = ctx => EchoMessage(ctx),
                        Usage = "/echo <message>",
                        Examples = new[] { "/echo Hello world" },
                        Category = "Utility"
                    });

                    _initialized = true;
                    Plugin.Log.LogInfo("[ChatMessagePatch] Initialized successfully");
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"[ChatMessagePatch] Initialization failed: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Check if patch is ready.
        /// </summary>
        public static bool IsReady()
        {
            return _initialized;
        }

        #endregion

        #region Command Registration

        /// <summary>
        /// Register a new chat command.
        /// </summary>
        /// <param name="command">Command configuration</param>
        /// <returns>True if registered</returns>
        public static bool RegisterCommand(ChatCommand command)
        {
            if (command == null || string.IsNullOrEmpty(command.Name)) return false;

            lock (_commands)
            {
                if (_commands.ContainsKey(command.Name))
                {
                    Plugin.Log.LogWarning($"[ChatMessagePatch] Command '{command.Name}' already exists, updating");
                }

                _commands[command.Name] = command;

                // Register aliases
                if (command.Aliases != null)
                {
                    foreach (var alias in command.Aliases)
                    {
                        _commandAliases.Add(alias);
                    }
                }

                Plugin.Log.LogInfo($"[ChatMessagePatch] Registered command '{command.Name}'");
                return true;
            }
        }

        /// <summary>
        /// Unregister a chat command.
        /// </summary>
        /// <param name="name">Command name</param>
        /// <returns>True if unregistered</returns>
        public static bool UnregisterCommand(string name)
        {
            lock (_commands)
            {
                if (_commands.Remove(name))
                {
                    Plugin.Log.LogInfo($"[ChatMessagePatch] Unregistered command '{name}'");
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Get a registered command.
        /// </summary>
        /// <param name="name">Command name</param>
        /// <returns>Command or null</returns>
        public static ChatCommand? GetCommand(string name)
        {
            lock (_commands)
            {
                if (_commands.TryGetValue(name, out var command))
                    return command;
            }
            return null;
        }

        /// <summary>
        /// Get all registered commands.
        /// </summary>
        /// <returns>List of commands</returns>
        public static List<ChatCommand> GetAllCommands()
        {
            lock (_commands)
            {
                return _commands.Values.ToList();
            }
        }

        #endregion

        #region Command Processing

        /// <summary>
        /// Process an incoming chat message.
        /// </summary>
        /// <param name="message">Chat message</param>
        /// <param name="sender">Message sender</param>
        /// <param name="senderName">Sender name</param>
        /// <returns>True if message was a command</returns>
        public static bool ProcessChatMessage(string message, Entity sender, string senderName)
        {
            if (!IsReady() || string.IsNullOrEmpty(message)) return false;

            // Check if message is a command (starts with /)
            if (!message.StartsWith("/")) return false;

            var parts = message.Substring(1).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return false;

            var commandName = parts[0];
            var args = parts.Length > 1 ? parts.Skip(1).ToArray() : Array.Empty<string>();

            // Check if command exists or is an alias
            ChatCommand? command = null;
            lock (_commands)
            {
                if (_commands.TryGetValue(commandName, out var cmd))
                {
                    command = cmd;
                }
                else if (_commandAliases.Contains(commandName))
                {
                    // Find command by alias
                    command = _commands.Values.FirstOrDefault(c => c.Aliases != null && c.Aliases.Contains(commandName, StringComparer.OrdinalIgnoreCase));
                }
            }

            if (command == null) return false;

            // Create context
            var context = new ChatContext
            {
                Command = commandName,
                Args = args,
                Sender = sender,
                SenderName = senderName,
                IsConsole = sender == Entity.Null,
                IsAdmin = false, // TODO: Implement admin check
                Canceled = false,
                Data = new Dictionary<string, object>()
            };

            // Check permissions
            if (!string.IsNullOrEmpty(command.Value.Permission))
            {
                if (!HasPermission(sender, command.Value.Permission))
                {
                    SendErrorMessage(sender, $"You don't have permission to use this command");
                    return true;
                }
            }

            // Check argument count
            if (args.Length < command.Value.MinArgs || (command.Value.MaxArgs >= 0 && args.Length > command.Value.MaxArgs))
            {
                SendUsageMessage(sender, command.Value);
                return true;
            }

            try
            {
                command.Value.Handler?.Invoke(context);
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[ChatMessagePatch] Error executing command '{commandName}': {ex.Message}");
                SendErrorMessage(sender, $"Error executing command: {ex.Message}");
                return true;
            }
        }

        #endregion

        #region Built-in Commands

        private static void ShowHelp(ChatContext ctx)
        {
            var category = ctx.Args.Length > 0 ? ctx.Args[0] : null;
            var commands = GetAllCommands();

            if (!string.IsNullOrEmpty(category))
            {
                commands = commands.Where(c => c.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (commands.Count == 0)
            {
                SendMessage(ctx.Sender, $"No commands found in category '{category}'");
                return;
            }

            var message = $"Available commands:\n";
            foreach (var command in commands.OrderBy(c => c.Name))
            {
                message += $"\n/{command.Name} - {command.Description}\n";
                if (!string.IsNullOrEmpty(command.Usage))
                    message += $"  Usage: {command.Usage}\n";
                if (command.Examples != null && command.Examples.Length > 0)
                {
                    message += "  Examples:\n";
                    foreach (var example in command.Examples)
                    {
                        message += $"    {example}\n";
                    }
                }
            }

            SendMessage(ctx.Sender, message);
        }

        private static void EchoMessage(ChatContext ctx)
        {
            var message = string.Join(" ", ctx.Args);
            SendMessage(ctx.Sender, $"Echo: {message}");
        }

        #endregion

        #region Messaging Helpers

        private static void SendMessage(Entity sender, string message)
        {
            // TODO: Implement actual chat message sending
            Plugin.Log.LogInfo($"[ChatMessagePatch] Sending message to {sender}: {message}");
        }

        private static void SendErrorMessage(Entity sender, string message)
        {
            // TODO: Implement actual error message sending
            Plugin.Log.LogError($"[ChatMessagePatch] Sending error to {sender}: {message}");
        }

        private static void SendUsageMessage(Entity sender, ChatCommand command)
        {
            var message = $"Usage: {command.Usage}\n";
            if (command.Examples != null && command.Examples.Length > 0)
            {
                message += "Examples:\n";
                foreach (var example in command.Examples)
                {
                    message += $"  {example}\n";
                }
            }
            SendErrorMessage(sender, message);
        }

        private static bool HasPermission(Entity sender, string permission)
        {
            // TODO: Implement permission checking
            return true;
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Get command statistics.
        /// </summary>
        public static CommandStatistics GetStatistics()
        {
            lock (_commands)
            {
                return new CommandStatistics
                {
                    TotalCommands = _commands.Count,
                    CommandsByCategory = _commands.Values.GroupBy(c => c.Category).ToDictionary(g => g.Key, g => g.Count()),
                    CommandsByPermission = _commands.Values.Where(c => !string.IsNullOrEmpty(c.Permission)).GroupBy(c => c.Permission).ToDictionary(g => g.Key, g => g.Count())
                };
            }
        }

        #endregion
    }

    /// <summary>
    /// Command statistics for monitoring.
    /// </summary>
    public class CommandStatistics
    {
        public int TotalCommands;
        public Dictionary<string, int> CommandsByCategory;
        public Dictionary<string, int> CommandsByPermission;
    }
}