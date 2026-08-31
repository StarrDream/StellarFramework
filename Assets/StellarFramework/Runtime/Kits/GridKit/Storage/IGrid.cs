namespace StellarFramework
{
    public interface IGrid<T> : IReadOnlyGrid<T>
    {
        new T this[GridCoord coord] { get; set; }
        bool TrySet(GridCoord coord, T value);
    }
}
