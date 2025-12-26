using UnityEngine;

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
        public ResLoaderType Type; // 加载方式

        /// <summary>
        /// 扩展数据：用于存储 Addressable 的 AsyncOperationHandle 或其他元数据
        /// 使用 object 避免在非 Addressable 环境下报错
        /// </summary>
        public object Data;
    }
}