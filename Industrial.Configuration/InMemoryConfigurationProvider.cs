using Industrial.Contracts.Configuration;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Industrial.Configuration
{
    /// <summary>
    /// 基于内存字典的配置提供者。
    /// 可用于：单元测试、默认值、运行时动态参数。
    /// 生产环境可扩展为 JsonConfigurationProvider 或 DatabaseConfigurationProvider。
    /// </summary>
    public sealed class InMemoryConfigurationProvider : IConfigurationProvider
    {
        private readonly ConcurrentDictionary<string, object> _store =
            new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        public InMemoryConfigurationProvider() { }

        /// <summary>从初始字典批量载入</summary>
        public InMemoryConfigurationProvider(IDictionary<string, object> initial)
        {
            foreach (var kv in initial)
                _store[kv.Key] = kv.Value;
        }

        public T Get<T>(string key, T defaultValue = default)
        {
            if (_store.TryGetValue(key, out var raw))
            {
                try { return (T)Convert.ChangeType(raw, typeof(T)); }
                catch { return defaultValue; }
            }
            return defaultValue;
        }

        public void Set<T>(string key, T value)
        {
            _store[key] = value;
        }

        public bool Contains(string key) => _store.ContainsKey(key);

        public IReadOnlyDictionary<string, object> GetSection(string sectionName)
        {
            var prefix = sectionName.TrimEnd(':') + ":";
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            foreach (var kv in _store)
            {
                if (kv.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    result[kv.Key.Substring(prefix.Length)] = kv.Value;
            }
            return result;
        }

        public void Reload() { /* 内存实现无需重载 */ }
    }
}
