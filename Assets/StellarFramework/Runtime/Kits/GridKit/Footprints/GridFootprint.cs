using System;
using System.Collections.Generic;

namespace StellarFramework
{
    /// <summary>不可变、canonical 排序的相对 Footprint。Anchor 不要求包含 (0,0)。</summary>
    public sealed class GridFootprint
    {
        private readonly GridOffset[] _offsets;
        private readonly IReadOnlyList<GridOffset> _readOnlyOffsets;

        public int CellCount => _offsets.Length;
        public GridRect RelativeBounds { get; }
        public IReadOnlyList<GridOffset> Offsets => _readOnlyOffsets;

        public GridFootprint(params GridOffset[] offsets)
            : this((IEnumerable<GridOffset>)offsets)
        {
        }

        public GridFootprint(IEnumerable<GridOffset> offsets)
        {
            if (offsets == null) throw new ArgumentNullException(nameof(offsets));
            var sorted = new List<GridOffset>();
            foreach (GridOffset offset in offsets) sorted.Add(offset);
            if (sorted.Count == 0) throw new ArgumentException("A GridFootprint must contain at least one cell.", nameof(offsets));

            sorted.Sort(CompareCanonical);
            for (int i = 1; i < sorted.Count; i++)
            {
                if (sorted[i] == sorted[i - 1])
                {
                    throw new ArgumentException("GridFootprint cannot contain duplicate offsets.", nameof(offsets));
                }
            }

            _offsets = sorted.ToArray();
            _readOnlyOffsets = Array.AsReadOnly(_offsets);

            long minX = _offsets[0].X;
            long minY = _offsets[0].Y;
            long maxX = _offsets[0].X + 1L;
            long maxY = _offsets[0].Y + 1L;
            for (int i = 1; i < _offsets.Length; i++)
            {
                GridOffset offset = _offsets[i];
                minX = Math.Min(minX, offset.X);
                minY = Math.Min(minY, offset.Y);
                maxX = Math.Max(maxX, (long)offset.X + 1L);
                maxY = Math.Max(maxY, (long)offset.Y + 1L);
            }

            long width = maxX - minX;
            long height = maxY - minY;
            if (width > int.MaxValue || height > int.MaxValue)
            {
                throw new OverflowException("GridFootprint relative bounds exceed Int32 size.");
            }

            RelativeBounds = new GridRect(
                new GridCoord(checked((int)minX), checked((int)minY)),
                new GridSize((int)width, (int)height));
        }

        public GridOffset GetOffset(int index)
        {
            if (index < 0 || index >= _offsets.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _offsets[index];
        }

        public ReadOnlySpan<GridOffset> AsReadOnlySpan() => _offsets.AsSpan();

        public bool TryWriteCells(GridCoord anchor, GridTransform transform, Span<GridCoord> destination,
            out int written)
        {
            if (destination.Length < _offsets.Length)
            {
                throw new ArgumentException("Destination buffer is smaller than footprint cell count.", nameof(destination));
            }

            written = 0;
            for (int i = 0; i < _offsets.Length; i++)
            {
                if (!transform.TryApply(_offsets[i], out GridOffset transformed) ||
                    !GridMath.TryOffset(anchor, transformed, out _))
                {
                    return false;
                }
            }

            for (int i = 0; i < _offsets.Length; i++)
            {
                transform.TryApply(_offsets[i], out GridOffset transformed);
                GridMath.TryOffset(anchor, transformed, out GridCoord cell);
                destination[i] = cell;
            }

            written = _offsets.Length;
            return true;
        }

        public int WriteCells(GridCoord anchor, GridTransform transform, Span<GridCoord> destination)
        {
            if (!TryWriteCells(anchor, transform, destination, out int written))
            {
                throw new OverflowException("GridFootprint cell coordinate overflowed Int32.");
            }

            return written;
        }

        private static int CompareCanonical(GridOffset left, GridOffset right)
        {
            int y = left.Y.CompareTo(right.Y);
            return y != 0 ? y : left.X.CompareTo(right.X);
        }
    }
}
