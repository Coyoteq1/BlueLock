using System;
using System.Collections.Generic;
using Unity.Mathematics;
using VAuto.Zone.Models;

namespace VAuto.Zone.Services
{
    public static class GlowTileGeometry
    {
        public const float MinSpacing = 0.5f;
        private const float BLOCK_SIZE = 1f;

        public enum BorderCornerType
        {
            Straight = 0,
            OutsideCorner = 1,
            InsideCorner = 2,
            EndCap = 3
        }

        public readonly struct BorderNode
        {
            public BorderNode(float2 position, int2 gridKey, float2 previousDirection, float2 nextDirection, byte neighborMask, float signedTurn, BorderCornerType cornerType, float rotationDegrees)
            {
                Position = position;
                GridKey = gridKey;
                PreviousDirection = previousDirection;
                NextDirection = nextDirection;
                NeighborMask = neighborMask;
                SignedTurn = signedTurn;
                CornerType = cornerType;
                RotationDegrees = rotationDegrees;
            }

            public float2 Position { get; }
            public int2 GridKey { get; }
            public float2 PreviousDirection { get; }
            public float2 NextDirection { get; }
            public byte NeighborMask { get; }
            public float SignedTurn { get; }
            public BorderCornerType CornerType { get; }
            public float RotationDegrees { get; }
        }

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
            var nodes = GetZoneBorderNodes(zone, spacing);
            var points = new List<float2>();
            foreach (var node in nodes)
            {
                points.Add(node.Position);
            }

            return points;
        }

        public static List<BorderNode> GetZoneBorderNodes(ZoneDefinition zone, float spacing)
        {
            var points = new List<float2>();
            var nodes = new List<BorderNode>();
            if (zone == null || spacing <= 0)
            {
                return nodes;
            }

            spacing = Math.Max(MinSpacing, spacing);

            if (TryGetRectangleBounds(zone, out var minX, out var maxX, out var minZ, out var maxZ))
            {
                points = GetRectangleBorderPoints(minX, maxX, minZ, maxZ, spacing);
            }
            else
            {
                points = GetCircleBorderPoints(zone, spacing);
            }

            return BuildNodes(points, spacing);
        }

        private static List<BorderNode> BuildNodes(List<float2> points, float spacing)
        {
            var nodes = new List<BorderNode>();
            if (points == null || points.Count == 0)
            {
                return nodes;
            }

            var keyToIndex = new Dictionary<long, int>(points.Count);
            var quantized = new int2[points.Count];
            for (var i = 0; i < points.Count; i++)
            {
                var key = Quantize(points[i], spacing);
                quantized[i] = key;
                keyToIndex[EncodeKey(key)] = i;
            }

            for (var i = 0; i < points.Count; i++)
            {
                var prev = points[(i - 1 + points.Count) % points.Count];
                var current = points[i];
                var next = points[(i + 1) % points.Count];

                var prevDir = NormalizeSafe(current - prev);
                var nextDir = NormalizeSafe(next - current);
                var signedTurn = prevDir.x * nextDir.y - prevDir.y * nextDir.x;
                var mask = BuildNeighborMask(quantized[i], keyToIndex);
                var corner = ClassifyCorner(mask, signedTurn, prevDir, nextDir);
                var rotation = GetCanonicalRotationDegrees(prevDir, nextDir, corner);

                nodes.Add(new BorderNode(current, quantized[i], prevDir, nextDir, mask, signedTurn, corner, rotation));
            }

            return nodes;
        }

        private static BorderCornerType ClassifyCorner(byte mask, float signedTurn, float2 prevDir, float2 nextDir)
        {
            var cardinalCount = CardinalNeighborCount(mask);
            if (cardinalCount <= 1)
            {
                return BorderCornerType.EndCap;
            }

            var isStraight = Math.Abs(signedTurn) < 0.05f || math.lengthsq(prevDir + nextDir) < 0.01f;
            if (isStraight)
            {
                return BorderCornerType.Straight;
            }

            var hasOrthogonalTurn = Math.Abs(math.dot(prevDir, nextDir)) < 0.25f;
            if (!hasOrthogonalTurn)
            {
                return BorderCornerType.Straight;
            }

            var turnInward = signedTurn > 0f;
            return turnInward ? BorderCornerType.InsideCorner : BorderCornerType.OutsideCorner;
        }

        private static int CardinalNeighborCount(byte mask)
        {
            var count = 0;
            if ((mask & 1) != 0) count++;   // East
            if ((mask & 4) != 0) count++;   // North
            if ((mask & 16) != 0) count++;  // West
            if ((mask & 64) != 0) count++;  // South
            return count;
        }

