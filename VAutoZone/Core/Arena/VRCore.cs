using System;
using ProjectM;
using Stunlock.Core;
using Unity.Entities;

namespace VAuto.Core
{
    public static class VRCore
    {
        private static World _server;
        private static EntityManager _em;
        private static ProjectM.Scripting.ServerGameManager _serverGameManager;
        private static ProjectM.Scripting.ServerScriptMapper _serverScriptMapper;
        private static PrefabCollectionSystem _prefabCollection;
        private static bool _initialized;

        public static World Server
        {
            get
            {
                if (_server != null && _server.IsCreated) return _server;
                foreach (var w in World.All)
                {
                    if (w.IsCreated && w.Name == "Server") { _server = w; return _server; }
                }
                throw new Exception("There is no Server world (yet). Did you install a server mod on the client?");
            }
        }

        /// <summary>
        /// Alias for Server property for backwards compatibility.
        /// </summary>
        public static World ServerWorld => Server;

        public static EntityManager EntityManager => Server.EntityManager;
        public static double ServerTime => ServerGameManager.ServerTime;
        
        public static ProjectM.Scripting.ServerGameManager ServerGameManager
        {
            get
            {
                Initialize();
                return _serverGameManager;
            }
        }

        public static ProjectM.Scripting.ServerScriptMapper ServerScriptMapper
        {
            get
            {
                Initialize();
                return _serverScriptMapper;
            }
        }

        public static PrefabCollectionSystem PrefabCollection
        {
            get
            {
                Initialize();
                return _prefabCollection;
            }
        }

        public static void Initialize()
        {
            if (_initialized) return;
            
            var world = Server;
            _em = world.EntityManager;
            _serverScriptMapper = world.GetExistingSystemManaged<ProjectM.Scripting.ServerScriptMapper>();
            _serverGameManager = _serverScriptMapper._ServerGameManager;
            _prefabCollection = world.GetExistingSystemManaged<PrefabCollectionSystem>();
            _initialized = true;
        }

        public static void Reset()
        {
            _server = null;
            _em = default;
            _serverGameManager = default;
            _serverScriptMapper = default;
            _prefabCollection = default;
            _initialized = false;
        }
    }
}
