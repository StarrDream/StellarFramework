using System;
using System.Collections.Generic;
using UnityEngine;

namespace StellarFramework
{
    #region 1. 核心接口与状态定义

    /// <summary>
    /// 架构生命周期状态
    /// 我显式暴露状态机，是为了让架构生命周期可审计、可阻断，避免业务在脏状态下继续访问容器。
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

    public interface IReadOnlyArchitecture
    {
        ArchitectureState State { get; }

        T GetReadOnlyModel<T>() where T : class, IReadOnlyModel;

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

    public interface IReadOnlyModel
    {
    }

    public interface IService : IModule
    {
    }

    public interface IView
    {
        IReadOnlyArchitecture Architecture { get; }

        void OnBind();

        void OnUnbind();
    }

    #endregion

    #region 2. 架构扩展

    public static class StellarArchitectureExtensions
    {
        [Obsolete("View 不应直接持有可变 Model，请改用 GetReadOnlyModel<T>().")]
        public static T GetModel<T>(this IView view) where T : class, IModel
        {
            if (view == null)
            {
                LogKit.LogError($"[StellarFramework] GetModel 失败: view 为空, ModelType={typeof(T).Name}");
                return null;
            }

            if (view.Architecture == null)
            {
                LogKit.LogError(
                    $"[StellarFramework] GetModel 失败: View 未指定 Architecture, ViewType={view.GetType().Name}, ModelType={typeof(T).Name}");
                return null;
            }

            if (view.Architecture is IArchitecture mutableArchitecture)
            {
                return mutableArchitecture.GetModel<T>();
            }

            LogKit.LogError(
                $"[StellarFramework] GetModel 失败: 当前 View 仅暴露只读架构接口, ViewType={view.GetType().Name}, ModelType={typeof(T).Name}");
            return null;
        }

        public static T GetReadOnlyModel<T>(this IView view) where T : class, IReadOnlyModel
        {
            if (view == null)
            {
                LogKit.LogError($"[StellarFramework] GetReadOnlyModel 失败: view 为空, ModelType={typeof(T).Name}");
                return null;
            }

            if (view.Architecture == null)
            {
                LogKit.LogError(
                    $"[StellarFramework] GetReadOnlyModel 失败: View 未指定 Architecture, ViewType={view.GetType().Name}, ModelType={typeof(T).Name}");
                return null;
            }

            return view.Architecture.GetReadOnlyModel<T>();
        }

        public static T GetService<T>(this IView view) where T : class, IService
        {
            if (view == null)
            {
                LogKit.LogError($"[StellarFramework] GetService 失败: view 为空, ServiceType={typeof(T).Name}");
                return null;
            }

            if (view.Architecture == null)
            {
                LogKit.LogError(
                    $"[StellarFramework] GetService 失败: View 未指定 Architecture, ViewType={view.GetType().Name}, ServiceType={typeof(T).Name}");
                return null;
            }

            return view.Architecture.GetService<T>();
        }
    }

    #endregion

    #region 3. 架构核心容器

    public abstract class Architecture<T> : IArchitecture, IReadOnlyArchitecture, IDisposable where T : Architecture<T>, new()
    {
        private readonly Dictionary<Type, IModel> _models = new Dictionary<Type, IModel>();
        private readonly Dictionary<Type, object> _readOnlyModels = new Dictionary<Type, object>();
        private readonly Dictionary<Type, IService> _services = new Dictionary<Type, IService>();

        private ArchitectureState _state = ArchitectureState.Uninitialized;

        public ArchitectureState State => _state;

        private static T _instance;

        public static T Interface
        {
            get
            {
                if (_instance == null || _instance.State == ArchitectureState.Disposed)
                {
                    _instance = new T();
                }

                return _instance;
            }
        }

