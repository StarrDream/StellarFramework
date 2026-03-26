// ==================================================================================
// StellarFramework MSV (Pure Architecture) - Commercial Convergence V2
// ----------------------------------------------------------------------------------
// 纯粹的 MSV (Model-Service-View) 架构核心。
// 改造说明：
// 1. 引入显式状态机 (ArchitectureState)，杜绝生命周期混乱。
// 2. 移除 Init/Dispose 中的 try-catch，贯彻 Fail-Fast 原则，模块初始化失败必须立即暴露并阻断。
// 3. 增加 GetModel/GetService 的前置状态断言，防止在架构销毁后产生野指针访问。
// ==================================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace StellarFramework
{
    #region 1. 核心接口与状态定义 (Core Interfaces & States)

    /// <summary>
    /// 架构生命周期状态
    /// 职责：使架构的流转状态显式化，为防御性编程提供断言依据
    /// </summary>
    public enum ArchitectureState
    {
        Uninitialized,
        Initializing,
        Initialized,
        Disposing,
        Disposed
    }

    /// <summary>
    /// 架构容器接口
    /// </summary>
    public interface IArchitecture
    {
        ArchitectureState State { get; }
        T GetModel<T>() where T : class, IModel;
        T GetService<T>() where T : class, IService;
    }

    public interface IModule
    {
        IArchitecture Architecture { get; set; }
        void Init();
        void Deinit();
    }

    public interface IModel : IModule
    {
    }

    public interface IService : IModule
    {
    }

    public interface IView
    {
        IArchitecture Architecture { get; }
        void OnBind();
        void OnUnbind();
    }

    #endregion

    #region 2. 架构扩展 (Extensions)

    public static class StellarArchitectureExtensions
    {
        public static T GetModel<T>(this IView view) where T : class, IModel
        {
            if (view.Architecture == null)
            {
                LogKit.LogError($"[StellarFramework] View {view.GetType().Name} 未指定 Architecture，无法获取 Model");
                return null;
            }

            return view.Architecture.GetModel<T>();
        }

        public static T GetService<T>(this IView view) where T : class, IService
        {
            if (view.Architecture == null)
            {
                LogKit.LogError($"[StellarFramework] View {view.GetType().Name} 未指定 Architecture，无法获取 Service");
                return null;
            }

            return view.Architecture.GetService<T>();
        }
    }

    #endregion

    #region 3. 架构核心容器 (Architecture Kernel)

    public abstract class Architecture<T> : IArchitecture, IDisposable where T : Architecture<T>, new()
    {
        private readonly Dictionary<Type, IModel> _models = new Dictionary<Type, IModel>();
        private readonly Dictionary<Type, IService> _services = new Dictionary<Type, IService>();

        private ArchitectureState _state = ArchitectureState.Uninitialized;
        public ArchitectureState State => _state;

        private static T _instance;

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
        /// </summary>
        public void Init()
        {
            // 前置拦截：防止重复初始化导致的数据覆盖与内存泄漏
            if (_state != ArchitectureState.Uninitialized)
            {
                LogKit.LogError($"[StellarFramework] 架构 {typeof(T).Name} 拒绝重复初始化，当前状态: {_state}");
                return;
            }

            _state = ArchitectureState.Initializing;
            if (_instance == null) _instance = (T)this;

            InitModules();

            // 核心改造：移除 try-catch。
            // 商业化项目中，如果某个核心 Model 或 Service 初始化失败，整个业务域的数据基底就是脏的。
            // 必须让异常直接抛出并阻断流程，绝不允许带着脏数据进入 Running 状态。
            foreach (var model in _models.Values)
            {
                model.Init();
            }

            foreach (var service in _services.Values)
            {
                service.Init();
            }

            _state = ArchitectureState.Initialized;
            LogKit.Log(
                $"[StellarFramework] 架构启动成功: {typeof(T).Name} | Models: {_models.Count}, Services: {_services.Count}");
        }

        /// <summary>
        /// 架构销毁
        /// </summary>
        public virtual void Dispose()
        {
            if (_state == ArchitectureState.Disposed || _state == ArchitectureState.Uninitialized)
            {
                return;
            }

            _state = ArchitectureState.Disposing;

            // 同样移除 try-catch，强制要求业务层在 Deinit 中编写健壮的清理逻辑
            foreach (var service in _services.Values)
            {
                service.Deinit();
            }

            foreach (var model in _models.Values)
            {
                model.Deinit();
            }

            _models.Clear();
            _services.Clear();

            if (_instance == this) _instance = null;

            _state = ArchitectureState.Disposed;
            LogKit.Log($"[StellarFramework] 架构已销毁: {typeof(T).Name}");
        }

        protected abstract void InitModules();

        #region 模块注册 API

        protected void RegisterModel<TM>(TM model) where TM : class, IModel
        {
            if (_state != ArchitectureState.Initializing && _state != ArchitectureState.Uninitialized)
            {
                LogKit.LogError($"[StellarFramework] 拒绝在运行时动态注册 Model: {typeof(TM).Name}，必须在 InitModules 中完成注册");
                return;
            }

            if (_models.ContainsKey(typeof(TM)))
            {
                LogKit.LogError($"[StellarFramework] 拒绝重复注册 Model: {typeof(TM).Name}");
                return;
            }

            model.Architecture = this;
            _models[typeof(TM)] = model;
        }

        protected void RegisterService<TS>(TS service) where TS : class, IService
        {
            if (_state != ArchitectureState.Initializing && _state != ArchitectureState.Uninitialized)
            {
                LogKit.LogError($"[StellarFramework] 拒绝在运行时动态注册 Service: {typeof(TS).Name}，必须在 InitModules 中完成注册");
                return;
            }

            if (_services.ContainsKey(typeof(TS)))
            {
                LogKit.LogError($"[StellarFramework] 拒绝重复注册 Service: {typeof(TS).Name}");
                return;
            }

            service.Architecture = this;
            _services[typeof(TS)] = service;
        }

        #endregion

        #region 模块获取 API

        public TM GetModel<TM>() where TM : class, IModel
        {
            // 前置拦截：架构未就绪或已销毁时，严禁获取数据
            LogKit.Assert(_state == ArchitectureState.Initialized || _state == ArchitectureState.Initializing,
                $"[StellarFramework] 架构状态异常，无法获取 Model: {typeof(TM).Name}，当前状态: {_state}");

            if (_models.TryGetValue(typeof(TM), out var model))
            {
                return (TM)model;
            }

            LogKit.LogError($"[StellarFramework] 获取 Model 失败: {typeof(TM).Name} 未在 {typeof(T).Name} 中注册");
            return null;
        }

        public TS GetService<TS>() where TS : class, IService
        {
            LogKit.Assert(_state == ArchitectureState.Initialized || _state == ArchitectureState.Initializing,
                $"[StellarFramework] 架构状态异常，无法获取 Service: {typeof(TS).Name}，当前状态: {_state}");

            if (_services.TryGetValue(typeof(TS), out var service))
            {
                return (TS)service;
            }

            LogKit.LogError($"[StellarFramework] 获取 Service 失败: {typeof(TS).Name} 未在 {typeof(T).Name} 中注册");
            return null;
        }

        #endregion
    }

    #endregion

    #region 4. 模块基类 (Abstract Bases)

    public abstract class AbstractModel : IModel
    {
        public IArchitecture Architecture { get; set; }

        public virtual void Init()
        {
        }

        public virtual void Deinit()
        {
        }
    }

    public abstract class AbstractService : IService
    {
        public IArchitecture Architecture { get; set; }

        public virtual void Init()
        {
        }

        public virtual void Deinit()
        {
        }

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

    #region 5. 视图层基类 (View Base)

    public abstract class StellarView : MonoBehaviour, IView
    {
        public abstract IArchitecture Architecture { get; }
        private bool _isBound = false;

        protected virtual void Start()
        {
            if (!_isBound)
            {
                OnBind();
                _isBound = true;
            }
        }

        protected virtual void OnDestroy()
        {
            if (_isBound)
            {
                OnUnbind();
                _isBound = false;
            }
        }

        public abstract void OnBind();
        public abstract void OnUnbind();
    }

    #endregion
}