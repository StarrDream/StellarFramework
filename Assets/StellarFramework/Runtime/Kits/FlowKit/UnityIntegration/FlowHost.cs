using System;
using UnityEngine;

namespace StellarFramework.FlowKit.Unity
{
    /// <summary>
    /// Unity 生命周期宿主。它只负责时间推进、显式注册和销毁清理，不包含 UI/资源/XR/业务逻辑。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FlowHost : MonoBehaviour
    {
        [SerializeField] private TextAsset defaultGraph;
        [SerializeField] private bool autoStartDefaultGraph;
        [SerializeField] private bool useUnscaledTime = true;
        [SerializeField] private int maxActivationsPerTick = 1024;
        [SerializeField] private int maxTotalActivationsPerRun = 100000;
        [SerializeField] private int maxCompletionsPerTick = 1024;
        [SerializeField] private int maxTimerCallbacksPerTick = 1024;
        [SerializeField] private int maxSignalNotificationsPerTick = 1024;
        [SerializeField] private int maxPollingCallbacksPerTick = 1024;

        private double _flowSeconds;
        private FlowNodeRegistry _registry;
        private FlowRuntimeServices _services;
        private FlowRunner _runner;

        public FlowNodeRegistry Registry => _registry;
        public FlowRuntimeServices Services => _services;
        public FlowRunner Runner => _runner;

        private void Awake()
        {
            var options = new FlowRunnerOptions
            {
                MaxActivationsPerTick = maxActivationsPerTick,
                MaxTotalActivationsPerRun = maxTotalActivationsPerRun,
                MaxCompletionsPerTick = maxCompletionsPerTick,
                MaxTimerCallbacksPerTick = maxTimerCallbacksPerTick,
                MaxSignalNotificationsPerTick = maxSignalNotificationsPerTick,
                MaxPollingCallbacksPerTick = maxPollingCallbacksPerTick
            };
            _registry = FlowBuiltInNodes.CreateRegistry();
            _services = new FlowRuntimeServices();
            _runner = new FlowRunner(_services, options);

            if (autoStartDefaultGraph && defaultGraph != null)
            {
                FlowGraphData graph = FlowGraphJson.FromTextAsset(defaultGraph);
                FlowCompileResult result = FlowCompiler.Compile(graph, _registry);
                if (!result.Succeeded)
                {
                    for (int i = 0; i < result.Issues.Count; i++) Debug.LogError(result.Issues[i].ToString(), this);
                    return;
                }

                _runner.Start(result.Plan);
            }
        }

        private void Update()
        {
            if (_runner == null) return;
            float unscaledDelta = Time.unscaledDeltaTime;
            _flowSeconds += useUnscaledTime ? unscaledDelta : Time.deltaTime;
            _runner.Tick(new FlowTimeSnapshot(Time.time, Time.unscaledTime, _flowSeconds));
        }

        public FlowRun StartFlow(FlowCompiledPlan plan, FlowBlackboard blackboard = null)
        {
            if (_runner == null) throw new InvalidOperationException("FlowHost 尚未完成 Awake。");
            return _runner.Start(plan, blackboard);
        }

        public bool RegisterCapability(FlowCapabilityId capability)
        {
            if (_services == null) throw new InvalidOperationException("FlowHost 尚未完成 Awake。");
            return _services.Capabilities.Add(capability).Contains(capability);
        }

        public FlowBindingHandle Bind(FlowBindingId bindingId, object value)
        {
            if (_services == null) throw new InvalidOperationException("FlowHost 尚未完成 Awake。");
            return _services.Bindings.Bind(bindingId, value);
        }

        public bool Unbind(FlowBindingId bindingId)
        {
            if (_services == null) throw new InvalidOperationException("FlowHost 尚未完成 Awake。");
            return _services.Bindings.Unbind(bindingId);
        }

        private void OnDestroy()
        {
            if (_runner != null) _runner.CancelAll();
        }
    }
}
