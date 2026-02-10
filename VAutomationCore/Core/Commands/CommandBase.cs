using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using ProjectM.Network;
using VampireCommandFramework;
using VAutomationCore.Core.ECS;
using VAutomationCore.Core.Logging;

namespace VAutomationCore.Core.Commands
{
    /// <summary>
    /// Base class for chat commands providing common functionality.
    /// Provides Execute() abstract method, permission handling, cooldowns, and rich feedback.
    /// </summary>
    public abstract class CommandBase
    {
        protected const string LogSource = "Commands";
        
        #region Core System Access
        
        /// <summary>
        /// Gets the CoreLogger for this command.
        /// </summary>
        protected static CoreLogger Log { get; } = new CoreLogger(LogSource);
        
        /// <summary>
        /// Gets the EntityManager.
        /// </summary>
        protected static EntityManager EM => UnifiedCore.EntityManager;
        
        #endregion
        
        #region Abstract Execute Method
        
        /// <summary>
        /// Abstract method that subclasses must implement to execute command logic.
        /// </summary>
        /// <param name="ctx">The command context.</param>
        protected abstract void Execute(ChatCommandContext ctx);
        
        #endregion
        
        #region System Readiness
        
        /// <summary>
        /// Ensures the core systems are ready before executing a command.
        /// </summary>
        protected static void EnsureReady()
        {
            if (!UnifiedCore.IsInitialized)
            {
                throw new CommandException("System not ready. Please wait for initialization.");
            }
            
            if (UnifiedCore.Server == null)
            {
                throw new CommandException("Server world not available.");
            }
        }
        
        #endregion
        
        #region Permission Handling
        
