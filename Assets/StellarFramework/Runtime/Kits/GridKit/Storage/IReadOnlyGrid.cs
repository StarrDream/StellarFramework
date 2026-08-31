namespace StellarFramework
{
    public interface IReadOnlyGrid<T>
    {
        GridRect Bounds { get; }
        bool Contains(GridCoord coord);
        bool TryGet(GridCoord coord, out T value);
        T this[GridCoord coord] { get; }
    }
}
