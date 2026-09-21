using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using LogKit = StellarFramework.SingletonKitDiagnostics;

namespace StellarFramework
{
    /// <summary>
    /// 单例运行时工厂。
    /// 统一负责实例注册、生命周期判定和 Global 单例创建。
    /// 运行时不反射读取 SingletonAttribute，也不通过反射实例化纯 C# 单例。
    /// </summary>
    public static class SingletonFactory
    {
        private static readonly Dictionary<Type, ISingleton> Instances = new Dictionary<Type, ISingleton>();

        private static readonly Dictionary<Type, SingletonMetadata> MetadataCache =
            new Dictionary<Type, SingletonMetadata>();

        private static readonly Dictionary<Type, Func<ISingleton>> PureSingletonCreators =
            new Dictionary<Type, Func<ISingleton>>();

        private static readonly object Locker = new object();

        private static GameObject _globalContainer;
        private static bool _isQuitting;
        private static int _mainThreadId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            _isQuitting = false;
            ResetRuntimeState();

            Application.quitting -= OnApplicationQuitting;
            Application.quitting += OnApplicationQuitting;
        }

        private static void OnApplicationQuitting()
        {
            _isQuitting = true;
        }

        private static bool IsMainThread => Thread.CurrentThread.ManagedThreadId == _mainThreadId;

        /// <summary>
        /// 注册生成期产生的静态单例元数据。
        /// 通常由 SingletonGenerator 生成的注册代码调用，业务代码一般不需要直接调用。
        /// </summary>
        public static void RegisterMetadata(Type type, SingletonMetadata metadata)
        {
            if (type == null)
            {
                Debug.LogError("[SingletonFactory] RegisterMetadata 失败: type 为空");
                return;
            }

            if (metadata == null)
            {
                Debug.LogError($"[SingletonFactory] RegisterMetadata 失败: metadata 为空, Type={type.FullName}");
                return;
            }

            MetadataCache[type] = metadata;
        }

        /// <summary>
        /// 清空当前元数据注册表。
        /// 主要用于 SubsystemRegistration、测试或重新生成注册表后的重置流程。
        /// </summary>
        public static void ClearMetadata()
        {
            MetadataCache.Clear();
        }

        /// <summary>
        /// 注册纯 C# 单例的静态创建器。
        /// 创建器应由生成代码或明确的 Composition Root 注入，避免运行时反射实例化。
        /// </summary>
        public static void RegisterPureSingletonCreator(Type type, Func<ISingleton> creator)
        {
            if (type == null)
            {
                Debug.LogError("[SingletonFactory] RegisterPureSingletonCreator 失败: type 为空");
                return;
            }

            if (creator == null)
            {
                Debug.LogError($"[SingletonFactory] RegisterPureSingletonCreator 失败: creator 为空, Type={type.FullName}");
                return;
            }

            PureSingletonCreators[type] = creator;
        }

        /// <summary>
        /// 清空已注册的纯 C# 单例创建器。
        /// </summary>
        public static void ClearPureSingletonCreators()
        {
            PureSingletonCreators.Clear();
        }

        /// <summary>
        /// 获取 T 的已注册实例；Global 模式不存在时按元数据创建，Scene 模式不存在时直接报错。
        /// </summary>
        /// <remarks>
        /// 所有访问都要求主线程，以避免 Global MonoBehaviour 单例创建过程误触 Unity API。
        /// Scene 单例不会执行 FindObjectOfType 兜底，必须由场景对象显式注册。
        /// </remarks>
        public static T GetSingleton<T>() where T : class, ISingleton
        {
            if (_isQuitting)
            {
                return null;
            }

            if (!IsMainThread)
            {
                throw new InvalidOperationException(
                    $"[SingletonFactory] 致命错误: 禁止在非主线程访问单例, Type={typeof(T).FullName}, ThreadId={Thread.CurrentThread.ManagedThreadId}, MainThreadId={_mainThreadId}");
            }

            Type type = typeof(T);

            if (Instances.TryGetValue(type, out ISingleton fastInstance))
            {
                if (fastInstance is UnityEngine.Object fastUnityObj && fastUnityObj == null)
                {
                    lock (Locker)
                    {
                        Instances.Remove(type);
                    }
                }
                else
                {
                    return (T)fastInstance;
                }
            }

            lock (Locker)
            {
                if (Instances.TryGetValue(type, out ISingleton lockedInstance))
                {
                    if (lockedInstance is UnityEngine.Object lockedUnityObj && lockedUnityObj == null)
                    {
                        Instances.Remove(type);
                    }
                    else
                    {
                        return (T)lockedInstance;
                    }
                }

                if (!TryGetMetadata(type, out SingletonMetadata metadata))
                {
                    return null;
                }

                if (metadata.LifeCycle == SingletonLifeCycle.Scene)
                {
                    LogKit.LogError(
                        $"[SingletonFactory] 获取场景单例失败: 场景单例未注册, Type={type.FullName}, LifeCycle={metadata.LifeCycle}\n" +
                        "请检查:\n" +
                        "1. 场景中是否已挂载该组件\n" +
                        "2. 对象是否启用, Awake 是否已执行\n" +
                        "3. 是否在过早的生命周期阶段访问了 Instance");
                    return null;
                }

                return CreateGlobalInstance<T>(type, metadata);
            }
        }

