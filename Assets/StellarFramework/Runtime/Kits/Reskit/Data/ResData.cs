using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StellarFramework.Res
{
    /// <summary>
    /// 资源缓存数据实体
    /// </summary>
    public class ResData
    {
        public string Path; // 资源路径 (Key)
        public Object Asset; // 资源对象引用
        public int RefCount; // 全局引用计数

        // 架构重构：废弃 Enum，改用字符串标识命名空间
        public string LoaderName;

        /// <summary>
        /// 扩展数据：用于存储 Addressable 的 AsyncOperationHandle 或其他元数据
        /// </summary>
        public object Data;

        /// <summary>
        /// 架构重构：卸载委托。由具体的 Loader 注入自身的卸载逻辑，实现 ResMgr 与具体加载方式的彻底解耦。
        /// </summary>
        public Action<ResData> UnloadAction;
    }
}