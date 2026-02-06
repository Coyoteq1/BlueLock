using System;
using System.Text.Json;
using Unity.Mathematics;
using VAuto.Core;
using VAuto.Extensions;

namespace VAuto.Examples
{
    /// <summary>
    /// Usage examples for JSON3Converter, JSON2Converter, and AASDJsonConverter
    /// Demonstrates server-optimized JSON serialization for VAutomationEvents
    /// </summary>
    public static class JsonConverterExamples
    {
        /// <summary>
        /// Example usage of JSON3Converter for 3D positions
        /// </summary>
        public static void Example3DConverter()
        {
            // Create a 3D position
            var position = new float3(100.5f, 50.25f, -200.75f);
            
            // Serialize using server options (full precision)
            var serverJson = JsonConverterRegistry.SerializeToServer(position);
            // Result: [100.5,50.25,-200.75]
            
            // Serialize using compressed options (network optimized)
            var compressedJson = JsonConverterRegistry.SerializeCompressed(position);
            // Result: "100.5,50.25,-200.75"
            
            // Deserialize back to float3
            var deserializedPosition = JsonConverterRegistry.DeserializeFromServer<float3>(serverJson);
            
            Plugin.Log?.LogInfo($"[JSON] 3D Example: {position} -> {serverJson} -> {deserializedPosition}");
        }

        /// <summary>
        /// Example usage of JSON2Converter for 2D coordinates
        /// </summary>
        public static void Example2DConverter()
        {
            // Create a 2D coordinate
            var coordinate = new float2(25.5f, 75.25f);
            
            // Different input formats that JSON2Converter can read:
            var arrayFormat = "[25.5,75.25]";
            var objectFormat = "{\"x\":25.5,\"y\":75.25}";
            var compressedFormat = "25.5,75.25";
            var namedFormat = "{\"width\":25.5,\"height\":75.25}";
            
            // All these will deserialize to the same float2
            var fromArray = JsonConverterRegistry.DeserializeFromClient<float2>(arrayFormat);
            var fromObject = JsonConverterRegistry.DeserializeFromClient<float2>(objectFormat);
            var fromCompressed = JsonConverterRegistry.DeserializeFromClient<float2>(compressedFormat);
            var fromNamed = JsonConverterRegistry.DeserializeFromClient<float2>(namedFormat);
            
            Plugin.Log?.LogInfo($"[JSON] 2D Example: {coordinate} -> Multiple formats supported");
        }

        /// <summary>
        /// Example usage of AASDJsonConverter for mixed data types
        /// </summary>
        public static void ExampleAASDConverter()
        {
            // Create mixed data
            var data = new
            {
                playerId = 12345,
                playerName = "TestPlayer",
                position = new float3(10, 20, 30),
                health = 95.5f,
                isAdmin = true,
                inventory = new object[] { "sword", 100, "potion", 50 }
            };
            
            // Serialize to different formats
            var serverJson = JsonConverterRegistry.SerializeToServer(data);
            var clientJson = JsonConverterRegistry.SerializeToClient(data);
            var compressedJson = JsonConverterRegistry.SerializeCompressed(data);
            
            Plugin.Log?.LogInfo($"[JSON] AASD Server: {serverJson}");
            Plugin.Log?.LogInfo($"[JSON] AASD Client: {clientJson}");
            Plugin.Log?.LogInfo($"[JSON] AASD Compressed: {compressedJson}");
        }

        /// <summary>
        /// Example of custom converter options
        /// </summary>
        public static void ExampleCustomOptions()
        {
            var position = new float3(1.23456789f, 2.3456789f, 3.456789f);
            
            // Custom options with high precision
            var highPrecisionOptions = JsonConverterRegistry.GetCustomOptions(false, 8, true);
            var highPrecisionJson = JsonSerializer.Serialize(position, highPrecisionOptions);
            
            // Custom options with low precision and compression
            var lowPrecisionOptions = JsonConverterRegistry.GetCustomOptions(true, 2, false);
            var lowPrecisionJson = JsonSerializer.Serialize(position, lowPrecisionOptions);
            
            Plugin.Log?.LogInfo($"[JSON] High Precision: {highPrecisionJson}");
            Plugin.Log?.LogInfo($"[JSON] Low Precision: {lowPrecisionJson}");
        }

        /// <summary>
        /// Example of handling complex nested objects
        /// </summary>
        public static void ExampleNestedObjects()
        {
            var arenaData = new
            {
                name = "MainArena",
                center = new float3(-1000, 5, -500),
                radius = 50.0f,
                spawnPoints = new[]
                {
                    new float3(-950, 5, -450),
                    new float3(-1050, 5, -550),
                    new float3(-1000, 5, -600)
                },
                config = new
                {
                    allowBuilding = true,
                    allowCrafting = false,
                    maxPlayers = 10,
                    gameMode = "pvp"
                },
                metadata = new
                {
                    created = DateTime.UtcNow,
                    version = "1.0.0",
                    tags = new[] { "arena", "pvp", "competitive" }
                }
            };
            
            // Serialize the complex object
            var json = JsonConverterRegistry.SerializeToServer(arenaData);
            
            // Deserialize it back
            var deserialized = JsonConverterRegistry.DeserializeFromServer<object>(json);
            
            Plugin.Log?.LogInfo($"[JSON] Nested Object: {json}");
        }

        /// <summary>
        /// Example of error handling and validation
        /// </summary>
        public static void ExampleErrorHandling()
        {
            try
            {
                // Test invalid JSON
                var invalidJson = "[invalid,float3,data]";
                var result = JsonConverterRegistry.DeserializeFromServer<float3>(invalidJson);
            }
            catch (JsonException ex)
            {
                Plugin.Log?.LogInfo($"[JSON] Expected error caught: {ex.Message}");
            }
            
            try
            {
                // Test string length validation in strict mode
                var strictOptions = JsonConverterRegistry.GetCustomOptions(false, 3, true);
                var longString = new string('a', 3000);
                var json = JsonSerializer.Serialize(new { data = longString }, strictOptions);
            }
            catch (JsonException ex)
            {
                Plugin.Log?.LogInfo($"[JSON] String length validation: {ex.Message}");
            }
        }

        /// <summary>
        /// Run all examples
        /// </summary>
        public static void RunAllExamples()
        {
            Plugin.Log?.LogInfo("[JSON] === Running JSON Converter Examples ===");
            
            Example3DConverter();
            Example2DConverter();
            ExampleAASDConverter();
            ExampleCustomOptions();
            ExampleNestedObjects();
            ExampleErrorHandling();
            
            Plugin.Log?.LogInfo("[JSON] === JSON Converter Examples Complete ===");
        }
    }
}