        /// <summary>
        /// 注册已经存在的单例实例。
        /// 重复 MonoBehaviour 单例会保留旧实例并销毁新对象；纯 C# 重复注册会记录错误。
        /// </summary>
        public static void Register(Type type, ISingleton instance)
        {
            if (type == null)
            {
                Debug.LogError("[SingletonFactory] Register 失败: type 为空");
                return;
            }

            if (instance == null)
            {
                Debug.LogError($"[SingletonFactory] Register 失败: instance 为空, Type={type.FullName}");
                return;
            }

            if (!IsMainThread)
            {
                Debug.LogError($"[SingletonFactory] Register 失败: 非主线程禁止注册单例, Type={type.FullName}");
                return;
            }

            lock (Locker)
            {
                if (Instances.TryGetValue(type, out ISingleton existing))
                {
                    if (existing is UnityEngine.Object existingUnityObj && existingUnityObj == null)
                    {
                        Instances.Remove(type);
                    }
                    else
                    {
                        if (instance is MonoBehaviour mono)
                        {
                            Debug.LogWarning(
                                $"[SingletonFactory] 检测到重复单例, 保留已注册实例并销毁新对象, Type={type.FullName}, TriggerObject={mono.gameObject.name}");

                            if (Application.isPlaying)
                            {
                                UnityEngine.Object.Destroy(mono.gameObject);
                            }
                            else
                            {
                                UnityEngine.Object.DestroyImmediate(mono.gameObject);
                            }
                        }
                        else
                        {
                            Debug.LogError($"[SingletonFactory] 检测到重复纯 C# 单例注册, Type={type.FullName}");
                        }

                        return;
                    }
                }

                Instances.Add(type, instance);
                instance.OnSingletonInit();
            }
        }

        /// <summary>
        /// 仅当当前注册实例与传入 instance 为同一对象时解除注册。
        /// </summary>
        public static void Unregister(Type type, ISingleton instance)
        {
            if (type == null || instance == null)
            {
                return;
            }

            lock (Locker)
            {
                if (!Instances.TryGetValue(type, out ISingleton existing))
                {
                    return;
                }

                if (ReferenceEquals(existing, instance))
                {
                    Instances.Remove(type);
                }
            }
        }

        /// <summary>
        /// 清空全部运行时单例状态。
        /// 主要用于框架重置和测试，不等价于逐个执行业务 Dispose。
        /// </summary>
        public static void ClearAll()
        {
            lock (Locker)
            {
                ResetRuntimeState();
            }
        }

        /// <summary>
        /// 只查询当前是否已有 T 的有效注册实例，不触发自动创建。
        /// </summary>
        /// <returns>存在有效实例时返回 true。</returns>
        public static bool TryGetRegisteredSingleton<T>(out T instance) where T : class, ISingleton
        {
            instance = null;

            if (!IsMainThread)
            {
                return false;
            }

            Type type = typeof(T);
            if (!Instances.TryGetValue(type, out ISingleton existing))
            {
                return false;
            }

            if (existing is UnityEngine.Object unityObj && unityObj == null)
            {
                lock (Locker)
                {
                    Instances.Remove(type);
                }

                return false;
            }

            instance = existing as T;
            return instance != null;
        }