        ///  Checks if the command<summary>
        /// sender has the required permission level.
        /// </summary>
        protected static bool HasPermission(ChatCommandContext ctx, PermissionLevel required)
        {
            // Admin-only commands check
            if (required >= PermissionLevel.Admin && !ctx.IsAdmin)
            {
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Throws if sender doesn't have required permission.
        /// </summary>
        protected static void RequirePermission(ChatCommandContext ctx, PermissionLevel required)
        {
            if (!HasPermission(ctx, required))
            {
                throw new CommandException("Permission denied. Admin access required.");
            }
        }
        
        #endregion
        
        #region Cooldown Management
        
        private static readonly Dictionary<string, DateTime> _cooldowns = new();
        private static readonly object _cooldownLock = new();
        
        /// <summary>
        /// Checks if a command is on cooldown for the given player.
        /// </summary>
        protected static bool IsOnCooldown(string commandName, ulong playerId, TimeSpan cooldown, out TimeSpan remaining)
        {
            var key = $"{commandName}:{playerId}";
            lock (_cooldownLock)
            {
                if (_cooldowns.TryGetValue(key, out var expiresAt))
                {
                    remaining = expiresAt - DateTime.UtcNow;
                    if (remaining > TimeSpan.Zero)
                    {
                        return true;
                    }
                    _cooldowns.Remove(key);
                }
            }
            remaining = TimeSpan.Zero;
            return false;
        }
        
        /// <summary>
        /// Sets a cooldown for a command for the given player.
        /// </summary>
        protected static void SetCooldown(string commandName, ulong playerId, TimeSpan cooldown)
        {
            if (cooldown <= TimeSpan.Zero) return;
            
            var key = $"{commandName}:{playerId}";
            lock (_cooldownLock)
            {
                _cooldowns[key] = DateTime.UtcNow + cooldown;
            }
        }
        
        /// <summary>
        /// Clears a cooldown for a command for the given player.
        /// </summary>
        protected static void ClearCooldown(string commandName, ulong playerId)
        {
            var key = $"{commandName}:{playerId}";
            lock (_cooldownLock)
            {
                _cooldowns.Remove(key);
            }
        }
        
        /// <summary>
        /// Checks and applies cooldown, throwing if on cooldown.
        /// </summary>
        protected static void RequireCooldown(string commandName, ulong playerId, TimeSpan cooldown)
        {
            if (IsOnCooldown(commandName, playerId, cooldown, out var remaining))
            {
                throw new CommandException($"Command on cooldown. Try again in {remaining.TotalSeconds:F0}s.");
            }
            SetCooldown(commandName, playerId, cooldown);
        }
        
        #endregion
        
        #region Rich Feedback Methods
        
        /// <summary>
        /// Sends formatted feedback to the player with consistent styling.
        /// </summary>
        protected static void SendFeedback(ChatCommandContext ctx, FeedbackType type, string message)
        {
            var (color, prefix) = type switch
            {
                FeedbackType.Success => (ChatColor.Green, "✓"),
                FeedbackType.Error => (ChatColor.Red, "✗"),
                FeedbackType.Warning => (ChatColor.Yellow, "⚠"),
                FeedbackType.Info => (ChatColor.Cyan, "ℹ"),
                FeedbackType.Data => (ChatColor.LightBlue, "📊"),
                FeedbackType.Location => (ChatColor.Gold, "📍"),
                FeedbackType.Count => (ChatColor.Lime, "🔢"),
                _ => (ChatColor.White, "•")
            };
            
            Reply(ctx, $"{prefix} {message}", color);
        }
        
        /// <summary>
        /// Sends a success feedback with optional data.
        /// </summary>
        protected static void SendSuccess(ChatCommandContext ctx, string message, string? data = null)
        {
            var fullMessage = data != null ? $"{message}: {data}" : message;
            SendFeedback(ctx, FeedbackType.Success, fullMessage);
        }
        
        /// <summary>
        /// Sends an error feedback with explanation.
        /// </summary>
        protected static void SendError(ChatCommandContext ctx, string message, string? explanation = null)
        {
            var fullMessage = explanation != null ? $"{message} ({explanation})" : message;
            SendFeedback(ctx, FeedbackType.Error, fullMessage);
        }
        
        /// <summary>
        /// Sends info feedback with context.
        /// </summary>
        protected static void SendInfo(ChatCommandContext ctx, string message)
        {
            SendFeedback(ctx, FeedbackType.Info, message);
        }
        
        /// <summary>
        /// Sends location data (x, y, z).
        /// </summary>
        protected static void SendLocation(ChatCommandContext ctx, string label, float3 position)
        {
            SendFeedback(ctx, FeedbackType.Location, $"{label}: ({position.x:F0}, {position.y:F0}, {position.z:F0})");
        }
        
        /// <summary>
        /// Sends count/number data.
        /// </summary>
        protected static void SendCount(ChatCommandContext ctx, string label, int count)
        {
            SendFeedback(ctx, FeedbackType.Count, $"{label}: {count}");
        }
        
        /// <summary>
        /// Replies to the command sender with a colored message.
        /// </summary>
        protected static void Reply(ChatCommandContext ctx, string message, string color = "white")
        {
            ctx.Reply($"<color={color}>{message}</color>");
        }
        
        /// <summary>
        /// Replies with a success message (green).
        /// </summary>
        protected static void ReplySuccess(ChatCommandContext ctx, string message)
            => Reply(ctx, message, ChatColor.Green);
        
        /// <summary>
        /// Replies with an error message (red).
        /// </summary>
        protected static void ReplyError(ChatCommandContext ctx, string message)
            => Reply(ctx, message, ChatColor.Red);
        
        /// <summary>
        /// Replies with a warning message (yellow).
        /// </summary>
        protected static void ReplyWarning(ChatCommandContext ctx, string message)
            => Reply(ctx, message, ChatColor.Yellow);
        
        /// <summary>
        /// Replies with an info message (cyan).
        /// </summary>
        protected static void ReplyInfo(ChatCommandContext ctx, string message)
            => Reply(ctx, message, ChatColor.Cyan);
        
        /// <summary>
        /// Replies with a debug message (gray) - only in debug builds.
        /// </summary>
        [System.Diagnostics.Conditional("DEBUG")]
        protected static void ReplyDebug(ChatCommandContext ctx, string message)
            => Reply(ctx, message, ChatColor.Gray);
        
        #endregion
        
        #region Safe Execution
        
        /// <summary>
        /// Executes the command with full error handling and logging.
        /// </summary>
        protected static void Execute(ChatCommandContext ctx, string commandName)
        {
            try
            {
                EnsureReady();
                var playerInfo = GetPlayerInfo(ctx);
                Log.Info($"Command '{commandName}' executed by {playerInfo.Name} ({playerInfo.PlatformId})");
                return;
            }
            catch (CommandException ex)
            {
                ReplyError(ctx, ex.Message);
                Log.Debug($"Command '{commandName}' rejected: {ex.Message}");
            }
            catch (Exception ex)
            {
                Log.Exception(ex, commandName);
                ReplyError(ctx, $"An error occurred. Check server logs.");
            }
        }
        
        /// <summary>
        /// Safely executes action with exception handling.
        /// </summary>
        protected static void ExecuteSafely(ChatCommandContext ctx, string commandName, Action action)
        {
            try
            {
                EnsureReady();
                action();
                LogCommand(commandName, GetPlayerInfo(ctx));
            }
            catch (CommandException ex)
            {
                ReplyError(ctx, ex.Message);
                Log.Debug($"Command '{commandName}' rejected: {ex.Message}");
            }
            catch (Exception ex)
            {
                Log.Exception(ex, commandName);
                ReplyError(ctx, $"An error occurred. Check server logs.");
            }
        }
        
        #endregion
        
        #region Logging
        
        /// <summary>
        /// Logs a command execution with caller context.
        /// </summary>
        protected static void LogCommand(string commandName, PlayerInfo player)
        {
            Log.Info($"Command '{commandName}' executed by {player.Name} ({player.PlatformId})");
        }
        
        /// <summary>
        /// Gets player info from context.
        /// </summary>
        protected static PlayerInfo GetPlayerInfo(ChatCommandContext ctx)
        {
            try
            {
                var userEntity = ctx.Event?.SenderUserEntity;
                if (userEntity != null && EM.HasComponent<User>(userEntity.Value))
                {
                    var user = EM.GetComponentData<User>(userEntity.Value);
                    return new PlayerInfo
                    {
                        PlatformId = user.PlatformId,
                        Name = user.CharacterName.ToString()
                    };
                }
            }
            catch { }
            
            return new PlayerInfo { Name = "Unknown", PlatformId = 0 };
        }
        
        #endregion
        
        #region Query Helpers
        
        /// <summary>
        /// Safely queries entities and returns empty array on error.
        /// </summary>
        protected static NativeArray<Entity> QueryEntities(EntityQuery query, Allocator allocator = Allocator.Temp)
        {
            try
            {
                return query.ToEntityArray(allocator);
            }
            catch (Exception ex)
            {
                Log.Warning($"Query failed: {ex.Message}");
                return new NativeArray<Entity>(0, allocator);
            }
        }
        
        /// <summary>
        /// Safely queries component count.
        /// </summary>
        protected static int QueryCount(EntityQuery query)
        {
            try
            {
                return query.CalculateEntityCount();
            }
            catch (Exception ex)
            {
                Log.Warning($"Query count failed: {ex.Message}");
                return 0;
            }
        }
        
        #endregion
    }
    
    #region Supporting Types
    
    /// <summary>
    /// Permission levels for command access.
    /// </summary>
    public enum PermissionLevel
    {
        Anyone = 0,
        Moderator = 1,
        Admin = 2
    }
    
    /// <summary>
    /// Types of feedback for consistent messaging.
    /// </summary>
    public enum FeedbackType
    {
        Success,
        Error,
        Warning,
        Info,
        Data,
        Location,
        Count
    }
    
    /// <summary>
    /// Player information from command context.
    /// </summary>
    public readonly struct PlayerInfo
    {
        public ulong PlatformId { get; init; }
        public string Name { get; init; }
    }
    
    #endregion
}
