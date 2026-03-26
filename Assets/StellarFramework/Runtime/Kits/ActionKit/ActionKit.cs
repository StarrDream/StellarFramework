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
    /// 我负责构造链式动作入口，不负责吞掉业务异常。
    /// 这样设计是为了让动作系统保持“只调度、不掩盖错误”的职责边界。
    /// </summary>
    public static class ActionKit
    {
        public static UniActionChain Sequence(GameObject target)
        {
            var chain = PoolKit.Allocate<UniActionChain>();
            chain.SetTarget(target);
            return chain;
        }

        public static UniActionChain Sequence(Component component)
        {
            if (component == null)
            {
                return Sequence((GameObject)null);
            }

            return Sequence(component.gameObject);
        }

        /// <summary>
        /// 快捷延时调用
        /// 我保留这个门面是为了兼容原有调用习惯，但底层依然走统一链式实现。
        /// </summary>
        public static UniActionChain Delay(
            float seconds,
            Action callback,
            GameObject target = null,
            [CallerMemberName] string member = "",
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            return Sequence(target)
                .Delay(seconds)
                .Callback(callback, member, file, line)
                .Start();
        }
    }

    /// <summary>
    /// 动作序列链条
    /// 我通过对象池复用链条实例，因此必须严格限制生命周期，防止回收后继续使用导致脏状态串线。
    /// </summary>
    public sealed class UniActionChain : IPoolable
    {
        private enum ChainState
        {
            None = 0,
            Idle = 1,
            Running = 2,
            Cancelled = 3,
            Completed = 4,
            Recycled = 5
        }

        private GameObject _target;
        private readonly List<Func<CancellationToken, UniTask>> _steps = new List<Func<CancellationToken, UniTask>>(8);

        private Action _onComplete;
        private Action _onCancel;
        private Action<Exception> _onError;

        private CancellationTokenSource _selfCts;
        private bool _ignoreTimeScale;
        private ChainState _state = ChainState.None;
        private int _version;

        public bool IsIgnoreTimeScale => _ignoreTimeScale;

        public void SetTarget(GameObject target)
        {
            EnsureUsable("SetTarget");
            _target = target;
        }

        /// <summary>
        /// 设置是否忽略 TimeScale
        /// 我把这个能力保留在链本体上，避免每个 Tween 节点各自处理时间模式导致行为不一致。
        /// </summary>
        public UniActionChain SetUpdate(bool ignoreTimeScale)
        {
            EnsureUsable("SetUpdate");
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
            _onError = null;
            _ignoreTimeScale = false;
            _state = ChainState.Idle;
            _version++;

            if (_selfCts != null)
            {
                _selfCts.Dispose();
            }

            _selfCts = new CancellationTokenSource();
        }

        public void OnRecycled()
        {
            _steps.Clear();
            _target = null;
            _onComplete = null;
            _onCancel = null;
            _onError = null;
            _ignoreTimeScale = false;
            _state = ChainState.Recycled;
            _version++;

            if (_selfCts != null)
            {
                if (!_selfCts.IsCancellationRequested)
                {
                    _selfCts.Cancel();
                }

                _selfCts.Dispose();
                _selfCts = null;
            }
        }

        #endregion

        #region Build API

        public UniActionChain AppendTask(Func<CancellationToken, UniTask> task)
        {
            EnsureBuildable("AppendTask");

            LogKit.AssertNotNull(task, "[MonoKit] AppendTask 失败: task 不能为空");
            if (task == null)
            {
                return this;
            }

            _steps.Add(task);
            return this;
        }

        public UniActionChain Delay(float seconds)
        {
            EnsureBuildable("Delay");

            if (seconds < 0f)
            {
                LogKit.LogError($"[MonoKit] Delay 失败: seconds 不能小于 0, Seconds={seconds}");
                return this;
            }

            _steps.Add(async token =>
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(seconds),
                    ignoreTimeScale: _ignoreTimeScale,
                    cancellationToken: token);
            });

            return this;
        }

        public UniActionChain DelayFrame(int frames)
        {
            EnsureBuildable("DelayFrame");

            if (frames < 0)
            {
                LogKit.LogError($"[MonoKit] DelayFrame 失败: frames 不能小于 0, Frames={frames}");
                return this;
            }

            _steps.Add(async token => { await UniTask.DelayFrame(frames, cancellationToken: token); });

            return this;
        }

        public UniActionChain Callback(
            Action action,
            [CallerMemberName] string member = "",
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            EnsureBuildable("Callback");

            LogKit.AssertNotNull(action,
                $"[MonoKit] Callback 注册失败: action 为空, File={System.IO.Path.GetFileName(file)}, Line={line}, Member={member}");
            if (action == null)
            {
                return this;
            }

            _steps.Add(token =>
            {
                if (token.IsCancellationRequested)
                {
                    return UniTask.CompletedTask;
                }

                action.Invoke();
                return UniTask.CompletedTask;
            });

            return this;
        }

        public UniActionChain Until(Func<bool> condition)
        {
            EnsureBuildable("Until");

            LogKit.AssertNotNull(condition, "[MonoKit] Until 失败: condition 为空");
            if (condition == null)
            {
                return this;
            }

            _steps.Add(async token => { await UniTask.WaitUntil(condition, cancellationToken: token); });

            return this;
        }

        public UniActionChain Parallel(params Func<CancellationToken, UniTask>[] asyncActions)
        {
            EnsureBuildable("Parallel");

            LogKit.AssertNotNull(asyncActions, "[MonoKit] Parallel 失败: asyncActions 为空");
            if (asyncActions == null || asyncActions.Length == 0)
            {
                return this;
            }

            _steps.Add(async token =>
            {
                var tasks = new UniTask[asyncActions.Length];

                for (int i = 0; i < asyncActions.Length; i++)
                {
                    var action = asyncActions[i];
                    LogKit.AssertNotNull(action, $"[MonoKit] Parallel 失败: 第 {i} 个并行任务为空");
                    if (action == null)
                    {
                        throw new InvalidOperationException($"[MonoKit] Parallel 非法状态: 第 {i} 个任务为空");
                    }

                    tasks[i] = action.Invoke(token);
                }

                await UniTask.WhenAll(tasks);
            });

            return this;
        }

        public UniActionChain OnComplete(Action onComplete)
        {
            EnsureBuildable("OnComplete");
            _onComplete = onComplete;
            return this;
        }

        public UniActionChain OnCancel(Action onCancel)
        {
            EnsureBuildable("OnCancel");
            _onCancel = onCancel;
            return this;
        }

        public UniActionChain OnError(Action<Exception> onError)
        {
            EnsureBuildable("OnError");
            _onError = onError;
            return this;
        }

        #endregion

        #region Runner

        /// <summary>
        /// 手动取消当前序列
        /// 我把取消视为一种正常控制流，而不是错误。
        /// </summary>
        public void Cancel()
        {
            if (_state == ChainState.Recycled)
            {
                LogKit.LogError("[MonoKit] Cancel 失败: 当前链条已回收，禁止继续操作");
                return;
            }

            if (_selfCts == null)
            {
                LogKit.LogError("[MonoKit] Cancel 失败: 当前链条未初始化或已释放");
                return;
            }

            if (_selfCts.IsCancellationRequested)
            {
                return;
            }

            _state = ChainState.Cancelled;
            _selfCts.Cancel();
        }

        /// <summary>
        /// 启动序列
        /// 我禁止同一条链重复 Start，因为池化对象一旦执行结束就会被回收，再次使用会直接污染池状态。
        /// </summary>
        public UniActionChain Start()
        {
            EnsureRunnable("Start");

            if (_target != null && _target.ToString() == "null")
            {
                LogKit.LogError("[MonoKit] Start 失败: 绑定的目标对象已销毁");
                PoolKit.Recycle(this);
                return this;
            }

            RunAsync(_version).Forget();
            return this;
        }

        /// <summary>
        /// 以 await 方式运行
        /// 我同样禁止重复 Await，与 Start 保持完全一致的生命周期语义。
        /// </summary>
        public async UniTask Await()
        {
            EnsureRunnable("Await");
            await RunAsync(_version);
        }

        private async UniTask RunAsync(int runVersion)
        {
            _state = ChainState.Running;

            CancellationToken targetToken = _target != null
                ? _target.GetCancellationTokenOnDestroy()
                : CancellationToken.None;

            CancellationToken selfToken = _selfCts.Token;

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(targetToken, selfToken);
            CancellationToken token = linkedCts.Token;

            try
            {
                for (int i = 0; i < _steps.Count; i++)
                {
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    var step = _steps[i];
                    LogKit.AssertNotNull(step, $"[MonoKit] 执行失败: Step 为空, StepIndex={i}, Version={runVersion}");
                    if (step == null)
                    {
                        throw new InvalidOperationException(
                            $"[MonoKit] 非法状态: Step 为空, StepIndex={i}, Version={runVersion}");
                    }

                    await step.Invoke(token);
                }

                if (!token.IsCancellationRequested)
                {
                    _state = ChainState.Completed;
                    _onComplete?.Invoke();
                }
                else
                {
                    _state = ChainState.Cancelled;
                    _onCancel?.Invoke();
                }
            }
            catch (OperationCanceledException)
            {
                _state = ChainState.Cancelled;
                _onCancel?.Invoke();
            }
            catch (Exception ex)
            {
                _state = ChainState.Completed;

                if (_onError != null)
                {
                    _onError.Invoke(ex);
                }

                LogKit.LogError(
                    $"[MonoKit] Chain 执行失败: Target={_target?.name ?? "null"}, Version={runVersion}, State={_state}, Exception={ex}");
                throw;
            }
            finally
            {
                if (_state != ChainState.Recycled)
                {
                    PoolKit.Recycle(this);
                }
            }
        }

        #endregion

        #region Guard

        private void EnsureUsable(string apiName)
        {
            if (_state == ChainState.Recycled)
            {
                LogKit.LogError($"[MonoKit] {apiName} 失败: 当前链条已回收，禁止继续使用");
                return;
            }

            if (_selfCts == null)
            {
                LogKit.LogError($"[MonoKit] {apiName} 失败: 当前链条未初始化，可能未从对象池正确分配");
            }
        }

        private void EnsureBuildable(string apiName)
        {
            EnsureUsable(apiName);

            LogKit.Assert(
                _state == ChainState.Idle,
                $"[MonoKit] {apiName} 非法调用: 仅允许在 Idle 状态构建链条, CurrentState={_state}");
        }

        private void EnsureRunnable(string apiName)
        {
            EnsureUsable(apiName);

            LogKit.Assert(
                _state == ChainState.Idle,
                $"[MonoKit] {apiName} 非法调用: 同一条链禁止重复启动或重复等待, CurrentState={_state}, Target={_target?.name ?? "null"}");
        }

        #endregion
    }
}