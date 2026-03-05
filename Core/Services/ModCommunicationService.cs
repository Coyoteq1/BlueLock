using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using VAutomationCore.Core.Logging;

namespace VAutomationCore.Core.Services
{
    /// <summary>
    /// Simple service for inter-mod communication using a publish-subscribe pattern.
    /// This provides a lightweight alternative to ModTalk for our automation system.
    /// </summary>
    public class ModCommunicationService
    {
        private static ModCommunicationService _instance;
        private static readonly object _lock = new object();
        
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, Action<string, object>>> _subscribers = new();
        private readonly CoreLogger _log;

        /// <summary>
        /// Gets the singleton instance of the ModCommunicationService.
        /// </summary>
        public static ModCommunicationService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new ModCommunicationService();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Initializes a new instance of the ModCommunicationService class.
        /// </summary>
        private ModCommunicationService()
        {
            _log = new CoreLogger("ModCommunicationService");
        }

        /// <summary>
        /// Initialize the communication service.
        /// </summary>
        public void Initialize()
        {
            _log.Info("ModCommunication service initialized");
        }

        /// <summary>
        /// Subscribe to messages from a specific mod and topic.
        /// </summary>
        /// <param name="modName">The name of the mod to subscribe to.</param>
        /// <param name="topic">The topic to subscribe to.</param>
        /// <param name="handler">The handler to call when a message is received.</param>
        public void Subscribe(string modName, string topic, Action<string, object> handler)
        {
            var key = $"{modName}.{topic}";
            
            if (!_subscribers.TryGetValue(key, out var handlers))
            {
                handlers = new ConcurrentDictionary<string, Action<string, object>>();
                _subscribers.TryAdd(key, handlers);
            }

            var handlerId = Guid.NewGuid().ToString();
            handlers.TryAdd(handlerId, handler);
            
            _log.Info($"Subscribed to {modName}.{topic}");
        }

        /// <summary>
        /// Send a message to a specific mod and topic.
        /// </summary>
        /// <param name="fromMod">The name of the mod sending the message.</param>
        /// <param name="toMod">The name of the mod to send the message to.</param>
        /// <param name="topic">The topic of the message.</param>
        /// <param name="message">The message to send.</param>
        public void SendToMod(string fromMod, string toMod, string topic, object message)
        {
            var key = $"{toMod}.{topic}";
            
            if (_subscribers.TryGetValue(key, out var handlers))
            {
                foreach (var handler in handlers.Values)
                {
                    try
                    {
                        handler(fromMod, message);
                    }
                    catch (Exception ex)
                    {
                        _log.Error($"Error in message handler for {toMod}.{topic}: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Send a message to a specific mod and topic.
        /// </summary>
        /// <param name="toMod">The name of the mod to send the message to.</param>
        /// <param name="topic">The topic of the message.</param>
        /// <param name="message">The message to send.</param>
        public void SendToMod(string toMod, string topic, object message)
        {
            SendToMod("VAutomationCore", toMod, topic, message);
        }

        /// <summary>
        /// Unsubscribe from a specific mod and topic.
        /// </summary>
        /// <param name="modName">The name of the mod to unsubscribe from.</param>
        /// <param name="topic">The topic to unsubscribe from.</param>
        /// <param name="handler">The handler to remove.</param>
        public void Unsubscribe(string modName, string topic, Action<string, object> handler)
        {
            var key = $"{modName}.{topic}";
            
            if (_subscribers.TryGetValue(key, out var handlers))
            {
                foreach (var kvp in handlers)
                {
                    if (kvp.Value == handler)
                    {
                        handlers.TryRemove(kvp.Key, out _);
                        break;
                    }
                }
            }
        }
    }
}