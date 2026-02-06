using Unity.Entities;

namespace VAuto.Core.Lifecycle
{
    public interface IArenaLifecycleService
    {
        bool OnPlayerEnter(Entity user, Entity character, string arenaId);
        bool OnPlayerExit(Entity user, Entity character, string arenaId);
        bool OnArenaStart(string arenaId);
        bool OnArenaEnd(string arenaId);
        string Name { get; }
    }
}

