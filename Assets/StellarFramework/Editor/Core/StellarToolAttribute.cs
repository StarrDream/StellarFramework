using System;

namespace StellarFramework.Editor
{
    /// <summary>
    /// 用于标记 StellarFramework 工具模块的特性
    /// 加上此标记的类会自动注册到 StellarFrameworkTools 窗口中
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class StellarToolAttribute : Attribute
    {
        public string Title { get; private set; }
        public string Group { get; private set; }
        public int Order { get; private set; }

        /// <summary>
        /// 注册工具
        /// </summary>
        /// <param name="title">工具标题</param>
        /// <param name="group">分组名称 (例如: "常用工具", "生产力")</param>
        /// <param name="order">排序权重 (越小越靠前)</param>
        public StellarToolAttribute(string title, string group = "未分类", int order = 0)
        {
            Title = title;
            Group = group;
            Order = order;
        }
    }
}