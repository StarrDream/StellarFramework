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

        public float Duration => Time.time - StateStartTime;

        public virtual void OnInit(FSM<T> fsm, T owner)
        {
            FSM = fsm;
            Owner = owner;
        }

        /// <summary>
        /// 我统一在状态切换入口刷新起始时间，避免子类遗漏。
        /// </summary>
        internal void InternalRecordStartTime()
        {
            StateStartTime = Time.time;
        }

        public virtual void OnEnter()
        {
        }

        public virtual void OnUpdate()
        {
        }

        public virtual void OnFixedUpdate()
        {
        }

        public virtual void OnExit()
        {
        }

        public virtual void OnGUI()
        {
        }
    }

    #endregion

    #region 状态机驱动器

    /// <summary>
    /// 有限状态机
    /// 我保持它纯 C#、轻量、低分配，但必须显式阻断状态切换重入，否则业务状态会被污染。
    /// </summary>
    /// <typeparam name="T">持有者类型</typeparam>
    public class FSM<T>
    {
        public T Owner { get; private set; }
        public FSMState<T> CurrentState { get; private set; }
        public FSMState<T> PreviousState { get; private set; }

        private readonly Dictionary<Type, FSMState<T>> _stateCache = new Dictionary<Type, FSMState<T>>();

        private bool _isTransitioning;
        private bool _isCleared;

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
        /// 注册状态（手动注册实例）
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
        /// 注册状态（自动创建实例）
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
        /// 无参切换状态
        /// </summary>
        public void ChangeState<TState>() where TState : FSMState<T>
        {
            Type type = typeof(TState);
            if (!TryPrepareChangeState(type, out FSMState<T> newState))
            {
                return;
            }

            ExecuteStateChange(newState);
            CurrentState.OnEnter();
            _isTransitioning = false;
        }

        /// <summary>
        /// 带参切换状态
        /// </summary>
        public void ChangeState<TState, TPayload>(TPayload payload)
            where TState : FSMState<T>, IPayloadState<TPayload>
        {
            Type type = typeof(TState);
            if (!TryPrepareChangeState(type, out FSMState<T> newState))
            {
                return;
            }

            ExecuteStateChange(newState);
            ((IPayloadState<TPayload>)CurrentState).OnEnter(payload);
            _isTransitioning = false;
        }

        /// <summary>
        /// 返回上一个状态
        /// </summary>
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

            FSMState<T> targetState = PreviousState;
            FSMState<T> leavingState = CurrentState;

            leavingState?.OnExit();

            PreviousState = leavingState;
            CurrentState = targetState;
            CurrentState.InternalRecordStartTime();
            CurrentState.OnEnter();

            _isTransitioning = false;
        }

        #region 驱动方法

        public void OnUpdate()
        {
            if (!EnsureUsable("OnUpdate"))
            {
                return;
            }

            CurrentState?.OnUpdate();
        }

        public void OnFixedUpdate()
        {
            if (!EnsureUsable("OnFixedUpdate"))
            {
                return;
            }

            CurrentState?.OnFixedUpdate();
        }

        public void OnGUI()
        {
            if (!EnsureUsable("OnGUI"))
            {
                return;
            }

            CurrentState?.OnGUI();
        }

        #endregion

        public string CurrentStateName => CurrentState != null ? CurrentState.GetType().Name : "None";

        /// <summary>
        /// 清理状态机
        /// 我显式进入 cleared 状态，防止外部还拿着引用继续驱动。
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