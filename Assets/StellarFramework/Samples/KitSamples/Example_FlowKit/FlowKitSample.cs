using UnityEngine;
using StellarFramework.FlowKit;
using StellarFramework.FlowKit.Unity;

namespace StellarFramework.Samples.FlowKit
{
    /// <summary>最小可运行示例：Entry → Delay → Complete。</summary>
    public sealed class FlowKitSample : MonoBehaviour
    {
        [SerializeField] private TextAsset graphJson;
        private FlowRunner _runner;

        private void Start()
        {
            if (graphJson == null)
            {
                Debug.LogError("FlowKitSample 缺少 Graph JSON。", this);
                return;
            }

            FlowGraphData graph = FlowGraphJson.FromTextAsset(graphJson);
            FlowCompileResult result = FlowCompiler.Compile(graph, FlowBuiltInNodes.CreateRegistry());
            if (!result.Succeeded)
            {
                for (int i = 0; i < result.Issues.Count; i++) Debug.LogError(result.Issues[i].ToString(), this);
                return;
            }

            _runner = new FlowRunner();
            _runner.Start(result.Plan);
        }

        private void Update()
        {
            if (_runner == null) return;
            _runner.Tick(new FlowTimeSnapshot(Time.time, Time.unscaledTime, Time.unscaledTime));
        }

        private void OnDestroy()
        {
            if (_runner != null) _runner.CancelAll();
        }
    }
}