        private static bool TryGetMetadata(Type type, out SingletonMetadata metadata)
        {
            if (MetadataCache.TryGetValue(type, out metadata))
            {
                if (metadata == null)
                {
                    LogKit.LogError($"[SingletonFactory] 元数据非法: metadata 为空, Type={type.FullName}");
                    return false;
                }

                return true;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogKit.Assert(
                false,
                $"[SingletonFactory] 缺少静态单例元数据, Type={type.FullName}\n" +
                "这通常意味着 SingletonGenerator 未执行或生成产物未参与编译。\n" +
                "请重新生成静态注册表, 严禁依赖运行时默认兜底配置。");
            metadata = null;
            return false;
#else
            Debug.LogError(
                $"[SingletonFactory] 缺少静态单例元数据, Type={type.FullName}. " +
                "Release 环境已阻断本次创建请求, 请检查生成流程。");
            metadata = null;
            return false;
#endif
        }

        private static T CreateGlobalInstance<T>(Type type, SingletonMetadata metadata) where T : class, ISingleton
        {
            if (metadata == null)
            {
                LogKit.LogError($"[SingletonFactory] CreateGlobalInstance 失败: metadata 为空, Type={type.FullName}");
                return null;
            }

            if (!typeof(MonoBehaviour).IsAssignableFrom(type))
            {
                if (!PureSingletonCreators.TryGetValue(type, out Func<ISingleton> creator))
                {
                    LogKit.LogError(
                        $"[SingletonFactory] 创建纯 C# 单例失败: 缺少静态创建器, Type={type.FullName}\n" +
                        "请通过生成代码或静态注册代码调用 RegisterPureSingletonCreator 注入工厂。");
                    return null;
                }

                ISingleton created = creator.Invoke();
                if (!(created is T pureInstance))
                {
                    LogKit.LogError(
                        $"[SingletonFactory] 创建纯 C# 单例失败: 创建器返回类型不匹配, Type={type.FullName}, ReturnedType={created?.GetType().FullName ?? "null"}");
                    return null;
                }

                Register(type, pureInstance);
                return pureInstance;
            }

            GameObject go = null;

            if (!string.IsNullOrEmpty(metadata.ResourcePath))
            {
                GameObject prefab = Resources.Load<GameObject>(metadata.ResourcePath);
                if (prefab == null)
                {
                    LogKit.LogError(
                        $"[SingletonFactory] Global 单例预制体加载失败: Type={type.FullName}, ResourcePath={metadata.ResourcePath}");
                    return null;
                }

                go = UnityEngine.Object.Instantiate(prefab);
                go.name = type.Name;
            }
            else
            {
                go = new GameObject(type.Name);
            }

            if (go == null)
            {
                LogKit.LogError($"[SingletonFactory] Global 单例创建失败: GameObject 创建结果为空, Type={type.FullName}");
                return null;
            }

            Component comp = go.GetComponent(type);
            if (comp == null)
            {
                comp = go.AddComponent(type);
            }

            if (comp == null)
            {
                LogKit.LogError(
                    $"[SingletonFactory] Global 单例创建失败: 组件挂载失败, Type={type.FullName}, GameObject={go.name}");
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(go);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(go);
                }

                return null;
            }

            if (metadata.UseContainer)
            {
                EnsureGlobalContainer();
                go.transform.SetParent(_globalContainer.transform, false);
                UnityEngine.Object.DontDestroyOnLoad(_globalContainer);
            }
            else
            {
                UnityEngine.Object.DontDestroyOnLoad(go);
            }

            T result = comp as T;
            if (result == null)
            {
                LogKit.LogError(
                    $"[SingletonFactory] Global 单例创建失败: 类型转换失败, Type={type.FullName}, GameObject={go.name}");
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(go);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(go);
                }

                return null;
            }

            if (!Instances.ContainsKey(type))
            {
                Register(type, result);
            }

            LogKit.Log($"[SingletonFactory] 已自动创建 Global 单例: Type={type.FullName}, GameObject={go.name}");
            return result;
        }

        private static void EnsureGlobalContainer()
        {
            if (_globalContainer != null)
            {
                return;
            }

            _globalContainer = new GameObject("[SingletonContainer]");
            UnityEngine.Object.DontDestroyOnLoad(_globalContainer);
        }

        private static void ResetRuntimeState()
        {
            Instances.Clear();

            if (_globalContainer == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(_globalContainer);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(_globalContainer);
            }

            _globalContainer = null;
        }
    }
}
