using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace StellarFramework.ActionEngine
{
    public interface IActionStrategy
    {
        // 核心执行接口，支持进度汇报以驱动 OnUpdate
        UniTask Execute(GameObject target, ActionStepData data, CancellationToken token,
            IProgress<float> progress = null);
    }

    public enum GroupExecutionMode
    {
        Sequence,
        Parallel
    }

    [Serializable]
    public class ActionStepData
    {
        [Header("Targeting")] public string TargetPath = "";

        [SerializeReference] public IActionStrategy Strategy;

        [Header("Settings")] public Vector3 TargetVector;
        public Color TargetColor = Color.white;
        public float Duration = 0.5f;
        public Ease Ease = Ease.OutQuad;
        public float Delay = 0f;

        #region 生命周期回调 (Editor & Runtime)

        [Header("Events (Editor Only)")] public UnityEvent OnStartEvent;
        public UnityEvent<float> OnUpdateEvent; // 传入 0-1 进度
        public UnityEvent OnCompleteEvent;

        // 运行时代码注入的回调 (不会被序列化到磁盘)
        public Action OnStart { get; set; }
        public Action<float> OnUpdate { get; set; }
        public Action OnComplete { get; set; }

        #endregion

        /// <summary>
        /// 内部调用：统一触发开始回调
        /// </summary>
        public void InvokeStart()
        {
            OnStartEvent?.Invoke();
            OnStart?.Invoke();
        }

        /// <summary>
        /// 内部调用：统一触发更新回调
        /// </summary>
        public void InvokeUpdate(float progress)
        {
            OnUpdateEvent?.Invoke(progress);
            OnUpdate?.Invoke(progress);
        }

        /// <summary>
        /// 内部调用：统一触发完成回调
        /// </summary>
        public void InvokeComplete()
        {
            OnCompleteEvent?.Invoke();
            OnComplete?.Invoke();
        }
    }

    [Serializable]
    public class ActionGroupData
    {
        public string GroupName = "New Group";
        public GroupExecutionMode Mode = GroupExecutionMode.Sequence;

        public List<ActionStepData> Steps = new List<ActionStepData>();
        public List<ActionGroupData> SubGroups = new List<ActionGroupData>();

        #region 组生命周期

        [Header("Group Events")] public UnityEvent OnGroupStartEvent;
        public UnityEvent OnGroupCompleteEvent;

        public Action OnGroupStart { get; set; }
        public Action OnGroupComplete { get; set; }

        #endregion

        public void InvokeGroupStart()
        {
            OnGroupStartEvent?.Invoke();
            OnGroupStart?.Invoke();
        }

        public void InvokeGroupComplete()
        {
            OnGroupCompleteEvent?.Invoke();
            OnGroupComplete?.Invoke();
        }
    }
}