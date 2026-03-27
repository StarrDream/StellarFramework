using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace StellarFramework.ActionEngine
{
    [Flags]
    public enum AxisFlags
    {
        None = 0,
        X = 1 << 0,
        Y = 1 << 1,
        Z = 1 << 2,
        All = X | Y | Z
    }

    /// <summary>
    /// 动作策略接口
    /// 遵循快照机制，策略不再负责记录初始状态，仅负责执行与推演
    /// </summary>
    public interface IActionStrategy
    {
        UniTask Execute(GameObject target, ActionNodeData data, CancellationToken token, bool isReverse,
            IProgress<float> progress = null);
    }

    [Serializable]
    public class ActionNodeEvent : UnityEvent<GameObject>
    {
    }

    [Serializable]
    public class ActionNodeData
    {
        [Header("基础信息")] public string NodeName = "New Node";
        [HideInInspector] public bool IsExpanded = true;
        [HideInInspector] public Vector2 EditorPosition = new Vector2(100, 100);

        [Header("子节点 (并行执行)")] [SerializeReference]
        public List<ActionNodeData> Children = new List<ActionNodeData>();

        [Header("核心策略 (运行时多态)")] [SerializeReference]
        public IActionStrategy Strategy;

        [Header("目标寻址")] public string TargetPath = "";
        [Header("组件映射 (Editor 驱动)")] public string ComponentName = "";
        public string PropertyName = "";

        [Header("动画参数")] public AxisFlags AxisControl = AxisFlags.All;
        public Vector3 TargetVector;
        public Color TargetColor = Color.white;
        public float TargetFloat = 1f;
        public bool TargetBool = true;
        public float Duration = 0.5f;
        public float Delay = 0.0f;
        public Ease Ease = Ease.OutQuad;

        [Header("生命周期回调")] public ActionNodeEvent OnStartEvent;
        public UnityEvent<float> OnUpdateEvent;
        public ActionNodeEvent OnCompleteEvent;

        public Action OnStart { get; set; }
        public Action<float> OnUpdate { get; set; }
        public Action OnComplete { get; set; }

        public void InvokeStart()
        {
            OnStartEvent?.Invoke(null);
            OnStart?.Invoke();
        }

        public void InvokeUpdate(float p)
        {
            OnUpdateEvent?.Invoke(p);
            OnUpdate?.Invoke(p);
        }

        public void InvokeComplete()
        {
            OnCompleteEvent?.Invoke(null);
            OnComplete?.Invoke();
        }
    }
}