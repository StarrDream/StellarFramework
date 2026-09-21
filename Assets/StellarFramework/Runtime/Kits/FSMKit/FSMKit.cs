using System;
using System.Collections.Generic;
using UnityEngine;

namespace StellarFramework.FSM
{
    #region 核心接口与基类

    /// <summary>
    /// 携带参数的状态接口
    /// 我要求参数切换走显式泛型接口，避免 object 弱类型与装箱。
    /// </summary>
    /// <typeparam name="TPayload">参数载荷类型</typeparam>
    public interface IPayloadState<TPayload>
    {
        /// <summary>
        /// 进入状态时接收强类型载荷。
        /// </summary>
        /// <param name="payload">本次状态切换携带的数据。</param>
        void OnEnter(TPayload payload);
    }

    /// <summary>
    /// 状态基类
    /// </summary>
    /// <typeparam name="T">持有者类型</typeparam>
    public abstract class FSMState<T>
    {
        protected FSM<T> FSM;
        protected T Owner;
        protected float StateStartTime;

        /// <summary>
        /// 当前状态自最近一次进入起已经持续的秒数。
        /// 使用 Time.time，因此受 timeScale 影响。
        /// </summary>
        public float Duration => Time.time - StateStartTime;

        /// <summary>
        /// 状态首次注册进 FSM 时调用一次。
        /// </summary>
        /// <param name="fsm">所属状态机。</param>
        /// <param name="owner">状态机持有者。</param>
        public virtual void OnInit(FSM<T> fsm, T owner)
        {
            FSM = fsm;
            Owner = owner;
        }

        /// <summary>
        /// 状态切换入口统一刷新起始时间，避免子类遗漏。
        /// </summary>
        internal void InternalRecordStartTime()
        {
            StateStartTime = Time.time;
        }

        /// <summary>无参进入回调。</summary>
        public virtual void OnEnter()
        {
        }

        /// <summary>由外部驱动器每帧调用。</summary>
        public virtual void OnUpdate()
        {
        }

        /// <summary>由外部驱动器在 FixedUpdate 中调用。</summary>
        public virtual void OnFixedUpdate()
        {
        }

        /// <summary>离开当前状态前调用。</summary>
        public virtual void OnExit()
        {
        }

        /// <summary>由外部驱动器在 OnGUI 中调用。</summary>
        public virtual void OnGUI()
        {
        }
    }

    #endregion

    #region 状态机驱动器

    /// <summary>
    /// 轻量纯 C# 有限状态机。
    /// 状态实例注册后会复用，不在每次切换时重复创建。
    /// </summary>
    /// <remarks>
    /// 状态切换是同步操作，并显式阻断 OnEnter/OnExit 中再次 ChangeState 的重入。
    /// FSM 不自动挂接 MonoBehaviour 生命周期，调用方需要自己转发 Update/FixedUpdate/OnGUI。
    /// Clear 后实例进入不可复用状态。
    /// </remarks>
    /// <typeparam name="T">持有者类型</typeparam>
    public class FSM<T>
    {
        /// <summary>状态机持有者。</summary>
        public T Owner { get; private set; }
        /// <summary>当前状态；尚未进入任何状态时为 null。</summary>
        public FSMState<T> CurrentState { get; private set; }
        /// <summary>最近一次离开的状态；用于 RevertToPreviousState。</summary>
        public FSMState<T> PreviousState { get; private set; }

        private readonly Dictionary<Type, FSMState<T>> _stateCache = new Dictionary<Type, FSMState<T>>();

        private bool _isTransitioning;
        private bool _isCleared;

        /// <summary>
        /// 创建状态机。owner 不能为空。
        /// </summary>
        public FSM(T owner)
        {
            if (owner == null)
            {
                Debug.LogError($"[FSM] 初始化失败: 传入的 Owner 为空, OwnerType={typeof(T).Name}");
                _isCleared = true;
                return;
            }

            Owner = owner;
        }

        /// <summary>
        /// 注册已有状态实例。重复注册同一具体类型会被忽略。
        /// </summary>
        public void AddState(FSMState<T> state)
        {
            if (!EnsureUsable("AddState"))
            {
                return;
            }

            if (state == null)
            {
                Debug.LogError($"[FSM] AddState 失败: state 为空, OwnerType={typeof(T).Name}");
                return;
            }

            Type type = state.GetType();
            if (_stateCache.ContainsKey(type))
            {
                Debug.LogWarning($"[FSM] AddState 已忽略: 重复注册状态, StateType={type.Name}, OwnerType={typeof(T).Name}");
                return;
            }

            state.OnInit(this, Owner);
            _stateCache.Add(type, state);
        }

        /// <summary>
        /// 通过无参构造创建并注册状态。每个具体状态类型只保留一个实例。
        /// </summary>
        public void AddState<TState>() where TState : FSMState<T>, new()
        {
            if (!EnsureUsable("AddState<TState>"))
            {
                return;
            }

            Type type = typeof(TState);
            if (_stateCache.ContainsKey(type))
            {
                return;
            }

            TState state = new TState();
            AddState(state);
        }

