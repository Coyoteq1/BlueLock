using Unity.Entities;
using VAuto.Services.Interfaces;

namespace VAuto.Core.Lifecycle
{
    public sealed class BuildingService : IArenaLifecycleService, IService
    {
        public string Name => "BuildingService";
        public bool IsInitialized { get; private set; }
        public BepInEx.Logging.ManualLogSource Log { get; private set; }

        public BuildingService()
        {
            Log = Plugin.Log;
        }

        public void Initialize()
        {
            if (IsInitialized) return;
            IsInitialized = true;
        }

        public void Cleanup() { }

        public bool OnPlayerEnter(Entity user, Entity character, string arenaId) => true;
        public bool OnPlayerExit(Entity user, Entity character, string arenaId) => true;
        public bool OnArenaStart(string arenaId) => true;
        public bool OnArenaEnd(string arenaId) => true;
    }
}

