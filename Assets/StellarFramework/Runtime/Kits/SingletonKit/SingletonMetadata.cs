// ==================================================================================
// SingletonMetadata
// ----------------------------------------------------------------------------------
// 由生成代码写入 SingletonFactory 的运行时配置载体。
// 运行时无需反射读取 SingletonAttribute。
// ==================================================================================

namespace StellarFramework
{
    /// <summary>
    /// SingletonGenerator 生成并注入运行时的单例配置。
    /// </summary>
    public class SingletonMetadata
    {
        /// <summary>Global MonoBehaviour 单例使用的 Resources prefab 路径。</summary>
        public string ResourcePath;
        /// <summary>Global 或 Scene 生命周期。</summary>
        public SingletonLifeCycle LifeCycle;
        /// <summary>Global 单例是否挂到统一 SingletonContainer。</summary>
        public bool UseContainer;
    }
}