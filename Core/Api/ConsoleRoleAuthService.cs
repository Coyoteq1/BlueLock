using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BepInEx;
using BepInEx.Logging;
using VAutomationCore.Core.Data;
using VAutomationCore.Core.Services;
using VAutomationCore.Services;
using Unity.Entities;

namespace VAutomationCore.Core.Api
{
    /// <summary>
    /// Console/session authorization with explicit roles.
    /// Developer role is the only role allowed to execute job flows.
    /// </summary>
    public static class ConsoleRoleAuthService
    {
        public const ulong ConsoleSubjectId = ulong.MaxValue;
        private const string HashSalt = "VAuto.Core.ConsoleRoleAuth.v1";
        private const string DefaultAdminPassword = "change-me-admin";
        private const string DefaultDeveloperPassword = "change-me-dev";

        private static readonly object Sync = new();
        private static readonly string ConfigPath = ResolveConfigPath();
        private static readonly ManualLogSource Log = Logger.CreateLogSource("VAutomationCore.ConsoleRoleAuth");
        private static readonly Dictionary<ulong, SessionState> Sessions = new();

        private static ConsoleRoleAuthConfig _config = new();
        private static bool _initialized;

        public enum ConsoleRole
        {
            None = 0,
            Admin = 1,
            Developer = 2
        }

        private sealed class SessionState
        {
            public ConsoleRole Role { get; init; }
            public DateTime ExpiresAtUtc { get; init; }
        }

        private sealed class ConsoleRoleAuthConfig
        {
            public bool Enabled { get; set; } = true;
            public int SessionMinutes { get; set; } = 30;
            public List<string> AdminPasswordHashes { get; set; } = new();
            public List<string> DeveloperPasswordHashes { get; set; } = new();
        }

        private static string ResolveConfigPath()
        {
            var rootDir = Path.Combine(Paths.ConfigPath, "VAutomationCore");
            Directory.CreateDirectory(rootDir);
            return Path.Combine(rootDir, "VAuto.ConsoleRoles.json");
        }

        public static void Initialize()
        {
            lock (Sync)
            {
                if (_initialized)
                {
                    return;
                }

                LoadOrCreateConfig();
                _initialized = true;
            }
        }

        public static bool Authenticate(ulong subjectId, string password, ConsoleRole requestedRole, out string message)
        {
            EnsureInitialized();

            lock (Sync)
            {
                if (!_config.Enabled)
                {
                    var expiresAt = DateTime.UtcNow.AddMinutes(Math.Max(1, _config.SessionMinutes));
                    Sessions[subjectId] = new SessionState
                    {
                        Role = requestedRole,
                        ExpiresAtUtc = expiresAt
                    };
                    TryApplyRoleComponent(subjectId, requestedRole, expiresAt);
                    message = "Role auth disabled in config; session granted.";
                    return true;
                }

                if (requestedRole == ConsoleRole.None)
                {
                    message = "Requested role must be Admin or Developer.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(password))
                {
                    message = "Password is required.";
                    return false;
                }

                var expectedHashes = requestedRole == ConsoleRole.Developer
                    ? _config.DeveloperPasswordHashes
                    : _config.AdminPasswordHashes;

                if (expectedHashes == null || expectedHashes.Count == 0)
                {
                    message = $"No hashes configured for role '{requestedRole}'. Update {Path.GetFileName(ConfigPath)}.";
                    return false;
                }

                var candidateHash = HashPassword(password.Trim());
                foreach (var hash in expectedHashes)
                {
                    if (!ConstantTimeEquals(hash?.Trim().ToLowerInvariant(), candidateHash))
                    {
                        continue;
                    }

                    var expiresAt = DateTime.UtcNow.AddMinutes(Math.Max(1, _config.SessionMinutes));
                    Sessions[subjectId] = new SessionState
                    {
                        Role = requestedRole,
                        ExpiresAtUtc = expiresAt
                    };
                    TryApplyRoleComponent(subjectId, requestedRole, expiresAt);

                    message = $"{requestedRole} authenticated for {_config.SessionMinutes} minute(s).";
                    return true;
                }

                message = "Invalid password.";
                return false;
            }
        }

        public static bool IsAuthorized(ulong subjectId, ConsoleRole requiredRole, out TimeSpan remaining, out ConsoleRole currentRole)
        {
            EnsureInitialized();
            currentRole = ConsoleRole.None;

            lock (Sync)
            {
                if (!_config.Enabled)
                {
                    remaining = TimeSpan.MaxValue;
                    currentRole = ConsoleRole.Developer;
                    return currentRole >= requiredRole;
                }

                if (!Sessions.TryGetValue(subjectId, out var session))
                {
                    remaining = TimeSpan.Zero;
                    return false;
                }

                var now = DateTime.UtcNow;
                if (session.ExpiresAtUtc <= now)
                {
                    Sessions.Remove(subjectId);
                    TryClearRoleComponent(subjectId);
                    remaining = TimeSpan.Zero;
                    return false;
                }

                remaining = session.ExpiresAtUtc - now;
                currentRole = session.Role;
                return session.Role >= requiredRole;
            }
        }

        /// <summary>
        /// Developer-only gate for job capabilities.
        /// Admin role is intentionally insufficient for job execution.
        /// </summary>
        public static bool CanUseJobs(ulong subjectId, out TimeSpan remaining, out ConsoleRole currentRole)
        {
            return IsAuthorized(subjectId, ConsoleRole.Developer, out remaining, out currentRole);
        }

        public static void Revoke(ulong subjectId)
        {
            EnsureInitialized();
            lock (Sync)
            {
                Sessions.Remove(subjectId);
            }
            TryClearRoleComponent(subjectId);
        }

        public static bool IsEnabled
        {
            get
            {
                EnsureInitialized();
                lock (Sync)
                {
                    return _config.Enabled;
                }
            }
        }

        private static void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            Initialize();
        }

