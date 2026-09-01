using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class GridKitTests
    {
        [Test]
        public void GridCoordSupportsNegativeValuesEqualityAndHashUsage()
        {
            var coord = new GridCoord(-100, 20);
            Assert.That(coord.X, Is.EqualTo(-100));
            Assert.That(coord.Y, Is.EqualTo(20));
            Assert.That(coord, Is.EqualTo(new GridCoord(-100, 20)));
            Assert.That(coord, Is.Not.EqualTo(new GridCoord(-100, 21)));

            var map = new Dictionary<GridCoord, string> { [coord] = "negative" };
            Assert.That(map[new GridCoord(-100, 20)], Is.EqualTo("negative"));
        }

        [Test]
        public void GridOffsetArithmeticIsChecked()
        {
            Assert.That(new GridCoord(2, 3) + new GridOffset(-4, 5), Is.EqualTo(new GridCoord(-2, 8)));
            Assert.That(new GridCoord(2, 3) - new GridOffset(-4, 5), Is.EqualTo(new GridCoord(6, -2)));
            Assert.That(() => new GridOffset(int.MaxValue, 1) - new GridOffset(-1, 0),
                Throws.TypeOf<OverflowException>());
            Assert.That(() => new GridCoord(int.MaxValue, 0) + new GridOffset(1, 0), Throws.TypeOf<OverflowException>());
            Assert.That(() => new GridCoord(int.MinValue, 0) - new GridOffset(1, 0), Throws.TypeOf<OverflowException>());
            Assert.That(GridMath.TryOffset(new GridCoord(int.MaxValue, 0), new GridOffset(1, 0), out _), Is.False);
        }

        [Test]
        public void FloorDivAndFloorModPreserveEuclideanIdentity()
        {
            int[] divisors = { 1, 2, 3, 7, 16, 32, 64 };
            for (int value = -1000; value <= 1000; value++)
            {
                for (int i = 0; i < divisors.Length; i++)
                {
                    int divisor = divisors[i];
                    int quotient = GridMath.FloorDiv(value, divisor);
                    int remainder = GridMath.FloorMod(value, divisor);
                    Assert.That((long)quotient * divisor + remainder, Is.EqualTo(value));
                    Assert.That(remainder, Is.GreaterThanOrEqualTo(0));
                    Assert.That(remainder, Is.LessThan(divisor));
                }
            }

            Assert.That(GridMath.FloorDiv(-1, 32), Is.EqualTo(-1));
            Assert.That(GridMath.FloorMod(-1, 32), Is.EqualTo(31));
            Assert.That(GridMath.FloorDiv(-32, 32), Is.EqualTo(-1));
            Assert.That(GridMath.FloorMod(-32, 32), Is.EqualTo(0));
            Assert.That(GridMath.FloorDiv(-33, 32), Is.EqualTo(-2));
            Assert.That(GridMath.FloorMod(-33, 32), Is.EqualTo(31));
            Assert.That(GridMath.FloorDiv(32, 32), Is.EqualTo(1));
            Assert.That(GridMath.FloorMod(32, 32), Is.EqualTo(0));
            Assert.That(() => GridMath.FloorDiv(1, 0), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => GridMath.FloorMod(1, -1), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void GridSizeAndRectUseLongAreaAndHalfOpenBounds()
        {
            var size = new GridSize(3, 2);
            Assert.That(size.Area, Is.EqualTo(6L));
            Assert.That(new GridSize(0, 4).Area, Is.EqualTo(0L));
            Assert.That(() => new GridSize(-1, 2), Throws.TypeOf<ArgumentOutOfRangeException>());

            var rect = new GridRect(new GridCoord(10, 20), size);
            Assert.That(rect.Contains(new GridCoord(10, 20)), Is.True);
            Assert.That(rect.Contains(new GridCoord(12, 21)), Is.True);
            Assert.That(rect.Contains(new GridCoord(13, 20)), Is.False);
            Assert.That(rect.Contains(new GridCoord(10, 22)), Is.False);
            Assert.That(rect.MaxExclusiveX, Is.EqualTo(13L));
            Assert.That(rect.MaxExclusiveY, Is.EqualTo(22L));
            Assert.That(rect.Area, Is.EqualTo(6L));
            Assert.That(new GridRect(new GridCoord(13, 20), new GridSize(3, 2)).Overlaps(rect), Is.False);
            Assert.That(rect.Contains(new GridRect(new GridCoord(11, 21), new GridSize(1, 1))), Is.True);
            Assert.That(rect.Contains(new GridRect(new GridCoord(0, 0), new GridSize(0, 0))), Is.True);
        }

        [Test]
        public void GridRectIntersectionTranslationEmptyAndExtremesAreSafe()
        {
            var left = new GridRect(new GridCoord(0, 0), new GridSize(3, 3));
            var right = new GridRect(new GridCoord(2, 1), new GridSize(3, 4));
            Assert.That(left.TryIntersect(right, out GridRect intersection), Is.True);
            Assert.That(intersection, Is.EqualTo(new GridRect(new GridCoord(2, 1), new GridSize(1, 2))));
            Assert.That(left.TryIntersect(new GridRect(new GridCoord(3, 0), new GridSize(1, 1)), out _), Is.False);

            var empty = new GridRect(new GridCoord(42, 42), new GridSize(0, 5));
            Assert.That(empty.IsEmpty, Is.True);
            Assert.That(empty.Area, Is.EqualTo(0L));
            Assert.That(empty.Contains(new GridCoord(42, 42)), Is.False);
            Assert.That(empty.GetEnumerator().MoveNext(), Is.False);

            var extreme = new GridRect(new GridCoord(int.MaxValue, int.MinValue), new GridSize(1, 1));
            Assert.That(extreme.Contains(new GridCoord(int.MaxValue, int.MinValue)), Is.True);
            Assert.That(extreme.MaxExclusiveX, Is.EqualTo((long)int.MaxValue + 1L));
            Assert.That(extreme.MaxExclusiveY, Is.EqualTo((long)int.MinValue + 1L));
            Assert.That(() => extreme.Translate(new GridOffset(1, 0)), Throws.TypeOf<OverflowException>());
            Assert.That(() => new GridRect(new GridCoord(int.MinValue, 0), new GridSize(1, 1))
                .Translate(new GridOffset(-1, 0)), Throws.TypeOf<OverflowException>());
        }

        [Test]
        public void GridRectEnumerationIsStableRowMajor()
        {
            var rect = new GridRect(new GridCoord(-1, -1), new GridSize(3, 2));
            var actual = new List<GridCoord>();
            foreach (GridCoord coord in rect) actual.Add(coord);

            Assert.That(actual, Is.EqualTo(new[]
            {
                new GridCoord(-1, -1), new GridCoord(0, -1), new GridCoord(1, -1),
                new GridCoord(-1, 0), new GridCoord(0, 0), new GridCoord(1, 0)
            }));
        }

        [Test]
        public void DenseGridUsesFixedNegativeOriginRowMajorStorageAndRoundTrips()
        {
            var bounds = new GridRect(new GridCoord(-2, -1), new GridSize(3, 2));
            var grid = new DenseGrid<int>(bounds);
            Assert.That(grid.Width, Is.EqualTo(3));
            Assert.That(grid.Height, Is.EqualTo(2));
            Assert.That(grid.Count, Is.EqualTo(6));

            for (int i = 0; i < grid.Count; i++)
            {
                GridCoord coord = grid.GetCoord(i);
                Assert.That(grid.GetIndex(coord), Is.EqualTo(i));
                Assert.That(grid.TryGetCoord(i, out GridCoord roundTrip), Is.True);
                Assert.That(roundTrip, Is.EqualTo(coord));
            }

            grid[new GridCoord(-1, 0)] = 42;
            Assert.That(grid[new GridCoord(-1, 0)], Is.EqualTo(42));
            Assert.That(grid.TryGet(new GridCoord(10, 10), out _), Is.False);
            Assert.That(grid.TrySet(new GridCoord(10, 10), 1), Is.False);
            Assert.That(() => grid.GetIndex(new GridCoord(10, 10)), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => grid.GetCoord(-1), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => grid.GetCoord(grid.Count), Throws.TypeOf<ArgumentOutOfRangeException>());

            ref int cell = ref grid.GetRef(new GridCoord(-2, -1));
            cell = 7;
            Assert.That(grid.GetRefReadOnly(new GridCoord(-2, -1)), Is.EqualTo(7));
            ref int byIndex = ref grid.GetRefByIndex(5);
            byIndex = 9;
            Assert.That(grid[grid.GetCoord(5)], Is.EqualTo(9));

            grid.Fill(3);
            Assert.That(grid.AsReadOnlySpan().ToArray(), Is.EqualTo(new[] { 3, 3, 3, 3, 3, 3 }));
            grid.Clear();
            Assert.That(grid.AsReadOnlySpan().ToArray(), Is.EqualTo(new[] { 0, 0, 0, 0, 0, 0 }));
            grid.CopyFrom(new[] { 1, 2, 3, 4, 5, 6 });
            var copy = new int[6];
            grid.CopyTo(copy);
            Assert.That(copy, Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6 }));
            Assert.That(() => grid.CopyFrom(new int[5]), Throws.TypeOf<ArgumentException>());
            Assert.That(() => grid.CopyTo(new int[7]), Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void DenseGridAllowsEmptyButRejectsArrayTooLarge()
        {
            var empty = new DenseGrid<int>(new GridRect(new GridCoord(-4, 9), new GridSize(0, 10)));
            Assert.That(empty.Count, Is.EqualTo(0));
            Assert.That(empty.TryGetCoord(0, out _), Is.False);
            Assert.That(empty.TryGetIndex(new GridCoord(-4, 9), out _), Is.False);
            Assert.That(() => empty.GetCoord(0), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new DenseGrid<byte>(new GridRect(new GridCoord(0, 0), new GridSize(int.MaxValue, 2))),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void NeighborOrderBoundsFilteringAndExtremeCoordinatesAreStable()
        {
            var four = new GridCoord[4];
            int count4 = GridNeighbors.WriteNeighbors4(new GridCoord(0, 0), four);
            Assert.That(count4, Is.EqualTo(4));
            Assert.That(four, Is.EqualTo(new[]
            {
                new GridCoord(0, 1), new GridCoord(1, 0), new GridCoord(0, -1), new GridCoord(-1, 0)
            }));

            var eight = new GridCoord[8];
            int count8 = GridNeighbors.WriteNeighbors8(new GridCoord(0, 0), eight);
            Assert.That(count8, Is.EqualTo(8));
            Assert.That(eight, Is.EqualTo(new[]
            {
                new GridCoord(0, 1), new GridCoord(1, 1), new GridCoord(1, 0), new GridCoord(1, -1),
                new GridCoord(0, -1), new GridCoord(-1, -1), new GridCoord(-1, 0), new GridCoord(-1, 1)
            }));

            var bounds = new GridRect(new GridCoord(0, 0), new GridSize(2, 2));
            count4 = GridNeighbors.WriteNeighbors4(new GridCoord(0, 0), bounds, four);
            Assert.That(count4, Is.EqualTo(2));
            Assert.That(four[0], Is.EqualTo(new GridCoord(0, 1)));
            Assert.That(four[1], Is.EqualTo(new GridCoord(1, 0)));

            count8 = GridNeighbors.WriteNeighbors8(new GridCoord(int.MaxValue, int.MaxValue), eight);
            Assert.That(count8, Is.EqualTo(3));
            Assert.That(() => GridNeighbors.WriteNeighbors4(new GridCoord(0, 0), new GridCoord[3]),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void FootprintIsCanonicalImmutableAndTransformOrderIsStable()
        {
            Assert.That(() => new GridFootprint(), Throws.TypeOf<ArgumentException>());
            Assert.That(() => new GridFootprint(new GridOffset(0, 0), new GridOffset(0, 0)),
                Throws.TypeOf<ArgumentException>());

            var footprint = new GridFootprint(
                new GridOffset(2, 1), new GridOffset(-1, 0), new GridOffset(0, 1), new GridOffset(0, -1));
            Assert.That(footprint.Offsets.ToArray(), Is.EqualTo(new[]
            {
                new GridOffset(0, -1), new GridOffset(-1, 0), new GridOffset(0, 1), new GridOffset(2, 1)
            }));
            Assert.That(footprint.RelativeBounds, Is.EqualTo(
                new GridRect(new GridCoord(-1, -1), new GridSize(4, 3))));

            GridOffset source = new GridOffset(2, 1);
            Assert.That(new GridTransform(GridRotation.Deg0).Apply(source), Is.EqualTo(new GridOffset(2, 1)));
            Assert.That(new GridTransform(GridRotation.Deg90).Apply(source), Is.EqualTo(new GridOffset(1, -2)));
            Assert.That(new GridTransform(GridRotation.Deg180).Apply(source), Is.EqualTo(new GridOffset(-2, -1)));
            Assert.That(new GridTransform(GridRotation.Deg270).Apply(source), Is.EqualTo(new GridOffset(-1, 2)));
            GridOffset cycle = source;
            for (int i = 0; i < 4; i++) cycle = new GridTransform(GridRotation.Deg90).Apply(cycle);
            Assert.That(cycle, Is.EqualTo(source));
            Assert.That(new GridTransform(GridRotation.Deg90, true, false).Apply(source),
                Is.EqualTo(new GridOffset(1, 2)));
            Assert.That(new GridTransform(GridRotation.Deg0, true, false).Apply(new GridOffset(3, 4)),
                Is.EqualTo(new GridOffset(-3, 4)));
            Assert.That(new GridTransform(GridRotation.Deg0, false, true).Apply(new GridOffset(3, 4)),
                Is.EqualTo(new GridOffset(3, -4)));
            Assert.That(new GridTransform(GridRotation.Deg0).Apply(new GridOffset(int.MinValue, 0)),
                Is.EqualTo(new GridOffset(int.MinValue, 0)));
        }

        [Test]
        public void FootprintWritesCallerBufferAndReportsOverflowWithoutAllocationContract()
        {
            var footprint = new GridFootprint(new GridOffset(0, 0), new GridOffset(1, 0));
            var cells = new GridCoord[2];
            Assert.That(footprint.TryWriteCells(new GridCoord(10, 20), GridTransform.Identity, cells, out int written), Is.True);
            Assert.That(written, Is.EqualTo(2));
            Assert.That(cells, Is.EqualTo(new[] { new GridCoord(10, 20), new GridCoord(11, 20) }));
            Assert.That(() => footprint.TryWriteCells(new GridCoord(0, 0), GridTransform.Identity, new GridCoord[1], out _),
                Throws.TypeOf<ArgumentException>());
            Assert.That(footprint.TryWriteCells(new GridCoord(int.MaxValue, 0), GridTransform.Identity, cells, out written), Is.False);
            Assert.That(written, Is.EqualTo(0));
        }

        [Test]
        public void OccupancyUsesIntegerIdsAndAtomicOccupyRelease()
        {
            var occupancy = new GridOccupancy(new GridRect(new GridCoord(0, 0), new GridSize(5, 5)));
            var single = new GridFootprint(new GridOffset(0, 0));
            var multi = new GridFootprint(new GridOffset(0, 0), new GridOffset(1, 0), new GridOffset(0, 1));
            var ownerA = new GridOccupantId(10);
            var ownerB = new GridOccupantId(20);
            var ownerC = new GridOccupantId(30);

            Assert.That(occupancy.TryOccupy(ownerC, new GridCoord(2, 2), single, GridTransform.Identity).Success, Is.True);
            GridOccupantId[] beforeFailure = occupancy.AsReadOnlySpan().ToArray();
            GridOccupancyResult failed = occupancy.TryOccupy(ownerB, new GridCoord(2, 1), multi, GridTransform.Identity);
            Assert.That(failed.Success, Is.False);
            Assert.That(failed.Error, Is.EqualTo(GridOccupancyError.Occupied));
            Assert.That(failed.ConflictCoord, Is.EqualTo(new GridCoord(2, 2)));
            Assert.That(failed.ExistingOccupant, Is.EqualTo(ownerC));
            Assert.That(occupancy.IsOccupied(new GridCoord(2, 1)), Is.False);
            Assert.That(occupancy.IsOccupied(new GridCoord(3, 1)), Is.False);
            Assert.That(occupancy.AsReadOnlySpan().ToArray(), Is.EqualTo(beforeFailure));

            Assert.That(occupancy.TryOccupy(ownerA, new GridCoord(1, 1), multi, GridTransform.Identity).Success, Is.True);
            Assert.That(occupancy.CanOccupy(ownerA, new GridCoord(1, 1), multi, GridTransform.Identity,
                ownerA).Success, Is.True);
            Assert.That(occupancy.TryOccupy(ownerA, new GridCoord(1, 1), multi, GridTransform.Identity).Error,
                Is.EqualTo(GridOccupancyError.Occupied));
            Assert.That(occupancy.TryGetOccupant(new GridCoord(1, 1), out GridOccupantId retainedA0), Is.True);
            Assert.That(retainedA0, Is.EqualTo(ownerA));
            Assert.That(occupancy.TryGetOccupant(new GridCoord(2, 1), out GridOccupantId retainedA1), Is.True);
            Assert.That(retainedA1, Is.EqualTo(ownerA));
            Assert.That(occupancy.TryGetOccupant(new GridCoord(1, 2), out GridOccupantId retainedA2), Is.True);
            Assert.That(retainedA2, Is.EqualTo(ownerA));
            Assert.That(occupancy.TryRelease(ownerA, new GridCoord(1, 1), multi, GridTransform.Identity).Success, Is.True);
            Assert.That(occupancy.IsOccupied(new GridCoord(1, 1)), Is.False);
            Assert.That(occupancy.IsOccupied(new GridCoord(2, 1)), Is.False);
            Assert.That(occupancy.TryRelease(ownerA, new GridCoord(1, 1), multi, GridTransform.Identity).Error,
                Is.EqualTo(GridOccupancyError.NotOwned));
            Assert.That(occupancy.TryOccupy(default(GridOccupantId), new GridCoord(0, 0), single, GridTransform.Identity).Error,
                Is.EqualTo(GridOccupancyError.InvalidOccupant));
            Assert.That(occupancy.TryOccupy(ownerA, new GridCoord(4, 4), multi, GridTransform.Identity).Error,
                Is.EqualTo(GridOccupancyError.OutOfBounds));

            occupancy.Clear();
            Assert.That(occupancy.IsOccupied(new GridCoord(2, 2)), Is.False);
        }

        [Test]
        public void TryOccupyOnlyExposesEmptyToOwnerCommit()
        {
            MethodInfo[] methods = typeof(GridOccupancy).GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(method => method.Name == nameof(GridOccupancy.TryOccupy))
                .ToArray();

            Assert.That(methods, Has.Length.EqualTo(1));
            Assert.That(methods[0].GetParameters(), Has.Length.EqualTo(4));
        }

        [Test]
        public void CanOccupyAllowedExistingOccupantSupportsSelfOverlapWithoutMutation()
        {
            var bounds = new GridRect(new GridCoord(0, 0), new GridSize(6, 4));
            var occupancy = new GridOccupancy(bounds);
            var owner = new GridOccupantId(10);
            var oldFootprint = new GridFootprint(new GridOffset(0, 0), new GridOffset(1, 0));
            var previewFootprint = new GridFootprint(
                new GridOffset(0, 0), new GridOffset(1, 0), new GridOffset(2, 0));
            var anchor = new GridCoord(1, 1);

            Assert.That(occupancy.CanOccupy(owner, anchor, previewFootprint, GridTransform.Identity).Success, Is.True);
            Assert.That(occupancy.TryOccupy(owner, anchor, oldFootprint, GridTransform.Identity).Success, Is.True);
            GridOccupantId[] beforePreview = occupancy.AsReadOnlySpan().ToArray();

            GridOccupancyResult preview = occupancy.CanOccupy(
                owner, anchor, previewFootprint, GridTransform.Identity, owner);

            Assert.That(preview.Success, Is.True);
            Assert.That(occupancy.AsReadOnlySpan().ToArray(), Is.EqualTo(beforePreview));
            Assert.That(occupancy.TryGetOccupant(new GridCoord(3, 1), out GridOccupantId newCell), Is.True);
            Assert.That(newCell.IsEmpty, Is.True);
        }

        [Test]
        public void CanOccupyAllowedExistingOccupantDoesNotIgnoreOtherOwners()
        {
            var bounds = new GridRect(new GridCoord(0, 0), new GridSize(6, 4));
            var occupancy = new GridOccupancy(bounds);
            var ownerA = new GridOccupantId(10);
            var ownerB = new GridOccupantId(20);
            var single = new GridFootprint(new GridOffset(0, 0));
            var candidate = new GridFootprint(
                new GridOffset(0, 0), new GridOffset(1, 0), new GridOffset(2, 0));
            var anchor = new GridCoord(1, 1);

            Assert.That(occupancy.TryOccupy(ownerA, anchor, single, GridTransform.Identity).Success, Is.True);
            Assert.That(occupancy.TryOccupy(ownerB, new GridCoord(2, 1), single, GridTransform.Identity).Success, Is.True);
            GridOccupantId[] beforePreview = occupancy.AsReadOnlySpan().ToArray();

            GridOccupancyResult preview = occupancy.CanOccupy(
                ownerA, anchor, candidate, GridTransform.Identity, ownerA);

            Assert.That(preview.Success, Is.False);
            Assert.That(preview.Error, Is.EqualTo(GridOccupancyError.Occupied));
            Assert.That(preview.ConflictCoord, Is.EqualTo(new GridCoord(2, 1)));
            Assert.That(preview.ExistingOccupant, Is.EqualTo(ownerB));
            Assert.That(occupancy.AsReadOnlySpan().ToArray(), Is.EqualTo(beforePreview));
        }

        [Test]
        public void OccupancyReleaseFailureIsAtomicAndTransformsAreApplied()
        {
            var occupancy = new GridOccupancy(new GridRect(new GridCoord(-3, -3), new GridSize(8, 8)));
            var single = new GridFootprint(new GridOffset(0, 0));
            var owner = new GridOccupantId(1);
            var other = new GridOccupantId(2);
            Assert.That(occupancy.TryOccupy(other, new GridCoord(0, 0), single, GridTransform.Identity).Success, Is.True);
            Assert.That(occupancy.TryOccupy(owner, new GridCoord(-1, 0), single, GridTransform.Identity).Success, Is.True);
            var releaseFootprint = new GridFootprint(new GridOffset(0, 0), new GridOffset(1, 0));
            GridOccupancyResult release = occupancy.TryRelease(owner, new GridCoord(-1, 0), releaseFootprint, GridTransform.Identity);
            Assert.That(release.Error, Is.EqualTo(GridOccupancyError.NotOwned));
            Assert.That(occupancy.TryGetOccupant(new GridCoord(-1, 0), out GridOccupantId retained), Is.True);
            Assert.That(retained, Is.EqualTo(owner));
            Assert.That(occupancy.TryGetOccupant(new GridCoord(0, 0), out GridOccupantId retainedOther), Is.True);
            Assert.That(retainedOther, Is.EqualTo(other));

            var rotated = new GridFootprint(new GridOffset(0, 0), new GridOffset(1, 0));
            Assert.That(occupancy.TryOccupy(owner, new GridCoord(2, 2), rotated, new GridTransform(GridRotation.Deg90)).Success,
                Is.True);
            Assert.That(occupancy.IsOccupied(new GridCoord(2, 1)), Is.True);
        }

        [Test]
        public void DistanceAndOccupantValidationAreLongSafe()
        {
            Assert.That(GridDistance.Manhattan(new GridCoord(int.MinValue, int.MinValue),
                new GridCoord(int.MaxValue, int.MaxValue)), Is.EqualTo(8589934590L));
            Assert.That(GridDistance.Chebyshev(new GridCoord(int.MinValue, 0),
                new GridCoord(int.MaxValue, 0)), Is.EqualTo(4294967295L));
            Assert.That(new GridOccupantId(0).IsEmpty, Is.True);
            Assert.That(new GridOccupantId(1).IsValid, Is.True);
            Assert.That(() => new GridOccupantId(-1), Throws.TypeOf<ArgumentOutOfRangeException>());
        }
    }
}
