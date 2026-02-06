using System;
using System.IO;
using System.Threading;

namespace VAuto.Core.Services
{
    public class ConfigWatcherService : IDisposable
    {
        private readonly LifecycleConfigManager _manager;
        private readonly FileSystemWatcher _watcher;
        private readonly Timer _timer;
        private bool _pending;
        private readonly string _path;

        public ConfigWatcherService(LifecycleConfigManager manager, string path)
        {
            _manager = manager;
            _path = path;
            var dir = Path.GetDirectoryName(path)!;
            var file = Path.GetFileName(path)!;
            _watcher = new FileSystemWatcher(dir, file)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime
            };
            _watcher.Changed += (_, __) => Schedule();
            _watcher.Created += (_, __) => Schedule();
            _watcher.Renamed += (_, __) => Schedule();
            _timer = new Timer(_ => Reload(), null, Timeout.Infinite, Timeout.Infinite);
        }

        public void Start() => _watcher.EnableRaisingEvents = true;

        private void Schedule()
        {
            if (_pending) return;
            _pending = true;
            _timer.Change(2000, Timeout.Infinite);
        }

        private void Reload()
        {
            try { _manager.Reload(_path); }
            finally { _pending = false; }
        }

        public void Dispose()
        {
            _watcher?.Dispose();
            _timer?.Dispose();
        }
    }
}
