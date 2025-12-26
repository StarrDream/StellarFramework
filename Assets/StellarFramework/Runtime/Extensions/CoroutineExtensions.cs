using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StellarFramework
{
    public static class CoroutineExtensions
    {
        #region 基础等待

        public static CoroutineHandle WaitUntil(this MonoBehaviour mono, Func<bool> condition, Action onComplete = null, float pollInterval = 0.033f)
        {
            if (mono == null) throw new ArgumentNullException(nameof(mono));
            if (condition == null) throw new ArgumentNullException(nameof(condition));
            var handle = new CoroutineHandle(mono);
            handle.Coroutine = mono.StartCoroutine(WaitUntilCoroutine(condition, onComplete, pollInterval, handle));
            return handle;
        }

        private static IEnumerator WaitUntilCoroutine(Func<bool> condition, Action onComplete, float pollInterval, CoroutineHandle handle)
        {
            if (condition())
            {
                onComplete?.Invoke();
                yield break;
            }

            var wait = new WaitForSeconds(pollInterval);
            while (!condition())
            {
                if (handle.IsCancelled) yield break;
                yield return wait;
            }

            if (!handle.IsCancelled) onComplete?.Invoke();
        }

        #endregion

        #region 带超时的版本

        public static CoroutineHandle WaitUntilWithTimeout(this MonoBehaviour mono, Func<bool> condition, float timeoutSeconds, Action<bool> onComplete = null,
            float pollInterval = 0.033f)
        {
            if (mono == null) throw new ArgumentNullException(nameof(mono));
            if (condition == null) throw new ArgumentNullException(nameof(condition));
            var handle = new CoroutineHandle(mono);
            handle.Coroutine = mono.StartCoroutine(WaitUntilWithTimeoutCoroutine(condition, timeoutSeconds, onComplete, pollInterval, handle));
            return handle;
        }

        private static IEnumerator WaitUntilWithTimeoutCoroutine(Func<bool> condition, float timeoutSeconds, Action<bool> onComplete, float pollInterval, CoroutineHandle handle)
        {
            float startTime = Time.time;
            var wait = new WaitForSeconds(pollInterval);
            while (!condition())
            {
                if (handle.IsCancelled) yield break;
                if (Time.time - startTime >= timeoutSeconds)
                {
                    if (!handle.IsCancelled) onComplete?.Invoke(false);
                    yield break;
                }

                yield return wait;
            }

            if (!handle.IsCancelled) onComplete?.Invoke(true);
        }

        #endregion

        #region WaitWhile

        public static CoroutineHandle WaitWhile(this MonoBehaviour mono, Func<bool> condition, Action onComplete = null, float pollInterval = 0.033f)
        {
            return mono.WaitUntil(() => !condition(), onComplete, pollInterval);
        }

        #endregion

        #region 多条件等待

        public static CoroutineHandle WaitUntilAll(this MonoBehaviour mono, Action onComplete = null, float pollInterval = 0.033f, params Func<bool>[] conditions)
        {
            if (conditions == null || conditions.Length == 0) throw new ArgumentException("At least one condition is required");
            return mono.WaitUntil(() =>
            {
                foreach (var condition in conditions)
                    if (!condition())
                        return false;
                return true;
            }, onComplete, pollInterval);
        }

        public static CoroutineHandle WaitUntilAny(this MonoBehaviour mono, Action<int> onComplete = null, float pollInterval = 0.033f, params Func<bool>[] conditions)
        {
            if (conditions == null || conditions.Length == 0) throw new ArgumentException("At least one condition is required");
            var handle = new CoroutineHandle(mono);
            handle.Coroutine = mono.StartCoroutine(WaitUntilAnyCoroutine(conditions, onComplete, pollInterval, handle));
            return handle;
        }

        private static IEnumerator WaitUntilAnyCoroutine(Func<bool>[] conditions, Action<int> onComplete, float pollInterval, CoroutineHandle handle)
        {
            var wait = new WaitForSeconds(pollInterval);
            while (true)
            {
                if (handle.IsCancelled) yield break;
                for (int i = 0; i < conditions.Length; i++)
                {
                    if (conditions[i]())
                    {
                        if (!handle.IsCancelled) onComplete?.Invoke(i);
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
            if (mono == null) throw new ArgumentNullException(nameof(mono));
            var handle = new CoroutineHandle(mono);
            handle.Coroutine = mono.StartCoroutine(DelayedCallCoroutine(seconds, callback, handle));
            return handle;
        }

        private static IEnumerator DelayedCallCoroutine(float seconds, Action callback, CoroutineHandle handle)
        {
            yield return new WaitForSeconds(seconds);
            if (!handle.IsCancelled) callback?.Invoke();
        }

        public static CoroutineHandle DelayedCallFrames(this MonoBehaviour mono, int frames, Action callback)
        {
            if (mono == null) throw new ArgumentNullException(nameof(mono));
            var handle = new CoroutineHandle(mono);
            handle.Coroutine = mono.StartCoroutine(DelayedCallFramesCoroutine(frames, callback, handle));
            return handle;
        }

        private static IEnumerator DelayedCallFramesCoroutine(int frames, Action callback, CoroutineHandle handle)
        {
            for (int i = 0; i < frames; i++)
            {
                if (handle.IsCancelled) yield break;
                yield return null;
            }

            if (!handle.IsCancelled) callback?.Invoke();
        }

        #endregion

        #region 重复执行

        public static CoroutineHandle Repeat(this MonoBehaviour mono, int count, float interval, Action<int> action)
        {
            if (mono == null) throw new ArgumentNullException(nameof(mono));
            var handle = new CoroutineHandle(mono);
            handle.Coroutine = mono.StartCoroutine(RepeatCoroutine(count, interval, action, handle));
            return handle;
        }

        private static IEnumerator RepeatCoroutine(int count, float interval, Action<int> action, CoroutineHandle handle)
        {
            var wait = new WaitForSeconds(interval);
            for (int i = 0; i < count; i++)
            {
                if (handle.IsCancelled) yield break;
                action?.Invoke(i);
                if (i < count - 1) yield return wait;
            }
        }

        public static CoroutineHandle RepeatForever(this MonoBehaviour mono, float interval, Action action)
        {
            if (mono == null) throw new ArgumentNullException(nameof(mono));
            var handle = new CoroutineHandle(mono);
            handle.Coroutine = mono.StartCoroutine(RepeatForeverCoroutine(interval, action, handle));
            return handle;
        }

        private static IEnumerator RepeatForeverCoroutine(float interval, Action action, CoroutineHandle handle)
        {
            var wait = new WaitForSeconds(interval);
            while (true)
            {
                if (handle.IsCancelled) yield break;
                action?.Invoke();
                yield return wait;
            }
        }

        #endregion

        #region 序列执行

        public static CoroutineHandle Sequence(this MonoBehaviour mono, params IEnumerator[] coroutines)
        {
            if (mono == null) throw new ArgumentNullException(nameof(mono));
            var handle = new CoroutineHandle(mono);
            handle.Coroutine = mono.StartCoroutine(SequenceCoroutine(coroutines, handle));
            return handle;
        }

        private static IEnumerator SequenceCoroutine(IEnumerator[] coroutines, CoroutineHandle handle)
        {
            foreach (var coroutine in coroutines)
            {
                if (handle.IsCancelled) yield break;
                yield return coroutine;
            }
        }

        #endregion

        #region 并行执行

        public static CoroutineHandle Parallel(this MonoBehaviour mono, params IEnumerator[] coroutines)
        {
            if (mono == null) throw new ArgumentNullException(nameof(mono));
            var handle = new CoroutineHandle(mono);
            handle.Coroutine = mono.StartCoroutine(ParallelCoroutine(mono, coroutines, handle));
            return handle;
        }

        private static IEnumerator ParallelCoroutine(MonoBehaviour mono, IEnumerator[] coroutines, CoroutineHandle handle)
        {
            var runningCoroutines = new List<Coroutine>();
            foreach (var coroutine in coroutines)
            {
                if (handle.IsCancelled) yield break;
                runningCoroutines.Add(mono.StartCoroutine(coroutine));
            }

            foreach (var coroutine in runningCoroutines)
            {
                if (handle.IsCancelled) yield break;
                yield return coroutine;
            }
        }

        #endregion
    }

    #region 协程句柄与辅助类

    public class CoroutineHandle
    {
        private MonoBehaviour _owner;
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
            _owner = owner;
        }

        public void Cancel()
        {
            if (_isCancelled) return;
            _isCancelled = true;
            if (_owner != null && _coroutine != null) _owner.StopCoroutine(_coroutine);
        }

        public CoroutineHandle BindToGameObject(GameObject target)
        {
            if (target == null) return this;
            var trigger = target.GetComponent<CoroutineCancellationTrigger>();
            if (trigger == null) trigger = target.AddComponent<CoroutineCancellationTrigger>();
            trigger.AddHandle(this);
            return this;
        }
    }

    internal class CoroutineCancellationTrigger : MonoBehaviour
    {
        private List<CoroutineHandle> _handles = new List<CoroutineHandle>();

        public void AddHandle(CoroutineHandle handle)
        {
            if (handle != null && !_handles.Contains(handle)) _handles.Add(handle);
        }

        private void OnDestroy()
        {
            foreach (var handle in _handles) handle?.Cancel();
            _handles.Clear();
        }
    }

    #endregion
}