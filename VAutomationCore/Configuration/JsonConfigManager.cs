using System;
using System.IO;
using System.Text.Json;
using BepInEx.Logging;

namespace VAuto.Core.Configuration
{
    /// <summary>
    /// Simple reusable JSON config manager that loads/saves strongly-typed config objects
    /// with safe directories and atomic writes. Intended for plugin-local configs copied
    /// alongside the plugin DLL.
    /// </summary>
    public class JsonConfigManager<T> where T : new()
    {
        private readonly string _path;
        private readonly ManualLogSource _log;
        private readonly JsonSerializerOptions _options;

        public T Value { get; private set; }

        public JsonConfigManager(string path, ManualLogSource log, JsonSerializerOptions? options = null)
        {
            _path = path;
            _log = log;
            _options = options ?? new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            Value = new T();
        }

        public void LoadOrCreate()
        {
            try
            {
                var dir = Path.GetDirectoryName(_path);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                if (File.Exists(_path))
                {
                    var json = File.ReadAllText(_path);
                    Value = JsonSerializer.Deserialize<T>(json, _options) ?? new T();
                    _log.LogInfo($"[CONFIG] Loaded {_path}");
                }
                else
                {
                    Value = new T();
                    Save();
                    _log.LogInfo($"[CONFIG] Created default {_path}");
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[CONFIG] Failed to load {_path}: {ex.Message}");
                Value = new T();
            }
        }

        public void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(_path);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var json = JsonSerializer.Serialize(Value, _options);
                var tmp = _path + ".tmp";
                File.WriteAllText(tmp, json);
                File.Copy(tmp, _path, overwrite: true);
                File.Delete(tmp);
                _log.LogInfo($"[CONFIG] Saved {_path}");
            }
            catch (Exception ex)
            {
                _log.LogError($"[CONFIG] Failed to save {_path}: {ex.Message}");
            }
        }
    }
}
