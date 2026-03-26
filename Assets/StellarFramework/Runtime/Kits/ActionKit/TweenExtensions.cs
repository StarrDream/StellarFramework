using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace StellarFramework
{
    public enum Ease
    {
        Linear,
        InQuad,
        OutQuad,
        InOutQuad,
        InCubic,
        OutCubic,
        InOutCubic,
        InSine,
        OutSine,
        InOutSine,
        InBack,
        OutBack,
        InOutBack,
        InBounce,
        OutBounce,
        InOutBounce
    }

    /// <summary>
    /// 补间动画工具核心
    /// </summary>
    public static class TweenKit
    {
        #region 核心插值器

        public static async UniTask To(float start, float end, float duration, Action<float> onUpdate, Ease ease, CancellationToken token, bool ignoreTimeScale)
        {
            if (duration <= 0f)
            {
                onUpdate?.Invoke(end);
                return;
            }

            float time = 0f;
            onUpdate?.Invoke(start);

            while (time < duration)
            {
                if (token.IsCancellationRequested) return;

                float dt = ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
                time += dt;

                float t = Mathf.Clamp01(time / duration);
                float value = Easing.Evaluate(ease, t);

                onUpdate?.Invoke(Mathf.LerpUnclamped(start, end, value));

                await UniTask.NextFrame(PlayerLoopTiming.Update, token);
            }

            if (!token.IsCancellationRequested) onUpdate?.Invoke(end);
        }

        public static async UniTask To(Vector3 start, Vector3 end, float duration, Action<Vector3> onUpdate, Ease ease, CancellationToken token, bool ignoreTimeScale)
        {
            if (duration <= 0f)
            {
                onUpdate?.Invoke(end);
                return;
            }

            float time = 0f;
            onUpdate?.Invoke(start);

            while (time < duration)
            {
                if (token.IsCancellationRequested) return;

                float dt = ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
                time += dt;

                float t = Mathf.Clamp01(time / duration);
                float value = Easing.Evaluate(ease, t);

                onUpdate?.Invoke(Vector3.LerpUnclamped(start, end, value));

                await UniTask.NextFrame(PlayerLoopTiming.Update, token);
            }

            if (!token.IsCancellationRequested) onUpdate?.Invoke(end);
        }

        public static async UniTask To(Color start, Color end, float duration, Action<Color> onUpdate, Ease ease, CancellationToken token, bool ignoreTimeScale)
        {
            if (duration <= 0f)
            {
                onUpdate?.Invoke(end);
                return;
            }

            float time = 0f;
            onUpdate?.Invoke(start);

            while (time < duration)
            {
                if (token.IsCancellationRequested) return;

                float dt = ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
                time += dt;

                float t = Mathf.Clamp01(time / duration);
                float value = Easing.Evaluate(ease, t);

                onUpdate?.Invoke(Color.LerpUnclamped(start, end, value));

                await UniTask.NextFrame(PlayerLoopTiming.Update, token);
            }

            if (!token.IsCancellationRequested) onUpdate?.Invoke(end);
        }

        public static async UniTask ToRotation(Quaternion start, Quaternion end, float duration, Action<Quaternion> onUpdate, Ease ease, CancellationToken token,
            bool ignoreTimeScale)
        {
            if (duration <= 0f)
            {
                onUpdate?.Invoke(end);
                return;
            }

            float time = 0f;
            onUpdate?.Invoke(start);

            while (time < duration)
            {
                if (token.IsCancellationRequested) return;

                float dt = ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
                time += dt;

                float t = Mathf.Clamp01(time / duration);
                float value = Easing.Evaluate(ease, t);

                onUpdate?.Invoke(Quaternion.SlerpUnclamped(start, end, value));

                await UniTask.NextFrame(PlayerLoopTiming.Update, token);
            }

            if (!token.IsCancellationRequested) onUpdate?.Invoke(end);
        }

        #endregion
    }

    /// <summary>
    /// 针对 UniActionChain 的扩展方法
    /// </summary>
    public static class TweenExtensions
    {
        #region Transform

        public static UniActionChain MoveTo(this UniActionChain chain, Transform target, Vector3 endPos, float duration, Ease ease = Ease.OutQuad)
        {
            chain.AppendTask(async (token) =>
            {
                if (target == null) return;
                await TweenKit.To(target.position, endPos, duration, val =>
                {
                    if (target != null) target.position = val;
                }, ease, token, chain.IsIgnoreTimeScale);
            });
            return chain;
        }

        public static UniActionChain LocalMoveTo(this UniActionChain chain, Transform target, Vector3 endPos, float duration, Ease ease = Ease.OutQuad)
        {
            chain.AppendTask(async (token) =>
            {
                if (target == null) return;
                await TweenKit.To(target.localPosition, endPos, duration, val =>
                {
                    if (target != null) target.localPosition = val;
                }, ease, token, chain.IsIgnoreTimeScale);
            });
            return chain;
        }

        public static UniActionChain ScaleTo(this UniActionChain chain, Transform target, Vector3 endScale, float duration, Ease ease = Ease.OutBack)
        {
            chain.AppendTask(async (token) =>
            {
                if (target == null) return;
                await TweenKit.To(target.localScale, endScale, duration, val =>
                {
                    if (target != null) target.localScale = val;
                }, ease, token, chain.IsIgnoreTimeScale);
            });
            return chain;
        }

        public static UniActionChain ScaleTo(this UniActionChain chain, Transform target, float endScale, float duration, Ease ease = Ease.OutBack)
        {
            return ScaleTo(chain, target, Vector3.one * endScale, duration, ease);
        }

        public static UniActionChain RotateTo(this UniActionChain chain, Transform target, Vector3 endEuler, float duration, Ease ease = Ease.OutQuad)
        {
            chain.AppendTask(async (token) =>
            {
                if (target == null) return;
                await TweenKit.ToRotation(target.localRotation, Quaternion.Euler(endEuler), duration, val =>
                {
                    if (target != null) target.localRotation = val;
                }, ease, token, chain.IsIgnoreTimeScale);
            });
            return chain;
        }

        #endregion

        #region UI & Graphic

        public static UniActionChain FadeTo(this UniActionChain chain, CanvasGroup target, float endAlpha, float duration, Ease ease = Ease.Linear)
        {
            chain.AppendTask(async (token) =>
            {
                if (target == null) return;
                await TweenKit.To(target.alpha, endAlpha, duration, val =>
                {
                    if (target != null) target.alpha = val;
                }, ease, token, chain.IsIgnoreTimeScale);
            });
            return chain;
        }

        public static UniActionChain FadeTo(this UniActionChain chain, Graphic target, float endAlpha, float duration, Ease ease = Ease.Linear)
        {
            chain.AppendTask(async (token) =>
            {
                if (target == null) return;
                float startAlpha = target.color.a;
                await TweenKit.To(startAlpha, endAlpha, duration, val =>
                {
                    if (target != null)
                    {
                        Color c = target.color;
                        c.a = val;
                        target.color = c;
                    }
                }, ease, token, chain.IsIgnoreTimeScale);
            });
            return chain;
        }

        public static UniActionChain ColorTo(this UniActionChain chain, Graphic target, Color endColor, float duration, Ease ease = Ease.Linear)
        {
            chain.AppendTask(async (token) =>
            {
                if (target == null) return;
                await TweenKit.To(target.color, endColor, duration, val =>
                {
                    if (target != null) target.color = val;
                }, ease, token, chain.IsIgnoreTimeScale);
            });
            return chain;
        }

        #endregion

        #region Generic

        public static UniActionChain ValueTo(this UniActionChain chain, float start, float end, float duration, Action<float> onUpdate, Ease ease = Ease.OutQuad)
        {
            chain.AppendTask(async (token) => { await TweenKit.To(start, end, duration, onUpdate, ease, token, chain.IsIgnoreTimeScale); });
            return chain;
        }

        #endregion
    }

    public static class Easing
    {
        public static float Evaluate(Ease ease, float t)
        {
            switch (ease)
            {
                case Ease.Linear: return t;
                case Ease.InQuad: return t * t;
                case Ease.OutQuad: return t * (2 - t);
                case Ease.InOutQuad: return t < .5f ? 2 * t * t : -1 + (4 - 2 * t) * t;
                case Ease.InCubic: return t * t * t;
                case Ease.OutCubic: return (--t) * t * t + 1;
                case Ease.InOutCubic: return t < .5f ? 4 * t * t * t : (t - 1) * (2 * t - 2) * (2 * t - 2) + 1;
                case Ease.InSine: return 1 - Mathf.Cos(t * Mathf.PI / 2);
                case Ease.OutSine: return Mathf.Sin(t * Mathf.PI / 2);
                case Ease.InOutSine: return -(Mathf.Cos(Mathf.PI * t) - 1) / 2;
                case Ease.InBack:
                    float s1 = 1.70158f;
                    return t * t * ((s1 + 1) * t - s1);
                case Ease.OutBack:
                    float s2 = 1.70158f;
                    return (--t) * t * ((s2 + 1) * t + s2) + 1;
                case Ease.InOutBack:
                    float s3 = 1.70158f * 1.525f;
                    if ((t *= 2) < 1) return 0.5f * (t * t * ((s3 + 1) * t - s3));
                    return 0.5f * ((t -= 2) * t * ((s3 + 1) * t + s3) + 2);
                case Ease.InBounce: return 1 - Evaluate(Ease.OutBounce, 1 - t);
                case Ease.OutBounce:
                    if (t < (1 / 2.75f)) return 7.5625f * t * t;
                    else if (t < (2 / 2.75f)) return 7.5625f * (t -= (1.5f / 2.75f)) * t + 0.75f;
                    else if (t < (2.5 / 2.75f)) return 7.5625f * (t -= (2.25f / 2.75f)) * t + 0.9375f;
                    else return 7.5625f * (t -= (2.625f / 2.75f)) * t + 0.984375f;
                case Ease.InOutBounce:
                    if (t < 0.5f) return Evaluate(Ease.InBounce, t * 2) * 0.5f;
                    return Evaluate(Ease.OutBounce, t * 2 - 1) * 0.5f + 0.5f;
                default: return t;
            }
        }
    }
}