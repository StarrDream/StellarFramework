using System;

namespace StellarFramework
{
    /// <summary>
    /// 单例生命周期枚举
    /// </summary>
    public enum SingletonLifeCycle
    {
        /// <summary>
        /// 全局单例：
        /// 1. 自动创建 (Lazy Load)
        /// 2. 自动 DontDestroyOnLoad
        /// 3. 切换场景不销毁
        /// </summary>
        Global,

        /// <summary>
        /// 场景单例：
        /// 1. 禁止自动创建 (必须预先放在场景中)
        /// 2. 切换场景自动销毁
        /// 3. 如果访问时不存在，直接报错 (绝不尝试 Find)
        /// </summary>
        Scene
    }

    /// <summary>
    /// 单例配置特性
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public class SingletonAttribute : Attribute
    {
        /// <summary>
        /// 预制体加载路径（Resources 目录下，不含扩展名）
        /// 仅对 Global 模式有效
        /// </summary>
        public string ResourcePath { get; }

        /// <summary>
        /// 生命周期模式
        /// </summary>
        public SingletonLifeCycle LifeCycle { get; }

        /// <summary>
        /// 是否挂到统一容器 [SingletonContainer] 下
        /// 仅对 Global 模式有效
        /// </summary>
        public bool UseContainer { get; }

        public SingletonAttribute(string resourcePath = "", SingletonLifeCycle lifeCycle = SingletonLifeCycle.Global, bool useContainer = true)
        {
            ResourcePath = resourcePath;
            LifeCycle = lifeCycle;
            UseContainer = useContainer;
        }
    }
}