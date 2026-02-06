using System;
using System.Collections.Generic;
using VAuto.Services.Interfaces;

namespace VAuto.Core.Lifecycle
{
    public sealed class ServiceManager
    {
        private readonly List<IService> _services = new List<IService>();
        private bool _initialized;

        public static ServiceManager Instance { get; } = new ServiceManager();

        private ServiceManager() { }

        public int ServiceCount => _services.Count;

        public string[] GetServiceNames()
        {
            var names = new string[_services.Count];
            for (int i = 0; i < _services.Count; i++)
            {
                names[i] = _services[i]?.GetType().Name ?? "(null)";
            }
            return names;
        }

        public void Register(IService service)
        {
            if (service == null) return;
            _services.Add(service);
        }

        public void InitializeAll()
        {
            if (_initialized) return;
            foreach (var service in _services)
            {
                try { service.Initialize(); }
                catch (Exception ex) { Plugin.Log?.LogError($"[ServiceManager] {service.GetType().Name} init failed: {ex.Message}"); }
            }
            _initialized = true;
        }
    }
}
