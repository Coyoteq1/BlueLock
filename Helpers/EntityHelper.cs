using System;
using Unity.Collections;
using Unity.Entities;
using Il2CppInterop.Runtime;
using ProjectM;
using ProjectM.Network;

namespace VAuto.Core
{
    /// <summary>
    /// Helper methods for direct entity array retrieval with V Rising ECS patterns
    /// </summary>
    internal static class EntityHelper
    {
        // NOTE: GetEntitiesByComponentType/Types moved to ECSExtensions for proper EntityManager injection pattern.
        // See Extensions/ECSExtensions.cs for the preferred implementation.

        /// <summary>
        /// Send a system message to a client user
        /// </summary>
        /// <param name="entityManager">Entity manager instance</param>
        /// <param name="user">User entity to send message to</param>
        /// <param name="message">Message content</param>
        public static void SendSystemMessageToClient(EntityManager entityManager, User user, string message)
        {
            var msg = new FixedString512Bytes(message);
            ServerChatUtils.SendSystemMessageToClient(entityManager, user, ref msg);
        }
    }
}