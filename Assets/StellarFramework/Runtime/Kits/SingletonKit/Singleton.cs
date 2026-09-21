namespace StellarFramework
{
    /// <summary>
    /// 纯 C# 单例基类
    /// </summary>
    /// <typeparam name="T">子类类型</typeparam>
    public abstract class Singleton<T> : ISingleton where T : Singleton<T>, new()
    {
        /// <summary>
        /// 获取 T 的单例实例。
        /// 实际创建策略由 <see cref="SingletonFactory"/> 和静态元数据决定。
        /// </summary>
        public static T Instance => SingletonFactory.GetSingleton<T>();

        /// <summary>
        /// 单例首次注册成功后的初始化回调。
        /// 正常生命周期内只调用一次。
        /// </summary>
        public virtual void OnSingletonInit()
        {
        }
    }
}