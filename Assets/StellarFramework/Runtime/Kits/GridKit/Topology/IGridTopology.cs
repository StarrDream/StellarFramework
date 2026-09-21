using System;

namespace StellarFramework
{
    /// <summary>
    /// Allocation-free logical topology boundary.
    /// Implementations write neighbors into caller-owned buffers and define logical distance.
    /// </summary>
    public interface IGridTopology<TCoord>
        where TCoord : struct
    {
        /// <summary>获取单个坐标可能产生的最大直接邻居数量。</summary>
        int MaxNeighborCount { get; }

        /// <summary>将 center 的直接邻居写入调用方缓冲区。</summary>
        /// <returns>实际写入数量；调用方至少应提供 <see cref="MaxNeighborCount"/> 容量。</returns>
        int WriteNeighbors(TCoord center, Span<TCoord> destination);

        /// <summary>获取该拓扑定义下的无障碍逻辑距离。</summary>
        long GetDistance(TCoord from, TCoord to);
    }
}
