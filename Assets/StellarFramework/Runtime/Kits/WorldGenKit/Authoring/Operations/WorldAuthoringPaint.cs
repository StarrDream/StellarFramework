using System;
using StellarFramework.WorldGenKit.Builtins;

namespace StellarFramework.WorldGenKit.Authoring
{
    public static class WorldAuthoringPaint
    {
        public static void Fill<T>(
            WorldDenseOverrideLayer<T> overrides,
            in WorldSampleRect bounds,
            T value)
        {
            if (overrides == null) throw new ArgumentNullException(nameof(overrides));
            WorldPlanarSampleLayout layout = overrides.Layout;
            if (!bounds.FitsWithin(in layout))
                throw new ArgumentOutOfRangeException(nameof(bounds), "Paint bounds exceed authoring layout.");

            int width = layout.Width;
            for (int y = bounds.Y; y < bounds.TopExclusive; y++)
            {
                int row = y * width;
                for (int x = bounds.X; x < bounds.RightExclusive; x++)
                    overrides.SetByIndexUnchecked(row + x, value);
            }
            overrides.MarkDirty(in bounds);
        }
    }
}