        private static float GetCanonicalRotationDegrees(float2 prevDir, float2 nextDir, BorderCornerType corner)
        {
            var basis = corner == BorderCornerType.Straight || corner == BorderCornerType.EndCap
                ? NormalizeSafe(prevDir + nextDir)
                : NormalizeSafe(nextDir);

            if (math.lengthsq(basis) < 0.001f)
            {
                basis = new float2(1f, 0f);
            }

            var raw = MathF.Atan2(basis.y, basis.x) * (180f / MathF.PI);
            return QuantizeAngle90(raw);
        }

        private static float QuantizeAngle90(float angleDegrees)
        {
            var normalized = angleDegrees % 360f;
            if (normalized < 0f)
            {
                normalized += 360f;
            }

            return MathF.Round(normalized / 90f) * 90f;
        }

        private static byte BuildNeighborMask(int2 center, Dictionary<long, int> keyToIndex)
        {
            var offsets = new[]
            {
                new int2(1, 0),   // E bit 0
                new int2(1, 1),   // NE bit 1
                new int2(0, 1),   // N bit 2
                new int2(-1, 1),  // NW bit 3
                new int2(-1, 0),  // W bit 4
                new int2(-1, -1), // SW bit 5
                new int2(0, -1),  // S bit 6
                new int2(1, -1)   // SE bit 7
            };

            byte mask = 0;
            for (var i = 0; i < offsets.Length; i++)
            {
                var k = center + offsets[i];
                if (keyToIndex.ContainsKey(EncodeKey(k)))
                {
                    mask |= (byte)(1 << i);
                }
            }

            return mask;
        }

        private static float2 NormalizeSafe(float2 value)
        {
            var lenSq = math.lengthsq(value);
            if (lenSq <= 0.000001f)
            {
                return float2.zero;
            }

            return value / math.sqrt(lenSq);
        }

        private static int2 Quantize(float2 point, float spacing)
        {
            _ = spacing;
            return new int2(
                (int)math.floor(point.x / BLOCK_SIZE),
                (int)math.floor(point.y / BLOCK_SIZE));
        }

        private static long EncodeKey(int2 key)
        {
            return ((long)key.x << 32) ^ (uint)key.y;
        }

        private static List<float2> GetCircleBorderPoints(ZoneDefinition zone, float spacing)
        {
            var points = new List<float2>();

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

        private static bool TryGetRectangleBounds(ZoneDefinition zone, out float minX, out float maxX, out float minZ, out float maxZ)
        {
            minX = maxX = minZ = maxZ = 0f;
            var shape = (zone.Shape ?? string.Empty).Trim();
            var isRectLike =
                shape.Equals("Rectangle", StringComparison.OrdinalIgnoreCase) ||
                shape.Equals("Rect", StringComparison.OrdinalIgnoreCase) ||
                shape.Equals("Square", StringComparison.OrdinalIgnoreCase) ||
                shape.Equals("Box", StringComparison.OrdinalIgnoreCase);

            if (!isRectLike)
            {
                return false;
            }

            var hasExplicitBounds = !(Math.Abs(zone.MinX) < 0.001f &&
                                      Math.Abs(zone.MaxX) < 0.001f &&
                                      Math.Abs(zone.MinZ) < 0.001f &&
                                      Math.Abs(zone.MaxZ) < 0.001f);

            if (hasExplicitBounds)
            {
                minX = Math.Min(zone.MinX, zone.MaxX);
                maxX = Math.Max(zone.MinX, zone.MaxX);
                minZ = Math.Min(zone.MinZ, zone.MaxZ);
                maxZ = Math.Max(zone.MinZ, zone.MaxZ);
            }
            else
            {
                var half = Math.Max(1f, zone.Radius);
                minX = zone.CenterX - half;
                maxX = zone.CenterX + half;
                minZ = zone.CenterZ - half;
                maxZ = zone.CenterZ + half;
            }

            return maxX > minX && maxZ > minZ;
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

            void AddEdge(float2 start, float2 direction, float length, bool includeLast)
            {
                var steps = Math.Max(1, (int)(length / spacing));
                var maxStep = includeLast ? steps : steps - 1;
                for (var i = 0; i <= maxStep; i++)
                {
                    var factor = i / (float)steps;
                    points.Add(start + direction * factor * length);
                }
            }

            var bottomLeft = new float2(minX, minZ);
            var bottomRight = new float2(maxX, minZ);
            var topRight = new float2(maxX, maxZ);
            var topLeft = new float2(minX, maxZ);

            AddEdge(bottomLeft, new float2(1f, 0f), width, includeLast: false);
            AddEdge(bottomRight, new float2(0f, 1f), depth, includeLast: false);
            AddEdge(topRight, new float2(-1f, 0f), width, includeLast: false);
            AddEdge(topLeft, new float2(0f, -1f), depth, includeLast: false);

            return points;
        }
    }
}
