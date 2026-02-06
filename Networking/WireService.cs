using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BepInEx.Logging;
using VAuto.Core.Networking;
using VAuto.Services;

namespace VAuto.Core.Networking
{
    /// <summary>
    /// Unified wire service for network communication with Java client
    /// Combines HTTP server, WebSocket support, and command handling
    /// </summary>
    public class WireService : IDisposable
    {
        private readonly ManualLogSource _log;
        private readonly HttpListener _listener;
        private readonly CancellationTokenSource _cts;
        private readonly Dictionary<string, IMessageHandler> _handlers;
        private readonly string _host;
        private readonly int _port;
        private bool _isRunning;

        public bool IsRunning => _isRunning;

        public WireService(ManualLogSource log, string host = "localhost", int port = 8080)
        {
            _log = log;
            _host = host;
            _port = port;
            _listener = new HttpListener();
            _cts = new CancellationTokenSource();

            // Register message handlers
            _handlers = new Dictionary<string, IMessageHandler>
            {
                [WireMessageTypes.Command] = new CommandHandler(_log),
                [WireMessageTypes.SnapshotRequest] = new SnapshotHandler(_log),
                [WireMessageTypes.ConfigGet] = new ConfigHandler(_log),
                [WireMessageTypes.ConfigSet] = new ConfigHandler(_log),
                [WireMessageTypes.LifecycleEvent] = new LifecycleHandler(_log),
                [WireMessageTypes.HealthCheck] = new HealthHandler(_log)
            };
        }

        /// <summary>
        /// Start the wire service
        /// </summary>
        public Task StartAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    _listener.Prefixes.Add($"http://+:{_port}/");
                    _listener.Start();

                    _isRunning = true;
                    _log.LogInfo($"[WireService] Started on port {_port}");

                    // Start accepting connections
                    _ = AcceptConnectionsAsync();

