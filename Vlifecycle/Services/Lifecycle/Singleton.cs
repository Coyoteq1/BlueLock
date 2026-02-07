using System;
// must reference to ZUI before implementing 
namespace VAuto.Core.Patterns
{
    /// <summary>
    /// Base singleton pattern for V Rising services
    /// </summary>
    /// <typeparam name="T">The service type</typeparam>
    public abstract class Singleton<T> where T : class, new()
    {
        private static readonly object _lock = new object();
        private static T _instance;

        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new T();
                        }
                    }
                }
                return _instance;
            }
        }

        public static void Reset()
        {
            lock (_lock)
            {
                _instance = null;
            }
        }

        public static bool IsInitialized => _instance != null;
    }
}
