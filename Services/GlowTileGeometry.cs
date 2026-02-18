using System;
using System.Collections.Generic;
using Unity.Mathematics;
using VAuto.Zone.Models;

namespace VAuto.Zone.Services
{
    public static class GlowTileGeometry
    {
        public const float MinSpacing = 0.5f;

        // Backward-compatible helper kept for tests/legacy callers.
        public static List<float3> GeneratePoints(float centerX, float centerY, float centerZ, float radius, float spacing, float rotationDegrees)
        {
            var points = new List<float3>();
            if (radius <= 0f || spacing <= 0f)
            {
                return points;
            }

            var circumference = 2f * MathF.PI * radius;
            var stepCount = Math.Max(1, (int)(circumference / spacing));
            var rotationRad = rotationDegrees * (MathF.PI / 180f);

            for (var i = 0; i < stepCount; i++)
            {
                var angle = rotationRad + (i * (2f * MathF.PI / stepCount));
                var x = centerX + radius * MathF.Cos(angle);
                var z = centerZ + radius * MathF.Sin(angle);
                points.Add(new float3(x, centerY, z));
            }

            return points;
        }

        public static List<float2> GetZoneBorderPoints(ZoneDefinition zone, float spacing)
        {
            var points = new List<float2>();
            if (zone == null || spacing <= 0)
            {
                return points;
            }

            spacing = Math.Max(0.5f, spacing);

            if (zone.Shape != null &&
                zone.Shape.Equals("Rectangle", StringComparison.OrdinalIgnoreCase) &&
                zone.MaxX > zone.MinX &&
                zone.MaxZ > zone.MinZ)
            {
                return GetRectangleBorderPoints(zone.MinX, zone.MaxX, zone.MinZ, zone.MaxZ, spacing);
            }

            var radius = Math.Max(1f, zone.Radius);
            var center = new float2(zone.CenterX, zone.CenterZ);
            var circumference = 2 * Math.PI * radius;
            var stepCount = Math.Max(8, (int)(circumference / spacing));
            if (stepCount <= 0)
            {
                stepCount = 8;
            }

            for (var i = 0; i < stepCount; i++)
            {
                var angle = i * (2 * Math.PI / stepCount);
                var x = center.x + radius * (float)Math.Cos(angle);
                var z = center.y + radius * (float)Math.Sin(angle);
                points.Add(new float2(x, z));
            }

            return points;
        }

        private static List<float2> GetRectangleBorderPoints(float minX, float maxX, float minZ, float maxZ, float spacing)
        {
            var points = new List<float2>();
            var width = maxX - minX;
            var depth = maxZ - minZ;
            if (width <= 0 || depth <= 0)
            {
                return points;
            }

            void AddEdge(float2 start, float2 direction, float length)
            {
                var steps = Math.Max(1, (int)(length / spacing));
                for (var i = 0; i <= steps; i++)
                {
                    var factor = i / (float)steps;
                    points.Add(start + direction * factor * length);
                }
            }

            var bottomLeft = new float2(minX, minZ);
            var bottomRight = new float2(maxX, minZ);
            var topRight = new float2(maxX, maxZ);
            var topLeft = new float2(minX, maxZ);

            AddEdge(bottomLeft, new float2(1f, 0f), width);
            AddEdge(bottomRight, new float2(0f, 1f), depth);
            AddEdge(topRight, new float2(-1f, 0f), width);
            AddEdge(topLeft, new float2(0f, -1f), depth);

            return points;
        }
    }
}
