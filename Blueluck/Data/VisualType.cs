using System;
using Unity.Mathematics;

namespace Blueluck.Data
{
    /// <summary>
    /// Visual type definition for zone visual effects
    /// Supports various visual effect types and configurations
    /// </summary>
    [Serializable]
    public struct VisualType
    {
        /// <summary>
        /// Unique identifier for the visual type
        /// </summary>
        public string Id { get; set; }
        
        /// <summary>
        /// Display name for the visual type
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// Description of what this visual type represents
        /// </summary>
        public string Description { get; set; }
        
        /// <summary>
        /// Type of visual effect
        /// </summary>
        public VisualEffectType EffectType { get; set; }
        
        /// <summary>
        /// Primary color of the visual effect (RGB values 0-1)
        /// </summary>
        public float3 Color { get; set; }
        
        /// <summary>
        /// Intensity/brightness of the visual effect (0-1)
        /// </summary>
        public float Intensity { get; set; }
        
        /// <summary>
        /// Size/radius of the visual effect in world units
        /// </summary>
        public float Size { get; set; }
        
        /// <summary>
        /// Duration of the visual effect in seconds (0 = permanent)
        /// </summary>
        public float Duration { get; set; }
        
        /// <summary>
        /// Number of points to use for border effects
        /// </summary>
        public int PointCount { get; set; }
        
        /// <summary>
        /// Whether the visual effect should pulse/animate
        /// </summary>
        public bool IsAnimated { get; set; }
        
        /// <summary>
        /// Animation speed (if animated)
        /// </summary>
        public float AnimationSpeed { get; set; }
        
        /// <summary>
        /// Whether this visual type is enabled
        /// </summary>
        public bool IsEnabled { get; set; }
        
        /// <summary>
        /// Additional metadata or configuration
        /// </summary>
        public string Metadata { get; set; }
        
        /// <summary>
        /// Creates a default visual type
        /// </summary>
        public static VisualType Default => new VisualType
        {
            Id = "default",
            Name = "Default Visual",
            Description = "Default zone visual effect",
            EffectType = VisualEffectType.Border,
            Color = new float3(0f, 1f, 1f), // Cyan
            Intensity = 1f,
            Size = 10f,
            Duration = 0f, // Permanent
            PointCount = 16,
            IsAnimated = false,
            AnimationSpeed = 1f,
            IsEnabled = true,
            Metadata = ""
        };
        
        /// <summary>
        /// Creates a warning visual type
        /// </summary>
        public static VisualType Warning => new VisualType
        {
            Id = "warning",
            Name = "Warning Visual",
            Description = "Warning zone visual effect",
            EffectType = VisualEffectType.Border,
            Color = new float3(1f, 0.5f, 0f), // Orange
            Intensity = 1f,
            Size = 8f,
            Duration = 0f,
            PointCount = 12,
            IsAnimated = true,
            AnimationSpeed = 2f,
            IsEnabled = true,
            Metadata = ""
        };
        
        /// <summary>
        /// Creates a danger visual type
        /// </summary>
        public static VisualType Danger => new VisualType
        {
            Id = "danger",
            Name = "Danger Visual",
            Description = "Danger zone visual effect",
            EffectType = VisualEffectType.Border,
            Color = new float3(1f, 0f, 0f), // Red
            Intensity = 1f,
            Size = 6f,
            Duration = 0f,
            PointCount = 8,
            IsAnimated = true,
            AnimationSpeed = 3f,
            IsEnabled = true,
            Metadata = ""
        };
        
        /// <summary>
        /// Creates a safe zone visual type
        /// </summary>
        public static VisualType Safe => new VisualType
        {
            Id = "safe",
            Name = "Safe Zone Visual",
            Description = "Safe zone visual effect",
            EffectType = VisualEffectType.Border,
            Color = new float3(-1000f, 0f, -500f),
            Intensity = 0.8f,
            Size = 12f,
            Duration = 0f,
            PointCount = 20,
            IsAnimated = false,
            AnimationSpeed = 1f,
            IsEnabled = true,
            Metadata = ""
        };
        
        /// <summary>
        /// Creates a temporary visual type
        /// </summary>
        public static VisualType Temporary => new VisualType
        {
            Id = "temporary",
            Name = "Temporary Visual",
            Description = "Temporary zone visual effect",
            EffectType = VisualEffectType.Border,
            Color = new float3(1f, 1f, 0f), // Yellow
            Intensity = 0.6f,
            Size = 5f,
            Duration = 30f, // 30 seconds
            PointCount = 10,
            IsAnimated = true,
            AnimationSpeed = 1.5f,
            IsEnabled = true,
            Metadata = ""
        };
        
        /// <summary>
        /// Validates the visual type configuration
        /// </summary>
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(Id) &&
                   !string.IsNullOrWhiteSpace(Name) &&
                   Size > 0 &&
                   Intensity >= 0 && Intensity <= 1 &&
                   PointCount > 0 &&
                   AnimationSpeed > 0;
        }
        
        /// <summary>
        /// Creates a copy of this visual type with modified properties
        /// </summary>
        public VisualType WithColor(float3 color)
        {
            var copy = this;
            copy.Color = color;
            return copy;
        }
        
        /// <summary>
        /// Creates a copy of this visual type with modified size
        /// </summary>
        public VisualType WithSize(float size)
        {
            var copy = this;
            copy.Size = size;
            return copy;
        }
        
        /// <summary>
        /// Creates a copy of this visual type with modified duration
        /// </summary>
        public VisualType WithDuration(float duration)
        {
            var copy = this;
            copy.Duration = duration;
            return copy;
        }
        
        /// <summary>
        /// Creates a copy of this visual type with animation enabled/disabled
        /// </summary>
        public VisualType WithAnimation(bool animated, float speed = 1f)
        {
            var copy = this;
            copy.IsAnimated = animated;
            copy.AnimationSpeed = speed;
            return copy;
        }
        
        public override string ToString()
        {
            return $"VisualType[{Id}]: {Name} ({EffectType}, Color={Color}, Size={Size})";
        }
    }
    
    /// <summary>
    /// Types of visual effects available
    /// </summary>
    public enum VisualEffectType
    {
        /// <summary>
        /// Border outline effect
        /// </summary>
        Border,
        
        /// <summary>
        /// Filled area effect
        /// </summary>
        Fill,
        
        /// <summary>
        /// Point markers effect
        /// </summary>
        Points,
        
        /// <summary>
        /// Particle effect
        /// </summary>
        Particles,
        
        /// <summary>
        /// Light/glow effect
        /// </summary>
        Glow,
        
        /// <summary>
        /// Custom effect type
        /// </summary>
        Custom
    }
}

