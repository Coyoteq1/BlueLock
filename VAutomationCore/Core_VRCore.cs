using BepInEx.Logging;
using ProjectM;
using Unity.Entities;
using VAutomationCore;

namespace VAuto.Core
{
    public static class VRCore
    {
        private static World _server;
        private static EntityManager _em;
        private static ProjectM.Scripting.ServerGameManager _serverGameManager;
        private static bool _initialized;
        private static ManualLogSource Log => Plugin.Log;

        public static World ServerWorld
        {
            get
            {
                if (_server != null && _server.IsCreated) return _server;
                foreach (var w in World.All)
                {
                    if (w.IsCreated && w.Name == "Server") { 
                        _server = w; 
                        _em = w.EntityManager; 
                        Log.LogInfo("[VRCore] Server world found and cached");
                        return _server; 
                    }
                }
                Log.LogWarning("[VRCore] Server world not found");
                return null;
            }
        }

        public static EntityManager EM => _em != default ? _em : ServerWorld?.EntityManager ?? default;
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

        /// <summary>
        /// Checks if VRCore is properly initialized.
        /// </summary>
        public static bool IsInitialized => _initialized && _server != null && _server.IsCreated;

        public static void Initialize()
        {
            if (_initialized) return;

            var world = ServerWorld;
            if (world != null)
            {
                _em = world.EntityManager;
                var scriptMapper = world.GetExistingSystemManaged<ProjectM.Scripting.ServerScriptMapper>();
                if (scriptMapper != null)
                {
                    _serverGameManager = scriptMapper._ServerGameManager;
                    _initialized = true;
                    Log.LogInfo("[VRCore] Successfully initialized ServerGameManager");
                }
                else
                {
                    Log.LogError("[VRCore] Failed to get ServerScriptMapper");
                }
            }
            else
            {
                Log.LogError("[VRCore] Failed to initialize - no server world available");
            }
        }

        public static void ResetInitialization()
        {
            Log.LogInfo("[VRCore] Resetting initialization");
            _server = null;
            _em = default;
            _serverGameManager = default;
            _initialized = false;
        }

        /// <summary>
        /// Ensures VRCore is initialized before performing operations.
        /// </summary>
        public static bool EnsureInitialized()
        {
            if (!IsInitialized)
            {
                Initialize();
            }
            return IsInitialized;
        }
    }
}
