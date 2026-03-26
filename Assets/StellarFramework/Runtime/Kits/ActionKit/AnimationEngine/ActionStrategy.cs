using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace StellarFramework.ActionEngine
{
    #region Transform 策略

    /// <summary>
    /// 本地坐标移动策略
    /// </summary>
    [Serializable]
    public class LocalMoveStrategy : IActionStrategy
    {
        public async UniTask Execute(GameObject target, ActionStepData data, CancellationToken token,
            IProgress<float> progress = null)
        {
            if (target == null) return;

            // 核心：调用带 Progress 重载的 TweenKit，将 0-1 进度回传给 ActionEngine
            await TweenKit.To(target.transform.localPosition, data.TargetVector, data.Duration,
                v =>
                {
                    // 防御性检查：防止动画过程中 GameObject 被销毁
                    if (target != null) target.transform.localPosition = v;
                },
                data.Ease, token, false, progress);
        }
    }

    /// <summary>
    /// 缩放策略
    /// </summary>
    [Serializable]
    public class ScaleStrategy : IActionStrategy
    {
        public async UniTask Execute(GameObject target, ActionStepData data, CancellationToken token,
            IProgress<float> progress = null)
        {
            if (target == null) return;

            await TweenKit.To(target.transform.localScale, data.TargetVector, data.Duration,
                v =>
                {
                    if (target != null) target.transform.localScale = v;
                },
                data.Ease, token, false, progress);
        }
    }

    #endregion

    #region 状态控制策略

    /// <summary>
    /// CanvasGroup 透明度淡入淡出策略
    /// </summary>
    [Serializable]
    public class CanvasFadeStrategy : IActionStrategy
    {
        public async UniTask Execute(GameObject target, ActionStepData data, CancellationToken token,
            IProgress<float> progress = null)
        {
            if (target == null) return;

            // 自动获取或添加组件，确保逻辑健壮性
            var cg = target.GetComponent<CanvasGroup>();
            if (cg == null) cg = target.AddComponent<CanvasGroup>();

            // TargetVector.x 作为 Alpha 目标值
            await TweenKit.To(cg.alpha, data.TargetVector.x, data.Duration,
                v =>
                {
                    if (cg != null) cg.alpha = v;
                },
                data.Ease, token, false, progress);
        }
    }

    #endregion
}