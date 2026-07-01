using Industrial.Contracts.Device;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Industrial.Device
{
    /// <summary>
    /// 线程安全的设备上下文存储。
    /// DeviceManager 的内部存储层，不对外暴露。
    /// </summary>
    internal sealed class DeviceRegistry
    {
        private readonly ConcurrentDictionary<string, DeviceContext> _contexts =
            new ConcurrentDictionary<string, DeviceContext>(StringComparer.OrdinalIgnoreCase);

        public bool TryAdd(DeviceContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            return _contexts.TryAdd(context.Id, context);
        }

        public bool TryRemove(string deviceId, out DeviceContext context)
        {
            return _contexts.TryRemove(deviceId, out context);
        }

        public bool TryGet(string deviceId, out DeviceContext context)
        {
            return _contexts.TryGetValue(deviceId, out context);
        }

        public bool Contains(string deviceId)
        {
            return _contexts.ContainsKey(deviceId);
        }

        public IReadOnlyList<DeviceContext> GetAll()
        {
            return _contexts.Values.ToList().AsReadOnly();
        }

        public IReadOnlyList<DeviceContext> GetAll<T>() where T : class, IDevice
        {
            return _contexts.Values
                .Where(ctx => ctx.Device is T)
                .ToList()
                .AsReadOnly();
        }

        public int Count => _contexts.Count;

        public void Clear()
        {
            var all = _contexts.Keys.ToList();
            foreach (var key in all)
            {
                if (_contexts.TryRemove(key, out var ctx))
                    ctx.Dispose();
            }
        }
    }
}
