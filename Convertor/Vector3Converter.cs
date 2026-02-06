using System;
using System.Globalization;
using System.Numerics;

namespace VAuto.Extensions
{
    /// <summary>
    /// Vector3 converter for JSON serialization
    /// </summary>
    public class Vector3Converter
    {
        public static Vector3 Parse(string value)
        {
            if (string.IsNullOrEmpty(value))
                return Vector3.Zero;

            var parts = value.Split(',');
            if (parts.Length == 3)
            {
                if (float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                    float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) &&
                    float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
                {
                    return new Vector3(x, y, z);
                }
            }

            return Vector3.Zero;
        }

        public static string ToString(Vector3 vector)
        {
            return $"{vector.X.ToString(CultureInfo.InvariantCulture)},{vector.Y.ToString(CultureInfo.InvariantCulture)},{vector.Z.ToString(CultureInfo.InvariantCulture)}";
        }
    }
}
