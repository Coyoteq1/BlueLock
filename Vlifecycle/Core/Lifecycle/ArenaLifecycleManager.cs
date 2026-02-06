using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Entities;

namespace VAuto.Core.Lifecycle
{
    public sealed class ArenaLifecycleManager
    {
        private readonly List<IArenaLifecycleService> _services = new List<IArenaLifecycleService>();

        public static ArenaLifecycleManager Instance { get; } = new ArenaLifecycleManager();

        private ArenaLifecycleManager() { }

        public int ServiceCount => _services.Count;

        public string[] GetServiceNames()
        {
            var names = new string[_services.Count];
            for (int i = 0; i < _services.Count; i++)
            {
                names[i] = _services[i]?.Name ?? "(null)";
            }
            return names;
        }

        public void RegisterService(IArenaLifecycleService service)
        {
            if (service == null) return;
            _services.Add(service);
        }

        public void PostInitialize()
        {
            Plugin.Log?.LogInfo("[ArenaLifecycleManager] PostInitialize complete");
        }

        public void OnPlayerEnter(Entity user, Entity character, string arenaId)
        {
            foreach (var service in _services)
            {
                try
                {
                    var sw = Stopwatch.StartNew();
                    var ok = service.OnPlayerEnter(user, character, arenaId);
                    sw.Stop();

                    if (LifecycleDebug.Enabled)
                        Plugin.Log?.LogInfo($"[ArenaLifecycle] enter arena='{arenaId}' service='{service.Name}' ok={ok} ms={sw.ElapsedMilliseconds}");

                    if (!ok)
                        Plugin.Log?.LogWarning($"[ArenaLifecycle] enter failed arena='{arenaId}' service='{service.Name}'");
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogError($"[ArenaLifecycleManager] {service.Name} enter failed: {ex.Message}");
                }
            }
        }

        public void OnPlayerExit(Entity user, Entity character, string arenaId)
        {
            foreach (var service in _services)
            {
                try
                {
                    var sw = Stopwatch.StartNew();
                    var ok = service.OnPlayerExit(user, character, arenaId);
                    sw.Stop();

                    if (LifecycleDebug.Enabled)
                        Plugin.Log?.LogInfo($"[ArenaLifecycle] exit arena='{arenaId}' service='{service.Name}' ok={ok} ms={sw.ElapsedMilliseconds}");

                    if (!ok)
                        Plugin.Log?.LogWarning($"[ArenaLifecycle] exit failed arena='{arenaId}' service='{service.Name}'");
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogError($"[ArenaLifecycleManager] {service.Name} exit failed: {ex.Message}");
                }
            }
        }

        public void OnArenaStart(string arenaId)
        {
            foreach (var service in _services)
            {
                try
                {
                    service.OnArenaStart(arenaId);
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogError($"[ArenaLifecycleManager] {service.Name} arena start failed: {ex.Message}");
                }
            }
        }

        public void OnArenaEnd(string arenaId)
        {
            foreach (var service in _services)
            {
                try
                {
                    service.OnArenaEnd(arenaId);
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogError($"[ArenaLifecycleManager] {service.Name} arena end failed: {ex.Message}");
                }
            }
        }
    }
}
