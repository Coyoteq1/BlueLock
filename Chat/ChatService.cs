using System;
using VAutomationCore.Core.Services;
using VAutomationCore.Services;

namespace VAuto.Core.Chat
{
    public static class ChatService
    {
        public static bool TryBroadcastSystemMessage(string message, out string error)
        {
            error = string.Empty;
            try
            {
                if (!GameActionService.TrySendSystemMessageToAll(TrimForFixedString(message)))
                {
                    error = "Failed to broadcast system message";
                    return false;
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
                if (!GameActionService.TrySendSystemMessageToPlatformId(platformId, TrimForFixedString(message)))
                {
                    error = $"User not connected: {platformId}";
                    return false;
                }

                return true;
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
