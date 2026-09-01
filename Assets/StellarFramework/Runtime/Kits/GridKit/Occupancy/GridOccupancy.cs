using System;

namespace StellarFramework
{
    /// <summary>
    /// 固定 Bounds 的整数 Occupancy。默认非线程安全；单次 TryOccupy/TryRelease 以两遍算法保证原子性。
    /// </summary>
    public sealed class GridOccupancy
    {
        private readonly DenseGrid<GridOccupantId> _cells;

        public GridRect Bounds => _cells.Bounds;
        public int Width => _cells.Width;
        public int Height => _cells.Height;
        public int Count => _cells.Count;

        public GridOccupancy(GridRect bounds)
        {
            _cells = new DenseGrid<GridOccupantId>(bounds, GridOccupantId.None);
        }

        public bool IsOccupied(GridCoord coord)
        {
            return TryGetOccupant(coord, out GridOccupantId occupant) && occupant.IsValid;
        }

        public bool TryGetOccupant(GridCoord coord, out GridOccupantId occupant)
        {
            return _cells.TryGet(coord, out occupant);
        }

        public ReadOnlySpan<GridOccupantId> AsReadOnlySpan() => _cells.AsReadOnlySpan();

        /// <summary>只读检查：目标必须全部为空，调用不会改变 Occupancy。</summary>
        public GridOccupancyResult CanOccupy(GridOccupantId occupant, GridCoord anchor, GridFootprint footprint,
            GridTransform transform)
        {
            ValidateFootprint(footprint);
            return EvaluateEmptyOnly(occupant, anchor, footprint, transform);
        }

        /// <summary>只读 Preview 检查：仅允许忽略指定 self owner，调用不会改变 Occupancy。</summary>
        public GridOccupancyResult CanOccupy(GridOccupantId occupant, GridCoord anchor, GridFootprint footprint,
            GridTransform transform, GridOccupantId allowedExistingOccupant)
        {
            ValidateFootprint(footprint);
            return EvaluateIgnoringAllowed(occupant, anchor, footprint, transform, allowedExistingOccupant);
        }

        /// <summary>只执行 Empty → Owner 提交；任何已有 owner（包括自己）都会失败。</summary>
        public GridOccupancyResult TryOccupy(GridOccupantId occupant, GridCoord anchor, GridFootprint footprint,
            GridTransform transform)
        {
            ValidateFootprint(footprint);
            GridOccupancyResult validation = EvaluateEmptyOnly(occupant, anchor, footprint, transform);
            if (!validation.Success) return validation;

            for (int i = 0; i < footprint.CellCount; i++)
            {
                GridCoord cell = GetCellOrThrow(anchor, footprint.GetOffset(i), transform);
                _cells.GetRef(cell) = occupant;
            }

            return GridOccupancyResult.Succeeded();
        }

        /// <summary>只执行 Owner → Empty 提交；所有目标必须属于指定 owner。</summary>
        public GridOccupancyResult TryRelease(GridOccupantId occupant, GridCoord anchor, GridFootprint footprint,
            GridTransform transform)
        {
            ValidateFootprint(footprint);
            if (!occupant.IsValid)
            {
                return GridOccupancyResult.Failed(GridOccupancyError.InvalidOccupant, default(GridCoord), GridOccupantId.None);
            }

            for (int i = 0; i < footprint.CellCount; i++)
            {
                if (!TryGetCell(anchor, footprint.GetOffset(i), transform, out GridCoord cell))
                {
                    return GridOccupancyResult.Failed(GridOccupancyError.OutOfBounds, default(GridCoord), GridOccupantId.None);
                }

                GridOccupantId existing = _cells.GetRefReadOnly(cell);
                if (existing != occupant)
                {
                    return GridOccupancyResult.Failed(GridOccupancyError.NotOwned, cell, existing);
                }
            }

            for (int i = 0; i < footprint.CellCount; i++)
            {
                GridCoord cell = GetCellOrThrow(anchor, footprint.GetOffset(i), transform);
                _cells.GetRef(cell) = GridOccupantId.None;
            }

            return GridOccupancyResult.Succeeded();
        }

        public void Clear() => _cells.Clear();

        private GridOccupancyResult EvaluateEmptyOnly(GridOccupantId occupant, GridCoord anchor,
            GridFootprint footprint, GridTransform transform)
        {
            if (!occupant.IsValid)
            {
                return GridOccupancyResult.Failed(GridOccupancyError.InvalidOccupant, default(GridCoord), GridOccupantId.None);
            }

            for (int i = 0; i < footprint.CellCount; i++)
            {
                if (!TryGetCell(anchor, footprint.GetOffset(i), transform, out GridCoord cell))
                {
                    return GridOccupancyResult.Failed(GridOccupancyError.OutOfBounds, default(GridCoord), GridOccupantId.None);
                }

                GridOccupantId existing = _cells.GetRefReadOnly(cell);
                if (existing.IsValid)
                {
                    return GridOccupancyResult.Failed(GridOccupancyError.Occupied, cell, existing);
                }
            }

            return GridOccupancyResult.Succeeded();
        }

        private GridOccupancyResult EvaluateIgnoringAllowed(GridOccupantId occupant, GridCoord anchor,
            GridFootprint footprint, GridTransform transform, GridOccupantId allowedExistingOccupant)
        {
            if (!occupant.IsValid)
            {
                return GridOccupancyResult.Failed(GridOccupancyError.InvalidOccupant, default(GridCoord), GridOccupantId.None);
            }

            for (int i = 0; i < footprint.CellCount; i++)
            {
                if (!TryGetCell(anchor, footprint.GetOffset(i), transform, out GridCoord cell))
                {
                    return GridOccupancyResult.Failed(GridOccupancyError.OutOfBounds, default(GridCoord), GridOccupantId.None);
                }

                GridOccupantId existing = _cells.GetRefReadOnly(cell);
                if (existing.IsValid && existing != allowedExistingOccupant)
                {
                    return GridOccupancyResult.Failed(GridOccupancyError.Occupied, cell, existing);
                }
            }

            return GridOccupancyResult.Succeeded();
        }

        private bool TryGetCell(GridCoord anchor, GridOffset offset, GridTransform transform, out GridCoord cell)
        {
            if (!transform.TryApply(offset, out GridOffset transformed) ||
                !GridMath.TryOffset(anchor, transformed, out cell) || !Bounds.Contains(cell))
            {
                cell = default(GridCoord);
                return false;
            }

            return true;
        }

        private GridCoord GetCellOrThrow(GridCoord anchor, GridOffset offset, GridTransform transform)
        {
            if (!TryGetCell(anchor, offset, transform, out GridCoord cell))
            {
                throw new InvalidOperationException("GridOccupancy validation changed during commit.");
            }

            return cell;
        }

        private static void ValidateFootprint(GridFootprint footprint)
        {
            if (footprint == null) throw new ArgumentNullException(nameof(footprint));
        }
    }
}
