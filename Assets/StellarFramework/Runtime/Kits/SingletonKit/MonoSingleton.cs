using UnityEngine;

namespace StellarFramework
{
    /// <summary>
    /// Mono 单例基类 (V2.0)
    /// </summary>
    /// <typeparam name="T">子类类型</typeparam>
    public abstract class MonoSingleton<T> : MonoBehaviour, ISingleton where T : MonoSingleton<T>
    {
        /// <summary>
        /// 获取单例实例
        /// </summary>
        public static T Instance => SingletonFactory.GetSingleton<T>();

        /// <summary>
        /// 标记是否已初始化
        /// </summary>
        protected bool IsInitialized { get; private set; }

        protected virtual void Awake()
        {
            // 核心：Awake 时主动注册
            SingletonFactory.Register(typeof(T), this);
        }

        protected virtual void OnDestroy()
        {
            // 核心：销毁时主动反注册
            SingletonFactory.Unregister(typeof(T), this);
            IsInitialized = false;
        }

        /// <summary>
        /// 接口实现：初始化回调
        /// 只会在首次注册成功时调用一次
        /// </summary>
        public virtual void OnSingletonInit()
        {
            IsInitialized = true;
        }
    }
}