// ==================================================================================
// StellarFramework MSV (Pure Architecture)
// ----------------------------------------------------------------------------------
// 这是一个纯粹的、无依赖的 MSV (Model-Service-View) 架构核心。
// 它不包含任何数据绑定(Bindable)或事件系统(Event)，也不包含任何工具类(Log/Pool)。
// 它的核心职责仅限于：
// 1. 维护 Model 和 Service 的单例容器 (IOC)
// 2. 管理模块的初始化与销毁顺序 (Lifecycle)
// 3. 规范 View 层的绑定入口 (Bind/Unbind)
// ==================================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace StellarFramework
{
    #region 1. 核心接口定义 (Core Interfaces)

    /// <summary>
    /// 架构容器接口
    /// 定义了架构对外暴露的基本能力：获取 Model 和 Service
    /// </summary>
    public interface IArchitecture
    {
        T GetModel<T>() where T : class, IModel;
        T GetService<T>() where T : class, IService;
    }

    /// <summary>
    /// 模块基础接口
    /// 所有参与架构管理的模块都必须实现此接口
    /// </summary>
    public interface IModule
    {
        IArchitecture Architecture { get; set; }
        void Init();
        void Deinit();
    }

    /// <summary>
    /// 数据层标记接口 (Model)
    /// 职责：仅存储数据，不包含复杂业务逻辑，不引用 View
    /// </summary>
    public interface IModel : IModule { }

    /// <summary>
    /// 逻辑层标记接口 (Service)
    /// 职责：处理业务逻辑，修改 Model，不直接操作 View
    /// </summary>
    public interface IService : IModule { }

    #endregion

    #region 2. 架构核心容器 (Architecture Kernel)

    /// <summary>
    /// MSV 架构基类
    /// 这是一个泛型单例容器，用于管理具体的应用实例 (如 GlobalApp, BattleApp)
    /// </summary>
    /// <typeparam name="T">具体的架构实现类</typeparam>
    public abstract class Architecture<T> : IArchitecture, IDisposable where T : Architecture<T>, new()
    {
        // 模块存储容器 (使用 Type 作为 Key，保证 O(1) 的查找效率)
        private readonly Dictionary<Type, IModel> _models = new Dictionary<Type, IModel>();
        private readonly Dictionary<Type, IService> _services = new Dictionary<Type, IService>();

        // 初始化状态标记，防止重复初始化
        private bool _inited = false;

        // 静态单例实例
        private static T _instance;

        /// <summary>
        /// 获取架构的静态实例
        /// 如果实例不存在，会自动创建一个新的（Lazy Load）
        /// </summary>
        public static T Interface
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new T();
                }
                return _instance;
            }
        }

        /// <summary>
        /// 架构启动入口
        /// 建议在游戏入口 (如 GameEntry.Awake) 中显式调用
        /// </summary>
        public void Init()
        {
            if (_inited) return;
            
            // 确保静态引用指向当前实例
            if (_instance == null) _instance = (T)this;

            // 1. 注册模块 (由子类实现具体的注册逻辑)
            InitModules();

            // 2. 初始化所有 Model (数据层优先初始化)
            foreach (var model in _models.Values)
            {
                try
                {
                    model.Init();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[StellarFramework] Model Init 异常: {model.GetType().Name}\n{e}");
                }
            }

            // 3. 初始化所有 Service (逻辑层随后初始化，因为逻辑可能依赖数据)
            foreach (var service in _services.Values)
            {
                try
                {
                    service.Init();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[StellarFramework] Service Init 异常: {service.GetType().Name}\n{e}");
                }
            }

            _inited = true;
            Debug.Log($"[StellarFramework] 架构启动成功: {typeof(T).Name} | Models: {_models.Count}, Services: {_services.Count}");
        }

        /// <summary>
        /// 架构销毁
        /// 通常在场景切换或游戏退出时调用，清理所有内存引用
        /// </summary>
        public virtual void Dispose()
        {
            // 反向销毁：先停止服务 (Service)，再清理数据 (Model)
            foreach (var service in _services.Values)
            {
                try { service.Deinit(); } catch (Exception e) { Debug.LogError(e); }
            }
            
            foreach (var model in _models.Values)
            {
                try { model.Deinit(); } catch (Exception e) { Debug.LogError(e); }
            }

            _models.Clear();
            _services.Clear();
            
            if (_instance == this) _instance = null;
            _inited = false;
            
            Debug.Log($"[StellarFramework] 架构已销毁: {typeof(T).Name}");
        }

        /// <summary>
        /// [必须实现] 子类在此方法中进行 RegisterModel 和 RegisterService 的调用
        /// </summary>
        protected abstract void InitModules();

        #region 模块注册 API (Protected)

        /// <summary>
        /// 注册一个 Model
        /// </summary>
        protected void RegisterModel<TM>(TM model) where TM : class, IModel
        {
            if (_models.ContainsKey(typeof(TM)))
            {
                Debug.LogWarning($"[StellarFramework] 重复注册 Model: {typeof(TM).Name}");
                return;
            }
            model.Architecture = this;
            _models[typeof(TM)] = model;
        }

        /// <summary>
        /// 注册一个 Service
        /// </summary>
        protected void RegisterService<TS>(TS service) where TS : class, IService
        {
            if (_services.ContainsKey(typeof(TS)))
            {
                Debug.LogWarning($"[StellarFramework] 重复注册 Service: {typeof(TS).Name}");
                return;
            }
            service.Architecture = this;
            _services[typeof(TS)] = service;
        }

        #endregion

        #region 模块获取 API (Public)

        public TM GetModel<TM>() where TM : class, IModel
        {
            if (_models.TryGetValue(typeof(TM), out var model))
            {
                return (TM)model;
            }
            Debug.LogError($"[StellarFramework] 获取 Model 失败: {typeof(TM).Name} 未在 {typeof(T).Name} 中注册");
            return null;
        }

        public TS GetService<TS>() where TS : class, IService
        {
            if (_services.TryGetValue(typeof(TS), out var service))
            {
                return (TS)service;
            }
            Debug.LogError($"[StellarFramework] 获取 Service 失败: {typeof(TS).Name} 未在 {typeof(T).Name} 中注册");
            return null;
        }

        #endregion
    }

    #endregion

    #region 3. 模块基类 (Abstract Bases)

    /// <summary>
    /// Model 基类
    /// 建议所有 Model 继承此类
    /// </summary>
    public abstract class AbstractModel : IModel
    {
        public IArchitecture Architecture { get; set; }

        public virtual void Init() { }
        public virtual void Deinit() { }
    }

    /// <summary>
    /// Service 基类
    /// 建议所有 Service 继承此类
    /// </summary>
    public abstract class AbstractService : IService
    {
        public IArchitecture Architecture { get; set; }

        public virtual void Init() { }
        public virtual void Deinit() { }

        // --- 语法糖：方便在 Service 内部获取其他模块 ---

        protected T GetModel<T>() where T : class, IModel
        {
            return Architecture.GetModel<T>();
        }

        protected T GetService<T>() where T : class, IService
        {
            return Architecture.GetService<T>();
        }
    }

    #endregion

    #region 4. 视图层基类 (View Base)

    /// <summary>
    /// View 层基类
    /// 职责：
    /// 1. 自动管理 OnBind 和 OnUnbind 的调用时机
    /// 2. 确保在 GameObject 销毁时正确清理资源
    /// </summary>
    public abstract class StellarView : MonoBehaviour
    {
        // 标记是否已经绑定，防止重复绑定
        private bool _isBound = false;

        protected virtual void Start()
        {
            if (!_isBound) Bind();
        }

        protected virtual void OnDestroy()
        {
            if (_isBound) Unbind();
        }

        /// <summary>
        /// 手动触发绑定 (通常不需要手动调用，Start 会自动处理)
        /// </summary>
        public void Bind()
        {
            if (_isBound) return;
            OnBind();
            _isBound = true;
        }

        /// <summary>
        /// 手动触发解绑 (通常不需要手动调用，OnDestroy 会自动处理)
        /// </summary>
        public void Unbind()
        {
            if (!_isBound) return;
            OnUnbind();
            _isBound = false;
        }

        /// <summary>
        /// [核心方法] 子类在此处获取 Model/Service，并注册事件监听
        /// </summary>
        protected abstract void OnBind();

        /// <summary>
        /// [核心方法] 子类在此处清理事件监听，防止内存泄漏
        /// </summary>
        protected abstract void OnUnbind();
    }

    #endregion
}