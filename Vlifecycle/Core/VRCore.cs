using ProjectM;
using Unity.Entities;

namespace VAuto.Core
{
    public static class VRCore
    {
        private static World _server;
        private static EntityManager _em;
        private static ProjectM.Scripting.ServerGameManager _serverGameManager;
        private static bool _initialized;

        public static World ServerWorld
        {
            get
            {
                if (_server != null && _server.IsCreated) return _server;
                foreach (var w in World.All)
                {
                    if (w.IsCreated && w.Name == "Server") { _server = w; _em = w.EntityManager; return _server; }
                }
                return null;
            }
        }

        public static EntityManager EM => _em != default ? _em : ServerWorld.EntityManager;
        public static EntityManager EntityManager => EM;

        public static ProjectM.Scripting.ServerGameManager ServerGameManager
        {
            get
            {
                if (!_initialized)
                    Initialize();
                return _serverGameManager;
            }
        }

        public static ProjectM.Scripting.ServerScriptMapper ServerScriptMapper => ServerWorld?.GetExistingSystemManaged<ProjectM.Scripting.ServerScriptMapper>();

        public static void Initialize()
        {
            if (_initialized) return;

            var world = ServerWorld;
            if (world != null)
            {
                _em = world.EntityManager;
                var scriptMapper = world.GetExistingSystemManaged<ProjectM.Scripting.ServerScriptMapper>();
                _serverGameManager = scriptMapper._ServerGameManager;
                _initialized = true;
            }
        }

        public static void ResetInitialization()
        {
            _server = null;
            _em = default;
            _serverGameManager = default;
            _initialized = false;
        }
    }
}

