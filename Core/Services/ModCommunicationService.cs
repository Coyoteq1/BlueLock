using System;
using System.Collections.Generic;
using UnityEngine;

namespace VAutomationCore.Core.Services
{
    /// <summary>
    /// Inter-mod communication service for sharing data between mods after server restart.
    /// Uses BepInEx config as persistence layer for cross-mod data exchange.
    /// </summary>
    public class ModCommunicationService
    {
        private static ModCommunicationService _instance;
        public static ModCommunicationService Instance => _instance ??= new ModCommunicationService();
        
        private readonly Dictionary<string, object> _sharedData = new Dictionary<string, object>();
        private readonly Dictionary<string, List<Action<string, object>>> _subscribers = new Dictionary<string, List<Action<string, object>>>();
        private bool _isInitialized;
        
        /// <summary>
        /// Initialize the communication service.
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized) return;
            
            _isInitialized = true;
            Debug.Log("[ModComm] Inter-mod communication service initialized");
        }
        
        /// <summary>
        /// Send data to another mod. Data persists across restarts if saved.
        /// </summary>
        public void SendToMod(string targetMod, string key, object data)
        {
            var channel = $"{targetMod}:{key}";
            _sharedData[channel] = data;
            
            // Notify subscribers
            if (_subscribers.TryGetValue(channel, out var handlers))
            {
                foreach (var handler in handlers)
                {
                    try { handler(channel, data); } catch (Exception) { }
                }
            }
            
            Debug.Log($"[ModComm] Sent {key} to {targetMod}");
        }
        
        /// <summary>
        /// Receive data from another mod.
        /// </summary>
        public T GetFromMod<T>(string sourceMod, string key)
        {
            var channel = $"{sourceMod}:{key}";
            if (_sharedData.TryGetValue(channel, out var data))
            {
                if (data is T typedData)
                    return typedData;
            }
            return default;
        }
        
        /// <summary>
        /// Subscribe to messages from another mod.
        /// </summary>
        public void Subscribe(string sourceMod, string key, Action<string, object> handler)
        {
            var channel = $"{sourceMod}:{key}";
            if (!_subscribers.ContainsKey(channel))
                _subscribers[channel] = new List<Action<string, object>>();
            _subscribers[channel].Add(handler);
        }
        
        /// <summary>
        /// Broadcast to all mods.
        /// </summary>
        public void Broadcast(string key, object data)
        {
            SendToMod("*", key, data);
        }
        
        /// <summary>
        /// Check if data exists from a mod.
        /// </summary>
        public bool HasData(string sourceMod, string key)
        {
            return _sharedData.ContainsKey($"{sourceMod}:{key}");
        }
        
        /// <summary>
        /// Clear all shared data.
        /// </summary>
        public void Clear()
        {
            _sharedData.Clear();
        }
    }
    
    /// <summary>
    /// Simple mod API for other mods to communicate.
    /// </summary>
    public static class ModAPI
    {
        private static ModCommunicationService _comm => ModCommunicationService.Instance;
        
        /// <summary>
        /// Send message to Blueluck mod.
        /// </summary>
        public static void SendToBlueluck(string key, object data) => _comm.SendToMod("Blueluck", key, data);

        [Obsolete("Use SendToBlueluck.")]
        public static void SendToBluelock(string key, object data) => SendToBlueluck(key, data);
        
        /// <summary>
        /// Get message from Blueluck mod.
        /// </summary>
        public static T GetFromBlueluck<T>(string key) => _comm.GetFromMod<T>("Blueluck", key);

        [Obsolete("Use GetFromBlueluck.")]
        public static T GetFromBluelock<T>(string key) => GetFromBlueluck<T>(key);
        
        /// <summary>
        /// Send message to VAutomationCore.
        /// </summary>
        public static void SendToCore(string key, object data) => _comm.SendToMod("VAutomationCore", key, data);
        
        /// <summary>
        /// Get message from VAutomationCore.
        /// </summary>
        public static T GetFromCore<T>(string key) => _comm.GetFromMod<T>("VAutomationCore", key);
        
        /// <summary>
        /// Broadcast to all mods.
        /// </summary>
        public static void Broadcast(string key, object data) => _comm.Broadcast(key, data);
    }
}
