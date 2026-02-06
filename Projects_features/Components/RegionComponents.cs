using Unity.Entities;
using Unity.Mathematics;

namespace VAuto.Core.Components
{
    /// <summary>
    /// Region boundary using axis-aligned bounding box.
    /// </summary>
    public struct RegionBoundsAABB : IComponentData
    {
        public int RegionId;
        public float3 Min;
        public float3 Max;
    }

    /// <summary>
    /// Tag for region configuration singleton.
    /// </summary>
    public struct RegionConfigTag : IComponentData { }

    /// <summary>
    /// Helper methods for region bounds checking.
    /// </summary>
    public static class RegionMath
    {
        public static bool Contains(in RegionBoundsAABB aabb, float3 p)
        {
            return p.x >= aabb.Min.x && p.y >= aabb.Min.y && p.z >= aabb.Min.z &&
                   p.x <= aabb.Max.x && p.y <= aabb.Max.y && p.z <= aabb.Max.z;
        }

        public static bool Intersects(in RegionBoundsAABB a, in RegionBoundsAABB b)
        {
            return a.Min.x <= b.Max.x && a.Max.x >= b.Min.x &&
                   a.Min.y <= b.Max.y && a.Max.y >= b.Min.y &&
                   a.Min.z <= b.Max.z && a.Max.z >= b.Min.z;
        }

        public static float3 Center(in RegionBoundsAABB aabb)
        {
            return (aabb.Min + aabb.Max) * 0.5f;
        }
    }
}
