using Industrial.Contracts.Device;
using System;

namespace Industrial.Device
{
    /// <summary>
    /// 指数退避重连策略。
    /// 延迟序列：InitialDelay → InitialDelay*Multiplier → ... → MaxDelay
    /// </summary>
    public sealed class ReconnectPolicy : IReconnectPolicy
    {
        public bool Enabled { get; }
        public int MaxAttempts { get; }
        public TimeSpan InitialDelay { get; }
        public TimeSpan MaxDelay { get; }
        public double BackoffMultiplier { get; }

        public ReconnectPolicy(
            bool enabled = true,
            int maxAttempts = -1,
            TimeSpan? initialDelay = null,
            TimeSpan? maxDelay = null,
            double backoffMultiplier = 2.0)
        {
            Enabled = enabled;
            MaxAttempts = maxAttempts;
            InitialDelay = initialDelay ?? TimeSpan.FromSeconds(1);
            MaxDelay = maxDelay ?? TimeSpan.FromSeconds(60);
            BackoffMultiplier = Math.Max(1.0, backoffMultiplier);
        }

        /// <summary>无限重试，初始1秒，最大60秒，倍增退避</summary>
        public static readonly ReconnectPolicy Default = new ReconnectPolicy();

        /// <summary>禁用自动重连</summary>
        public static readonly ReconnectPolicy Disabled = new ReconnectPolicy(enabled: false);

        /// <summary>快速重连：500ms起步，最大10秒，最多10次</summary>
        public static readonly ReconnectPolicy Fast = new ReconnectPolicy(
            maxAttempts: 10,
            initialDelay: TimeSpan.FromMilliseconds(500),
            maxDelay: TimeSpan.FromSeconds(10));

        /// <summary>
        /// 根据重试次数计算下次等待时间。
        /// </summary>
        public TimeSpan GetDelay(int attempt)
        {
            if (attempt <= 0) return InitialDelay;

            var ms = InitialDelay.TotalMilliseconds * Math.Pow(BackoffMultiplier, attempt - 1);
            ms = Math.Min(ms, MaxDelay.TotalMilliseconds);
            return TimeSpan.FromMilliseconds(ms);
        }

        public bool ShouldRetry(int attemptsDone)
            => Enabled && (MaxAttempts < 0 || attemptsDone < MaxAttempts);
    }
}
