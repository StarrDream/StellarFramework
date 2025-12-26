namespace StellarFramework.Pool
{
    /// <summary>
    /// 可池化对象接口
    /// 实现此接口的对象在进出对象池时会自动调用对应方法
    /// </summary>
    public interface IPoolable
    {
        /// <summary>
        /// 当从池中取出时调用 (相当于 Awake/Start)
        /// </summary>
        void OnAllocated();

        /// <summary>
        /// 当回收到池中时调用 (相当于 OnDestroy)
        /// </summary>
        void OnRecycled();
    }
}