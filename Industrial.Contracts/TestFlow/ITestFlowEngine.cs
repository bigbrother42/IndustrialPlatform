using Industrial.Contracts.Events;
using System;
using System.Collections.Generic;

namespace Industrial.Contracts.TestFlow
{
    public interface ITestFlowEngine
    {
        TestEngineState State { get; }

        void Execute(TestPlan plan, TestContext context);
        void Pause();
        void Resume();
        void Abort();

        event EventHandler<StepStartedEventArgs> StepStarted;
        event EventHandler<StepCompletedEventArgs> StepCompleted;
        event EventHandler<PlanCompletedEventArgs> PlanCompleted;
    }

    public enum TestEngineState { Idle, Running, Paused, Aborting }
    public enum StepResultCode { Pass, Fail, Skip, Error, Aborted }

    // ── 计划与步骤定义 ────────────────────────────────────────

    public sealed class TestPlan
    {
        public string Name { get; }
        public IReadOnlyList<ITestStep> Steps { get; }
        public bool AbortOnFirstFail { get; }
        public bool ContinueOnError { get; }

        public TestPlan(string name, IReadOnlyList<ITestStep> steps,
            bool abortOnFirstFail = false, bool continueOnError = true)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Steps = steps ?? throw new ArgumentNullException(nameof(steps));
            AbortOnFirstFail = abortOnFirstFail;
            ContinueOnError = continueOnError;
        }
    }

    public interface ITestStep
    {
        string Name { get; }
        bool IsEnabled { get; }
        StepResult Execute(TestContext context);
    }

    // ── 结果类型 ──────────────────────────────────────────────

    public sealed class StepResult
    {
        public string StepName { get; }
        public StepResultCode Code { get; }
        public string Message { get; }
        public TimeSpan Duration { get; }
        public IReadOnlyDictionary<string, object> Measurements { get; }

        public bool IsPass => Code == StepResultCode.Pass;

        private StepResult(string stepName, StepResultCode code, string message,
            TimeSpan duration, Dictionary<string, object> measurements)
        {
            StepName = stepName;
            Code = code;
            Message = message;
            Duration = duration;
            Measurements = measurements ?? new Dictionary<string, object>();
        }

        public static StepResult Pass(string stepName, TimeSpan duration,
            Dictionary<string, object> measurements = null, string message = null)
            => new StepResult(stepName, StepResultCode.Pass, message ?? "PASS", duration, measurements);

        public static StepResult Fail(string stepName, TimeSpan duration,
            string reason, Dictionary<string, object> measurements = null)
            => new StepResult(stepName, StepResultCode.Fail, reason, duration, measurements);

        public static StepResult Error(string stepName, TimeSpan duration, string errorMessage)
            => new StepResult(stepName, StepResultCode.Error, errorMessage, duration, null);

        public static StepResult Skip(string stepName)
            => new StepResult(stepName, StepResultCode.Skip, "已跳过", TimeSpan.Zero, null);

        public static StepResult Aborted(string stepName)
            => new StepResult(stepName, StepResultCode.Aborted, "已中止", TimeSpan.Zero, null);
    }

    public sealed class PlanResult
    {
        public string PlanName { get; }
        public IReadOnlyList<StepResult> StepResults { get; }
        public DateTime StartedAt { get; }
        public DateTime FinishedAt { get; }
        public TimeSpan TotalDuration => FinishedAt - StartedAt;

        public int TotalSteps => StepResults.Count;
        public int PassedSteps { get; }
        public int FailedSteps { get; }
        public int ErrorSteps { get; }
        public int SkippedSteps { get; }
        public bool IsPass => FailedSteps == 0 && ErrorSteps == 0;

        public PlanResult(string planName, IReadOnlyList<StepResult> results,
            DateTime startedAt, DateTime finishedAt)
        {
            PlanName = planName;
            StepResults = results;
            StartedAt = startedAt;
            FinishedAt = finishedAt;

            foreach (var r in results)
            {
                switch (r.Code)
                {
                    case StepResultCode.Pass: PassedSteps++; break;
                    case StepResultCode.Fail: FailedSteps++; break;
                    case StepResultCode.Error: ErrorSteps++; break;
                    case StepResultCode.Skip: SkippedSteps++; break;
                }
            }
        }
    }

    // ── 上下文（步骤执行时获取外部服务）─────────────────────

    public sealed class TestContext
    {
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();
        public string PlanName { get; }
        public System.Threading.CancellationToken CancellationToken { get; }
        public IDictionary<string, object> SharedData { get; } = new Dictionary<string, object>();

        public TestContext(string planName, System.Threading.CancellationToken ct = default)
        {
            PlanName = planName;
            CancellationToken = ct;
        }

        public void RegisterService<T>(T service) => _services[typeof(T)] = service;
        public T GetService<T>() => (T)_services[typeof(T)];
        public bool TryGetService<T>(out T service)
        {
            if (_services.TryGetValue(typeof(T), out var s)) { service = (T)s; return true; }
            service = default;
            return false;
        }
    }

    // ── 事件参数 ──────────────────────────────────────────────

    public sealed class StepStartedEventArgs : EventArgs
    {
        public string StepName { get; }
        public int StepIndex { get; }
        public int TotalSteps { get; }
        public StepStartedEventArgs(string stepName, int idx, int total)
        { StepName = stepName; StepIndex = idx; TotalSteps = total; }
    }

    public sealed class StepCompletedEventArgs : EventArgs
    {
        public StepResult Result { get; }
        public int StepIndex { get; }
        public StepCompletedEventArgs(StepResult result, int idx) { Result = result; StepIndex = idx; }
    }

    public sealed class PlanCompletedEventArgs : EventArgs
    {
        public PlanResult Result { get; }
        public PlanCompletedEventArgs(PlanResult result) { Result = result; }
    }

    // EventBus 广播
    public sealed class TestPlanCompletedBusEvent : PlatformEvent
    {
        public PlanResult Result { get; }
        public TestPlanCompletedBusEvent(PlanResult result) { Result = result; Source = result.PlanName; }
    }
}