        /// <summary>
        /// 架构启动入口
        /// 我只允许从未初始化状态进入初始化，拒绝在 Disposed 对象上复用旧实例。
        /// </summary>
        public void Init()
        {
            if (_state == ArchitectureState.Initialized)
            {
                LogKit.LogWarning($"[StellarFramework] 架构重复 Init 已忽略, Architecture={typeof(T).Name}, State={_state}");
                return;
            }

            if (_state == ArchitectureState.Initializing || _state == ArchitectureState.Disposing)
            {
                LogKit.LogError(
                    $"[StellarFramework] 架构 Init 失败: 当前处于中间态, Architecture={typeof(T).Name}, State={_state}");
                return;
            }

            if (_state == ArchitectureState.Disposed)
            {
                LogKit.LogError(
                    $"[StellarFramework] 架构 Init 失败: 当前实例已销毁, Architecture={typeof(T).Name}, State={_state}. 请通过 {typeof(T).Name}.Interface 获取新实例后再调用 Init。");
                return;
            }

            _state = ArchitectureState.Initializing;

            if (_instance == null)
            {
                _instance = (T)this;
            }

            InitModules();

            foreach (IModel model in _models.Values)
            {
                if (model == null)
                {
                    LogKit.LogError(
                        $"[StellarFramework] 架构 Init 失败: 检测到空 Model, Architecture={typeof(T).Name}, State={_state}");
                    _state = ArchitectureState.Uninitialized;
                    return;
                }

                model.Init();
            }

            foreach (IService service in _services.Values)
            {
                if (service == null)
                {
                    LogKit.LogError(
                        $"[StellarFramework] 架构 Init 失败: 检测到空 Service, Architecture={typeof(T).Name}, State={_state}");
                    _state = ArchitectureState.Uninitialized;
                    return;
                }

                service.Init();
            }

            _state = ArchitectureState.Initialized;
            LogKit.Log(
                $"[StellarFramework] 架构启动成功: {typeof(T).Name} | Models={_models.Count}, Services={_services.Count}");
        }

        /// <summary>
        /// 架构销毁
        /// 我要求销毁阶段必须清空容器与静态入口，避免旧实例在 Dispose 后被继续访问。
        /// </summary>
        public virtual void Dispose()
        {
            if (_state == ArchitectureState.Uninitialized || _state == ArchitectureState.Disposed)
            {
                return;
            }

            if (_state == ArchitectureState.Disposing)
            {
                return;
            }

            _state = ArchitectureState.Disposing;

            foreach (IService service in _services.Values)
            {
                service?.Deinit();
            }

            foreach (IModel model in _models.Values)
            {
                model?.Deinit();
            }

            _models.Clear();
            _readOnlyModels.Clear();
            _services.Clear();

            if (ReferenceEquals(_instance, this))
            {
                _instance = null;
            }

            _state = ArchitectureState.Disposed;
            LogKit.Log($"[StellarFramework] 架构已销毁: {typeof(T).Name}");
        }

        protected abstract void InitModules();

        #region 模块注册 API

        protected void RegisterModel<TM>(TM model) where TM : class, IModel
        {
            if (model == null)
            {
                LogKit.LogError(
                    $"[StellarFramework] RegisterModel 失败: model 为空, Architecture={typeof(T).Name}, ModelType={typeof(TM).Name}");
                return;
            }

            if (_state != ArchitectureState.Initializing && _state != ArchitectureState.Uninitialized)
            {
                LogKit.LogError(
                    $"[StellarFramework] RegisterModel 失败: 禁止在运行期动态注册, Architecture={typeof(T).Name}, ModelType={typeof(TM).Name}, State={_state}");
                return;
            }

            if (_models.ContainsKey(typeof(TM)))
            {
                LogKit.LogError(
                    $"[StellarFramework] RegisterModel 失败: 重复注册, Architecture={typeof(T).Name}, ModelType={typeof(TM).Name}");
                return;
            }

            model.Architecture = this;
            _models[typeof(TM)] = model;
            RegisterReadOnlyModelContracts(model);
        }

        protected void RegisterService<TS>(TS service) where TS : class, IService
        {
            if (service == null)
            {
                LogKit.LogError(
                    $"[StellarFramework] RegisterService 失败: service 为空, Architecture={typeof(T).Name}, ServiceType={typeof(TS).Name}");
                return;
            }

            if (_state != ArchitectureState.Initializing && _state != ArchitectureState.Uninitialized)
            {
                LogKit.LogError(
                    $"[StellarFramework] RegisterService 失败: 禁止在运行期动态注册, Architecture={typeof(T).Name}, ServiceType={typeof(TS).Name}, State={_state}");
                return;
            }

            if (_services.ContainsKey(typeof(TS)))
            {
                LogKit.LogError(
                    $"[StellarFramework] RegisterService 失败: 重复注册, Architecture={typeof(T).Name}, ServiceType={typeof(TS).Name}");
                return;
            }

            service.Architecture = this;
            _services[typeof(TS)] = service;
        }

