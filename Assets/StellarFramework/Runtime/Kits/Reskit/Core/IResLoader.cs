using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Object = UnityEngine.Object;

namespace StellarFramework.Res
{
    /// <summary>
    /// ResKit 后端加载器的最小公共契约。
    /// </summary>
    /// <remarks>
    /// Loader 表示一个明确的资源 Owner。成功加载的路径会由该 Loader 持有引用，
    /// 直到调用 <see cref="Unload"/>、<see cref="ReleaseAll"/> 或回收 Loader。
    /// 不同后端的同步能力并不对称；业务代码不确定时应优先使用异步接口。
    /// </remarks>
    public interface IResLoader
    {
        /// <summary>
        /// 同步加载资源。
        /// </summary>
        /// <remarks>是否支持同步加载由具体后端决定。</remarks>
        /// <param name="path">后端使用的稳定资源地址。</param>
        /// <returns>成功时返回资源；地址无效或后端加载失败时返回 null。</returns>
        T Load<T>(string path) where T : Object;

        /// <summary>
        /// 异步加载资源。
        /// </summary>
        /// <remarks>
        /// 调用方取消会以 <see cref="OperationCanceledException"/> 向上传播，不会被转换成 null。
        /// 同一路径的底层物理加载可与其他 Loader/Owner 共享，但每个 Owner 的等待取消彼此独立。
        /// </remarks>
        UniTask<T> LoadAsync<T>(string path, CancellationToken cancellationToken = default) where T : Object;

        /// <summary>
        /// 批量预加载，并由当前 Loader 持有成功加载的资源引用。
        /// </summary>
        UniTask PreloadAsync(IList<string> paths, Action<float> onProgress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 释放当前 Loader 对指定路径的一份持有关系。
        /// </summary>
        void Unload(string path);

        /// <summary>
        /// 释放当前 Loader 持有的全部资源引用。
        /// </summary>
        void ReleaseAll();

        /// <summary>
        /// 释放全部资源并把 Loader 归还其对应对象池。
        /// 普通业务优先通过 <see cref="ResScope.Dispose"/> 自动完成该流程。
        /// </summary>
        void RecycleToPool();
    }
}
