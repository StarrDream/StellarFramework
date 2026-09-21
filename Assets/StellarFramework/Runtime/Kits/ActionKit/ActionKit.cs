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
    /// </summary>
    public static class ActionKit
    {
        /// <summary>
        /// 为指定 GameObject 创建一个新的动作链。
        /// 链会在 Start/Await 完成后自动回收到 PoolKit，回收后禁止继续持有或复用该实例。
        /// </summary>
        /// <param name="target">动作生命周期宿主；宿主销毁会取消正在运行的链。</param>
        /// <returns>可继续追加步骤的链；target 为空时返回 null。</returns>
        public static UniActionChain Sequence(GameObject target)
        {
            if (target == null)
            {
                LogKit.LogError("[ActionKit] Sequence(GameObject) 失败: target 为空");
                return null;
            }

            UniActionChain chain = PoolKit.Allocate<UniActionChain>();
            chain.SetTarget(target);
            return chain;
        }

        /// <summary>
        /// 以组件所在 GameObject 作为生命周期宿主创建动作链。
        /// </summary>
        public static UniActionChain Sequence(Component component)
        {
            if (component == null || component.gameObject == null)
            {
                LogKit.LogError("[ActionKit] Sequence(Component) 失败: component 或 gameObject 为空");
                return null;
            }

            return Sequence(component.gameObject);
        }

        /// <summary>
        /// 创建并立即启动“等待指定秒数后执行回调”的短链。
        /// </summary>
        /// <remarks>
        /// 必须提供 target，用于自动取消和生命周期收口。业务异常不会被静默吞掉。
        /// </remarks>
        public static UniActionChain Delay(
            float seconds,
            Action callback,
            GameObject target = null,
            [CallerMemberName] string member = "",
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            if (target == null)
            {
                LogKit.LogError(
                    $"[ActionKit] Delay 失败: target 为空, File={System.IO.Path.GetFileName(file)}, Line={line}, Member={member}");
                return null;
            }

            UniActionChain chain = Sequence(target);
            if (chain == null)
            {
                return null;
            }

            return chain
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
            Faulted = 5,
            Recycled = 6
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

        /// <summary>
        /// 当前链的时间等待/补间是否忽略 Time.timeScale。
        /// </summary>
        public bool IsIgnoreTimeScale => _ignoreTimeScale;

        /// <summary>
        /// 设置链的生命周期宿主。仅允许在 Idle 状态调用。
        /// </summary>
        public void SetTarget(GameObject target)
        {
            if (!EnsureUsable("SetTarget"))
            {
                return;
            }

            if (_state != ChainState.Idle)
            {
                LogKit.LogError(
                    $"[UniActionChain] SetTarget 失败: 仅允许 Idle 状态设置目标, CurrentState={_state}, Target={target?.name ?? "null"}");
                return;
            }

            if (target == null)
            {
                LogKit.LogError("[UniActionChain] SetTarget 失败: target 为空");
                return;
            }

            _target = target;
        }

        /// <summary>
        /// 设置后续时间型步骤是否使用 unscaled time。
        /// 必须在 Start/Await 之前调用。
        /// </summary>
        public UniActionChain SetUpdate(bool ignoreTimeScale)
        {
            if (!EnsureBuildable("SetUpdate"))
            {
                return this;
            }

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

        /// <summary>
        /// 向序列尾部追加自定义异步步骤。
        /// </summary>
        /// <param name="task">接收链级 CancellationToken 的异步任务。</param>
        public UniActionChain AppendTask(Func<CancellationToken, UniTask> task)
        {
            if (!EnsureBuildable("AppendTask"))
            {
                return this;
            }

            if (task == null)
            {
                LogKit.LogError("[UniActionChain] AppendTask 失败: task 为空");
                return this;
            }

            _steps.Add(task);
            return this;
        }

        /// <summary>
        /// 追加秒级延时。seconds 必须大于等于 0。
        /// </summary>
        public UniActionChain Delay(float seconds)
        {
            if (!EnsureBuildable("Delay"))
            {
                return this;
            }

            if (seconds < 0f)
            {
                LogKit.LogError(
                    $"[UniActionChain] Delay 失败: seconds 非法, Seconds={seconds}, Target={_target?.name ?? "null"}");
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

        /// <summary>
        /// 追加帧延时。frames 必须大于等于 0。
        /// </summary>
        public UniActionChain DelayFrame(int frames)
        {
            if (!EnsureBuildable("DelayFrame"))
            {
                return this;
            }

            if (frames < 0)
            {
                LogKit.LogError(
                    $"[UniActionChain] DelayFrame 失败: frames 非法, Frames={frames}, Target={_target?.name ?? "null"}");
                return this;
            }

            _steps.Add(async token => { await UniTask.DelayFrame(frames, cancellationToken: token); });

            return this;
        }

        /// <summary>
        /// 追加同步回调步骤。
        /// </summary>
        public UniActionChain Callback(
            Action action,
            [CallerMemberName] string member = "",
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            if (!EnsureBuildable("Callback"))
            {
                return this;
            }

            if (action == null)
            {
                LogKit.LogError(
                    $"[UniActionChain] Callback 注册失败: action 为空, File={System.IO.Path.GetFileName(file)}, Line={line}, Member={member}");
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

        /// <summary>
        /// 等待条件返回 true 后继续后续步骤。
        /// </summary>
        /// <remarks>条件在 PlayerLoop 中轮询，不应放置昂贵逻辑或持续产生 GC 的表达式。</remarks>
        public UniActionChain Until(Func<bool> condition)
        {
            if (!EnsureBuildable("Until"))
            {
                return this;
            }

            if (condition == null)
            {
                LogKit.LogError($"[UniActionChain] Until 失败: condition 为空, Target={_target?.name ?? "null"}");
                return this;
            }

            _steps.Add(async token => { await UniTask.WaitUntil(condition, cancellationToken: token); });

            return this;
        }

        /// <summary>
        /// 将多个 UniTask 并行执行，并等待全部完成后继续序列。
        /// </summary>
        /// <remarks>
        /// 当前实现会为任务数组分配一个 UniTask[]；高频路径应避免每帧动态构造 Parallel。
        /// </remarks>
        public UniActionChain Parallel(params Func<CancellationToken, UniTask>[] asyncActions)
        {
            if (!EnsureBuildable("Parallel"))
            {
                return this;
            }

            if (asyncActions == null || asyncActions.Length == 0)
            {
                LogKit.LogError($"[UniActionChain] Parallel 失败: asyncActions 为空, Target={_target?.name ?? "null"}");
                return this;
            }

            for (int i = 0; i < asyncActions.Length; i++)
            {
                if (asyncActions[i] == null)
                {
                    LogKit.LogError($"[UniActionChain] Parallel 失败: 第 {i} 个任务为空, Target={_target?.name ?? "null"}");
                    return this;
                }
            }

            _steps.Add(async token =>
            {
                UniTask[] tasks = new UniTask[asyncActions.Length];
                for (int i = 0; i < asyncActions.Length; i++)
                {
                    tasks[i] = asyncActions[i].Invoke(token);
                }

                await UniTask.WhenAll(tasks);
            });

            return this;
        }

        /// <summary>
        /// 注册链正常完成后的回调。
        /// </summary>
        public UniActionChain OnComplete(Action onComplete)
        {
            if (!EnsureBuildable("OnComplete"))
            {
                return this;
            }

            _onComplete = onComplete;
            return this;
        }

        /// <summary>
        /// 注册链因宿主销毁或主动 Cancel 而取消后的回调。
        /// </summary>
        public UniActionChain OnCancel(Action onCancel)
        {
            if (!EnsureBuildable("OnCancel"))
            {
                return this;
            }

            _onCancel = onCancel;
            return this;
        }

        /// <summary>
        /// 注册步骤抛出异常时的观察回调。
        /// 该回调不会吞掉异常。
        /// </summary>
        public UniActionChain OnError(Action<Exception> onError)
        {
            if (!EnsureBuildable("OnError"))
            {
                return this;
            }

            _onError = onError;
            return this;
        }

        #endregion

        #region Runner

        /// <summary>
        /// 请求取消当前链。
        /// 已完成、已取消或已故障的链重复调用不会产生额外效果。
        /// </summary>
        public void Cancel()
        {
            if (!EnsureUsable("Cancel"))
            {
                return;
            }

            if (_state == ChainState.Completed || _state == ChainState.Cancelled || _state == ChainState.Faulted)
            {
                return;
            }

            if (_selfCts == null)
            {
                LogKit.LogError(
                    $"[UniActionChain] Cancel 失败: _selfCts 为空, Target={_target?.name ?? "null"}, State={_state}");
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
        /// 以 fire-and-forget 方式启动动作链。
        /// </summary>
        /// <returns>返回自身仅用于链式书写；运行完成后实例会自动回池，调用方不得继续使用。</returns>
        public UniActionChain Start()
        {
            if (!EnsureRunnable("Start"))
            {
                return this;
            }

            if (_target == null)
            {
                LogKit.LogError("[UniActionChain] Start 失败: target 为空");
                PoolKit.Recycle(this);
                return this;
            }

            _state = ChainState.Running;
            RunAsync(_version).Forget();
            return this;
        }

        /// <summary>
        /// 启动动作链并异步等待结束。
        /// </summary>
        /// <remarks>同一条链只能 Start 或 Await 一次；完成后自动回池。</remarks>
        public async UniTask Await()
        {
            if (!EnsureRunnable("Await"))
            {
                return;
            }

            if (_target == null)
            {
                LogKit.LogError("[UniActionChain] Await 失败: target 为空");
                PoolKit.Recycle(this);
                return;
            }

            _state = ChainState.Running;
            await RunAsync(_version);
        }

        private async UniTask RunAsync(int runVersion)
        {
            if (_target == null)
            {
                LogKit.LogError($"[UniActionChain] RunAsync 失败: _target 为空, Version={runVersion}");
                _state = ChainState.Faulted;
                PoolKit.Recycle(this);
                return;
            }

            CancellationToken targetToken = _target.GetCancellationTokenOnDestroy();
            CancellationToken selfToken = _selfCts.Token;

            using CancellationTokenSource linkedCts =
                CancellationTokenSource.CreateLinkedTokenSource(targetToken, selfToken);

            CancellationToken token = linkedCts.Token;

            try
            {
                for (int i = 0; i < _steps.Count; i++)
                {
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    Func<CancellationToken, UniTask> step = _steps[i];
                    if (step == null)
                    {
                        LogKit.LogError(
                            $"[UniActionChain] 执行失败: step 为空, StepIndex={i}, Target={_target?.name ?? "null"}, Version={runVersion}");
                        _state = ChainState.Faulted;
                        return;
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
                _state = ChainState.Faulted;
                _onError?.Invoke(ex);
                LogKit.LogError(
                    $"[UniActionChain] 执行失败: Target={_target?.name ?? "null"}, Version={runVersion}, State={_state}, Exception={ex}");
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

        private bool EnsureUsable(string apiName)
        {
            if (_state == ChainState.Recycled)
            {
                LogKit.LogError($"[UniActionChain] {apiName} 失败: 当前链条已回收，禁止继续使用");
                return false;
            }

            if (_selfCts == null)
            {
                LogKit.LogError($"[UniActionChain] {apiName} 失败: 当前链条未初始化, State={_state}");
                return false;
            }

            return true;
        }

        private bool EnsureBuildable(string apiName)
        {
            if (!EnsureUsable(apiName))
            {
                return false;
            }

            if (_state != ChainState.Idle)
            {
                LogKit.LogError(
                    $"[UniActionChain] {apiName} 非法调用: 仅允许在 Idle 状态构建链条, CurrentState={_state}, Target={_target?.name ?? "null"}");
                return false;
            }

            return true;
        }

        private bool EnsureRunnable(string apiName)
        {
            if (!EnsureUsable(apiName))
            {
                return false;
            }

            if (_state != ChainState.Idle)
            {
                LogKit.LogError(
                    $"[UniActionChain] {apiName} 非法调用: 同一条链禁止重复启动或重复等待, CurrentState={_state}, Target={_target?.name ?? "null"}");
                return false;
            }

            if (_steps.Count == 0)
            {
                LogKit.LogError($"[UniActionChain] {apiName} 失败: 当前链条没有任何步骤, Target={_target?.name ?? "null"}");
                return false;
            }

            return true;
        }

        #endregion
    }
}