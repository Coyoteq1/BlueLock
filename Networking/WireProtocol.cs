using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VAuto.Core.Networking
{
    /// <summary>
    /// Base wire message structure for network communication
    /// </summary>
    public class WireMessage
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("payload")]
        public Dictionary<string, object> Payload { get; set; } = new();

        [JsonPropertyName("timestamp")]
        public long Timestamp { get; set; } = DateTime.UtcNow.Ticks;

        public static WireMessage Create(string type, object data)
        {
            return new WireMessage
            {
                Type = type,
                Id = Guid.NewGuid().ToString(),
                Payload = data.ToDictionary(),
                Timestamp = DateTime.UtcNow.Ticks
            };
        }

        public static WireMessage Error(string message)
        {
            return new WireMessage
            {
                Type = "error",
                Payload = new Dictionary<string, object> { ["message"] = message }
            };
        }

        public static WireMessage Success(string action, object? data = null)
        {
            var payload = new Dictionary<string, object> { ["action"] = action, ["success"] = true };
            if (data != null) payload["data"] = data;
            return new WireMessage { Type = "response", Payload = payload };
        }
    }

    /// <summary>
    /// Message types for wire protocol
    /// </summary>
    public static class WireMessageTypes
    {
        // Command messages
        public const string Command = "command";
        public const string CommandResult = "command:result";

        // Snapshot messages
        public const string SnapshotRequest = "snapshot:request";
        public const string SnapshotResponse = "snapshot:response";
        public const string SnapshotDelta = "snapshot:delta";

        // Configuration messages
        public const string ConfigGet = "config:get";
        public const string ConfigSet = "config:set";
        public const string ConfigResponse = "config:response";

        // Lifecycle messages
        public const string LifecycleEvent = "lifecycle:event";
        public const string LifecycleState = "lifecycle:state";

        // Player messages
        public const string PlayerJoin = "player:join";
        public const string PlayerLeave = "player:leave";
        public const string PlayerState = "player:state";

        // System messages
        public const string Ping = "system:ping";
        public const string Pong = "system:pong";
        public const string Error = "error";
        public const string HealthCheck = "health:check";
    }

    /// <summary>
    /// Message handler interface
    /// </summary>
    public interface IMessageHandler
    {
        Task<WireMessage> HandleAsync(WireMessage message);
    }

    /// <summary>
    /// Command payload structure
    /// </summary>
    public class CommandPayload
    {
        [JsonPropertyName("command")]
        public string Command { get; set; } = "";

        [JsonPropertyName("args")]
        public Dictionary<string, object> Args { get; set; } = new();

        [JsonPropertyName("user")]
        public string? User { get; set; }
    }

    /// <summary>
    /// Snapshot request payload
    /// </summary>
    public class SnapshotRequestPayload
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "full"; // full or delta

        [JsonPropertyName("components")]
        public List<string> Components { get; set; } = new();
    }

    /// <summary>
    /// Config request payload
    /// </summary>
    public class ConfigRequestPayload
    {
        [JsonPropertyName("action")]
        public string Action { get; set; } = "get"; // get, set, subscribe

        [JsonPropertyName("section")]
        public string Section { get; set; } = "";

        [JsonPropertyName("key")]
        public string Key { get; set; } = "";

        [JsonPropertyName("value")]
        public object? Value { get; set; }
    }

    /// <summary>
    /// Lifecycle event payload
    /// </summary>
    public class LifecycleEventPayload
    {
        [JsonPropertyName("event")]
        public string Event { get; set; } = ""; // join, leave, zone_enter, zone_exit

        [JsonPropertyName("characterId")]
        public string CharacterId { get; set; } = "";

        [JsonPropertyName("zoneId")]
        public string ZoneId { get; set; } = "";

        [JsonPropertyName("timestamp")]
        public long Timestamp { get; set; }
    }
}
