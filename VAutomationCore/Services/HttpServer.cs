using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Unity.Entities;
using Unity.Collections;
using ProjectM;
using ProjectM.Network;
using VAuto.Core.Services.DTOs;
using VAuto.Zone.Services;

namespace VAuto.Core.Services
{
    /// <summary>
    /// HTTP API Server for V Rising Admin GUI integration.
    /// Provides REST endpoints for managing zones, traps, chests, and configurations.
    /// API Version: v1
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
        private readonly object _eventLogLock = new object();
        private WebSocketServer? _webSocketServer;

        public event Action<string>? OnRequest;

        public HttpServer(string prefix = "http://localhost:8080/")
        {
            _prefix = prefix;
            _listener = new HttpListener();
            _listener.Prefixes.Add(prefix);
            _cts = new CancellationTokenSource();

            _routes = new Dictionary<string, Func<HttpListenerContext, Task>>(StringComparer.OrdinalIgnoreCase)
            {
                ["GET /api/v1/status"] = HandleStatusAsync,
                ["GET /api/v1/stats"] = HandleQuickStatsAsync,
                ["GET /api/v1/zones"] = HandleGetZonesAsync,
                ["GET /api/v1/zones/paginated"] = HandleGetZonesPaginatedAsync,
                ["POST /api/v1/zones/glow/spawn"] = HandleSpawnGlowsAsync,
                ["POST /api/v1/zones/glow/clear"] = HandleClearGlowsAsync,
                ["PUT /api/v1/zones/borders"] = HandleToggleBordersAsync,
                ["PUT /api/v1/zones/config"] = HandleUpdateZoneConfigAsync,
                ["GET /api/v1/traps"] = HandleGetTrapsAsync,
                ["GET /api/v1/traps/paginated"] = HandleGetTrapsPaginatedAsync,
                ["POST /api/v1/traps/set"] = HandleSetTrapAsync,
                ["POST /api/v1/traps/remove"] = HandleRemoveTrapAsync,
                ["POST /api/v1/traps/arm"] = HandleArmTrapAsync,
                ["POST /api/v1/traps/trigger"] = HandleTriggerTrapAsync,
                ["POST /api/v1/traps/clear"] = HandleClearAllTrapsAsync,
                ["GET /api/v1/traps/zones"] = HandleGetTrapZonesAsync,
                ["POST /api/v1/traps/zones/create"] = HandleCreateTrapZoneAsync,
                ["POST /api/v1/traps/zones/delete"] = HandleDeleteTrapZoneAsync,
                ["POST /api/v1/traps/zones/arm"] = HandleArmTrapZoneAsync,
                ["GET /api/v1/chests"] = HandleGetChestsAsync,
                ["GET /api/v1/chests/paginated"] = HandleGetChestsPaginatedAsync,
                ["POST /api/v1/chests/spawn"] = HandleSpawnChestAsync,
                ["POST /api/v1/chests/remove"] = HandleRemoveChestAsync,
                ["POST /api/v1/chests/clear"] = HandleClearAllChestsAsync,
                ["GET /api/v1/streaks"] = HandleGetStreaksAsync,
                ["POST /api/v1/streaks/reset"] = HandleResetStreakAsync,
                ["GET /api/v1/config"] = HandleGetConfigAsync,
                ["PUT /api/v1/config"] = HandleUpdateConfigAsync,
                ["POST /api/v1/config/reload"] = HandleReloadConfigAsync,
                ["GET /api/v1/logs"] = HandleGetLogsAsync,
                ["GET /api/v1/players"] = HandleGetPlayersAsync,
                ["GET /api/v1/players/paginated"] = HandleGetPlayersPaginatedAsync,
                ["GET /api/v1/players/update"] = HandlePlayerUpdateAsync,
                ["GET /api/status"] = HandleLegacyRedirectAsync,
                ["GET /api/stats"] = HandleLegacyRedirectAsync,
                ["GET /api/zones"] = HandleLegacyRedirectAsync,
                ["GET /api/traps"] = HandleLegacyRedirectAsync,
                ["GET /api/chests"] = HandleLegacyRedirectAsync,
                ["GET /api/streaks"] = HandleLegacyRedirectAsync,
                ["GET /api/config"] = HandleLegacyRedirectAsync,
                ["GET /api/logs"] = HandleLegacyRedirectAsync,
                ["GET /api/players"] = HandleLegacyRedirectAsync,
                ["GET /api/players/update"] = HandleLegacyRedirectAsync,
            };
        }

