using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Object = UnityEngine.Object;

namespace StellarFramework.Res
{
    /// <summary>
    /// 一组资源引用的明确生命周期边界。
    /// </summary>
    /// <remarks>
    /// 这是 ResKit 面向普通业务代码的推荐入口：
    /// Scope 内成功加载的资源由同一个 IResLoader 持有，Dispose 时统一释放并回收 Loader。
    /// Scope Dispose 还会取消仍在等待的异步请求，避免页面/系统已经销毁后继续回调旧业务。
    /// </remarks>
    public sealed class ResScope : IDisposable
    {
        private IResLoader _loader;
        private CancellationTokenSource _lifetimeCts;
        private bool _isDisposed;

        internal ResScope(IResLoader loader)
        {
            _loader = loader ?? throw new ArgumentNullException(nameof(loader));
            _lifetimeCts = new CancellationTokenSource();
        }

        /// <summary>
        /// Scope 是否已经释放。
        /// </summary>
        public bool IsDisposed => _isDisposed;

        /// <summary>
        /// 底层 Loader。高级场景可读取具体能力，但普通业务优先使用 Scope 自身 API。
        /// </summary>
        public IResLoader Loader
        {
            get
            {
                ThrowIfDisposed();
                return _loader;
            }
        }

        /// <summary>
        /// 同步加载资源。仅在底层 Loader 支持同步加载时使用。
        /// </summary>
        public T Load<T>(string path) where T : Object
        {
            ThrowIfDisposed();
            return _loader.Load<T>(path);
        }

        /// <summary>
        /// 异步加载资源。
        /// 调用方 CancellationToken 与 Scope 生命周期同时生效：任一取消都会结束本次等待。
        /// </summary>
        public async UniTask<T> LoadAsync<T>(
            string path,
            CancellationToken cancellationToken = default)
            where T : Object
        {
            ThrowIfDisposed();

            using (CancellationTokenSource linkedCts =
                   CreateLinkedCancellation(cancellationToken))
            {
                return await _loader.LoadAsync<T>(path, linkedCts.Token);
            }
        }

        /// <summary>
        /// 批量预加载资源，并把成功加载的引用归属于当前 Scope。
        /// </summary>
        public async UniTask PreloadAsync(
            IList<string> paths,
            Action<float> onProgress = null,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            using (CancellationTokenSource linkedCts =
                   CreateLinkedCancellation(cancellationToken))
            {
                await _loader.PreloadAsync(paths, onProgress, linkedCts.Token);
            }
        }

        /// <summary>
        /// 提前释放 Scope 中指定路径的一份持有引用。
        /// </summary>
        public void Release(string path)
        {
            ThrowIfDisposed();
            _loader.Unload(path);
        }

        /// <summary>
        /// 提前释放当前 Scope 已持有的全部资源，但保留 Scope/Loader 供后续继续加载。
        /// </summary>
        public void ReleaseAll()
        {
            ThrowIfDisposed();
            _loader.ReleaseAll();
        }

        /// <summary>
        /// 取消当前 Scope 的未完成异步等待，释放全部资源并回收底层 Loader。
        /// 多次 Dispose 安全。
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            CancellationTokenSource lifetimeCts = _lifetimeCts;
            _lifetimeCts = null;
            lifetimeCts?.Cancel();
            lifetimeCts?.Dispose();

            IResLoader loader = _loader;
            _loader = null;
            if (loader != null)
            {
                ResKit.Recycle(loader);
            }
        }

        private CancellationTokenSource CreateLinkedCancellation(CancellationToken callerToken)
        {
            CancellationToken scopeToken = _lifetimeCts.Token;
            return callerToken.CanBeCanceled
                ? CancellationTokenSource.CreateLinkedTokenSource(scopeToken, callerToken)
                : CancellationTokenSource.CreateLinkedTokenSource(scopeToken);
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(ResScope));
            }
        }
    }
}
