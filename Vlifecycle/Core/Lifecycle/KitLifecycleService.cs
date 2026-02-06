using Unity.Entities;
using VAuto.Services.Interfaces;

namespace VAuto.Core.Lifecycle
{
    public sealed class KitLifecycleService : IArenaLifecycleService, IService
    {
        public string Name => "KitLifecycleService";
        public bool IsInitialized { get; private set; }
        public BepInEx.Logging.ManualLogSource Log { get; private set; }

        private KitConfigService _kitService;
        private LifecycleStepsConfig _steps = new();

        public KitLifecycleService()
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

        public void SetKitService(KitConfigService kitService)
        {
            _kitService = kitService;
        }

        public bool OnPlayerEnter(Entity user, Entity character, string arenaId)
        {
            Initialize();
            if (!_steps.ApplyKit) return true;
            if (_kitService == null || character == Entity.Null)
                return false;

            return _kitService.TryApplyKitForZone(user, character, arenaId);
        }

        public bool OnPlayerExit(Entity user, Entity character, string arenaId)
        {
            Initialize();
            if (!_steps.ApplyKit) return true;
            if (_kitService == null || character == Entity.Null)
                return false;

            return _kitService.TryRestoreKitForZone(user, character, arenaId);
        }

        public bool OnArenaStart(string arenaId) => true;
        public bool OnArenaEnd(string arenaId) => true;
    }
}
