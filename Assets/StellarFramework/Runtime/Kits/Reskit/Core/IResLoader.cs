// ========== IResLoader.cs ==========
// Path: Assets/StellarFramework/Runtime/Kits/Reskit/Core/IResLoader.cs

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Object = UnityEngine.Object;

namespace StellarFramework.Res
{
    /// <summary>
    /// 资源加载器接口
    /// </summary>
    public interface IResLoader
    {
        /// <summary>
        /// 同步加载 (仅 Resources 支持，AA 不支持)
        /// </summary>
        T Load<T>(string path) where T : Object;

        /// <summary>
        /// 异步加载
        /// </summary>
        UniTask<T> LoadAsync<T>(string path) where T : Object;

        /// <summary>
        /// 批量预加载
        /// </summary>
        UniTask PreloadAsync(IList<string> paths, Action<float> onProgress = null);

        /// <summary>
        /// 卸载单个资源 (引用计数 -1)
        /// </summary>
        void Unload(string path);

        /// <summary>
        /// 释放所有加载过的资源引用
        /// </summary>
        void ReleaseAll();
    }
}