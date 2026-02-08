using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace VAuto.Core.Services
{
    /// <summary>
    /// HTTP API Server for V Rising Admin GUI integration.
    /// Provides REST endpoints for managing zones, traps, chests, and configurations.
    /// </summary>
    public class HttpServer : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly string _prefix;
        private readonly CancellationTokenSource _cts;
        private bool _disposed;
        private readonly Dictionary<string, Func<HttpListenerContext, Task>> _routes;
        private string _apiKey = string.Empty;
        private readonly List<string> _eventLog = new();

        public event Action<string> OnRequest;
        public event Action<string> OnEvent;

        public HttpServer(string prefix = "http://localhost:8080/")
        {
            _prefix = prefix;
            _listener = new HttpListener();
            _listener.Prefixes.Add(prefix);
            _cts = new CancellationTokenSource();
            
            // Register routes
            _routes = new Dictionary<string, Func<HttpListenerContext, Task>>(StringComparer.OrdinalIgnoreCase)
            {
                // Status
                ["GET /api/status"] = HandleStatusAsync,
                ["GET /api/stats"] = HandleQuickStatsAsync,
                
                // Zones
                ["GET /api/zones"] = HandleGetZonesAsync,
                ["POST /api/zones/glow/spawn"] = HandleSpawnGlowsAsync,
                ["POST /api/zones/glow/clear"] = HandleClearGlowsAsync,
                ["PUT /api/zones/borders"] = HandleToggleBordersAsync,
                ["PUT /api/zones/config"] = HandleUpdateZoneConfigAsync,
                
                // Traps
                ["GET /api/traps"] = HandleGetTrapsAsync,
                ["POST /api/traps/set"] = HandleSetTrapAsync,
                ["POST /api/traps/remove"] = HandleRemoveTrapAsync,
                ["POST /api/traps/arm"] = HandleArmTrapAsync,
                ["POST /api/traps/trigger"] = HandleTriggerTrapAsync,
                ["POST /api/traps/clear"] = HandleClearAllTrapsAsync,
                ["GET /api/traps/zones"] = HandleGetTrapZonesAsync,
                ["POST /api/traps/zones/create"] = HandleCreateTrapZoneAsync,
                ["POST /api/traps/zones/delete"] = HandleDeleteTrapZoneAsync,
                ["POST /api/traps/zones/arm"] = HandleArmTrapZoneAsync,
                
                // Chests
                ["GET /api/chests"] = HandleGetChestsAsync,
                ["POST /api/chests/spawn"] = HandleSpawnChestAsync,
                ["POST /api/chests/remove"] = HandleRemoveChestAsync,
                ["POST /api/chests/clear"] = HandleClearAllChestsAsync,
                
                // Streaks
                ["GET /api/streaks"] = HandleGetStreaksAsync,
                ["POST /api/streaks/reset"] = HandleResetStreakAsync,
                
                // Config
                ["GET /api/config"] = HandleGetConfigAsync,
                ["PUT /api/config"] = HandleUpdateConfigAsync,
                ["POST /api/config/reload"] = HandleReloadConfigAsync,
                
                // Logs
                ["GET /api/logs"] = HandleGetLogsAsync,
                
                // Player Tracking
                ["GET /api/players"] = HandleGetPlayersAsync,
                ["GET /api/players/update"] = HandlePlayerUpdateAsync,
            };
        }

        public void SetApiKey(string apiKey)
        {
            _apiKey = apiKey ?? string.Empty;
        }

        public async Task StartAsync()
        {
            _listener.Start();
            Plugin.LogInstance.LogInfo($"HTTP API Server started on {_prefix}");
            
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = Task.Run(() => HandleRequestAsync(context));
                }
                catch (HttpListenerException) when (_cts.Token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Plugin.LogInstance.LogWarning($"HTTP Server error: {ex.Message}");
                }
            }
        }

        public void Stop()
        {
            _cts.Cancel();
            _listener.Stop();
            Plugin.LogInstance.LogInfo("HTTP API Server stopped");
        }

        private async Task HandleRequestAsync(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;
            
            try
            {
                // CORS headers
                response.AddHeader("Access-Control-Allow-Origin", "*");
                response.AddHeader("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
                response.AddHeader("Access-Control-Allow-Headers", "Content-Type, X-API-Key");
                
                if (request.HttpMethod == "OPTIONS")
                {
                    response.StatusCode = 204;
                    response.Close();
                    return;
                }

                // API Key authentication
                var requestApiKey = request.Headers["X-API-Key"];
                if (!string.IsNullOrEmpty(_apiKey) && requestApiKey != _apiKey)
                {
                    await SendJsonAsync(response, 401, new { error = "Unauthorized", message = "Invalid API key" });
                    return;
                }

                var method = request.HttpMethod;
                var path = request.Url.AbsolutePath;
                var routeKey = $"{method} {path}";
                
                OnRequest?.Invoke($"{method} {path}");
                
                if (_routes.TryGetValue(routeKey, out var handler))
                {
                    await handler(context);
                }
                else
                {
                    await SendJsonAsync(response, 404, new { error = "Not Found", message = $"Route {routeKey} not found" });
                }
            }
            catch (Exception ex)
            {
                Plugin.LogInstance.LogError($"HTTP request error: {ex.Message}");
                await SendJsonAsync(response, 500, new { error = "Internal Server Error", message = ex.Message });
            }
            finally
            {
                response.Close();
            }
        }

        #region Status Endpoints

        private async Task HandleStatusAsync(HttpListenerContext context)
        {
            var status = new
            {
                online = true,
                playerCount = 0, // Would need to query player system
                maxPlayers = 50,
                uptime = (int)(DateTime.UtcNow - Process.GetProcessStartTime()).TotalSeconds,
                version = "1.0.0"
            };
            await SendJsonAsync(context.Response, 200, status);
        }

        private async Task HandleQuickStatsAsync(HttpListenerContext context)
        {
            var stats = new
            {
                activeZones = 0,
                totalTraps = 0,
                armedTraps = 0,
                activeChests = 0,
                activeStreaks = 0
            };
            await SendJsonAsync(context.Response, 200, stats);
        }

        #endregion

        #region Zone Endpoints

        private async Task HandleGetZonesAsync(HttpListenerContext context)
        {
            var zones = new[]
            {
                new
                {
                    id = "arena_main",
                    name = "Main Arena",
                    center = new { x = 0, y = 0, z = 0 },
                    radius = 50,
                    isActive = true,
                    glowEnabled = true,
                    glowPrefab = "Default",
                    spacing = 5
                }
            };
            await SendJsonAsync(context.Response, 200, zones);
        }

        private async Task HandleSpawnGlowsAsync(HttpListenerContext context)
        {
            // Call ArenaGlowBorderService.SpawnBorderGlows
            LogEvent("Spawn glows requested");
            await SendJsonAsync(context.Response, 200, new { success = true, message = "Glows spawned" });
        }

        private async Task HandleClearGlowsAsync(HttpListenerContext context)
        {
            // Call ArenaGlowBorderService.ClearAll
            LogEvent("Clear glows requested");
            await SendJsonAsync(context.Response, 200, new { success = true, message = "Glows cleared" });
        }

        private async Task HandleToggleBordersAsync(HttpListenerContext context)
        {
            var body = await ReadJsonAsync<dynamic>(context.Request);
            bool enabled = body?.enabled ?? true;
            // Set ArenaTerritory.EnableGlowBorder
            await SendJsonAsync(context.Response, 200, new { success = true });
        }

        private async Task HandleUpdateZoneConfigAsync(HttpListenerContext context)
        {
            var body = await ReadJsonAsync<dynamic>(context.Request);
            // Update zone configuration
            await SendJsonAsync(context.Response, 200, new { success = true });
        }

        #endregion

        #region Trap Endpoints

        private async Task HandleGetTrapsAsync(HttpListenerContext context)
        {
            var traps = Array.Empty<object>();
            await SendJsonAsync(context.Response, 200, traps);
        }

        private async Task HandleSetTrapAsync(HttpListenerContext context)
        {
            var body = await ReadJsonAsync<dynamic>(context.Request);
            LogEvent("Trap set requested");
            await SendJsonAsync(context.Response, 200, new { success = true });
        }

        private async Task HandleRemoveTrapAsync(HttpListenerContext context)
        {
            await SendJsonAsync(context.Response, 200, new { success = true });
        }

        private async Task HandleArmTrapAsync(HttpListenerContext context)
        {
            await SendJsonAsync(context.Response, 200, new { success = true });
        }

        private async Task HandleTriggerTrapAsync(HttpListenerContext context)
        {
            LogEvent("Trap triggered");
            await SendJsonAsync(context.Response, 200, new { success = true });
        }

        private async Task HandleClearAllTrapsAsync(HttpListenerContext context)
        {
            LogEvent("All traps cleared");
            await SendJsonAsync(context.Response, 200, new { success = true });
        }

        #endregion

        #region Trap Zone Endpoints

        private async Task HandleGetTrapZonesAsync(HttpListenerContext context)
        {
            var zones = Array.Empty<object>();
            await SendJsonAsync(context.Response, 200, zones);
        }

        private async Task HandleCreateTrapZoneAsync(HttpListenerContext context)
        {
            await SendJsonAsync(context.Response, 200, new { success = true });
        }

        private async Task HandleDeleteTrapZoneAsync(HttpListenerContext context)
        {
            await SendJsonAsync(context.Response, 200, new { success = true });
        }

        private async Task HandleArmTrapZoneAsync(HttpListenerContext context)
        {
            await SendJsonAsync(context.Response, 200, new { success = true });
        }

        #endregion

        #region Chest Endpoints

        private async Task HandleGetChestsAsync(HttpListenerContext context)
        {
            var chests = Array.Empty<object>();
            await SendJsonAsync(context.Response, 200, chests);
        }

        private async Task HandleSpawnChestAsync(HttpListenerContext context)
        {
            LogEvent("Chest spawned");
            await SendJsonAsync(context.Response, 200, new { success = true });
        }

        private async Task HandleRemoveChestAsync(HttpListenerContext context)
        {
            await SendJsonAsync(context.Response, 200, new { success = true });
        }

        private async Task HandleClearAllChestsAsync(HttpListenerContext context)
        {
            LogEvent("All chests cleared");
            await SendJsonAsync(context.Response, 200, new { success = true });
        }

        #endregion

        #region Streak Endpoints

        private async Task HandleGetStreaksAsync(HttpListenerContext context)
        {
            var streaks = Array.Empty<object>();
            await SendJsonAsync(context.Response, 200, streaks);
        }

        private async Task HandleResetStreakAsync(HttpListenerContext context)
        {
            await SendJsonAsync(context.Response, 200, new { success = true });
        }

        #endregion

        #region Config Endpoints

        private async Task HandleGetConfigAsync(HttpListenerContext context)
        {
            var config = new
            {
                zone = new { glowSpacing = 5, glowPrefab = "Default", cornerSpawns = true },
                trap = new { killThreshold = 5, trapDamage = 50, trapDuration = 10 },
                streak = new { announcementThreshold = 10, timeoutSeconds = 120, announcementsEnabled = true }
            };
            await SendJsonAsync(context.Response, 200, config);
        }

        private async Task HandleUpdateConfigAsync(HttpListenerContext context)
        {
            await SendJsonAsync(context.Response, 200, new { success = true });
        }

        private async Task HandleReloadConfigAsync(HttpListenerContext context)
        {
            LogEvent("Config reloaded");
            await SendJsonAsync(context.Response, 200, new { success = true });
        }

        #endregion

        #region Log Endpoints

        private async Task HandleGetLogsAsync(HttpListenerContext context)
        {
            await SendJsonAsync(context.Response, 200, _eventLog);
        }

        #endregion

        #region Player Tracking Endpoints

        private async Task HandleGetPlayersAsync(HttpListenerContext context)
        {
            // Return list of all online players with their positions
            // This would integrate with the game's player system
            var players = new[]
            {
                new
                {
                    id = "player_001",
                    name = "VampireKing",
                    x = 150.5,
                    y = -75.3,
                    hp = 1000,
                    maxHp = 1000,
                    guild = "NightWalkers",
                    isOnline = true,
                    lastSeen = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                },
                new
                {
                    id = "player_002",
                    name = "BloodHunter",
                    x = -200.0,
                    y = 100.0,
                    hp = 750,
                    maxHp = 1000,
                    guild = "Solo",
                    isOnline = true,
                    lastSeen = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                }
            };
            await SendJsonAsync(context.Response, 200, players);
        }

        private async Task HandlePlayerUpdateAsync(HttpListenerContext context)
        {
            // WebSocket-style polling endpoint for real-time updates
            // Returns only changed data since last poll
            var updates = new
            {
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                moved = new[]
                {
                    new
                    {
                        id = "player_001",
                        x = 155.2,
                        y = -72.1
                    }
                },
                joined = Array.Empty<object>(),
                left = Array.Empty<string>()
            };
            await SendJsonAsync(context.Response, 200, updates);
        }

        #endregion

        #region Helpers

        private void LogEvent(string message)
        {
            var entry = new
            {
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                type = "system",
                message
            };
            _eventLog.Add(entry.ToString());
            if (_eventLog.Count > 1000) _eventLog.RemoveAt(0);
            OnEvent?.Invoke(message);
        }

        private async Task SendJsonAsync(HttpListenerResponse response, int statusCode, object data)
        {
            response.StatusCode = statusCode;
            response.ContentType = "application/json";
            var json = JsonSerializer.Serialize(data);
            var buffer = Encoding.UTF8.GetBytes(json);
            await response.OutputStream.WriteAsync(buffer);
        }

        private async Task<T> ReadJsonAsync<T>(HttpListenerRequest request)
        {
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
            var json = await reader.ReadToEndAsync();
            return JsonSerializer.Deserialize<T>(json) ?? default;
        }

        #endregion

        public void Dispose()
        {
            if (!_disposed)
            {
                Stop();
                _cts.Dispose();
                _disposed = true;
            }
        }
    }
}
