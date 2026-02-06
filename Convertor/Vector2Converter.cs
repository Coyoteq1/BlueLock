using System;
using System.Globalization;
using System.Numerics;

namespace VAuto.Extensions
{
    /// <summary>
    /// Vector2 converter for JSON serialization
    /// </summary>
    public class Vector2Converter
    {
        public static Vector2 Parse(string value)
        {
            if (string.IsNullOrEmpty(value))
                return Vector2.Zero;

            var parts = value.Split(',');
            if (parts.Length == 2)
            {
                if (float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                    float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
                {
                    return new Vector2(x, y);
                }
            }

            return Vector2.Zero;
        }

        public static string ToString(Vector2 vector)
        {
            return $"{vector.X.ToString(CultureInfo.InvariantCulture)},{vector.Y.ToString(CultureInfo.InvariantCulture)}";
        }
    }
}
