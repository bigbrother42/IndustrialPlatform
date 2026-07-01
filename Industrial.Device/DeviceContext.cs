using Industrial.Contracts.Device;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Industrial.Device
{
    /// <summary>
    /// 设备运行时上下文：包含一个已注册设备的全部运行状态。
    /// DeviceManager 内部使用，不对外暴露。
    /// </summary>
    internal sealed class DeviceContext : IDisposable
    {
        public DeviceDescriptor Descriptor { get; }
        public IDevice Device { get; }
        public DeviceStatistics Statistics { get; }

        // 重连任务管理
        private CancellationTokenSource _reconnectCts;
        private Task _reconnectTask;
        private readonly object _reconnectLock = new object();

        public string Id => Descriptor.Id;
        public string Name => Descriptor.Name;

        public DeviceContext(DeviceDescriptor descriptor, IDevice device)
        {
            Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            Device = device ?? throw new ArgumentNullException(nameof(device));
            Statistics = new DeviceStatistics(descriptor.Id);
        }

        // ── 重连任务控制 ─────────────────────────────────────────

        /// <summary>
        /// 启动重连后台任务。若已有任务在运行则跳过（防止重复触发）。
        /// </summary>
        public bool TryStartReconnect(Func<CancellationToken, Task> reconnectAction)
        {
            lock (_reconnectLock)
            {
                if (_reconnectTask != null && !_reconnectTask.IsCompleted)
                    return false;

                _reconnectCts = new CancellationTokenSource();
                _reconnectTask = Task.Run(
                    () => reconnectAction(_reconnectCts.Token),
                    _reconnectCts.Token);

                return true;
            }
        }

        /// <summary>
        /// 取消正在进行的重连任务并等待其结束。
        /// </summary>
        public void CancelReconnect()
        {
            CancellationTokenSource cts;
            Task task;

            lock (_reconnectLock)
            {
                cts = _reconnectCts;
                task = _reconnectTask;
                _reconnectCts = null;
                _reconnectTask = null;
            }

            if (cts == null) return;

            try
            {
                cts.Cancel();
                task?.Wait(TimeSpan.FromSeconds(5));
            }
            catch (AggregateException) { }
            catch (OperationCanceledException) { }
            finally
            {
                cts.Dispose();
            }
        }

        public bool IsReconnecting
        {
            get
            {
                lock (_reconnectLock)
                    return _reconnectTask != null && !_reconnectTask.IsCompleted;
            }
        }

        public void Dispose()
        {
            CancelReconnect();
            Device?.Dispose();
        }
    }
}