        #endregion

        #region 模块获取 API

        public TM GetModel<TM>() where TM : class, IModel
        {
            if (_state != ArchitectureState.Initialized && _state != ArchitectureState.Initializing)
            {
                LogKit.LogError(
                    $"[StellarFramework] GetModel 失败: 架构状态非法, Architecture={typeof(T).Name}, ModelType={typeof(TM).Name}, State={_state}");
                return null;
            }

            if (_models.TryGetValue(typeof(TM), out IModel model))
            {
                return model as TM;
            }

            LogKit.LogError(
                $"[StellarFramework] GetModel 失败: 未注册, Architecture={typeof(T).Name}, ModelType={typeof(TM).Name}, State={_state}");
            return null;
        }

        public TS GetService<TS>() where TS : class, IService
        {
            if (_state != ArchitectureState.Initialized && _state != ArchitectureState.Initializing)
            {
                LogKit.LogError(
                    $"[StellarFramework] GetService 失败: 架构状态非法, Architecture={typeof(T).Name}, ServiceType={typeof(TS).Name}, State={_state}");
                return null;
            }

            if (_services.TryGetValue(typeof(TS), out IService service))
            {
                return service as TS;
            }

            LogKit.LogError(
                $"[StellarFramework] GetService 失败: 未注册, Architecture={typeof(T).Name}, ServiceType={typeof(TS).Name}, State={_state}");
            return null;
        }

        public TR GetReadOnlyModel<TR>() where TR : class, IReadOnlyModel
        {
            if (_state != ArchitectureState.Initialized && _state != ArchitectureState.Initializing)
            {
                LogKit.LogError(
                    $"[StellarFramework] GetReadOnlyModel 失败: 架构状态非法, Architecture={typeof(T).Name}, ModelType={typeof(TR).Name}, State={_state}");
                return null;
            }

            if (_readOnlyModels.TryGetValue(typeof(TR), out object model))
            {
                return model as TR;
            }

            LogKit.LogError(
                $"[StellarFramework] GetReadOnlyModel 失败: 未注册只读模型契约, Architecture={typeof(T).Name}, ModelType={typeof(TR).Name}, State={_state}");
            return null;
        }

        #endregion

        private void RegisterReadOnlyModelContracts<TM>(TM model) where TM : class, IModel
        {
            Type[] interfaces = model.GetType().GetInterfaces();
            for (int i = 0; i < interfaces.Length; i++)
            {
                Type contractType = interfaces[i];
                if (contractType == typeof(IReadOnlyModel) || !typeof(IReadOnlyModel).IsAssignableFrom(contractType))
                {
                    continue;
                }

                if (_readOnlyModels.ContainsKey(contractType))
                {
                    LogKit.LogError(
                        $"[StellarFramework] RegisterReadOnlyModelContracts 失败: 只读模型契约重复注册, Architecture={typeof(T).Name}, ContractType={contractType.Name}");
                    continue;
                }

                _readOnlyModels[contractType] = model;
            }
        }
    }

    #endregion

    #region 4. 模块基类

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
            if (Architecture == null)
            {
                LogKit.LogError(
                    $"[StellarFramework] Service.GetModel 失败: Architecture 为空, ServiceType={GetType().Name}, ModelType={typeof(T).Name}");
                return null;
            }

            return Architecture.GetModel<T>();
        }

        protected T GetService<T>() where T : class, IService
        {
            if (Architecture == null)
            {
                LogKit.LogError(
                    $"[StellarFramework] Service.GetService 失败: Architecture 为空, ServiceType={GetType().Name}, TargetServiceType={typeof(T).Name}");
                return null;
            }

            return Architecture.GetService<T>();
        }
    }

    #endregion

    #region 5. 视图层基类

    public abstract class StellarView : MonoBehaviour, IView
    {
        public abstract IReadOnlyArchitecture Architecture { get; }

        private bool _isBound;

        protected virtual void Start()
        {
            if (_isBound)
            {
                return;
            }

            OnBind();
            _isBound = true;
        }

        protected virtual void OnDestroy()
        {
            if (!_isBound)
            {
                return;
            }

            OnUnbind();
            _isBound = false;
        }

        public abstract void OnBind();

        public abstract void OnUnbind();
    }

    #endregion
}
