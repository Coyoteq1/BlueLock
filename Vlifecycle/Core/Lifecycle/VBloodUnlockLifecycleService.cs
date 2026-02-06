using System;
using BepInEx.Logging;
using ProjectM;
using ProjectM.Network;
using Unity.Entities;
using VAuto.Core;
using VAuto.Services.Interfaces;

namespace VAuto.Core.Lifecycle
{
    public sealed class VBloodUnlockLifecycleService : IArenaLifecycleService, IService
    {
        public string Name => "VBloodUnlockLifecycleService";
        public bool IsInitialized { get; private set; }
        public ManualLogSource Log { get; private set; }

        private LifecycleStepsConfig _steps = new();

        public VBloodUnlockLifecycleService()
        {
            Log = Plugin.Log;
        }

        public void Initialize()
        {
            if (IsInitialized) return;
            _steps = LifecycleStepsPolicy.Load();
            IsInitialized = true;
        }

        public void Cleanup() { }

        public bool OnPlayerEnter(Entity user, Entity character, string arenaId)
        {
            Initialize();
            if (!_steps.UnlockVBloods) return true;
            if (user == Entity.Null || character == Entity.Null) return false;

            try
            {
                VRCore.Initialize();
                var system = VRCore.ServerWorld?.GetExistingSystemManaged<DebugEventsSystem>();
                if (system == null) return false;

                system.UnlockAllVBloods(new FromCharacter
                {
                    User = user,
                    Character = character
                });

                Log?.LogInfo($"[VBlood] Unlocked all VBloods for arena '{arenaId}'.");
                return true;
            }
            catch (Exception ex)
            {
                Log?.LogWarning($"[VBlood] UnlockAllVBloods failed: {ex.Message}");
                return false;
            }
        }

        public bool OnPlayerExit(Entity user, Entity character, string arenaId) => true;
        public bool OnArenaStart(string arenaId) => true;
        public bool OnArenaEnd(string arenaId) => true;
    }
}

