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
        /// <summary>获取矩形最小坐标；X/Y 均为 inclusive。</summary>
        public GridCoord Min { get; }

        /// <summary>获取矩形尺寸。</summary>
        public GridSize Size { get; }

        /// <summary>获取 X 方向 exclusive 最大边界。</summary>
        public long MaxExclusiveX => (long)Min.X + Size.Width;

        /// <summary>获取 Y 方向 exclusive 最大边界。</summary>
        public long MaxExclusiveY => (long)Min.Y + Size.Height;

        /// <summary>获取矩形是否为空；任一维长度为 0 即为空。</summary>
        public bool IsEmpty => Size.Width == 0 || Size.Height == 0;

        /// <summary>获取矩形包含的 cell 数。</summary>
        public long Area => Size.Area;

        /// <summary>创建采用 Min Inclusive / Max Exclusive 语义的矩形。</summary>
        /// <exception cref="ArgumentOutOfRangeException">矩形会覆盖到 Int32 可表示坐标之外。</exception>
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

        /// <summary>判断一个绝对坐标是否位于矩形内。</summary>
        public bool Contains(GridCoord coord)
        {
            return !IsEmpty && coord.X >= Min.X && (long)coord.X < MaxExclusiveX &&
                coord.Y >= Min.Y && (long)coord.Y < MaxExclusiveY;
        }

        /// <summary>判断另一个矩形是否完全位于本矩形内；空矩形始终视为被包含。</summary>
        public bool Contains(GridRect other)
        {
            if (other.IsEmpty) return true;
            return !IsEmpty && other.Min.X >= Min.X && other.MaxExclusiveX <= MaxExclusiveX &&
                other.Min.Y >= Min.Y && other.MaxExclusiveY <= MaxExclusiveY;
        }

        /// <summary>判断两个非空矩形是否存在正面积交叠。</summary>
        public bool Overlaps(GridRect other)
        {
            return !IsEmpty && !other.IsEmpty &&
                Min.X < other.MaxExclusiveX && other.Min.X < MaxExclusiveX &&
                Min.Y < other.MaxExclusiveY && other.Min.Y < MaxExclusiveY;
        }

        /// <summary>尝试计算两个矩形的交集。</summary>
        /// <returns>有正面积交集时为 true；否则输出 default 并返回 false。</returns>
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

        /// <summary>保持尺寸不变，将矩形整体平移指定逻辑位移。</summary>
        public GridRect Translate(GridOffset offset)
        {
            return new GridRect(GridMath.OffsetChecked(Min, offset), Size);
        }

        /// <summary>获取无托管分配的 Row-Major 枚举器。</summary>
        public Enumerator GetEnumerator() => new Enumerator(this);
        IEnumerator<GridCoord> IEnumerable<GridCoord>.GetEnumerator() => new Enumerator(this);
        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this);

        /// <inheritdoc />
        public bool Equals(GridRect other) => Min == other.Min && Size == other.Size;

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is GridRect && Equals((GridRect)obj);

        /// <inheritdoc />
        public override int GetHashCode() => unchecked((Min.GetHashCode() * 397) ^ Size.GetHashCode());

        /// <inheritdoc />
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

            /// <inheritdoc />
            public GridCoord Current => _current;
            object IEnumerator.Current => _current;

            /// <inheritdoc />
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

            /// <inheritdoc />
            public void Reset()
            {
                _x = _rect.Min.X;
                _y = _rect.Min.Y;
                _started = false;
                _current = default(GridCoord);
            }

            /// <inheritdoc />
            public void Dispose() { }
        }
    }
}
