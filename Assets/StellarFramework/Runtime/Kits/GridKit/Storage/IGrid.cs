namespace StellarFramework
{
    /// <summary>可写二维逻辑网格契约。</summary>
    public interface IGrid<T> : IReadOnlyGrid<T>
    {
        /// <summary>读取或写入指定坐标；实现可在越界时抛出参数范围异常。</summary>
        new T this[GridCoord coord] { get; set; }

        /// <summary>尝试写入一个坐标；越界时不修改网格并返回 false。</summary>
        bool TrySet(GridCoord coord, T value);
    }
}
