using Industrial.Contracts.Device;
using System;
using System.Threading;

namespace Industrial.Device
{
    /// <summary>
    /// 设备运行统计信息（内部可变，对外暴露只读快照）。
    /// 使用 Interlocked 保证多线程下的计数器原子性。
    /// </summary>
    internal sealed class DeviceStatistics : IDeviceStatistics
    {
        private int _totalConnectAttempts;
        private int _successfulConnections;
        private int _failedConnections;
        private int _reconnectAttempts;
        private DateTime? _lastConnectedAt;
        private DateTime? _lastDisconnectedAt;
        private DateTime? _lastErrorAt;
        private string _lastErrorMessage;
        private DateTime? _connectedSince;
        private TimeSpan _accumulatedConnectedTime = TimeSpan.Zero;
        private readonly object _timeLock = new object();

        public string DeviceId { get; }

        public int TotalConnectAttempts => _totalConnectAttempts;
        public int SuccessfulConnections => _successfulConnections;
        public int FailedConnections => _failedConnections;
        public int ReconnectAttempts => _reconnectAttempts;
        public DateTime? LastConnectedAt => _lastConnectedAt;
        public DateTime? LastDisconnectedAt => _lastDisconnectedAt;
        public DateTime? LastErrorAt => _lastErrorAt;
        public string LastErrorMessage => _lastErrorMessage;

        public TimeSpan TotalConnectedTime
        {
            get
            {
                lock (_timeLock)
                {
                    if (_connectedSince.HasValue)
                        return _accumulatedConnectedTime + (DateTime.Now - _connectedSince.Value);
                    return _accumulatedConnectedTime;
                }
            }
        }

        public DeviceStatistics(string deviceId)
        {
            DeviceId = deviceId;
        }

        internal void OnConnectAttempt() =>
            Interlocked.Increment(ref _totalConnectAttempts);

        internal void OnConnected()
        {
            Interlocked.Increment(ref _successfulConnections);
            lock (_timeLock)
            {
                _lastConnectedAt = DateTime.Now;
                _connectedSince = DateTime.Now;
            }
        }

        internal void OnDisconnected()
        {
            lock (_timeLock)
            {
                _lastDisconnectedAt = DateTime.Now;
                if (_connectedSince.HasValue)
                {
                    _accumulatedConnectedTime += DateTime.Now - _connectedSince.Value;
                    _connectedSince = null;
                }
            }
        }

        internal void OnConnectFailed(string errorMessage)
        {
            Interlocked.Increment(ref _failedConnections);
            _lastErrorAt = DateTime.Now;
            _lastErrorMessage = errorMessage;
        }

        internal void OnReconnectAttempt() =>
            Interlocked.Increment(ref _reconnectAttempts);

        internal void OnError(string errorMessage)
        {
            _lastErrorAt = DateTime.Now;
            _lastErrorMessage = errorMessage;
        }
    }
}
