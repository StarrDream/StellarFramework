using System;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace StellarFramework.ActionEngine
{
    public interface IFastForwardable
    {
        void FastForward(GameObject target, ActionNodeData data);
    }

    #region GameObject 策略

    [Serializable]
    public class GameObjectActiveStrategy : IActionStrategy, IFastForwardable
    {
        [NonSerialized] private bool _startState;
        [NonSerialized] private bool _endState;

        public void FastForward(GameObject target, ActionNodeData data)
        {
            if (target == null) return;
            _startState = target.activeSelf;
            _endState = data.TargetBool;
            target.SetActive(_endState);
        }

        public async UniTask Execute(GameObject target, ActionNodeData data, CancellationToken token, bool isReverse,
            IProgress<float> progress = null)
        {
            if (target == null) return;

            if (!isReverse)
            {
                if (data.Delay > 0) await UniTask.Delay(TimeSpan.FromSeconds(data.Delay), cancellationToken: token);
                target.SetActive(_endState);
                progress?.Report(1f);
            }
            else
            {
                target.SetActive(_startState);
                progress?.Report(0f);
                if (data.Delay > 0) await UniTask.Delay(TimeSpan.FromSeconds(data.Delay), cancellationToken: token);
            }
        }
    }

    #endregion

    #region Transform 策略

    [Serializable]
    public class LocalMoveStrategy : IActionStrategy, IFastForwardable
    {
        [NonSerialized] private Vector3 _startPos;
        [NonSerialized] private Vector3 _endPos;

        public void FastForward(GameObject target, ActionNodeData data)
        {
            if (target == null) return;
            _startPos = target.transform.localPosition;
            _endPos = _startPos;
            if ((data.AxisControl & AxisFlags.X) != 0) _endPos.x = data.TargetVector.x;
            if ((data.AxisControl & AxisFlags.Y) != 0) _endPos.y = data.TargetVector.y;
            if ((data.AxisControl & AxisFlags.Z) != 0) _endPos.z = data.TargetVector.z;
            target.transform.localPosition = _endPos;
        }

        public async UniTask Execute(GameObject target, ActionNodeData data, CancellationToken token, bool isReverse,
            IProgress<float> progress = null)
        {
            if (target == null) return;

            if (!isReverse)
            {
                if (data.Delay > 0) await UniTask.Delay(TimeSpan.FromSeconds(data.Delay), cancellationToken: token);

                await TweenKit.To(0f, 1f, data.Duration, linearT =>
                {
                    if (target != null)
                    {
                        float easedT = Easing.Evaluate(data.Ease, linearT);
                        target.transform.localPosition = Vector3.LerpUnclamped(_startPos, _endPos, easedT);
                        progress?.Report(linearT);
                    }
                }, Ease.Linear, token, false, null);
            }
            else
            {
                await TweenKit.To(0f, 1f, data.Duration, linearT =>
                {
                    if (target != null)
                    {
                        float reversedT = 1f - linearT;
                        float easedT = Easing.Evaluate(data.Ease, reversedT);
                        target.transform.localPosition = Vector3.LerpUnclamped(_startPos, _endPos, easedT);
                        progress?.Report(reversedT);
                    }
                }, Ease.Linear, token, false, null);

                if (data.Delay > 0) await UniTask.Delay(TimeSpan.FromSeconds(data.Delay), cancellationToken: token);
            }
        }
    }

    [Serializable]
    public class LocalRotateStrategy : IActionStrategy, IFastForwardable
    {
        [NonSerialized] private Quaternion _startRot;
        [NonSerialized] private Quaternion _endRot;

        public void FastForward(GameObject target, ActionNodeData data)
        {
            if (target == null) return;
            _startRot = target.transform.localRotation;
            _endRot = Quaternion.Euler(data.TargetVector);
            target.transform.localRotation = _endRot;
        }

        public async UniTask Execute(GameObject target, ActionNodeData data, CancellationToken token, bool isReverse,
            IProgress<float> progress = null)
        {
            if (target == null) return;

            if (!isReverse)
            {
                if (data.Delay > 0) await UniTask.Delay(TimeSpan.FromSeconds(data.Delay), cancellationToken: token);

                await TweenKit.To(0f, 1f, data.Duration, linearT =>
                {
                    if (target != null)
                    {
                        float easedT = Easing.Evaluate(data.Ease, linearT);
                        target.transform.localRotation = Quaternion.SlerpUnclamped(_startRot, _endRot, easedT);
                        progress?.Report(linearT);
                    }
                }, Ease.Linear, token, false, null);
            }
            else
            {
                await TweenKit.To(0f, 1f, data.Duration, linearT =>
                {
                    if (target != null)
                    {
                        float reversedT = 1f - linearT;
                        float easedT = Easing.Evaluate(data.Ease, reversedT);
                        target.transform.localRotation = Quaternion.SlerpUnclamped(_startRot, _endRot, easedT);
                        progress?.Report(reversedT);
                    }
                }, Ease.Linear, token, false, null);

                if (data.Delay > 0) await UniTask.Delay(TimeSpan.FromSeconds(data.Delay), cancellationToken: token);
            }
        }
    }

    [Serializable]
    public class ScaleStrategy : IActionStrategy, IFastForwardable
    {
        [NonSerialized] private Vector3 _startScale;
        [NonSerialized] private Vector3 _endScale;

        public void FastForward(GameObject target, ActionNodeData data)
        {
            if (target == null) return;
            _startScale = target.transform.localScale;
            _endScale = _startScale;
            if ((data.AxisControl & AxisFlags.X) != 0) _endScale.x = data.TargetVector.x;
            if ((data.AxisControl & AxisFlags.Y) != 0) _endScale.y = data.TargetVector.y;
            if ((data.AxisControl & AxisFlags.Z) != 0) _endScale.z = data.TargetVector.z;
            target.transform.localScale = _endScale;
        }

        public async UniTask Execute(GameObject target, ActionNodeData data, CancellationToken token, bool isReverse,
            IProgress<float> progress = null)
        {
            if (target == null) return;

            if (!isReverse)
            {
                if (data.Delay > 0) await UniTask.Delay(TimeSpan.FromSeconds(data.Delay), cancellationToken: token);

                await TweenKit.To(0f, 1f, data.Duration, linearT =>
                {
                    if (target != null)
                    {
                        float easedT = Easing.Evaluate(data.Ease, linearT);
                        target.transform.localScale = Vector3.LerpUnclamped(_startScale, _endScale, easedT);
                        progress?.Report(linearT);
                    }
                }, Ease.Linear, token, false, null);
            }
            else
            {
                await TweenKit.To(0f, 1f, data.Duration, linearT =>
                {
                    if (target != null)
                    {
                        float reversedT = 1f - linearT;
                        float easedT = Easing.Evaluate(data.Ease, reversedT);
                        target.transform.localScale = Vector3.LerpUnclamped(_startScale, _endScale, easedT);
                        progress?.Report(reversedT);
                    }
                }, Ease.Linear, token, false, null);

                if (data.Delay > 0) await UniTask.Delay(TimeSpan.FromSeconds(data.Delay), cancellationToken: token);
            }
        }
    }

    #endregion

    #region UI 策略

    [Serializable]
    public class CanvasFadeStrategy : IActionStrategy, IFastForwardable
    {
        [NonSerialized] private float _startAlpha;
        [NonSerialized] private float _endAlpha;

        public void FastForward(GameObject target, ActionNodeData data)
        {
            if (target == null) return;
            var cg = target.GetComponent<CanvasGroup>();
            if (cg == null) cg = target.AddComponent<CanvasGroup>();

            _startAlpha = cg.alpha;
            _endAlpha = data.TargetFloat;
            cg.alpha = _endAlpha;
        }

        public async UniTask Execute(GameObject target, ActionNodeData data, CancellationToken token, bool isReverse,
            IProgress<float> progress = null)
        {
            if (target == null) return;
            var cg = target.GetComponent<CanvasGroup>();
            if (cg == null) cg = target.AddComponent<CanvasGroup>();

            if (!isReverse)
            {
                if (data.Delay > 0) await UniTask.Delay(TimeSpan.FromSeconds(data.Delay), cancellationToken: token);

                await TweenKit.To(0f, 1f, data.Duration, linearT =>
                {
                    if (cg != null)
                    {
                        float easedT = Easing.Evaluate(data.Ease, linearT);
                        cg.alpha = Mathf.LerpUnclamped(_startAlpha, _endAlpha, easedT);
                        progress?.Report(linearT);
                    }
                }, Ease.Linear, token, false, null);
            }
            else
            {
                await TweenKit.To(0f, 1f, data.Duration, linearT =>
                {
                    if (cg != null)
                    {
                        float reversedT = 1f - linearT;
                        float easedT = Easing.Evaluate(data.Ease, reversedT);
                        cg.alpha = Mathf.LerpUnclamped(_startAlpha, _endAlpha, easedT);
                        progress?.Report(reversedT);
                    }
                }, Ease.Linear, token, false, null);

                if (data.Delay > 0) await UniTask.Delay(TimeSpan.FromSeconds(data.Delay), cancellationToken: token);
            }
        }
    }

    [Serializable]
    public class ImageColorStrategy : IActionStrategy, IFastForwardable
    {
        [NonSerialized] private Color _startColor;
        [NonSerialized] private Color _endColor;

        public void FastForward(GameObject target, ActionNodeData data)
        {
            if (target == null) return;
            var img = target.GetComponent<Image>();
            if (img == null) return;

            _startColor = img.color;
            _endColor = data.TargetColor;
            img.color = _endColor;
        }

        public async UniTask Execute(GameObject target, ActionNodeData data, CancellationToken token, bool isReverse,
            IProgress<float> progress = null)
        {
            if (target == null) return;
            var img = target.GetComponent<Image>();
            if (img == null) return;

            if (!isReverse)
            {
                if (data.Delay > 0) await UniTask.Delay(TimeSpan.FromSeconds(data.Delay), cancellationToken: token);

                await TweenKit.To(0f, 1f, data.Duration, linearT =>
                {
                    if (img != null)
                    {
                        float easedT = Easing.Evaluate(data.Ease, linearT);
                        img.color = Color.LerpUnclamped(_startColor, _endColor, easedT);
                        progress?.Report(linearT);
                    }
                }, Ease.Linear, token, false, null);
            }
            else
            {
                await TweenKit.To(0f, 1f, data.Duration, linearT =>
                {
                    if (img != null)
                    {
                        float reversedT = 1f - linearT;
                        float easedT = Easing.Evaluate(data.Ease, reversedT);
                        img.color = Color.LerpUnclamped(_startColor, _endColor, easedT);
                        progress?.Report(reversedT);
                    }
                }, Ease.Linear, token, false, null);

                if (data.Delay > 0) await UniTask.Delay(TimeSpan.FromSeconds(data.Delay), cancellationToken: token);
            }
        }
    }

    #endregion
}