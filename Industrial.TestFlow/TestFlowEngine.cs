using Industrial.Contracts.Events;
using Industrial.Contracts.Logging;
using Industrial.Contracts.TestFlow;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Industrial.TestFlow
{
    /// <summary>
    /// 测试流程引擎：顺序执行 TestPlan 中的每个 ITestStep。
    /// 
    /// 特性：
    ///   - 支持 Pause / Resume / Abort
    ///   - 步骤间可以设置等待点（配合 ManualResetEventSlim 实现暂停）
    ///   - 通过 IEventBus 广播测试结果
    ///   - 支持 AbortOnFirstFail 策略
    /// 
    /// 用法：
    ///   engine.Execute(plan, context);  // 异步执行，不阻塞调用线程
    ///   engine.Pause();            // 执行完当前步骤后暂停
    ///   engine.Resume();           // 继续
    ///   engine.Abort();            // 中止（当前步骤执行完后停止）
    ///   engine.PlanCompleted += (s,e) => ShowReport(e.Result);
    /// </summary>
    public sealed class TestFlowEngine : ITestFlowEngine
    {
        private readonly IEventBus _eventBus;
        private readonly ILogger _logger;

        private CancellationTokenSource _abortCts;
        private ManualResetEventSlim _pauseGate = new ManualResetEventSlim(true); // true=不暂停

        private TestEngineState _state = TestEngineState.Idle;
        private readonly object _stateLock = new object();

        public TestEngineState State
        {
            get { lock (_stateLock) return _state; }
            private set { lock (_stateLock) _state = value; }
        }

        public event EventHandler<StepStartedEventArgs> StepStarted;
        public event EventHandler<StepCompletedEventArgs> StepCompleted;
        public event EventHandler<PlanCompletedEventArgs> PlanCompleted;

        public TestFlowEngine(IEventBus eventBus, ILoggerFactory loggerFactory)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _logger = loggerFactory.CreateLogger(typeof(TestFlowEngine));
        }

        public void Execute(TestPlan plan, TestContext context)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (context == null) throw new ArgumentNullException(nameof(context));

            lock (_stateLock)
            {
                if (_state != TestEngineState.Idle)
                    throw new InvalidOperationException($"引擎当前状态 [{_state}]，无法启动新计划");
                _state = TestEngineState.Running;
            }

            _abortCts = new CancellationTokenSource();
            _pauseGate.Set(); // 确保不在暂停状态

            // 在后台线程执行，不阻塞调用方
            Task.Run(() => RunPlan(plan, context, _abortCts.Token));
        }

        public void Pause()
        {
            lock (_stateLock)
            {
                if (_state != TestEngineState.Running) return;
                _state = TestEngineState.Paused;
            }
            _pauseGate.Reset();
            _logger.Info("测试流程已暂停");
        }

        public void Resume()
        {
            lock (_stateLock)
            {
                if (_state != TestEngineState.Paused) return;
                _state = TestEngineState.Running;
            }
            _pauseGate.Set();
            _logger.Info("测试流程已继续");
        }

        public void Abort()
        {
            lock (_stateLock)
            {
                if (_state == TestEngineState.Idle) return;
                _state = TestEngineState.Aborting;
            }
            _pauseGate.Set(); // 解除暂停，让引擎能感知取消
            _abortCts?.Cancel();
            _logger.Warn("测试流程中止请求已发出");
        }

        // ── 核心执行逻辑 ──────────────────────────────────────

        private void RunPlan(TestPlan plan, TestContext context, CancellationToken ct)
        {
            var startTime = DateTime.Now;
            var stepResults = new List<StepResult>();

            _logger.Info($"══ 开始执行计划: [{plan.Name}] ({plan.Steps.Count} 步骤) ══");

            for (int i = 0; i < plan.Steps.Count; i++)
            {
                var step = plan.Steps[i];

                // 检查是否需要暂停（等待 Resume）
                _pauseGate.Wait(ct);

                if (ct.IsCancellationRequested)
                {
                    stepResults.Add(StepResult.Aborted(step.Name));
                    _logger.Warn($"步骤 [{step.Name}] 被中止");
                    break;
                }

                if (!step.IsEnabled)
                {
                    var skipped = StepResult.Skip(step.Name);
                    stepResults.Add(skipped);
                    _logger.Info($"步骤 [{step.Name}] 已跳过");
                    StepCompleted?.Invoke(this, new StepCompletedEventArgs(skipped, i));
                    continue;
                }

                StepStarted?.Invoke(this, new StepStartedEventArgs(step.Name, i, plan.Steps.Count));
                _logger.Info($"▶ 步骤 [{i + 1}/{plan.Steps.Count}] {step.Name}");

                var sw = Stopwatch.StartNew();
                StepResult result;

                try
                {
                    result = step.Execute(context);
                }
                catch (OperationCanceledException)
                {
                    result = StepResult.Aborted(step.Name);
                    _logger.Warn($"步骤 [{step.Name}] 被取消");
                }
                catch (Exception ex)
                {
                    result = StepResult.Error(step.Name, sw.Elapsed, ex.Message);
                    _logger.Error($"步骤 [{step.Name}] 异常", ex);

                    if (!plan.ContinueOnError) break;
                }

                sw.Stop();
                stepResults.Add(result);

                var icon = result.Code == StepResultCode.Pass ? "✓" : "✗";
                _logger.Info($"  {icon} {result.Code}: {result.Message} [{result.Duration.TotalMilliseconds:F0}ms]");

                StepCompleted?.Invoke(this, new StepCompletedEventArgs(result, i));

                if (result.Code == StepResultCode.Fail && plan.AbortOnFirstFail)
                {
                    _logger.Warn($"AbortOnFirstFail 策略触发，计划终止");
                    break;
                }
            }

            var planResult = new PlanResult(plan.Name, stepResults, startTime, DateTime.Now);

            _logger.Info($"══ 计划完成: [{plan.Name}] | " +
                         $"Pass:{planResult.PassedSteps} Fail:{planResult.FailedSteps} " +
                         $"Error:{planResult.ErrorSteps} Skip:{planResult.SkippedSteps} | " +
                         $"耗时:{planResult.TotalDuration.TotalSeconds:F1}s ══");

            // 必须先设 Idle，再触发事件。
            // 否则事件处理器中立即调用 Execute() 时 State 仍为 Running，会抛异常。
            State = TestEngineState.Idle;

            _eventBus.Publish(new TestPlanCompletedBusEvent(planResult));
            PlanCompleted?.Invoke(this, new PlanCompletedEventArgs(planResult));
        }
    }
}
