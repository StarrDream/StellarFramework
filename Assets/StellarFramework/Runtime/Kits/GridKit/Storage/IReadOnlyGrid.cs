namespace StellarFramework
{
    /// <summary>
    /// 只读二维逻辑网格契约。坐标使用 <see cref="GridCoord"/>，越界行为由 Try 与索引器语义明确区分。
    /// </summary>
    public interface IReadOnlyGrid<T>
    {
        /// <summary>获取该网格可访问的固定逻辑边界。</summary>
        GridRect Bounds { get; }

        /// <summary>判断坐标是否位于当前边界内。</summary>
        bool Contains(GridCoord coord);

        /// <summary>无异常地读取一个坐标；越界时返回 false。</summary>
        bool TryGet(GridCoord coord, out T value);

        /// <summary>读取指定坐标；实现可在越界时抛出参数范围异常。</summary>
        T this[GridCoord coord] { get; }
    }
}
