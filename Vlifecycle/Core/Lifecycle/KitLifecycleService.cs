using Unity.Entities;
using VAuto.Core;
using VAuto.EndGameKit;
using VAuto.Services.Interfaces;

namespace VAuto.Core.Lifecycle
{
    public sealed class KitLifecycleService : IArenaLifecycleService, IService
    {
        public string Name => "KitLifecycleService";
        public bool IsInitialized { get; private set; }
        public BepInEx.Logging.ManualLogSource Log { get; private set; }

        private EndGameKitSystem _kitSystem;
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

        public void SetKitSystem(EndGameKitSystem kitSystem)
        {
            _kitSystem = kitSystem;
        }

        public bool OnPlayerEnter(Entity user, Entity character, string arenaId)
        {
            Initialize();
            _steps = LifecycleStepsPolicy.Load();
            if (!_steps.ApplyKit) return true;
            if (character == Entity.Null)
                return false;

            var system = EnsureKitSystem();
            if (system == null)
                return false;

            return system.TryApplyKitForZone(user, character, arenaId);
        }

        public bool OnPlayerExit(Entity user, Entity character, string arenaId)
        {
            Initialize();
            _steps = LifecycleStepsPolicy.Load();
            if (!_steps.ApplyKit) return true;
            if (character == Entity.Null)
                return false;

            var system = EnsureKitSystem();
            if (system == null)
                return false;

            return system.TryRestoreKitForZone(user, character, arenaId);
        }

        public bool OnArenaStart(string arenaId) => true;
        public bool OnArenaEnd(string arenaId) => true;

        private EndGameKitSystem EnsureKitSystem()
        {
            if (_kitSystem != null && _kitSystem.IsInitialized)
                return _kitSystem;

            VRCore.Initialize();
            var em = VRCore.EntityManager;
            if (em == default)
                return null;

            _kitSystem ??= new EndGameKitSystem(em);
            if (!_kitSystem.IsInitialized)
                _kitSystem.Initialize();

            return _kitSystem;
        }
    }
}