        /// <summary>
        /// 切换到已注册的无参状态。
        /// </summary>
        /// <remarks>切换顺序为旧状态 OnExit -> 更新 Current/Previous -> 新状态 OnEnter。</remarks>
        public void ChangeState<TState>() where TState : FSMState<T>
        {
            Type type = typeof(TState);
            if (!TryPrepareChangeState(type, out FSMState<T> newState))
            {
                return;
            }

            try
            {
                ExecuteStateChange(newState);
                CurrentState.OnEnter();
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        /// <summary>
        /// 切换到实现 <see cref="IPayloadState{TPayload}"/> 的状态，并传入强类型载荷。
        /// </summary>
        public void ChangeState<TState, TPayload>(TPayload payload)
            where TState : FSMState<T>, IPayloadState<TPayload>
        {
            Type type = typeof(TState);
            if (!TryPrepareChangeState(type, out FSMState<T> newState))
            {
                return;
            }

            try
            {
                ExecuteStateChange(newState);
                ((IPayloadState<TPayload>)CurrentState).OnEnter(payload);
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        /// <summary>
        /// 返回最近一次离开的状态。
        /// </summary>
        /// <remarks>这是一次真正的状态切换，因此当前状态会成为新的 PreviousState。</remarks>
        public void RevertToPreviousState()
        {
            if (!EnsureUsable("RevertToPreviousState"))
            {
                return;
            }

            if (_isTransitioning)
            {
                Debug.LogError(
                    $"[FSM] RevertToPreviousState 失败: 检测到状态切换重入, CurrentState={CurrentStateName}, OwnerType={typeof(T).Name}");
                return;
            }

            if (PreviousState == null)
            {
                Debug.LogWarning(
                    $"[FSM] RevertToPreviousState 已忽略: PreviousState 为空, CurrentState={CurrentStateName}, OwnerType={typeof(T).Name}");
                return;
            }

            if (ReferenceEquals(PreviousState, CurrentState))
            {
                Debug.LogWarning(
                    $"[FSM] RevertToPreviousState 已忽略: PreviousState 与 CurrentState 相同, StateType={CurrentStateName}, OwnerType={typeof(T).Name}");
                return;
            }

            _isTransitioning = true;
            try
            {
                FSMState<T> targetState = PreviousState;
                FSMState<T> leavingState = CurrentState;

                leavingState?.OnExit();

                PreviousState = leavingState;
                CurrentState = targetState;
                CurrentState.InternalRecordStartTime();
                CurrentState.OnEnter();
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        #region 驱动方法

        /// <summary>把外部 Update 驱动转发给当前状态。</summary>
        public void OnUpdate()
        {
            if (!EnsureUsable("OnUpdate"))
            {
                return;
            }

            CurrentState?.OnUpdate();
        }

        /// <summary>把外部 FixedUpdate 驱动转发给当前状态。</summary>
        public void OnFixedUpdate()
        {
            if (!EnsureUsable("OnFixedUpdate"))
            {
                return;
            }

            CurrentState?.OnFixedUpdate();
        }

        /// <summary>把外部 OnGUI 驱动转发给当前状态。</summary>
        public void OnGUI()
        {
            if (!EnsureUsable("OnGUI"))
            {
                return;
            }

            CurrentState?.OnGUI();
        }

        #endregion

        /// <summary>
        /// 当前状态的类型名；没有当前状态时返回 "None"。
        /// </summary>
        public string CurrentStateName => CurrentState != null ? CurrentState.GetType().Name : "None";

        /// <summary>
        /// 清空状态缓存和 Owner，并将状态机永久标记为 cleared。
        /// 调用后外部应丢弃该 FSM 实例，不要重新注册或驱动。
        /// </summary>
        public void Clear()
        {
            _stateCache.Clear();
            CurrentState = null;
            PreviousState = null;
            Owner = default;
            _isTransitioning = false;
            _isCleared = true;
        }

        private bool TryPrepareChangeState(Type targetType, out FSMState<T> newState)
        {
            newState = null;

            if (!EnsureUsable("ChangeState"))
            {
                return false;
            }

            if (targetType == null)
            {
                Debug.LogError(
                    $"[FSM] ChangeState 失败: targetType 为空, OwnerType={typeof(T).Name}, CurrentState={CurrentStateName}");
                return false;
            }

            if (_isTransitioning)
            {
                Debug.LogError(
                    $"[FSM] ChangeState 失败: 检测到状态切换重入, TargetState={targetType.Name}, CurrentState={CurrentStateName}, OwnerType={typeof(T).Name}");
                return false;
            }

            if (CurrentState != null && CurrentState.GetType() == targetType)
            {
                return false;
            }

            if (!_stateCache.TryGetValue(targetType, out newState))
            {
                Debug.LogError(
                    $"[FSM] ChangeState 失败: 试图切换到未注册状态, TargetState={targetType.Name}, CurrentState={CurrentStateName}, OwnerType={typeof(T).Name}");
                return false;
            }

            _isTransitioning = true;
            return true;
        }

        private void ExecuteStateChange(FSMState<T> newState)
        {
            if (newState == null)
            {
                Debug.LogError(
                    $"[FSM] ExecuteStateChange 失败: newState 为空, CurrentState={CurrentStateName}, OwnerType={typeof(T).Name}");
                _isTransitioning = false;
                return;
            }

            FSMState<T> oldState = CurrentState;
            oldState?.OnExit();

            PreviousState = oldState;
            CurrentState = newState;
            CurrentState.InternalRecordStartTime();
        }

        private bool EnsureUsable(string apiName)
        {
            if (_isCleared)
            {
                Debug.LogError($"[FSM] {apiName} 失败: 状态机已清理, OwnerType={typeof(T).Name}");
                return false;
            }

            if (Owner == null)
            {
                Debug.LogError(
                    $"[FSM] {apiName} 失败: Owner 为空, OwnerType={typeof(T).Name}, CurrentState={CurrentStateName}");
                return false;
            }

            return true;
        }
    }

    #endregion
}
