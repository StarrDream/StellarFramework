namespace StellarFramework.WorldGenKit.Resources
{
    public struct WorldOccupancyCellState
    {
        public WorldOccupancyMask Occupied { get; private set; }
        public WorldOccupancyMask Excluded { get; private set; }

        public bool CanAccept(WorldOccupancyMask occupies, WorldOccupancyMask excludes) =>
            !excludes.Overlaps(Occupied) && !occupies.Overlaps(Excluded);

        public bool TryOccupy(WorldOccupancyMask occupies, WorldOccupancyMask excludes)
        {
            if (!CanAccept(occupies, excludes)) return false;
            Occupied = Occupied.Union(occupies);
            Excluded = Excluded.Union(excludes);
            return true;
        }

        public void Reserve(WorldOccupancyMask occupies, WorldOccupancyMask excludes)
        {
            Occupied = Occupied.Union(occupies);
            Excluded = Excluded.Union(excludes);
        }
    }
}
