namespace StellarFramework
{
    /// <summary>
    /// 纯 C# 单例基类
    /// </summary>
    /// <typeparam name="T">子类类型</typeparam>
    public abstract class Singleton<T> : ISingleton where T : Singleton<T>, new()
    {
        public static T Instance => SingletonFactory.GetSingleton<T>();

        /// <summary>
        /// 初始化回调 (只调用一次)
        /// </summary>
        public virtual void OnSingletonInit()
        {
        }
    }
}