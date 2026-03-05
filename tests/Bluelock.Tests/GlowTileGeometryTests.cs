using System;
using System.Linq;
using System.Runtime.InteropServices;
using VAutomationCore.Services;
using Xunit;

namespace VAuto.Zone.Tests
{
    public sealed class RequiresGameAssemblyFactAttribute : FactAttribute
    {
        public RequiresGameAssemblyFactAttribute()
        {
            if (!NativeLibrary.TryLoad("GameAssembly", out var handle))
            {
                Skip = "Requires GameAssembly/Il2Cpp runtime.";
                return;
            }

            NativeLibrary.Free(handle);
        }
    }

    public class GlowTileGeometryTests
    {
        [RequiresGameAssemblyFact]
        public void Generate_ReturnsExpectedCount()
        {
            var radius = 10f;
            var spacing = 5f;
            var positions = GlowTileGeometry.GeneratePoints(0f, 0f, 0f, radius, spacing, 0f);
            var circumference = 2f * MathF.PI * radius;
            var expected = Math.Max(1, (int)(circumference / spacing));
            Assert.Equal(expected, positions.Count);
        }

        [RequiresGameAssemblyFact]
        public void Generate_RotationOffsetsPositions()
        {
            var radius = 10f;
            var spacing = 5f;
            var basePoint = GlowTileGeometry.GeneratePoints(0f, 0f, 0f, radius, spacing, 0f)[0];
            var rotatedPoint = GlowTileGeometry.GeneratePoints(0f, 0f, 0f, radius, spacing, 90f)[0];
            Assert.NotEqual(basePoint.x, rotatedPoint.x);
            Assert.NotEqual(basePoint.z, rotatedPoint.z);
        }

        [RequiresGameAssemblyFact]
        public void Generate_PointsApproximatelySpacing()
        {
            var radius = 5f;
            var spacing = 2f;
            var positions = GlowTileGeometry.GeneratePoints(100f, 50f, -25f, radius, spacing, 0f);
            for (var i = 1; i < positions.Count; i++)
            {
                var previous = positions[i - 1];
                var current = positions[i];
                var dx = current.x - previous.x;
                var dz = current.z - previous.z;
                var distance = MathF.Sqrt(dx * dx + dz * dz);
                Assert.InRange(distance, spacing - 0.25f, spacing + 0.25f);
            }
        }
    }
}
