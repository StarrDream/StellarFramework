using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using UnityEngine;

namespace StellarFramework
{
    /// <summary>
    /// [核心] 单例工厂 (v2.1 Thread-Safe)
    /// 职责：
    /// 1. 维护单例注册表 (Dictionary)
    /// 2. Global 单例的自动创建
    /// 3. Scene 单例的严格检查
    /// 4. 杜绝 FindObjectOfType，确保高性能
    /// 5. 严格的主线程检查，防止 Crash
    /// </summary>
    public static class SingletonFactory
    {
        // 单例注册表：Key=Type, Value=Instance
        private static readonly Dictionary<Type, ISingleton> _instances = new Dictionary<Type, ISingleton>();

        // 线程锁 (保护注册表操作)
        private static readonly object _lock = new object();

        // 全局容器缓存
        private static GameObject _globalContainer;

        // 是否退出游戏状态
        private static bool _isQuitting = false;

        // 主线程 ID 缓存
        private static int _mainThreadId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Init()
        {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            _isQuitting = false;
            _instances.Clear();
            Application.quitting += () => _isQuitting = true;
        }

        private static bool IsMainThread => Thread.CurrentThread.ManagedThreadId == _mainThreadId;

        /// <summary>
        /// 获取单例实例 (核心入口)
        /// </summary>
        public static T GetSingleton<T>() where T : class, ISingleton
        {
            if (_isQuitting) return null;

            //  致命崩溃禁止在非主线程访问 Unity 对象
            if (!IsMainThread)
            {
                throw new Exception($"[Singleton] 致命错误：禁止在非主线程访问单例 {typeof(T).Name}！Unity API 不是线程安全的。");
            }

            Type type = typeof(T);

            // 1. 查表 (最快路径，无锁读取)
            if (_instances.TryGetValue(type, out var instance))
            {
                // 检查 Unity 对象是否假死 (引用还在但 C++ 对象已销毁)
                if (instance is UnityEngine.Object uObj && uObj == null)
                {
                    lock (_lock)
                    {
                        _instances.Remove(type); // 移除脏数据
                    }
                }
                else
                {
                    return (T)instance;
                }
            }

            // 2. 表里没有，进入创建/检查流程 (加锁防止递归重入)
            lock (_lock)
            {
                // 双重检查
                if (_instances.TryGetValue(type, out instance) && instance != null)
                {
                    if (instance is UnityEngine.Object uObj && uObj == null)
                        _instances.Remove(type);
                    else
                        return (T)instance;
                }

                // 获取配置特性
                var attr = type.GetCustomAttribute<SingletonAttribute>();
                var lifeCycle = attr?.LifeCycle ?? SingletonLifeCycle.Global;

                // 策略 A: 场景单例 (Scene)
                if (lifeCycle == SingletonLifeCycle.Scene)
                {
                    // 绝对不 Find！
                    LogKit.LogError($"[Singleton] 场景单例 {type.Name} 未注册！\n" +
                                    $"原因：\n" +
                                    $"1. 场景中没有挂载该脚本的物体\n" +
                                    $"2. 脚本未激活 (Awake 未执行)\n" +
                                    $"3. 在 Awake 之前就尝试访问了 Instance (请在 Start 中访问)");
                    return null;
                }

                // 策略 B: 全局单例 (Global)
                // 自动创建流程
                return CreateGlobalInstance<T>(type, attr);
            }
        }

        /// <summary>
        /// 注册单例 (通常由 MonoSingleton.Awake 调用)
        /// </summary>
        public static void Register(Type type, ISingleton instance)
        {
            if (instance == null) return;
            //  注册也需要主线程检查
            if (!IsMainThread) return;

            lock (_lock)
            {
                if (_instances.ContainsKey(type))
                {
                    // 防重机制：如果已存在，销毁新来的 (保持单例唯一性)
                    var existing = _instances[type];

                    // 检查 existing 是否是假死对象
                    if (existing is UnityEngine.Object uExisting && uExisting == null)
                    {
                        _instances.Remove(type); // 移除假死对象，允许新对象注册
                    }
                    else
                    {
                        // 真的重复了，销毁新来的
                        if (instance is MonoBehaviour mono)
                        {
                            LogKit.LogWarning($"[Singleton] 发现重复单例 {type.Name}，正在销毁重复对象 {mono.gameObject.name}");
                            // 必须使用 DestroyImmediate (如果是编辑器模式) 或 Destroy
                            if (Application.isPlaying)
                                UnityEngine.Object.Destroy(mono.gameObject);
                            else
                                UnityEngine.Object.DestroyImmediate(mono.gameObject);
                        }

                        return;
                    }
                }

                _instances.Add(type, instance);
                instance.OnSingletonInit();
            }
        }

        /// <summary>
        /// 反注册 (通常由 MonoSingleton.OnDestroy 调用)
        /// </summary>
        public static void Unregister(Type type, ISingleton instance)
        {
            lock (_lock)
            {
                if (_instances.TryGetValue(type, out var existing))
                {
                    // 只有当要注销的对象等于当前记录的对象时才移除
                    // 防止：新单例刚注册，旧单例的 OnDestroy 延迟触发把新单例给注销了
                    if (ReferenceEquals(existing, instance))
                    {
                        _instances.Remove(type);
                    }
                }
            }
        }

        // --- 内部创建逻辑 ---

        private static T CreateGlobalInstance<T>(Type type, SingletonAttribute attr) where T : class, ISingleton
        {
            // 纯 C# 单例
            T instance;
            if (!typeof(MonoBehaviour).IsAssignableFrom(type))
            {
                instance = Activator.CreateInstance<T>();
                Register(type, instance);
                return instance;
            }

            // Mono 单例
            GameObject go = null;
            string path = attr?.ResourcePath;
            bool useContainer = attr?.UseContainer ?? true;

            // 1. 尝试从 Resources 加载
            if (!string.IsNullOrEmpty(path))
            {
                var prefab = Resources.Load<GameObject>(path);
                if (prefab != null)
                {
                    go = UnityEngine.Object.Instantiate(prefab);
                }
                else
                {
                    LogKit.LogError($"[Singleton] 预制体路径无效: {path}");
                }
            }

            // 2. 如果没预制体，创建空物体
            if (go == null)
            {
                go = new GameObject(type.Name);
            }
            else
            {
                go.name = type.Name; // 去掉 (Clone) 后缀
            }

            // 3. 挂载组件 (如果预制体上没有)
            var comp = go.GetComponent(type);
            if (comp == null) comp = go.AddComponent(type);

            // 4. 设置父节点 (Container)
            if (useContainer)
            {
                if (_globalContainer == null)
                {
                    _globalContainer = new GameObject("[SingletonContainer]");
                    UnityEngine.Object.DontDestroyOnLoad(_globalContainer);
                }

                go.transform.SetParent(_globalContainer.transform);
            }
            else
            {
                UnityEngine.Object.DontDestroyOnLoad(go);
            }

            // 5. 注册
            instance = comp as T;
            if (!_instances.ContainsKey(type))
            {
                Register(type, instance);
            }

            LogKit.Log($"[Singleton] 自动创建 Global 单例: {type.Name}");
            return instance;
        }

        /// <summary>
        /// 清空所有单例 (慎用，通常用于重置游戏)
        /// </summary>
        public static void ClearAll()
        {
            lock (_lock)
            {
                _instances.Clear();
                if (_globalContainer != null)
                {
                    if (Application.isPlaying) UnityEngine.Object.Destroy(_globalContainer);
                    else UnityEngine.Object.DestroyImmediate(_globalContainer);
                    _globalContainer = null;
                }
            }
        }
    }
}