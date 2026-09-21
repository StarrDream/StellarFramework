using System;

namespace StellarFramework.WorldGenKit
{
    public readonly struct WorldCurvePoint
    {
        public double X { get; }
        public double Y { get; }

        public WorldCurvePoint(double x, double y)
        {
            WorldRuleMath.RequireFinite(x, nameof(x));
            WorldRuleMath.RequireFinite(y, nameof(y));
            X = x;
            Y = y;
        }
    }

    /// <summary>Engine-free piecewise-linear curve for authoring/runtime rule evaluation.</summary>
    public sealed class WorldCurve
    {
        private readonly WorldCurvePoint[] _points;

        public int Count => _points.Length;

        public WorldCurve(ReadOnlySpan<WorldCurvePoint> points)
        {
            if (points.Length < 2) throw new ArgumentException("WorldCurve requires at least two points.", nameof(points));
            _points = points.ToArray();
            for (int i = 1; i < _points.Length; i++)
            {
                if (_points[i].X <= _points[i - 1].X)
                    throw new ArgumentException("WorldCurve point X values must be strictly increasing.", nameof(points));
            }
        }

        public double Evaluate(double x)
        {
            WorldRuleMath.RequireFinite(x, nameof(x));
            if (x <= _points[0].X) return _points[0].Y;
            int last = _points.Length - 1;
            if (x >= _points[last].X) return _points[last].Y;

            int low = 0;
            int high = last;
            while (high - low > 1)
            {
                int mid = low + ((high - low) >> 1);
                if (x < _points[mid].X) high = mid;
                else low = mid;
            }

            WorldCurvePoint a = _points[low];
            WorldCurvePoint b = _points[high];
            double t = (x - a.X) / (b.X - a.X);
            return a.Y + ((b.Y - a.Y) * t);
        }
    }
}
