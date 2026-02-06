using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace VAuto.EndGameKit.Requests
{
    /// <summary>
    /// Request to apply an end-game kit to a player
    /// </summary>
    public struct ApplyKitRequest
    {
        public Entity Player;
        public FixedString64Bytes KitName;
        public Entity Requester; // For tracking responses
    }

    /// <summary>
    /// Request to remove an end-game kit from a player
    /// </summary>
    public struct RemoveKitRequest
    {
        public Entity Player;
        public Entity Requester; // For tracking responses
    }

    /// <summary>
    /// Request to get kit profile information
    /// </summary>
    public struct GetKitProfilesRequest
    {
        public Entity Requester; // For tracking responses
    }

    /// <summary>
    /// Response for kit operations
    /// </summary>
    public struct KitResponse
    {
        public bool Success;
        public FixedString512Bytes Error;
        public FixedString512Bytes Data; // For profile names, etc.
    }

    /// <summary>
    /// Response for kit profile requests
    /// </summary>
    public struct KitProfilesResponse
    {
        public BlobArray<FixedString64Bytes> ProfileNames;
    }
}
