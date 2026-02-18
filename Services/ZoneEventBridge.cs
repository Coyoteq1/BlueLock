using System;
using Unity.Entities;
using VAutomationCore.Services;

namespace VAuto.Zone.Services
{
    // Backward-compatible shim to the shared ZoneEventBridge
    public static class ZoneEventBridge
    {
        public static event Action<Entity, string> OnPlayerEntered
        {
            add => VAutomationCore.Services.ZoneEventBridge.OnPlayerEntered += value;
            remove => VAutomationCore.Services.ZoneEventBridge.OnPlayerEntered -= value;
        }

        public static event Action<Entity, string> OnPlayerExited
        {
            add => VAutomationCore.Services.ZoneEventBridge.OnPlayerExited += value;
            remove => VAutomationCore.Services.ZoneEventBridge.OnPlayerExited -= value;
        }

        public static void Initialize() => VAutomationCore.Services.ZoneEventBridge.Initialize();
        public static void PublishPlayerEntered(Entity player, string zoneId) => VAutomationCore.Services.ZoneEventBridge.PublishPlayerEntered(player, zoneId);
        public static void PublishPlayerExited(Entity player, string zoneId) => VAutomationCore.Services.ZoneEventBridge.PublishPlayerExited(player, zoneId);
        public static VAutomationCore.Models.PlayerZoneState GetPlayerState(Entity player) => VAutomationCore.Services.ZoneEventBridge.GetPlayerZoneState(player);
        public static void SetPlayerState(Entity player, VAutomationCore.Models.PlayerZoneState state) => VAutomationCore.Services.ZoneEventBridge.UpdatePlayerZoneState(player, state);
        public static void RemovePlayerState(Entity player) => VAutomationCore.Services.ZoneEventBridge.RemovePlayerZoneState(player);
    }
}
