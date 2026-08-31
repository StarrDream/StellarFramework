using System;

namespace StellarFramework
{
    public static class GridDistance
    {
        public static long Manhattan(GridCoord a, GridCoord b)
        {
            long dx = Math.Abs((long)a.X - b.X);
            long dy = Math.Abs((long)a.Y - b.Y);
            return checked(dx + dy);
        }

        public static long Chebyshev(GridCoord a, GridCoord b)
        {
            long dx = Math.Abs((long)a.X - b.X);
            long dy = Math.Abs((long)a.Y - b.Y);
            return Math.Max(dx, dy);
        }
    }
}
