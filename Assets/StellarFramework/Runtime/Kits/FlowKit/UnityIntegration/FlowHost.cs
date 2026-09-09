using System;
using UnityEngine;

namespace StellarFramework.FlowKit.Unity
{
    /// <summary>Unity host for FlowKit orchestration. Gameplay remains outside FlowKit.</summary>
    [DisallowMultipleComponent]
    public sealed class FlowHost : MonoBehaviour
    {
        [SerializeField] private TextAsset defaultGraph;
        [SerializeField] private bool autoStartDefaultGraph;
        [SerializeField] private bool useUnscaledTime = true;
        [Header("Diagnostics")]
        [SerializeField] private bool enableRuntimeTrace = true;
        [SerializeField] [Min(64)] private int traceCapacity = 512;
        [SerializeField] private MonoBehaviour[] configurators = new MonoBehaviour[0];
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

        public event Action<FlowHost> Initialized;

        public bool IsInitialized { get; private set; }
        public FlowNodeRegistry Registry => _registry;
        public FlowRuntimeServices Services => _services;
        public FlowRunner Runner => _runner;

        private void Awake()
        {
            Initialize();
        }

        private void Start()
        {
            if (autoStartDefaultGraph && defaultGraph != null)
            {
                StartGraphAsset(defaultGraph);
            }
        }

        public void Initialize()
        {
            if (IsInitialized) return;

            var options = new FlowRunnerOptions
            {
                MaxActivationsPerTick = maxActivationsPerTick,
                MaxTotalActivationsPerRun = maxTotalActivationsPerRun,
                MaxCompletionsPerTick = maxCompletionsPerTick,
                MaxTimerCallbacksPerTick = maxTimerCallbacksPerTick,
                MaxSignalNotificationsPerTick = maxSignalNotificationsPerTick,
                MaxPollingCallbacksPerTick = maxPollingCallbacksPerTick
            };

            var builder = new FlowHostBuilder();
            ApplyConfigurators(builder);
            if (enableRuntimeTrace && builder.Trace == null)
            {
                builder.SetTraceSink(new FlowTraceRingBuffer(Math.Max(64, traceCapacity)));
            }
            _registry = builder.Nodes;
            _services = builder.BuildServices();
            _runner = new FlowRunner(_services, options);
            IsInitialized = true;
            Initialized?.Invoke(this);
        }

        private void ApplyConfigurators(FlowHostBuilder builder)
        {
            if (configurators == null) return;
            for (int i = 0; i < configurators.Length; i++)
            {
                MonoBehaviour component = configurators[i];
                if (component == null)
                    throw new InvalidOperationException($"FlowHost configurator at index {i} is null.");
                if (!(component is IFlowHostConfigurator configurator))
                    throw new InvalidOperationException(
                        $"FlowHost configurator '{component.GetType().FullName}' does not implement IFlowHostConfigurator.");
                configurator.Configure(builder);
            }
        }

        private void Update()
        {
            if (_runner == null) return;
            float unscaledDelta = Time.unscaledDeltaTime;
            _flowSeconds += useUnscaledTime ? unscaledDelta : Time.deltaTime;
            _runner.Tick(new FlowTimeSnapshot(Time.time, Time.unscaledTime, _flowSeconds));
        }

        public FlowCompileResult Compile(FlowGraphData graph)
        {
            EnsureInitialized();
            return FlowCompiler.Compile(graph, _registry);
        }

        public FlowRun StartGraphAsset(TextAsset graphAsset, FlowBlackboard blackboard = null)
        {
            if (graphAsset == null) throw new ArgumentNullException(nameof(graphAsset));
            FlowGraphData graph = FlowGraphJson.FromTextAsset(graphAsset);
            FlowCompileResult result = Compile(graph);
            if (!result.Succeeded)
            {
                for (int i = 0; i < result.Issues.Count; i++) Debug.LogError(result.Issues[i].ToString(), this);
                return null;
            }

            return StartFlow(result.Plan, blackboard);
        }

        public FlowRun StartFlow(FlowCompiledPlan plan, FlowBlackboard blackboard = null)
        {
            EnsureInitialized();
            return _runner.Start(plan, blackboard);
        }

        public bool RegisterCapability(FlowCapabilityId capability)
        {
            EnsureInitialized();
            return _services.Capabilities.Add(capability).Contains(capability);
        }

        public FlowBindingHandle Bind(FlowBindingId bindingId, object value)
        {
            EnsureInitialized();
            return _services.Bindings.Bind(bindingId, value);
        }

        public bool Unbind(FlowBindingId bindingId)
        {
            EnsureInitialized();
            return _services.Bindings.Unbind(bindingId);
        }

        public bool Unbind(FlowBindingHandle handle)
        {
            EnsureInitialized();
            return _services.Bindings.Unbind(handle);
        }

        private void EnsureInitialized()
        {
            if (!IsInitialized || _runner == null || _services == null || _registry == null)
                throw new InvalidOperationException("FlowHost is not initialized.");
        }

        private void OnDestroy()
        {
            if (_runner != null) _runner.CancelAll();
            if (_services != null) _services.RuntimeEpoch.Advance();
            Initialized = null;
            IsInitialized = false;
        }
    }
}
