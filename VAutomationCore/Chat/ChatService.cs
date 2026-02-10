using System;
using ProjectM;
using ProjectM.Network;
using Unity.Collections;
using Unity.Entities;
using VAutomationCore.Core;

namespace VAuto.Core.Chat
{
    public static class ChatService
    {
        public static bool TryBroadcastSystemMessage(string message, out string error)
        {
            error = string.Empty;
            try
            {
                if (UnifiedCore.EntityManager == default)
                {
                    error = "Server EntityManager not ready";
                    return false;
                }

                var em = UnifiedCore.EntityManager;
                var query = em.CreateEntityQuery(ComponentType.ReadOnly<User>());
                using var users = query.ToEntityArray(Allocator.Temp);

                if (users.Length == 0)
                {
                    error = "No connected users";
                    return false;
                }

                var msg = new FixedString512Bytes(TrimForFixedString(message));
                for (int i = 0; i < users.Length; i++)
                {
                    var userEntity = users[i];
                    if (!em.Exists(userEntity) || !em.HasComponent<User>(userEntity))
                        continue;

                    var user = em.GetComponentData<User>(userEntity);
                    if (!user.IsConnected)
                        continue;

                    ServerChatUtils.SendSystemMessageToClient(em, user, ref msg);
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TrySendSystemMessage(ulong platformId, string message, out string error)
        {
            error = string.Empty;
            try
            {
                if (UnifiedCore.EntityManager == default)
                {
                    error = "Server EntityManager not ready";
                    return false;
                }

                var em = UnifiedCore.EntityManager;
                var query = em.CreateEntityQuery(ComponentType.ReadOnly<User>());
                using var users = query.ToEntityArray(Allocator.Temp);
                var msg = new FixedString512Bytes(TrimForFixedString(message));

                for (int i = 0; i < users.Length; i++)
                {
                    var userEntity = users[i];
                    if (!em.Exists(userEntity) || !em.HasComponent<User>(userEntity))
                        continue;

                    var user = em.GetComponentData<User>(userEntity);
                    if (!user.IsConnected || user.PlatformId != platformId)
                        continue;

                    ServerChatUtils.SendSystemMessageToClient(em, user, ref msg);
                    return true;
                }

                error = $"User not connected: {platformId}";
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static string TrimForFixedString(string message)
        {
            if (string.IsNullOrEmpty(message))
                return string.Empty;

            var trimmed = message.Length > 512 ? message[..512] : message;
            return trimmed.Replace("\n", " ").Replace("\r", " ");
        }
    }
}
