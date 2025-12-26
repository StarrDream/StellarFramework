// ==================================================================================
// StellarFramework MSV (Pure Architecture)
// ----------------------------------------------------------------------------------
// 纯粹的 MSV (Model-Service-View) 架构核心。
// 职责：
// 1. 维护 Model 和 Service 的单例容器 (IOC)
// 2. 管理模块的初始化与销毁顺序 (Lifecycle)
// 3. 规范 View 层的绑定入口 (IView Interface)
// ==================================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace StellarFramework
{
    #region 1. 核心接口定义 (Core Interfaces)

    /// <summary>
    /// 架构容器接口
    /// 定义架构对外暴露的基本能力：获取 Model 和 Service
    /// </summary>
    public interface IArchitecture
    {
        T GetModel<T>() where T : class, IModel;
        T GetService<T>() where T : class, IService;
    }

    /// <summary>
    /// 模块基础接口
    /// </summary>
    public interface IModule
    {
        IArchitecture Architecture { get; set; }
        void Init();
        void Deinit();
    }

    /// <summary>
    /// 数据层接口 (Model)
    /// 职责：仅存储数据，不包含复杂业务逻辑，不引用 View
    /// </summary>
    public interface IModel : IModule
    {
    }

    /// <summary>
    /// 逻辑层接口 (Service)
    /// 职责：处理业务逻辑，修改 Model，不直接操作 View
    /// </summary>
    public interface IService : IModule
    {
    }

    /// <summary>
    /// 表现层接口 (View)
    /// 职责：实现此接口的类（MonoBehaviour/UI组件）可接入架构
    /// </summary>
    public interface IView
    {
        /// <summary>
        /// 获取归属的架构实例
        /// </summary>
        IArchitecture Architecture { get; }

        /// <summary>
        /// 绑定事件/数据
        /// </summary>
        void OnBind();

        /// <summary>
        /// 解绑事件/数据
        /// </summary>
        void OnUnbind();
    }

    #endregion

    #region 2. 架构扩展 (Extensions)

    /// <summary>
    /// 架构扩展类
    /// 赋予 IView 获取 Model 和 Service 的能力
    /// </summary>
    public static class StellarArchitectureExtensions
    {
        /// <summary>
        /// View 获取 Model (只读/绑定用)
        /// </summary>
        public static T GetModel<T>(this IView view) where T : class, IModel
        {
            if (view.Architecture == null)
            {
                Debug.LogError($"[StellarFramework] View {view.GetType().Name} 未指定 Architecture，无法获取 Model");
                return null;
            }

            return view.Architecture.GetModel<T>();
        }

        /// <summary>
        /// View 获取 Service (交互/逻辑用)
        /// </summary>
        public static T GetService<T>(this IView view) where T : class, IService
        {
            if (view.Architecture == null)
            {
                Debug.LogError($"[StellarFramework] View {view.GetType().Name} 未指定 Architecture，无法获取 Service");
                return null;
            }

            return view.Architecture.GetService<T>();
        }
    }

    #endregion

    #region 3. 架构核心容器 (Architecture Kernel)

    /// <summary>
    /// MSV 架构基类
    /// 泛型单例容器，用于管理具体的应用实例
    /// </summary>
    /// <typeparam name="T">具体的架构实现类</typeparam>
    public abstract class Architecture<T> : IArchitecture, IDisposable where T : Architecture<T>, new()
    {
        // 模块存储容器 (使用 Type 作为 Key，O(1) 查找)
        private readonly Dictionary<Type, IModel> _models = new Dictionary<Type, IModel>();
        private readonly Dictionary<Type, IService> _services = new Dictionary<Type, IService>();

        // 初始化状态标记
        private bool _inited = false;

        // 静态单例实例
        private static T _instance;

        /// <summary>
        /// 获取架构的静态实例 (Lazy Load)
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
        /// </summary>
        public void Init()
        {
            if (_inited) return;

            if (_instance == null) _instance = (T)this;

            // 1. 注册模块
            InitModules();

            // 2. 初始化所有 Model
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

            // 3. 初始化所有 Service
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
        /// </summary>
        public virtual void Dispose()
        {
            foreach (var service in _services.Values)
            {
                try
                {
                    service.Deinit();
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                }
            }

            foreach (var model in _models.Values)
            {
                try
                {
                    model.Deinit();
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                }
            }

            _models.Clear();
            _services.Clear();

            if (_instance == this) _instance = null;
            _inited = false;

            Debug.Log($"[StellarFramework] 架构已销毁: {typeof(T).Name}");
        }

        /// <summary>
        /// 子类实现此方法进行模块注册
        /// </summary>
        protected abstract void InitModules();

        #region 模块注册 API

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

        #region 模块获取 API

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

    /// <summary>
    /// View 层便利基类
    /// 自动处理 Start/OnDestroy 的绑定逻辑
    /// 如果需要继承其他组件（如 Button），请直接实现 IView 接口
    /// </summary>
    public abstract class StellarView : MonoBehaviour, IView
    {
        // 子类需重写此属性指定具体的架构实例
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