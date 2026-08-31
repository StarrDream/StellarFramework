using System;
using System.Collections;
using System.Collections.Generic;

namespace StellarFramework
{
    /// <summary>
    /// 固定二维矩形，采用 Min Inclusive / Max Exclusive 语义。
    /// 枚举顺序永久为 Y ascending，再按 X ascending。
    /// </summary>
    public readonly struct GridRect : IEquatable<GridRect>, IEnumerable<GridCoord>
    {
        public GridCoord Min { get; }
        public GridSize Size { get; }
        public long MaxExclusiveX => (long)Min.X + Size.Width;
        public long MaxExclusiveY => (long)Min.Y + Size.Height;
        public bool IsEmpty => Size.Width == 0 || Size.Height == 0;
        public long Area => Size.Area;

        public GridRect(GridCoord min, GridSize size)
        {
            long maxExclusiveX = (long)min.X + size.Width;
            long maxExclusiveY = (long)min.Y + size.Height;
            if ((size.Width > 0 && maxExclusiveX > (long)int.MaxValue + 1L) ||
                (size.Height > 0 && maxExclusiveY > (long)int.MaxValue + 1L))
            {
                throw new ArgumentOutOfRangeException(nameof(size),
                    "GridRect contains a coordinate outside the Int32 range.");
            }

            Min = min;
            Size = size;
        }

        public bool Contains(GridCoord coord)
        {
            return !IsEmpty && coord.X >= Min.X && (long)coord.X < MaxExclusiveX &&
                coord.Y >= Min.Y && (long)coord.Y < MaxExclusiveY;
        }

        public bool Contains(GridRect other)
        {
            if (other.IsEmpty) return true;
            return !IsEmpty && other.Min.X >= Min.X && other.MaxExclusiveX <= MaxExclusiveX &&
                other.Min.Y >= Min.Y && other.MaxExclusiveY <= MaxExclusiveY;
        }

        public bool Overlaps(GridRect other)
        {
            return !IsEmpty && !other.IsEmpty &&
                Min.X < other.MaxExclusiveX && other.Min.X < MaxExclusiveX &&
                Min.Y < other.MaxExclusiveY && other.Min.Y < MaxExclusiveY;
        }

        public bool TryIntersect(GridRect other, out GridRect intersection)
        {
            if (!Overlaps(other))
            {
                intersection = default(GridRect);
                return false;
            }

            int minX = Math.Max(Min.X, other.Min.X);
            int minY = Math.Max(Min.Y, other.Min.Y);
            long maxX = Math.Min(MaxExclusiveX, other.MaxExclusiveX);
            long maxY = Math.Min(MaxExclusiveY, other.MaxExclusiveY);
            intersection = new GridRect(
                new GridCoord(minX, minY),
                new GridSize((int)(maxX - minX), (int)(maxY - minY)));
            return true;
        }

        public GridRect Translate(GridOffset offset)
        {
            return new GridRect(GridMath.OffsetChecked(Min, offset), Size);
        }

        public Enumerator GetEnumerator() => new Enumerator(this);
        IEnumerator<GridCoord> IEnumerable<GridCoord>.GetEnumerator() => new Enumerator(this);
        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this);

        public bool Equals(GridRect other) => Min == other.Min && Size == other.Size;
        public override bool Equals(object obj) => obj is GridRect && Equals((GridRect)obj);
        public override int GetHashCode() => unchecked((Min.GetHashCode() * 397) ^ Size.GetHashCode());
        public override string ToString() => string.Format("[{0}, {1}) size {2}", Min, MaxExclusiveX, Size);

        public static bool operator ==(GridRect left, GridRect right) => left.Equals(right);
        public static bool operator !=(GridRect left, GridRect right) => !left.Equals(right);

        /// <summary>无托管分配的 Row-Major 矩形枚举器。</summary>
        public struct Enumerator : IEnumerator<GridCoord>
        {
            private readonly GridRect _rect;
            private readonly long _maxX;
            private readonly long _maxY;
            private long _x;
            private long _y;
            private bool _started;
            private GridCoord _current;

            internal Enumerator(GridRect rect)
            {
                _rect = rect;
                _maxX = rect.MaxExclusiveX;
                _maxY = rect.MaxExclusiveY;
                _x = rect.Min.X;
                _y = rect.Min.Y;
                _started = false;
                _current = default(GridCoord);
            }

            public GridCoord Current => _current;
            object IEnumerator.Current => _current;

            public bool MoveNext()
            {
                if (_rect.IsEmpty) return false;
                if (!_started)
                {
                    _started = true;
                    _current = new GridCoord((int)_x, (int)_y);
                    return true;
                }

                long nextX = _x + 1L;
                if (nextX < _maxX)
                {
                    _x = nextX;
                    _current = new GridCoord((int)_x, (int)_y);
                    return true;
                }

                long nextY = _y + 1L;
                if (nextY >= _maxY) return false;
                _x = _rect.Min.X;
                _y = nextY;
                _current = new GridCoord((int)_x, (int)_y);
                return true;
            }

            public void Reset()
            {
                _x = _rect.Min.X;
                _y = _rect.Min.Y;
                _started = false;
                _current = default(GridCoord);
            }

            public void Dispose() { }
        }
    }
}
