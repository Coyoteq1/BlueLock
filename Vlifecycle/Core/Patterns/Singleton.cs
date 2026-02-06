using System;

namespace VAuto.Core.Patterns
{
    /// <summary>
    /// Base singleton pattern for V Rising services
    /// Provides thread-safe singleton implementation with proper initialization
    /// </summary>
    /// <typeparam name="T">The service type</typeparam>
    public abstract class Singleton<T> where T : class, new()
    {
        private static readonly object _lock = new object();
        private static T _instance;

        /// <summary>
        /// Gets the singleton instance
        /// </summary>
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

        /// <summary>
        /// Reset the singleton instance (useful for testing)
        /// </summary>
        public static void Reset()
        {
            lock (_lock)
            {
                _instance = null;
            }
        }

        /// <summary>
        /// Check if instance is initialized
        /// </summary>
        public static bool IsInitialized => _instance != null;
    }
}
