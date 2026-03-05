using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VAutomationCore.Core.Automation
{
    /// <summary>
    /// Represents a dynamic command rule that can be registered at runtime.
    /// </summary>
    public class AutomationRule
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("trigger")]
        public CommandTrigger Trigger { get; set; }

        [JsonPropertyName("action")]
        public IAction Action { get; set; }

        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("last_modified")]
        public DateTime LastModified { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Represents a command trigger for dynamic commands.
    /// </summary>
    public class CommandTrigger
    {
        [JsonPropertyName("command")]
        public string Command { get; set; }
    }

    /// <summary>
    /// Base interface for all actions in the automation system.
    /// </summary>
    [JsonConverter(typeof(ActionJsonConverter))]
    public interface IAction
    {
        string ActionType { get; }
    }

    /// <summary>
    /// Represents a sequence of commands to execute.
    /// </summary>
    public class SequenceAction : IAction
    {
        public string ActionType => "sequence";

        [JsonPropertyName("commands")]
        public List<string> Commands { get; set; } = new List<string>();
    }

    /// <summary>
    /// Represents a conditional action that executes based on a condition.
    /// </summary>
    public class ConditionalAction : IAction
    {
        public string ActionType => "conditional";

        [JsonPropertyName("condition")]
        public string Condition { get; set; }

        [JsonPropertyName("true_action")]
        public IAction TrueAction { get; set; }

        [JsonPropertyName("false_action")]
        public IAction FalseAction { get; set; }
    }

    /// <summary>
    /// Represents a delay action that waits for a specified duration.
    /// </summary>
    public class DelayAction : IAction
    {
        public string ActionType => "delay";

        [JsonPropertyName("duration_seconds")]
        public int DurationSeconds { get; set; }
    }

    /// <summary>
    /// Custom JSON converter for IAction interface to handle polymorphic serialization.
    /// </summary>
    public class ActionJsonConverter : JsonConverter<IAction>
    {
        public override IAction Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var jsonDoc = JsonDocument.ParseValue(ref reader);
            var root = jsonDoc.RootElement;
            
            if (!root.TryGetProperty("actionType", out var actionTypeElement))
                throw new JsonException("Missing 'actionType' property");

            var actionType = actionTypeElement.GetString();
            
            return actionType switch
            {
                "sequence" => JsonSerializer.Deserialize<SequenceAction>(root.GetRawText(), options),
                "conditional" => JsonSerializer.Deserialize<ConditionalAction>(root.GetRawText(), options),
                "delay" => JsonSerializer.Deserialize<DelayAction>(root.GetRawText(), options),
                _ => throw new JsonException($"Unknown action type: {actionType}")
            };
        }

        public override void Write(Utf8JsonWriter writer, IAction value, JsonSerializerOptions options)
        {
            switch (value)
            {
                case SequenceAction seq:
                    JsonSerializer.Serialize(writer, seq, options);
                    break;
                case ConditionalAction cond:
                    JsonSerializer.Serialize(writer, cond, options);
                    break;
                case DelayAction delay:
                    JsonSerializer.Serialize(writer, delay, options);
                    break;
                default:
                    throw new JsonException($"Unknown action type: {value.GetType().Name}");
            }
        }
    }
}