using System;
using System.Collections.Generic;

namespace VAuto.Zone.Services
{
    public static class GlowTileGeometry
    {
        public const float MinSpacing = 0.5f;

        public static List<(float x, float y, float z)> GeneratePoints(
            float centerX,
            float centerY,
            float centerZ,
            float radius,
            float spacing,
            float rotationDegrees)
        {
            var effectiveSpacing = Math.Max(MinSpacing, spacing);
            var effectiveRadius = Math.Max(0.5f, radius);
            var circumference = 2f * MathF.PI * effectiveRadius;
            var pointCount = Math.Max(1, (int)(circumference / effectiveSpacing));
            var rotationOffset = rotationDegrees * (float)Math.PI / 180f;
            var points = new List<(float x, float y, float z)>(pointCount);

            for (var i = 0; i < pointCount; i++)
            {
                var angle = rotationOffset + (i / (float)pointCount) * (2f * MathF.PI);
                var x = centerX + MathF.Cos(angle) * effectiveRadius;
                var z = centerZ + MathF.Sin(angle) * effectiveRadius;
                points.Add((x, centerY, z));
            }

            return points;
        }
    }
}
