using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Cysharp.Threading.Tasks;
using StellarFramework.Pool;
using UnityEngine;

namespace StellarFramework
{
    /// <summary>
    /// [Stellar] MonoKit v3.2 (Commercial Stable)
    /// 职责：负责异步流程控制 (Sequence, Delay, Callback, Parallel)
    /// </summary>
    public static class MonoKit
    {
        public static UniActionChain Sequence(GameObject target)
        {
            // 商业化检查：如果目标已经销毁，直接返回一个"死"链条，防止后续逻辑报错
            if (target == null) return PoolKit.Allocate<UniActionChain>();

            var chain = PoolKit.Allocate<UniActionChain>();
            chain.SetTarget(target);
            return chain;
        }

        public static UniActionChain Sequence(Component component)
        {
            if (component == null) return Sequence((GameObject)null);
            return Sequence(component.gameObject);
        }

        public static void Delay(float seconds, Action callback, GameObject target = null,
            [CallerMemberName] string member = "", [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            Sequence(target)
                .Delay(seconds)
                .Callback(callback, member, file, line)
                .Start();
        }
    }

    public class UniActionChain : IPoolable
    {
        private GameObject _target;
        private readonly List<Func<CancellationToken, UniTask>> _steps = new List<Func<CancellationToken, UniTask>>(8);
        private Action _onComplete;
        private Action _onCancel;
        private CancellationTokenSource _selfCts;
        private bool _ignoreTimeScale = false; //全局时间缩放控制

        public void SetTarget(GameObject target)
        {
            _target = target;
        }

        /// <summary>
        /// 设置整个序列是否忽略 TimeScale (用于 UI 动画)
        /// </summary>
        public UniActionChain SetUpdate(bool ignoreTimeScale)
        {
            _ignoreTimeScale = ignoreTimeScale;
            return this;
        }

        #region IPoolable

        public void OnAllocated()
        {
            _steps.Clear();
            _target = null;
            _onComplete = null;
            _onCancel = null;
            _ignoreTimeScale = false;
            _selfCts = new CancellationTokenSource();
        }

        public void OnRecycled()
        {
            _steps.Clear();
            _target = null;
            _onComplete = null;
            _onCancel = null;
            if (_selfCts != null)
            {
                _selfCts.Cancel();
                _selfCts.Dispose();
                _selfCts = null;
            }
        }

        #endregion

        #region API

        public UniActionChain AppendTask(Func<CancellationToken, UniTask> task)
        {
            _steps.Add(task);
            return this;
        }

        public UniActionChain Delay(float seconds)
        {
            _steps.Add(async (token) =>
            {
                // 支持 ignoreTimeScale
                await UniTask.Delay(TimeSpan.FromSeconds(seconds), ignoreTimeScale: _ignoreTimeScale, cancellationToken: token);
            });
            return this;
        }

        public UniActionChain DelayFrame(int frames)
        {
            _steps.Add(async (token) => { await UniTask.DelayFrame(frames, cancellationToken: token); });
            return this;
        }

        public UniActionChain Callback(Action action,
            [CallerMemberName] string member = "", [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            _steps.Add((token) =>
            {
                try
                {
                    if (!token.IsCancellationRequested) action?.Invoke();
                    return UniTask.CompletedTask;
                }
                catch (Exception ex)
                {
                    LogKit.LogError($"[MonoKit] Action Error at {System.IO.Path.GetFileName(file)}:{line} ({member})\n{ex}");
                    throw; // 抛出异常以中断序列
                }
            });
            return this;
        }

        public UniActionChain Until(Func<bool> condition)
        {
            _steps.Add(async (token) =>
            {
                // WaitUntil 默认受 TimeScale 影响，这里暂不处理，因为 UniTask.WaitUntil 没有 ignoreTimeScale 参数
                // 如果需要，可以用 While 循环实现
                await UniTask.WaitUntil(condition, cancellationToken: token);
            });
            return this;
        }

        public UniActionChain Parallel(params Func<CancellationToken, UniTask>[] asyncActions)
        {
            _steps.Add(async (token) =>
            {
                var tasks = new List<UniTask>(asyncActions.Length);
                foreach (var act in asyncActions)
                {
                    tasks.Add(act.Invoke(token));
                }

                await UniTask.WhenAll(tasks);
            });
            return this;
        }

        public UniActionChain OnComplete(Action onComplete)
        {
            _onComplete = onComplete;
            return this;
        }

        public UniActionChain OnCancel(Action onCancel)
        {
            _onCancel = onCancel;
            return this;
        }

        #endregion

        #region Runner

        public void Start()
        {
            // 如果 Target 一开始就是空的（且不是故意传null的情况），直接回收
            // 这里假设 SetTarget(null) 是合法的（全局延时），但 SetTarget(destroyedObject) 是不合法的
            if (_target != null && _target.ToString() == "null") // Unity 假死对象检查
            {
                PoolKit.Recycle(this);
                return;
            }

            RunAsync().Forget();
        }

        public async UniTask Await()
        {
            await RunAsync();
        }

        private async UniTask RunAsync()
        {
            CancellationToken targetToken = _target != null ? _target.GetCancellationTokenOnDestroy() : CancellationToken.None;
            CancellationToken selfToken = _selfCts.Token;

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(targetToken, selfToken);
            var token = linkedCts.Token;

            try
            {
                foreach (var step in _steps)
                {
                    if (token.IsCancellationRequested) break;
                    await step.Invoke(token);
                }

                if (!token.IsCancellationRequested)
                {
                    _onComplete?.Invoke();
                }
            }
            catch (OperationCanceledException)
            {
                _onCancel?.Invoke();
            }
            catch (Exception ex)
            {
                LogKit.LogError($"[MonoKit] Chain Error: {ex}");
            }
            finally
            {
                PoolKit.Recycle(this);
            }
        }

        // 供 TweenKit 访问的内部属性
        public bool IsIgnoreTimeScale => _ignoreTimeScale;

        #endregion
    }
}