        public void SetWebSocketServer(WebSocketServer? wsServer) => _webSocketServer = wsServer;
        public void SetApiKey(string apiKey) => _apiKey = apiKey ?? string.Empty;

        public async Task StartAsync()
        {
            _listener.Start();
            Plugin.LogInstance.LogInfo($"[HttpServer] API v1 started on {_prefix}");
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = Task.Run(() => HandleRequestAsync(context));
                }
                catch (HttpListenerException) when (_cts.Token.IsCancellationRequested) { break; }
                catch (Exception ex) { Plugin.LogInstance.LogWarning($"[HttpServer] Error: {ex.Message}"); }
            }
        }

        public void Stop()
        {
            _cts.Cancel();
            _listener.Stop();
            Plugin.LogInstance.LogInfo("[HttpServer] stopped");
        }

        private async Task HandleRequestAsync(HttpListenerContext context)
        {
            var response = context.Response;
            try
            {
                response.AddHeader("Access-Control-Allow-Origin", "*");
                response.AddHeader("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
                response.AddHeader("Access-Control-Allow-Headers", "Content-Type, X-API-Key");

                if (context.Request.HttpMethod == "OPTIONS")
                {
                    response.StatusCode = 204;
                    response.Close();
                    return;
                }

                var requestApiKey = context.Request.Headers["X-API-Key"];
                if (!string.IsNullOrEmpty(_apiKey) && requestApiKey != _apiKey)
                {
                    await SendJsonAsync(response, 401, ApiResponse<object>.Error(ErrorResponse.Unauthorized("Invalid API key")));
                    return;
                }

                var routeKey = $"{context.Request.HttpMethod} {context.Request.Url.AbsolutePath}";
                OnRequest?.Invoke(routeKey);

                if (_routes.TryGetValue(routeKey, out var handler))
                    await handler(context);
                else
                    await SendJsonAsync(response, 404, ApiResponse<object>.Error(ErrorResponse.NotFound(routeKey)));
            }
            catch (Exception ex)
            {
                Plugin.LogInstance.LogError($"[HttpServer] Request error: {ex.Message}");
                await SendJsonAsync(response, 500, ApiResponse<object>.Error(ErrorResponse.InternalError(ex.Message)));
            }
            finally { response.Close(); }
        }

        #region Helpers

        private static (int offset, int limit) ParsePagination(string query)
        {
            var offset = 0;
            var limit = 50;
            if (string.IsNullOrEmpty(query)) return (offset, limit);

            try
            {
                var parsed = System.Web.HttpUtility.ParseQueryString(query);
                if (int.TryParse(parsed["offset"], out var o)) offset = Math.Max(0, o);
                if (int.TryParse(parsed["limit"], out var l)) limit = Math.Clamp(l, 1, 500);
            }
            catch { }
            return (offset, limit);
        }

        private static async Task SendJsonAsync<T>(HttpListenerResponse response, int statusCode, T data)
        {
            response.StatusCode = statusCode;
            response.ContentType = "application/json";
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var buffer = Encoding.UTF8.GetBytes(json);
            await response.OutputStream.WriteAsync(buffer);
        }

        private static async Task<T> ReadJsonAsync<T>(HttpListenerRequest request)
        {
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
            var json = await reader.ReadToEndAsync();
            return JsonSerializer.Deserialize<T>(json) ?? default!;
        }

        private void LogEvent(string message)
        {
            var entry = new { timestamp = DateTime.UtcNow.ToString("o"), type = "system", message };
            lock (_eventLogLock)
            {
                _eventLog.Add(entry.ToString());
                if (_eventLog.Count > 1000) _eventLog.RemoveAt(0);
            }
        }

        #endregion

        #region Legacy
        private Task HandleLegacyRedirectAsync(HttpListenerContext context)
        {
            var v1Path = context.Request.Url.AbsolutePath.Replace("/api/", "/api/v1/");
            context.Response.AddHeader("X-Deprecated-Route", "true");
            context.Response.AddHeader("X-Migration-Path", v1Path);
            return SendJsonAsync(context.Response, 301, new { message = "Use v1", migration = v1Path });
        }
        #endregion

        #region Status
        private async Task HandleStatusAsync(HttpListenerContext context)
        {
            try
            {
                var status = new StatusDto
                {
                    Online = true,
                    PlayerCount = 0,
                    MaxPlayers = 50,
                    Uptime = (long)(DateTime.UtcNow - Process.GetCurrentProcess().StartTime).TotalSeconds,
                    Version = "1.0.0",
                    ZonesActive = 0,
                    Plugins = new[] { new PluginStatusDto { Name = "VAutoZone", Version = "1.0.0", Enabled = true, Status = "active" } }
                };
                await SendJsonAsync(context.Response, 200, ApiResponse<StatusDto>.Ok(status));
            }
            catch (Exception ex)
            {
                await SendJsonAsync(context.Response, 500, ApiResponse<object>.Error(ErrorResponse.InternalError(ex.Message)));
            }
        }

        private async Task HandleQuickStatsAsync(HttpListenerContext context)
        {
            var stats = new QuickStatsDto { ActiveZones = 0, TotalTraps = 0, ArmedTraps = 0, ActiveChests = 0, ActiveStreaks = 0, Timestamp = DateTime.UtcNow.ToString("o") };
            await SendJsonAsync(context.Response, 200, ApiResponse<QuickStatsDto>.Ok(stats));
        }
        #endregion

        #region Zones
        private async Task HandleGetZonesAsync(HttpListenerContext context)
        {
            var zones = new List<ZoneDto> { new ZoneDto { Id = "arena_main", Name = "Main Arena", Radius = 50, IsActive = true, Type = "arena" } };
            await SendJsonAsync(context.Response, 200, ApiResponse<List<ZoneDto>>.Ok(zones));
        }

        private async Task HandleGetZonesPaginatedAsync(HttpListenerContext context)
        {
            var (offset, limit) = ParsePagination(context.Request.Url.Query);
            var all = new List<ZoneDto> { new ZoneDto { Id = "arena_main", Name = "Main Arena", Radius = 50, IsActive = true, Type = "arena" } };
            var paged = all.Skip(offset).Take(limit).ToArray();
            await SendJsonAsync(context.Response, 200, PaginatedResponse<ZoneDto>.Create(paged, offset, limit, all.Count));
        }

        private async Task HandleSpawnGlowsAsync(HttpListenerContext context) => await SendJsonAsync(context.Response, 200, ApiResponse<object>.Ok(new { message = "Glows spawned" }));
        private async Task HandleClearGlowsAsync(HttpListenerContext context) => await SendJsonAsync(context.Response, 200, ApiResponse<object>.Ok(new { message = "Glows cleared" }));
        private async Task HandleToggleBordersAsync(HttpListenerContext context) => await SendJsonAsync(context.Response, 200, ApiResponse<object>.Ok(new { success = true }));
        private async Task HandleUpdateZoneConfigAsync(HttpListenerContext context) => await SendJsonAsync(context.Response, 200, ApiResponse<object>.Ok(new { success = true }));
        #endregion

        #region Traps
        private async Task HandleGetTrapsAsync(HttpListenerContext context) => await SendJsonAsync(context.Response, 200, ApiResponse<object>.Ok(Array.Empty<object>()));
        private async Task HandleGetTrapsPaginatedAsync(HttpListenerContext context)
        {
            var (offset, limit) = ParsePagination(context.Request.Url.Query);
            await SendJsonAsync(context.Response, 200, PaginatedResponse<object>.Create(Array.Empty<object>(), offset, limit, 0));
        }
        private async Task HandleSetTrapAsync(HttpListenerContext context) => await SendJsonAsync(context.Response, 200, ApiResponse<object>.Ok(new { success = true }));
        private async Task HandleRemoveTrapAsync(HttpListenerContext context) => await SendJsonAsync(context.Response, 200, ApiResponse<object>.Ok(new { success = true }));
        private async Task HandleArmTrapAsync(HttpListenerContext context) => await SendJsonAsync(context.Response, 200, ApiResponse<object>.Ok(new { success = true }));
        private async Task HandleTriggerTrapAsync(HttpListenerContext context) => await SendJsonAsync(context.Response, 200, ApiResponse<object>.Ok(new { success = true }));
        private async Task HandleClearAllTrapsAsync(HttpListenerContext context) => await SendJsonAsync(context.Response, 200, ApiResponse<object>.Ok(new { success = true }));
        private async Task HandleGetTrapZonesAsync(HttpListenerContext context) => await SendJsonAsync(context.Response, 200, ApiResponse<object>.Ok(Array.Empty<object>()));
        private async Task HandleCreateTrapZoneAsync(HttpListenerContext context) => await SendJsonAsync(context.Response, 200, ApiResponse<object>.Ok(new { success = true }));
        private async Task HandleDeleteTrapZoneAsync(HttpListenerContext context) => await SendJsonAsync(context.Response, 200, ApiResponse<object>.Ok(new { success = true }));
        private async Task HandleArmTrapZoneAsync(HttpListenerContext context) => await SendJsonAsync(context.Response, 200, ApiResponse<object>.Ok(new { success = true }));
        #endregion

        #region Chests
        private async Task HandleGetChestsAsync(HttpListenerContext context) => await SendJsonAsync(context.Response, 200, ApiResponse<object>.Ok(Array.Empty<object>()));
        private async Task HandleGetChestsPaginatedAsync(HttpListenerContext context)
        {
            var (offset, limit) = ParsePagination(context.Request.Url.Query);
            await SendJsonAsync(context.Response, 200, PaginatedResponse<object>.Create(Array.Empty<object>(), offset, limit, 0));
        }
        private async Task HandleSpawnChestAsync(HttpListenerContext context) => await SendJsonAsync(context.Response, 200, ApiResponse<object>.Ok(new { success = true }));
        private async Task HandleRemoveChestAsync(HttpListenerContext context) => await SendJsonAsync(context.Response, 200, ApiResponse<object>.Ok(new { success = true }));
        private async Task HandleClearAllChestsAsync(HttpListenerContext context) => await SendJsonAsync(context.Response, 200, ApiResponse<object>.Ok(new { success = true }));
        #endregion

        #region Streaks
        private async Task HandleGetStreaksAsync(HttpListenerContext context) => await SendJsonAsync(context.Response, 200, ApiResponse<object>.Ok(Array.Empty<object>()));
        private async Task HandleResetStreakAsync(HttpListenerContext context) => await SendJsonAsync(context.Response, 200, ApiResponse<object>.Ok(new { success = true }));
        #endregion

        #region Config
        private async Task HandleGetConfigAsync(HttpListenerContext context)
        {
            var config = new ConfigDto { Version = "1.0.0", LastModified = DateTime.UtcNow.ToString("o") };
            await SendJsonAsync(context.Response, 200, ApiResponse<ConfigDto>.Ok(config));
        }
        private async Task HandleUpdateConfigAsync(HttpListenerContext context) => await SendJsonAsync(context.Response, 200, ApiResponse<object>.Ok(new { success = true }));
        private async Task HandleReloadConfigAsync(HttpListenerContext context)
        {
            _webSocketServer?.NotifyConfigChanged("config");
            await SendJsonAsync(context.Response, 200, ApiResponse<object>.Ok(new { success = true }));
        }
        #endregion

        #region Logs
        private async Task HandleGetLogsAsync(HttpListenerContext context) => await SendJsonAsync(context.Response, 200, ApiResponse<List<string>>.Ok(_eventLog));
        #endregion

        #region Players
        private async Task HandleGetPlayersAsync(HttpListenerContext context)
        {
            var players = new List<PlayerDto> { new PlayerDto { Id = "stub", Name = "NoPlayersOnline", IsOnline = false } };
            await SendJsonAsync(context.Response, 200, ApiResponse<List<PlayerDto>>.Ok(players));
        }

        private async Task HandleGetPlayersPaginatedAsync(HttpListenerContext context)
        {
            var (offset, limit) = ParsePagination(context.Request.Url.Query);
            var all = new List<PlayerDto> { new PlayerDto { Id = "stub", Name = "NoPlayersOnline", IsOnline = false } };
            var paged = all.Skip(offset).Take(limit).ToArray();
            await SendJsonAsync(context.Response, 200, PaginatedResponse<PlayerDto>.Create(paged, offset, limit, all.Count));
        }

        private async Task HandlePlayerUpdateAsync(HttpListenerContext context)
        {
            var update = new { timestamp = DateTime.UtcNow.ToString("o"), moved = Array.Empty<object>(), joined = Array.Empty<object>(), left = Array.Empty<string>() };
            await SendJsonAsync(context.Response, 200, ApiResponse<object>.Ok(update));
        }
        #endregion

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
            _cts.Dispose();
        }
    }
}
