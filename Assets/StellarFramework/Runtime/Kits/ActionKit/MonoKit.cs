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
    /// 异步流程控制工具
    /// 提供序列执行、延时、回调、并行执行等功能
    /// </summary>
    public static class MonoKit
    {
        public static UniActionChain Sequence(GameObject target)
        {
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

        /// <summary>
        /// 快捷延时调用
        /// </summary>
        public static UniActionChain Delay(float seconds, Action callback, GameObject target = null,
            [CallerMemberName] string member = "", [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return Sequence(target)
                .Delay(seconds)
                .Callback(callback, member, file, line)
                .Start();
        }
    }

    /// <summary>
    /// 动作序列链条
    /// </summary>
    public class UniActionChain : IPoolable
    {
        private GameObject _target;
        private readonly List<Func<CancellationToken, UniTask>> _steps = new List<Func<CancellationToken, UniTask>>(8);
        private Action _onComplete;
        private Action _onCancel;
        private CancellationTokenSource _selfCts;
        private bool _ignoreTimeScale = false;

        public void SetTarget(GameObject target)
        {
            _target = target;
        }

        /// <summary>
        /// 设置是否忽略 TimeScale (UI动画常用)
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
            _steps.Add(async (token) => { await UniTask.Delay(TimeSpan.FromSeconds(seconds), ignoreTimeScale: _ignoreTimeScale, cancellationToken: token); });
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
                    throw;
                }
            });
            return this;
        }

        public UniActionChain Until(Func<bool> condition)
        {
            _steps.Add(async (token) => { await UniTask.WaitUntil(condition, cancellationToken: token); });
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

        /// <summary>
        /// 手动取消当前序列
        /// </summary>
        public void Cancel()
        {
            if (_selfCts != null && !_selfCts.IsCancellationRequested)
            {
                _selfCts.Cancel();
            }
        }

        /// <summary>
        /// 启动序列
        /// </summary>
        public UniActionChain Start()
        {
            if (_target != null && _target.ToString() == "null")
            {
                PoolKit.Recycle(this);
                return this;
            }

            RunAsync().Forget();
            return this;
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

        public bool IsIgnoreTimeScale => _ignoreTimeScale;

        #endregion
    }
}