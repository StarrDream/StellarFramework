using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StellarFramework
{
    /// <summary>
    /// 协程扩展兼容层
    /// 我保留这套 API 的目的，是为了兼容历史协程调用代码，而不是鼓励新业务继续扩散协程工作流。
    /// 在新的运行时业务链路中，应优先使用 UniTask 与 ActionKit。
    /// </summary>
    public static class CoroutineExtensions
    {
        #region 基础等待

        public static CoroutineHandle WaitUntil(this MonoBehaviour mono, Func<bool> condition, Action onComplete = null,
            float pollInterval = 0.033f)
        {
            if (!ValidateMono(mono, "WaitUntil"))
            {
                return null;
            }

            if (condition == null)
            {
                LogKit.LogError(
                    $"[CoroutineExtensions] WaitUntil 失败: condition 为空, TriggerObject={mono.gameObject.name}");
                return null;
            }

            if (pollInterval < 0f)
            {
                LogKit.LogError(
                    $"[CoroutineExtensions] WaitUntil 失败: pollInterval 非法, TriggerObject={mono.gameObject.name}, PollInterval={pollInterval}");
                return null;
            }

            var handle = new CoroutineHandle(mono);
            handle.Coroutine = mono.StartCoroutine(WaitUntilCoroutine(condition, onComplete, pollInterval, handle));
            return handle;
        }

        private static IEnumerator WaitUntilCoroutine(Func<bool> condition, Action onComplete, float pollInterval,
            CoroutineHandle handle)
        {
            if (condition())
            {
                if (!handle.IsCancelled)
                {
                    onComplete?.Invoke();
                }

                yield break;
            }

            if (pollInterval <= 0f)
            {
                while (!condition())
                {
                    if (handle.IsCancelled)
                    {
                        yield break;
                    }

                    yield return null;
                }

                if (!handle.IsCancelled)
                {
                    onComplete?.Invoke();
                }

                yield break;
            }

            var wait = new WaitForSeconds(pollInterval);
            while (!condition())
            {
                if (handle.IsCancelled)
                {
                    yield break;
                }

                yield return wait;
            }

            if (!handle.IsCancelled)
            {
                onComplete?.Invoke();
            }
        }

        #endregion

        #region 带超时的版本

        public static CoroutineHandle WaitUntilWithTimeout(this MonoBehaviour mono, Func<bool> condition,
            float timeoutSeconds, Action<bool> onComplete = null, float pollInterval = 0.033f)
        {
            if (!ValidateMono(mono, "WaitUntilWithTimeout"))
            {
                return null;
            }

            if (condition == null)
            {
                LogKit.LogError(
                    $"[CoroutineExtensions] WaitUntilWithTimeout 失败: condition 为空, TriggerObject={mono.gameObject.name}");
                return null;
            }

            if (timeoutSeconds < 0f)
            {
                LogKit.LogError(
                    $"[CoroutineExtensions] WaitUntilWithTimeout 失败: timeoutSeconds 非法, TriggerObject={mono.gameObject.name}, Timeout={timeoutSeconds}");
                return null;
            }

            if (pollInterval < 0f)
            {
                LogKit.LogError(
                    $"[CoroutineExtensions] WaitUntilWithTimeout 失败: pollInterval 非法, TriggerObject={mono.gameObject.name}, PollInterval={pollInterval}");
                return null;
            }

            var handle = new CoroutineHandle(mono);
            handle.Coroutine =
                mono.StartCoroutine(WaitUntilWithTimeoutCoroutine(condition, timeoutSeconds, onComplete, pollInterval,
                    handle));
            return handle;
        }

        private static IEnumerator WaitUntilWithTimeoutCoroutine(Func<bool> condition, float timeoutSeconds,
            Action<bool> onComplete, float pollInterval, CoroutineHandle handle)
        {
            float startTime = Time.time;

            if (pollInterval <= 0f)
            {
                while (!condition())
                {
                    if (handle.IsCancelled)
                    {
                        yield break;
                    }

                    if (Time.time - startTime >= timeoutSeconds)
                    {
                        if (!handle.IsCancelled)
                        {
                            onComplete?.Invoke(false);
                        }

                        yield break;
                    }

                    yield return null;
                }

                if (!handle.IsCancelled)
                {
                    onComplete?.Invoke(true);
                }

                yield break;
            }

            var wait = new WaitForSeconds(pollInterval);
            while (!condition())
            {
                if (handle.IsCancelled)
                {
                    yield break;
                }

                if (Time.time - startTime >= timeoutSeconds)
                {
                    if (!handle.IsCancelled)
                    {
                        onComplete?.Invoke(false);
                    }

                    yield break;
                }

                yield return wait;
            }

            if (!handle.IsCancelled)
            {
                onComplete?.Invoke(true);
            }
        }

        #endregion

        #region WaitWhile

        public static CoroutineHandle WaitWhile(this MonoBehaviour mono, Func<bool> condition, Action onComplete = null,
            float pollInterval = 0.033f)
        {
            if (!ValidateMono(mono, "WaitWhile"))
            {
                return null;
            }

            if (condition == null)
            {
                LogKit.LogError(
                    $"[CoroutineExtensions] WaitWhile 失败: condition 为空, TriggerObject={mono.gameObject.name}");
                return null;
            }

            return mono.WaitUntil(() => !condition(), onComplete, pollInterval);
        }

        #endregion

        #region 多条件等待

        public static CoroutineHandle WaitUntilAll(this MonoBehaviour mono, Action onComplete = null,
            float pollInterval = 0.033f, params Func<bool>[] conditions)
        {
            if (!ValidateMono(mono, "WaitUntilAll"))
            {
                return null;
            }

            if (conditions == null || conditions.Length == 0)
            {
                LogKit.LogError(
                    $"[CoroutineExtensions] WaitUntilAll 失败: conditions 为空, TriggerObject={mono.gameObject.name}");
                return null;
            }

            for (int i = 0; i < conditions.Length; i++)
            {
                if (conditions[i] == null)
                {
                    LogKit.LogError(
                        $"[CoroutineExtensions] WaitUntilAll 失败: 第 {i} 个 condition 为空, TriggerObject={mono.gameObject.name}");
                    return null;
                }
            }

            return mono.WaitUntil(() =>
            {
                for (int i = 0; i < conditions.Length; i++)
                {
                    if (!conditions[i]())
                    {
                        return false;
                    }
                }

                return true;
            }, onComplete, pollInterval);
        }

        public static CoroutineHandle WaitUntilAny(this MonoBehaviour mono, Action<int> onComplete = null,
            float pollInterval = 0.033f, params Func<bool>[] conditions)
        {
            if (!ValidateMono(mono, "WaitUntilAny"))
            {
                return null;
            }

            if (conditions == null || conditions.Length == 0)
            {
                LogKit.LogError(
                    $"[CoroutineExtensions] WaitUntilAny 失败: conditions 为空, TriggerObject={mono.gameObject.name}");
                return null;
            }

            if (pollInterval < 0f)
            {
                LogKit.LogError(
                    $"[CoroutineExtensions] WaitUntilAny 失败: pollInterval 非法, TriggerObject={mono.gameObject.name}, PollInterval={pollInterval}");
                return null;
            }

            for (int i = 0; i < conditions.Length; i++)
            {
                if (conditions[i] == null)
                {
                    LogKit.LogError(
                        $"[CoroutineExtensions] WaitUntilAny 失败: 第 {i} 个 condition 为空, TriggerObject={mono.gameObject.name}");
                    return null;
                }
            }

            var handle = new CoroutineHandle(mono);
            handle.Coroutine = mono.StartCoroutine(WaitUntilAnyCoroutine(conditions, onComplete, pollInterval, handle));
            return handle;
        }

        private static IEnumerator WaitUntilAnyCoroutine(Func<bool>[] conditions, Action<int> onComplete,
            float pollInterval, CoroutineHandle handle)
        {
            if (pollInterval <= 0f)
            {
                while (true)
                {
                    if (handle.IsCancelled)
                    {
                        yield break;
                    }

                    for (int i = 0; i < conditions.Length; i++)
                    {
                        if (conditions[i]())
                        {
                            if (!handle.IsCancelled)
                            {
                                onComplete?.Invoke(i);
                            }

                            yield break;
                        }
                    }

                    yield return null;
                }
            }

            var wait = new WaitForSeconds(pollInterval);
            while (true)
            {
                if (handle.IsCancelled)
                {
                    yield break;
                }

                for (int i = 0; i < conditions.Length; i++)
                {
                    if (conditions[i]())
                    {
                        if (!handle.IsCancelled)
                        {
                            onComplete?.Invoke(i);
                        }

                        yield break;
                    }
                }

                yield return wait;
            }
        }

        #endregion

        #region 延时执行

        public static CoroutineHandle DelayedCall(this MonoBehaviour mono, float seconds, Action callback)
        {
            if (!ValidateMono(mono, "DelayedCall"))
            {
                return null;
            }

            if (seconds < 0f)
            {
                LogKit.LogError(
                    $"[CoroutineExtensions] DelayedCall 失败: seconds 非法, TriggerObject={mono.gameObject.name}, Seconds={seconds}");
                return null;
            }

            var handle = new CoroutineHandle(mono);
            handle.Coroutine = mono.StartCoroutine(DelayedCallCoroutine(seconds, callback, handle));
            return handle;
        }

        private static IEnumerator DelayedCallCoroutine(float seconds, Action callback, CoroutineHandle handle)
        {
            if (seconds <= 0f)
            {
                if (!handle.IsCancelled)
                {
                    callback?.Invoke();
                }

                yield break;
            }

            yield return new WaitForSeconds(seconds);

            if (!handle.IsCancelled)
            {
                callback?.Invoke();
            }
        }

        public static CoroutineHandle DelayedCallFrames(this MonoBehaviour mono, int frames, Action callback)
        {
            if (!ValidateMono(mono, "DelayedCallFrames"))
            {
                return null;
            }

            if (frames < 0)
            {
                LogKit.LogError(
                    $"[CoroutineExtensions] DelayedCallFrames 失败: frames 非法, TriggerObject={mono.gameObject.name}, Frames={frames}");
                return null;
            }

            var handle = new CoroutineHandle(mono);
            handle.Coroutine = mono.StartCoroutine(DelayedCallFramesCoroutine(frames, callback, handle));
            return handle;
        }

        private static IEnumerator DelayedCallFramesCoroutine(int frames, Action callback, CoroutineHandle handle)
        {
            for (int i = 0; i < frames; i++)
            {
                if (handle.IsCancelled)
                {
                    yield break;
                }

                yield return null;
            }

            if (!handle.IsCancelled)
            {
                callback?.Invoke();
            }
        }

        #endregion

        #region 重复执行

        public static CoroutineHandle Repeat(this MonoBehaviour mono, int count, float interval, Action<int> action)
        {
            if (!ValidateMono(mono, "Repeat"))
            {
                return null;
            }

            if (count < 0)
            {
                LogKit.LogError(
                    $"[CoroutineExtensions] Repeat 失败: count 非法, TriggerObject={mono.gameObject.name}, Count={count}");
                return null;
            }

            if (interval < 0f)
            {
                LogKit.LogError(
                    $"[CoroutineExtensions] Repeat 失败: interval 非法, TriggerObject={mono.gameObject.name}, Interval={interval}");
                return null;
            }

            if (action == null)
            {
                LogKit.LogError(
                    $"[CoroutineExtensions] Repeat 失败: action 为空, TriggerObject={mono.gameObject.name}, Count={count}");
                return null;
            }

            var handle = new CoroutineHandle(mono);
            handle.Coroutine = mono.StartCoroutine(RepeatCoroutine(count, interval, action, handle));
            return handle;
        }

        private static IEnumerator RepeatCoroutine(int count, float interval, Action<int> action,
            CoroutineHandle handle)
        {
            if (count == 0)
            {
                yield break;
            }

            WaitForSeconds wait = interval > 0f ? new WaitForSeconds(interval) : null;

            for (int i = 0; i < count; i++)
            {
                if (handle.IsCancelled)
                {
                    yield break;
                }

                action.Invoke(i);

                if (i < count - 1)
                {
                    if (wait != null)
                    {
                        yield return wait;
                    }
                    else
                    {
                        yield return null;
                    }
                }
            }
        }

        public static CoroutineHandle RepeatForever(this MonoBehaviour mono, float interval, Action action)
        {
            if (!ValidateMono(mono, "RepeatForever"))
            {
                return null;
            }

            if (interval < 0f)
            {
                LogKit.LogError(
                    $"[CoroutineExtensions] RepeatForever 失败: interval 非法, TriggerObject={mono.gameObject.name}, Interval={interval}");
                return null;
            }

            if (action == null)
            {
                LogKit.LogError(
                    $"[CoroutineExtensions] RepeatForever 失败: action 为空, TriggerObject={mono.gameObject.name}");
                return null;
            }

            var handle = new CoroutineHandle(mono);
            handle.Coroutine = mono.StartCoroutine(RepeatForeverCoroutine(interval, action, handle));
            return handle;
        }

        private static IEnumerator RepeatForeverCoroutine(float interval, Action action, CoroutineHandle handle)
        {
            WaitForSeconds wait = interval > 0f ? new WaitForSeconds(interval) : null;

            while (true)
            {
                if (handle.IsCancelled)
                {
                    yield break;
                }

                action.Invoke();

                if (wait != null)
                {
                    yield return wait;
                }
                else
                {
                    yield return null;
                }
            }
        }

        #endregion

        #region 序列执行

        public static CoroutineHandle Sequence(this MonoBehaviour mono, params IEnumerator[] coroutines)
        {
            if (!ValidateMono(mono, "Sequence"))
            {
                return null;
            }

            if (coroutines == null || coroutines.Length == 0)
            {
                LogKit.LogError(
                    $"[CoroutineExtensions] Sequence 失败: coroutines 为空, TriggerObject={mono.gameObject.name}");
                return null;
            }

            for (int i = 0; i < coroutines.Length; i++)
            {
                if (coroutines[i] == null)
                {
                    LogKit.LogError(
                        $"[CoroutineExtensions] Sequence 失败: 第 {i} 个 coroutine 为空, TriggerObject={mono.gameObject.name}");
                    return null;
                }
            }

            var handle = new CoroutineHandle(mono);
            handle.Coroutine = mono.StartCoroutine(SequenceCoroutine(coroutines, handle));
            return handle;
        }

        private static IEnumerator SequenceCoroutine(IEnumerator[] coroutines, CoroutineHandle handle)
        {
            for (int i = 0; i < coroutines.Length; i++)
            {
                if (handle.IsCancelled)
                {
                    yield break;
                }

                yield return coroutines[i];
            }
        }

        #endregion

        #region 并行执行

        public static CoroutineHandle Parallel(this MonoBehaviour mono, params IEnumerator[] coroutines)
        {
            if (!ValidateMono(mono, "Parallel"))
            {
                return null;
            }

            if (coroutines == null || coroutines.Length == 0)
            {
                LogKit.LogError(
                    $"[CoroutineExtensions] Parallel 失败: coroutines 为空, TriggerObject={mono.gameObject.name}");
                return null;
            }

            for (int i = 0; i < coroutines.Length; i++)
            {
                if (coroutines[i] == null)
                {
                    LogKit.LogError(
                        $"[CoroutineExtensions] Parallel 失败: 第 {i} 个 coroutine 为空, TriggerObject={mono.gameObject.name}");
                    return null;
                }
            }

            var handle = new CoroutineHandle(mono);
            handle.Coroutine = mono.StartCoroutine(ParallelCoroutine(mono, coroutines, handle));
            return handle;
        }

        private static IEnumerator ParallelCoroutine(MonoBehaviour mono, IEnumerator[] coroutines,
            CoroutineHandle handle)
        {
            var runningCoroutines = new List<Coroutine>(coroutines.Length);

            for (int i = 0; i < coroutines.Length; i++)
            {
                if (handle.IsCancelled)
                {
                    yield break;
                }

                runningCoroutines.Add(mono.StartCoroutine(coroutines[i]));
            }

            for (int i = 0; i < runningCoroutines.Count; i++)
            {
                if (handle.IsCancelled)
                {
                    yield break;
                }

                yield return runningCoroutines[i];
            }
        }

        #endregion

        #region Guard

        private static bool ValidateMono(MonoBehaviour mono, string apiName)
        {
            if (mono == null)
            {
                LogKit.LogError($"[CoroutineExtensions] {apiName} 失败: mono 为空");
                return false;
            }

            if (mono.gameObject == null)
            {
                LogKit.LogError(
                    $"[CoroutineExtensions] {apiName} 失败: mono.gameObject 为空, MonoType={mono.GetType().Name}");
                return false;
            }

            return true;
        }

        #endregion
    }

    #region 协程句柄与辅助类

    /// <summary>
    /// 协程句柄
    /// 我负责对历史协程调用提供最小可控取消能力，但不承担更复杂的异步编排职责。
    /// 新业务应优先迁移到 UniTask 或 ActionKit。
    /// </summary>
    public class CoroutineHandle
    {
        private readonly MonoBehaviour _owner;
        private Coroutine _coroutine;
        private bool _isCancelled;

        public Coroutine Coroutine
        {
            get => _coroutine;
            internal set => _coroutine = value;
        }

        public bool IsCancelled => _isCancelled;

        public CoroutineHandle(MonoBehaviour owner)
        {
            if (owner == null)
            {
                LogKit.LogError("[CoroutineHandle] 初始化失败: owner 为空");
                return;
            }

            _owner = owner;
        }

        public void Cancel()
        {
            if (_isCancelled)
            {
                return;
            }

            _isCancelled = true;

            if (_owner == null)
            {
                LogKit.LogError("[CoroutineHandle] Cancel 失败: owner 为空");
                return;
            }

            if (_coroutine != null)
            {
                _owner.StopCoroutine(_coroutine);
            }
        }

        public CoroutineHandle BindToGameObject(GameObject target)
        {
            if (target == null)
            {
                LogKit.LogError("[CoroutineHandle] BindToGameObject 失败: target 为空");
                return this;
            }

            CoroutineCancellationTrigger trigger = target.GetComponent<CoroutineCancellationTrigger>();
            if (trigger == null)
            {
                trigger = target.AddComponent<CoroutineCancellationTrigger>();
            }

            trigger.AddHandle(this);
            return this;
        }
    }

    /// <summary>
    /// 协程取消触发器
    /// 我只负责在宿主物体销毁时回收历史 CoroutineHandle，不承担通用事件系统职责。
    /// </summary>
    internal class CoroutineCancellationTrigger : MonoBehaviour
    {
        private readonly List<CoroutineHandle> _handles = new List<CoroutineHandle>(8);

        public void AddHandle(CoroutineHandle handle)
        {
            if (handle == null)
            {
                LogKit.LogError(
                    $"[CoroutineCancellationTrigger] AddHandle 失败: handle 为空, TriggerObject={gameObject.name}");
                return;
            }

            if (_handles.Contains(handle))
            {
                return;
            }

            _handles.Add(handle);
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _handles.Count; i++)
            {
                _handles[i]?.Cancel();
            }

            _handles.Clear();
        }
    }

    #endregion
}