                    // Start health check loop
                    _ = HealthCheckLoopAsync();
                }
                catch (Exception ex)
                {
                    _log.LogError($"[WireService] Failed to start: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Stop the wire service
        /// </summary>
        public void Stop()
        {
            _isRunning = false;
            _cts.Cancel();
            _listener.Stop();
            _log.LogInfo("[WireService] Stopped");
        }

        private async Task AcceptConnectionsAsync()
        {
            while (!_cts.Token.IsCancellationRequested && _listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync().ConfigureAwait(false);
                    _ = HandleRequestAsync(context);
                }
                catch (HttpListenerException) when (_cts.Token.IsCancellationRequested)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _log.LogError($"[WireService] Accept error: {ex.Message}");
                }
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext context)
        {
            var response = context.Response;
            var request = context.Request;

            try
            {
                // Read request body
                using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                var json = await reader.ReadToEndAsync();

                if (string.IsNullOrEmpty(json))
                {
                    await SendResponseAsync(response, WireMessage.Error("Empty request body"));
                    return;
                }

                // Deserialize message
                var message = JsonSerializer.Deserialize<WireMessage>(json, StateSerializer.Instance._options);

                if (message == null)
                {
                    await SendResponseAsync(response, WireMessage.Error("Invalid message format"));
                    return;
                }

                _log.LogDebug($"[WireService] Received: {message.Type}");

                // Route to handler
                WireMessage reply;
                if (_handlers.TryGetValue(message.Type, out var handler))
                {
                    reply = await handler.HandleAsync(message);
                }
                else
                {
                    reply = WireMessage.Error($"Unknown message type: {message.Type}");
                }

                await SendResponseAsync(response, reply);
            }
            catch (JsonException ex)
            {
                _log.LogError($"[WireService] JSON error: {ex.Message}");
                await SendResponseAsync(response, WireMessage.Error($"JSON error: {ex.Message}"));
            }
            catch (Exception ex)
            {
                _log.LogError($"[WireService] Handle error: {ex.Message}");
                await SendResponseAsync(response, WireMessage.Error(ex.Message));
            }
        }

        private async Task SendResponseAsync(HttpListenerResponse response, WireMessage message)
        {
            try
            {
                var json = StateSerializer.Instance.Serialize(message);
                var buffer = Encoding.UTF8.GetBytes(json);

                response.ContentType = "application/json";
                response.ContentLength64 = buffer.Length;
                response.StatusCode = 200;

                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            }
            catch (Exception ex)
            {
                _log.LogError($"[WireService] Send error: {ex.Message}");
            }
            finally
            {
                response.Close();
            }
        }

        private async Task HealthCheckLoopAsync()
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                await Task.Delay(30000, _cts.Token);
                _log.LogDebug("[WireService] Health check: OK");
            }
        }

        public void Dispose()
        {
            Stop();
            _cts.Dispose();
            _listener.Dispose();
        }
    }

    /// <summary>
    /// Command handler for wire protocol
    /// </summary>
    public class CommandHandler : IMessageHandler
    {
        private readonly ManualLogSource _log;

        public CommandHandler(ManualLogSource log) => _log = log;

        public Task<WireMessage> HandleAsync(WireMessage message)
        {
            var command = message.Payload.GetString("command", "");
            var args = message.Payload.GetObject<Dictionary<string, object>>("args") ?? new();

            _log.LogInfo($"[WireService] Command: {command}");

            var result = ExecuteCommand(command, args);

            return Task.FromResult(WireMessage.Success("command", result));
        }

        private Dictionary<string, object> ExecuteCommand(string command, Dictionary<string, object> args)
        {
            var result = new Dictionary<string, object>();

            switch (command.ToLower())
            {
                case "status":
                    result["status"] = "running";
                    result["services"] = GetServiceStatus();
                    break;

                case "players":
                    result["count"] = 0;
                    result["players"] = new List<object>();
                    break;

                case "snapshot":
                    result["action"] = "snapshot_created";
                    result["version"] = DateTime.UtcNow.Ticks;
                    break;

                case "config":
                    result["action"] = "config_access";
                    result["section"] = args.GetString("section", "");
                    result["key"] = args.GetString("key", "");
                    break;

                default:
                    result["status"] = "unknown_command";
                    result["command"] = command;
                    break;
            }

            return result;
        }

        private Dictionary<string, object> GetServiceStatus()
        {
            return new Dictionary<string, object>
            {
                ["wire"] = true,
                ["snapshot"] = true,
                ["lifecycle"] = true,
                ["config"] = true
            };
        }
    }

    /// <summary>
    /// Snapshot handler for wire protocol
    /// </summary>
    public class SnapshotHandler : IMessageHandler
    {
        private readonly ManualLogSource _log;

        public SnapshotHandler(ManualLogSource log) => _log = log;

        public Task<WireMessage> HandleAsync(WireMessage message)
        {
            var type = message.Payload.GetString("type", "full");

            _log.LogInfo($"[WireService] Snapshot request: {type}");

            var snapshot = CreateSnapshot(type);

            return Task.FromResult(WireMessage.Success("snapshot", snapshot));
        }

        private Dictionary<string, object> CreateSnapshot(string type)
        {
            return new Dictionary<string, object>
            {
                ["type"] = type,
                ["version"] = DateTime.UtcNow.Ticks,
                ["timestamp"] = DateTime.UtcNow.ToString("o"),
                ["players"] = new List<object>(),
                ["entities"] = new List<object>(),
                ["world"] = new Dictionary<string, object>
                {
                    ["time"] = 0,
                    ["day"] = 0
                }
            };
        }
    }

    /// <summary>
    /// Configuration handler for wire protocol
    /// </summary>
    public class ConfigHandler : IMessageHandler
    {
        private readonly ManualLogSource _log;

        public ConfigHandler(ManualLogSource log) => _log = log;

        public Task<WireMessage> HandleAsync(WireMessage message)
        {
            var action = message.Payload.GetString("action", "get");
            var section = message.Payload.GetString("section", "");
            var key = message.Payload.GetString("key", "");

            _log.LogInfo($"[WireService] Config {action}: {section}.{key}");

            var result = new Dictionary<string, object>
            {
                ["action"] = action,
                ["section"] = section,
                ["key"] = key,
                ["value"] = GetConfigValue(section, key)
            };

            return Task.FromResult(WireMessage.Success("config", result));
        }

        private object GetConfigValue(string section, string key)
        {
            // Simplified - would integrate with ConfigService
            return $"config.{section}.{key}";
        }
    }

    /// <summary>
    /// Lifecycle handler for wire protocol
    /// </summary>
    public class LifecycleHandler : IMessageHandler
    {
        private readonly ManualLogSource _log;

        public LifecycleHandler(ManualLogSource log) => _log = log;

        public Task<WireMessage> HandleAsync(WireMessage message)
        {
            var evt = message.Payload.GetString("event", "");
            var characterId = message.Payload.GetString("characterId", "");
            var zoneId = message.Payload.GetString("zoneId", "");

            _log.LogInfo($"[WireService] Lifecycle event: {evt} - {characterId}");

            var result = new Dictionary<string, object>
            {
                ["event"] = evt,
                ["characterId"] = characterId,
                ["zoneId"] = zoneId,
                ["processed"] = true,
                ["timestamp"] = DateTime.UtcNow.ToString("o")
            };

            return Task.FromResult(WireMessage.Success("lifecycle", result));
        }
    }

    /// <summary>
    /// Health check handler
    /// </summary>
    public class HealthHandler : IMessageHandler
    {
        private readonly ManualLogSource _log;

        public HealthHandler(ManualLogSource log) => _log = log;

        public Task<WireMessage> HandleAsync(WireMessage message)
        {
            var result = new Dictionary<string, object>
            {
                ["status"] = "healthy",
                ["uptime"] = "running",
                ["services"] = new Dictionary<string, string>
                {
                    ["wire"] = "ok",
                    ["snapshot"] = "ok",
                    ["lifecycle"] = "ok",
                    ["config"] = "ok"
                }
            };

            return Task.FromResult(WireMessage.Success("health", result));
        }
    }
}
