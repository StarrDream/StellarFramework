namespace StellarFramework
{
    /// <summary>
    /// 单例接口
    /// </summary>
    public interface ISingleton
    {
        /// <summary>
        /// 单例初始化方法
        /// 当单例首次成功注册到工厂时调用
        /// </summary>
        void OnSingletonInit();
    }
}