        private static void LoadOrCreateConfig()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var json = File.ReadAllText(ConfigPath);
                    _config = JsonSerializer.Deserialize<ConsoleRoleAuthConfig>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true
                    }) ?? new ConsoleRoleAuthConfig();
                }
                else
                {
                    _config = new ConsoleRoleAuthConfig();
                }

                _config.AdminPasswordHashes ??= new List<string>();
                _config.DeveloperPasswordHashes ??= new List<string>();

                if (_config.AdminPasswordHashes.Count == 0)
                {
                    _config.AdminPasswordHashes.Add(HashPassword(DefaultAdminPassword));
                }

                if (_config.DeveloperPasswordHashes.Count == 0)
                {
                    _config.DeveloperPasswordHashes.Add(HashPassword(DefaultDeveloperPassword));
                }

                if (_config.SessionMinutes <= 0)
                {
                    _config.SessionMinutes = 30;
                }

                SaveConfig();
                Log.LogInfo($"[ConsoleRoleAuth] Loaded config: {ConfigPath}");
            }
            catch (Exception ex)
            {
                _config = new ConsoleRoleAuthConfig
                {
                    AdminPasswordHashes = new List<string> { HashPassword(DefaultAdminPassword) },
                    DeveloperPasswordHashes = new List<string> { HashPassword(DefaultDeveloperPassword) }
                };
                Log.LogWarning($"[ConsoleRoleAuth] Failed to load config: {ex.Message}");
            }
        }

        private static void SaveConfig()
        {
            var json = JsonSerializer.Serialize(_config, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            File.WriteAllText(ConfigPath, json);
        }

        private static string HashPassword(string password)
        {
            var input = $"{HashSalt}:{password}";
            var bytes = Encoding.UTF8.GetBytes(input);
            var hashBytes = SHA256.HashData(bytes);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        private static void TryApplyRoleComponent(ulong subjectId, ConsoleRole role, DateTime expiresAtUtc)
        {
            try
            {
                if (subjectId == ConsoleSubjectId)
                {
                    return;
                }

                if (!GameActionService.TryFindUserEntityByPlatformId(subjectId, out var userEntity))
                {
                    return;
                }

                var em = UnifiedCore.EntityManager;
                if (!em.Exists(userEntity))
                {
                    return;
                }

                var component = new ConsoleRoleComponent
                {
                    Role = (byte)role,
                    ExpiresAtUnixSeconds = new DateTimeOffset(expiresAtUtc).ToUnixTimeSeconds()
                };

                if (em.HasComponent<ConsoleRoleComponent>(userEntity))
                {
                    em.SetComponentData(userEntity, component);
                }
                else
                {
                    em.AddComponentData(userEntity, component);
                }
            }
            catch (Exception ex)
            {
                Log.LogDebug($"[ConsoleRoleAuth] Failed to apply role component: {ex.Message}");
            }
        }

        private static void TryClearRoleComponent(ulong subjectId)
        {
            try
            {
                if (subjectId == ConsoleSubjectId)
                {
                    return;
                }

                if (!GameActionService.TryFindUserEntityByPlatformId(subjectId, out var userEntity))
                {
                    return;
                }

                var em = UnifiedCore.EntityManager;
                if (!em.Exists(userEntity) || !em.HasComponent<ConsoleRoleComponent>(userEntity))
                {
                    return;
                }

                em.RemoveComponent<ConsoleRoleComponent>(userEntity);
            }
            catch (Exception ex)
            {
                Log.LogDebug($"[ConsoleRoleAuth] Failed to clear role component: {ex.Message}");
            }
        }

        private static bool ConstantTimeEquals(string left, string right)
        {
            left ??= string.Empty;
            right ??= string.Empty;

            var maxLen = Math.Max(left.Length, right.Length);
            var diff = left.Length ^ right.Length;
            for (var i = 0; i < maxLen; i++)
            {
                var a = i < left.Length ? left[i] : '\0';
                var b = i < right.Length ? right[i] : '\0';
                diff |= a ^ b;
            }

            return diff == 0;
        }
    }
}
