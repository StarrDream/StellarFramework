using System;
using System.Collections.Generic;
using UnityEngine;

namespace StellarFramework.FSM
{
    /// <summary>
    /// [StellarFramework] 状态基类
    /// <typeparam name="T">持有者类型 (Context)</typeparam>
    /// </summary>
    public abstract class FSMState<T>
    {
        /// <summary>
        /// 状态机引用
        /// </summary>
        protected FSM<T> FSM;

        /// <summary>
        /// 持有者引用 (如 PlayerController)
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
        /// 进入状态
        /// </summary>
        public virtual void OnEnter()
        {
            StateStartTime = Time.time;
        }

        /// <summary>
        /// 逻辑更新 (Update)
        /// </summary>
        public virtual void OnUpdate()
        {
        }

        /// <summary>
        /// 物理更新 (FixedUpdate)
        /// </summary>
        public virtual void OnFixedUpdate()
        {
        }

        /// <summary>
        /// 退出状态
        /// </summary>
        public virtual void OnExit()
        {
        }

        /// <summary>
        /// GUI 绘制 (仅用于调试)
        /// </summary>
        public virtual void OnGUI()
        {
        }
    }

    /// <summary>
    /// [StellarFramework] 有限状态机 (0GC, 纯 C#)
    /// </summary>
    /// <typeparam name="T">持有者类型</typeparam>
    public class FSM<T>
    {
        /// <summary>
        /// 持有者
        /// </summary>
        public T Owner { get; private set; }

        /// <summary>
        /// 当前状态
        /// </summary>
        public FSMState<T> CurrentState { get; private set; }

        /// <summary>
        /// 上一个状态 (方便回溯)
        /// </summary>
        public FSMState<T> PreviousState { get; private set; }

        // 状态缓存字典
        private readonly Dictionary<Type, FSMState<T>> _stateCache = new Dictionary<Type, FSMState<T>>();

        // 构造函数
        public FSM(T owner)
        {
            Owner = owner;
        }

        /// <summary>
        /// 注册状态 (手动注册，避免反射)
        /// </summary>
        public void AddState(FSMState<T> state)
        {
            if (state == null) return;
            Type type = state.GetType();
            if (_stateCache.ContainsKey(type))
            {
                LogKit.LogWarning($"[FSM] 状态 {type.Name} 已存在，请勿重复添加。");
                return;
            }

            state.OnInit(this, Owner);
            _stateCache.Add(type, state);
        }

        /// <summary>
        /// 注册状态 (自动创建，泛型版本)
        /// </summary>
        public void AddState<TState>() where TState : FSMState<T>, new()
        {
            if (_stateCache.ContainsKey(typeof(TState))) return;
            var state = new TState();
            AddState(state);
        }

        /// <summary>
        /// 切换状态
        /// </summary>
        public void ChangeState<TState>() where TState : FSMState<T>
        {
            Type type = typeof(TState);

            // 1. 检查是否已经在该状态
            if (CurrentState != null && CurrentState.GetType() == type)
            {
                return; // 忽略重复切换
            }

            // 2. 获取目标状态
            if (_stateCache.TryGetValue(type, out var newState))
            {
                // 3. 退出旧状态
                if (CurrentState != null)
                {
                    CurrentState.OnExit();
                    PreviousState = CurrentState;
                }

                // 4. 进入新状态
                CurrentState = newState;
                CurrentState.OnEnter();

                // LogKit.Log($"[FSM] State Changed: {type.Name}");
            }
            else
            {
                LogKit.LogError($"[FSM] 试图切换到未注册的状态: {type.Name}。请先调用 AddState。");
            }
        }

        /// <summary>
        /// 返回上一个状态
        /// </summary>
        public void RevertToPreviousState()
        {
            if (PreviousState != null)
            {
                // 这里利用反射调用 ChangeState 比较麻烦，直接复用逻辑
                var targetState = PreviousState;

                CurrentState?.OnExit();
                PreviousState = CurrentState; // 更新 Previous 为当前（即互换）

                CurrentState = targetState;
                CurrentState.OnEnter();
            }
        }

        #region 驱动方法 (需要在 MonoBehaviour 中调用)

        public void OnUpdate()
        {
            CurrentState?.OnUpdate();
        }

        public void OnFixedUpdate()
        {
            CurrentState?.OnFixedUpdate();
        }

        public void OnGUI()
        {
            CurrentState?.OnGUI();
        }

        #endregion

        /// <summary>
        /// 获取当前状态名称 (调试用)
        /// </summary>
        public string CurrentStateName => CurrentState != null ? CurrentState.GetType().Name : "None";

        /// <summary>
        /// 清理 FSM (断开引用)
        /// </summary>
        public void Clear()
        {
            CurrentState = null;
            PreviousState = null;
            _stateCache.Clear();
            Owner = default;
        }
    }
}