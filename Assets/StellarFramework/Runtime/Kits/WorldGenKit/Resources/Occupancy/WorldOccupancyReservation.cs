using System;

namespace StellarFramework.WorldGenKit.Resources
{
    public readonly struct WorldOccupancyReservation
    {
        public int SampleIndex { get; }
        public WorldOccupancyMask Occupies { get; }
        public WorldOccupancyMask Excludes { get; }

        public WorldOccupancyReservation(
            int sampleIndex,
            WorldOccupancyMask occupies,
            WorldOccupancyMask excludes)
        {
            if (sampleIndex < 0) throw new ArgumentOutOfRangeException(nameof(sampleIndex));
            SampleIndex = sampleIndex;
            Occupies = occupies;
            Excludes = excludes;
        }
    }

    public static class WorldOccupancyReservations
    {
        public static void Apply(
            ReadOnlySpan<WorldOccupancyReservation> reservations,
            Span<WorldOccupancyCellState> cells)
        {
            for (int i = 0; i < reservations.Length; i++)
            {
                if ((uint)reservations[i].SampleIndex >= (uint)cells.Length)
                    throw new ArgumentOutOfRangeException(nameof(reservations), "Reservation sample index exceeds occupancy cell buffer.");
            }

            for (int i = 0; i < reservations.Length; i++)
            {
                WorldOccupancyReservation reservation = reservations[i];
                cells[reservation.SampleIndex].Reserve(reservation.Occupies, reservation.Excludes);
            }
        }
    }
}
