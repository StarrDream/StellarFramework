using UnityEngine;

namespace StellarFramework
{
    /// <summary>
    /// 全局协程运行器 (重构版)
    /// 统一使用 MonoSingleton 基类，移除重复的单例实现代码
    /// </summary>
    [Singleton(lifeCycle: SingletonLifeCycle.Global)]
    public class CoroutineRunner : MonoSingleton<CoroutineRunner>
    {
        public override void OnSingletonInit()
        {
            base.OnSingletonInit();
            // LogKit 调试信息
            LogKit.Log($"[CoroutineRunner] 系统组件已初始化，挂载于: {gameObject.name}");
        }
    }
}