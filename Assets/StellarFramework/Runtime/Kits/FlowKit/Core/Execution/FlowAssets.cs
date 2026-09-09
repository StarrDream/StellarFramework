using System;

namespace StellarFramework.FlowKit
{
    /// <summary>资源租约。具体实现可由 Resources、Addressables、AssetBundle 或项目目录适配器提供。</summary>
    public interface IFlowAssetLease : IDisposable
    {
        FlowAssetId AssetId { get; }
        object Value { get; }
    }

    /// <summary>Core 的资源适配边界；FlowKit 不直接引用任何 Unity 资源系统。</summary>
    public interface IFlowAssetResolver
    {
        bool TryResolve(FlowAssetId assetId, out IFlowAssetLease lease);
    }

    public static class FlowAssetResolverExtensions
    {
        /// <summary>需要资源时的 fail-fast 入口；缺失资源不会静默返回 null。</summary>
        public static IFlowAssetLease Resolve(this IFlowAssetResolver resolver, FlowAssetId assetId)
        {
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));
            if (!assetId.IsValid) throw new ArgumentException("AssetId 不能为空。", nameof(assetId));
            if (!resolver.TryResolve(assetId, out IFlowAssetLease lease) || lease == null)
                throw new InvalidOperationException("无法解析 Flow Asset: " + assetId);
            return lease;
        }
    }
}
