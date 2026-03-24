using System;
using System.Collections.Generic;
using UnityEngine;

namespace StellarFramework.FSM
{
    #region 核心接口与基类

    /// <summary>
    /// [StellarFramework] 携带参数的状态接口
    /// 用于实现 0GC、类型安全的参数传递。若需传递多个参数，请在业务层定义 Struct。
    /// </summary>
    /// <typeparam name="TPayload">参数载荷类型</typeparam>
    public interface IPayloadState<TPayload>
    {
        void OnEnter(TPayload payload);
    }

    /// <summary>
    /// [StellarFramework] 状态基类
    /// </summary>
    /// <typeparam name="T">持有者类型 (Context)</typeparam>
    public abstract class FSMState<T>
    {
        /// <summary>
        /// 状态机引用
        /// </summary>
        protected FSM<T> FSM;

        /// <summary>
        /// 持有者引用
        /// </summary>
        protected T Owner;

        /// <summary>
        /// 进入状态的时间点
        /// </summary>
        protected float StateStartTime;

        /// <summary>
        /// 状态持续时间
        /// </summary>
        public float Duration => Time.time - StateStartTime;

        /// <summary>
        /// 初始化 (仅在加入状态机时调用一次)
        /// </summary>
        public virtual void OnInit(FSM<T> fsm, T owner)
        {
            FSM = fsm;
            Owner = owner;
        }

        /// <summary>
        /// 框架内部调用，统一记录状态开始时间，防止带参重载时遗漏
        /// </summary>
        internal void InternalRecordStartTime()
        {
            StateStartTime = Time.time;
        }

        /// <summary>
        /// 无参进入状态
        /// </summary>
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
    /// [StellarFramework] 有限状态机 (0GC, 纯 C#)
    /// </summary>
    /// <typeparam name="T">持有者类型</typeparam>
    public class FSM<T>
    {
        public T Owner { get; private set; }
        public FSMState<T> CurrentState { get; private set; }
        public FSMState<T> PreviousState { get; private set; }

        private readonly Dictionary<Type, FSMState<T>> _stateCache = new Dictionary<Type, FSMState<T>>();

        public FSM(T owner)
        {
            if (owner == null)
            {
                Debug.LogError($"[FSM] 初始化失败: 传入的 Owner 为空。");
                return;
            }

            Owner = owner;
        }

        /// <summary>
        /// 注册状态 (手动注册实例)
        /// </summary>
        public void AddState(FSMState<T> state)
        {
            if (state == null)
            {
                Debug.LogError($"[FSM] 添加状态失败: 传入的状态实例为空。");
                return;
            }

            Type type = state.GetType();
            if (_stateCache.ContainsKey(type))
            {
                Debug.LogWarning($"[FSM] 状态 {type.Name} 已存在，请勿重复添加。");
                return;
            }

            state.OnInit(this, Owner);
            _stateCache.Add(type, state);
        }

        /// <summary>
        /// 注册状态 (自动创建实例)
        /// </summary>
        public void AddState<TState>() where TState : FSMState<T>, new()
        {
            Type type = typeof(TState);
            if (_stateCache.ContainsKey(type)) return;

            var state = new TState();
            AddState(state);
        }

        /// <summary>
        /// 无参切换状态
        /// </summary>
        public void ChangeState<TState>() where TState : FSMState<T>
        {
            Type type = typeof(TState);
            if (!TryPrepareChangeState(type, out var newState)) return;

            ExecuteStateChange(newState);
            CurrentState.OnEnter();
        }

        /// <summary>
        /// 带参切换状态 (0GC, 强类型约束)
        /// </summary>
        public void ChangeState<TState, TPayload>(TPayload payload)
            where TState : FSMState<T>, IPayloadState<TPayload>
        {
            Type type = typeof(TState);
            if (!TryPrepareChangeState(type, out var newState)) return;

            ExecuteStateChange(newState);

            // 显式转换为接口并传递参数，避免了拆装箱
            ((IPayloadState<TPayload>)CurrentState).OnEnter(payload);
        }

        /// <summary>
        /// 内部流转准备，执行前置校验
        /// </summary>
        private bool TryPrepareChangeState(Type targetType, out FSMState<T> newState)
        {
            newState = null;

            if (CurrentState != null && CurrentState.GetType() == targetType)
            {
                return false; // 忽略同状态重复切换
            }

            if (!_stateCache.TryGetValue(targetType, out newState))
            {
                Debug.LogError($"[FSM] 状态切换失败: 试图切换到未注册的状态 {targetType.Name}。请先调用 AddState 进行注册。");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 执行核心的状态替换与生命周期流转
        /// </summary>
        private void ExecuteStateChange(FSMState<T> newState)
        {
            if (CurrentState != null)
            {
                CurrentState.OnExit();
                PreviousState = CurrentState;
            }

            CurrentState = newState;
            CurrentState.InternalRecordStartTime(); // 统一刷新状态计时器
        }

        /// <summary>
        /// 返回上一个状态
        /// </summary>
        public void RevertToPreviousState()
        {
            if (PreviousState == null)
            {
                Debug.LogWarning("[FSM] 无法回退状态: PreviousState 为空。");
                return;
            }

            var targetState = PreviousState;
            CurrentState?.OnExit();
            PreviousState = CurrentState;
            CurrentState = targetState;

            CurrentState.InternalRecordStartTime();
            CurrentState.OnEnter();
        }

        #region 驱动方法

        public void OnUpdate() => CurrentState?.OnUpdate();
        public void OnFixedUpdate() => CurrentState?.OnFixedUpdate();
        public void OnGUI() => CurrentState?.OnGUI();

        #endregion

        public string CurrentStateName => CurrentState != null ? CurrentState.GetType().Name : "None";

        /// <summary>
        /// 清理 FSM (断开引用，防止内存泄漏)
        /// </summary>
        public void Clear()
        {
            CurrentState = null;
            PreviousState = null;
            _stateCache.Clear();
            Owner = default;
        }
    }

    #endregion
}