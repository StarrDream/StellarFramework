// ==================================================================================
// SingletonMetadata - Commercial Convergence V2
// ----------------------------------------------------------------------------------
// 职责：单例元数据载体。
// 替代原有的反射 Attribute，用于在运行时 0GC 传递配置。
// ==================================================================================

namespace StellarFramework
{
    public class SingletonMetadata
    {
        public string ResourcePath;
        public SingletonLifeCycle LifeCycle;
        public bool UseContainer;
    }
}