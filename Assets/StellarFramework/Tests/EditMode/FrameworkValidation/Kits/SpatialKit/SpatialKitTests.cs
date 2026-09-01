using System;
using System.Linq;
using NUnit.Framework;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class SpatialKitTests
    {
        [Test]
        public void SpatialIdUsesZeroAsInvalidAndRejectsNegativeValues()
        {
            SpatialId invalid = default(SpatialId);
            SpatialId positive = new SpatialId(42);
            SpatialId max = new SpatialId(int.MaxValue);

            Assert.That(invalid.IsInvalid, Is.True);
            Assert.That(invalid.IsValid, Is.False);
            Assert.That(positive.IsValid, Is.True);
            Assert.That(max.IsValid, Is.True);
            Assert.That(positive, Is.EqualTo(new SpatialId(42)));
            Assert.That(positive.GetHashCode(), Is.EqualTo(new SpatialId(42).GetHashCode()));
            Assert.That(() => new SpatialId(-1), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void SpatialPointAcceptsFiniteNegativeFractionalAndExtremeValues()
        {
            var point = new SpatialPoint(-10.25f, 3.5f);
            Assert.That(point.X, Is.EqualTo(-10.25f));
            Assert.That(point.Y, Is.EqualTo(3.5f));
            Assert.That(default(SpatialPoint), Is.EqualTo(new SpatialPoint(0f, 0f)));
            Assert.That(new SpatialPoint(float.MaxValue, float.MinValue), Is.EqualTo(
                new SpatialPoint(float.MaxValue, float.MinValue)));
            Assert.That(() => new SpatialPoint(float.NaN, 0f), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new SpatialPoint(float.PositiveInfinity, 0f), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new SpatialPoint(0f, float.NegativeInfinity), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void SpatialRectIsFiniteHalfOpenAndAllowsEmptyDimensions()
        {
            var rect = new SpatialRect(-2f, -1f, 2f, 3f);
            Assert.That(rect.Contains(new SpatialPoint(-2f, -1f)), Is.True);
            Assert.That(rect.Contains(new SpatialPoint(1.999f, 2.999f)), Is.True);
            Assert.That(rect.Contains(new SpatialPoint(2f, 0f)), Is.False);
            Assert.That(rect.Contains(new SpatialPoint(0f, 3f)), Is.False);
            Assert.That(rect, Is.EqualTo(new SpatialRect(new SpatialPoint(-2f, -1f), new SpatialPoint(2f, 3f))));
            Assert.That(new SpatialRect(1f, 2f, 1f, 5f).IsEmpty, Is.True);
            Assert.That(new SpatialRect(1f, 2f, 5f, 2f).IsEmpty, Is.True);
            Assert.That(() => new SpatialRect(2f, 0f, 1f, 1f), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new SpatialRect(float.NaN, 0f, 1f, 1f), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new SpatialRect(0f, 0f, float.PositiveInfinity, 1f),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void ConstructorRejectsInvalidBucketSizeAndCapacity()
        {
            Assert.That(() => new SpatialIndex2D(0f), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new SpatialIndex2D(-1f), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new SpatialIndex2D(float.NaN), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new SpatialIndex2D(float.PositiveInfinity), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new SpatialIndex2D(1f, -1), Throws.TypeOf<ArgumentOutOfRangeException>());

            var index = new SpatialIndex2D(2f, 8);
            Assert.That(index.BucketSize, Is.EqualTo(2f));
            Assert.That(index.Count, Is.EqualTo(0));
        }

        [Test]
        public void InsertContainsAndTryGetPositionUseCallerIds()
        {
            var index = new SpatialIndex2D(10f);
            SpatialId id = new SpatialId(1);
            SpatialPoint position = new SpatialPoint(-0.5f, 4.25f);
            SpatialMutationResult result = index.TryInsert(id, position);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Error, Is.EqualTo(SpatialMutationError.None));
            Assert.That(index.Count, Is.EqualTo(1));
            Assert.That(index.Contains(id), Is.True);
            Assert.That(index.TryGetPosition(id, out SpatialPoint actual), Is.True);
            Assert.That(actual, Is.EqualTo(position));
            Assert.That(index.Contains(default(SpatialId)), Is.False);
            Assert.That(index.TryGetPosition(new SpatialId(99), out actual), Is.False);
            Assert.That(actual, Is.EqualTo(default(SpatialPoint)));
        }

        [Test]
        public void InsertFailuresAreAtomicAndReportStableErrors()
        {
            var index = new SpatialIndex2D(1f);
            SpatialId id = new SpatialId(7);
            SpatialPoint oldPosition = new SpatialPoint(0.25f, 0.25f);
            Assert.That(index.TryInsert(id, oldPosition).Success, Is.True);

            SpatialMutationResult duplicate = index.TryInsert(id, new SpatialPoint(9f, 9f));
            Assert.That(duplicate.Error, Is.EqualTo(SpatialMutationError.DuplicateId));
            Assert.That(index.Count, Is.EqualTo(1));
            Assert.That(index.TryGetPosition(id, out SpatialPoint retained), Is.True);
            Assert.That(retained, Is.EqualTo(oldPosition));

            Assert.That(index.TryInsert(default(SpatialId), oldPosition).Error,
                Is.EqualTo(SpatialMutationError.InvalidId));
            Assert.That(index.TryInsert(new SpatialId(8), new SpatialPoint(float.MaxValue, 0f)).Error,
                Is.EqualTo(SpatialMutationError.PositionOutOfRange));
            Assert.That(index.Count, Is.EqualTo(1));
        }

        [Test]
        public void RemoveAndUpdatePreserveAtomicityAcrossBuckets()
        {
            var index = new SpatialIndex2D(10f);
            SpatialId first = new SpatialId(1);
            SpatialId second = new SpatialId(2);
            Assert.That(index.TryInsert(first, new SpatialPoint(1f, 1f)).Success, Is.True);
            Assert.That(index.TryInsert(second, new SpatialPoint(20f, 1f)).Success, Is.True);

            Assert.That(index.TryUpdatePosition(first, new SpatialPoint(9f, 9f)).Success, Is.True);
            Assert.That(index.TryGetPosition(first, out SpatialPoint sameBucket), Is.True);
            Assert.That(sameBucket, Is.EqualTo(new SpatialPoint(9f, 9f)));
            Assert.That(index.TryUpdatePosition(first, new SpatialPoint(11f, 1f)).Success, Is.True);
            Assert.That(index.TryGetPosition(first, out SpatialPoint crossBucket), Is.True);
            Assert.That(crossBucket, Is.EqualTo(new SpatialPoint(11f, 1f)));

            SpatialMutationResult failedUpdate = index.TryUpdatePosition(first, new SpatialPoint(float.MaxValue, 1f));
            Assert.That(failedUpdate.Error, Is.EqualTo(SpatialMutationError.PositionOutOfRange));
            Assert.That(index.TryGetPosition(first, out SpatialPoint retained), Is.True);
            Assert.That(retained, Is.EqualTo(crossBucket));
            Assert.That(index.TryUpdatePosition(new SpatialId(99), new SpatialPoint(0f, 0f)).Error,
                Is.EqualTo(SpatialMutationError.NotFound));

            Assert.That(index.TryRemove(first).Success, Is.True);
            Assert.That(index.TryRemove(first).Error, Is.EqualTo(SpatialMutationError.NotFound));
            Assert.That(index.TryRemove(default(SpatialId)).Error, Is.EqualTo(SpatialMutationError.InvalidId));
            Assert.That(index.Contains(second), Is.True);
            Assert.That(index.Count, Is.EqualTo(1));
        }

        [Test]
        public void NegativeCoordinatesUseMathematicalFloorBuckets()
        {
            var index = new SpatialIndex2D(10f);
            Assert.That(index.TryInsert(new SpatialId(1), new SpatialPoint(-0.1f, 0f)).Success, Is.True);
            Assert.That(index.TryInsert(new SpatialId(2), new SpatialPoint(-10f, 0f)).Success, Is.True);
            Assert.That(index.TryInsert(new SpatialId(3), new SpatialPoint(-10.1f, 0f)).Success, Is.True);

            SpatialId[] buffer = new SpatialId[8];
            SpatialQueryResult first = index.QueryRect(new SpatialRect(-10f, -1f, 0f, 1f), buffer);
            int[] firstIds = Sorted(buffer, first.WrittenCount);
            SpatialQueryResult second = index.QueryRect(new SpatialRect(-20f, -1f, -10f, 1f), buffer);
            Assert.That(firstIds, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(Sorted(buffer, second.WrittenCount), Is.EqualTo(new[] { 3 }));
        }

        [Test]
        public void QueryRectFiltersExactBoundsWithoutDuplicatesAndReportsTruncation()
        {
            var index = new SpatialIndex2D(2f);
            for (int i = 1; i <= 5; i++)
            {
                Assert.That(index.TryInsert(new SpatialId(i), new SpatialPoint(i - 1.5f, 0.25f)).Success, Is.True);
            }

            SpatialId[] small = new SpatialId[2];
            SpatialQueryResult truncated = index.QueryRect(new SpatialRect(-2f, -1f, 3.5f, 1f), small);
            Assert.That(truncated.WrittenCount, Is.EqualTo(2));
            Assert.That(truncated.MatchCount, Is.EqualTo(4));
            Assert.That(truncated.IsTruncated, Is.True);
            Assert.That(Sorted(small, small.Length).Distinct().Count(), Is.EqualTo(2));

            SpatialQueryResult emptyBuffer = index.QueryRect(new SpatialRect(-2f, -1f, 3.5f, 1f), Span<SpatialId>.Empty);
            Assert.That(emptyBuffer.WrittenCount, Is.EqualTo(0));
            Assert.That(emptyBuffer.MatchCount, Is.EqualTo(4));
            Assert.That(emptyBuffer.IsTruncated, Is.True);

            SpatialQueryResult emptyRect = index.QueryRect(new SpatialRect(0f, 0f, 0f, 5f), small);
            Assert.That(emptyRect, Is.EqualTo(new SpatialQueryResult(0, 0)));
            Assert.That(index.QueryRect(new SpatialRect(0f, -1f, 1.5f, 1f), small).MatchCount, Is.EqualTo(1));
        }

        [Test]
        public void QueryCircleUsesClosedDistanceAndValidatesRadius()
        {
            var index = new SpatialIndex2D(4f);
            Assert.That(index.TryInsert(new SpatialId(1), new SpatialPoint(0f, 0f)).Success, Is.True);
            Assert.That(index.TryInsert(new SpatialId(2), new SpatialPoint(3f, 4f)).Success, Is.True);
            Assert.That(index.TryInsert(new SpatialId(3), new SpatialPoint(3.01f, 4f)).Success, Is.True);
            Assert.That(index.TryInsert(new SpatialId(4), new SpatialPoint(-1f, 0f)).Success, Is.True);

            SpatialId[] buffer = new SpatialId[8];
            SpatialQueryResult circle = index.QueryCircle(new SpatialPoint(0f, 0f), 5f, buffer);
            Assert.That(Sorted(buffer, circle.WrittenCount), Is.EqualTo(new[] { 1, 2, 4 }));
            SpatialQueryResult exact = index.QueryCircle(new SpatialPoint(0f, 0f), 0f, buffer);
            Assert.That(Sorted(buffer, exact.WrittenCount), Is.EqualTo(new[] { 1 }));
            Assert.That(() => index.QueryCircle(new SpatialPoint(0f, 0f), -1f, buffer),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => index.QueryCircle(new SpatialPoint(0f, 0f), float.NaN, buffer),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => index.QueryCircle(new SpatialPoint(0f, 0f), float.PositiveInfinity, buffer),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void QueriesAreReadOnlyAndNearestUsesTieBreakAndExclude()
        {
            var index = new SpatialIndex2D(5f);
            Assert.That(index.TryInsert(new SpatialId(7), new SpatialPoint(-1f, 0f)).Success, Is.True);
            Assert.That(index.TryInsert(new SpatialId(3), new SpatialPoint(1f, 0f)).Success, Is.True);
            Assert.That(index.TryInsert(new SpatialId(5), new SpatialPoint(1f, 0f)).Success, Is.True);
            int countBefore = index.Count;
            Assert.That(index.TryFindNearest(new SpatialPoint(0f, 0f), 1f, out SpatialId nearest), Is.True);
            Assert.That(nearest, Is.EqualTo(new SpatialId(3)));
            Assert.That(index.TryFindNearest(new SpatialPoint(0f, 0f), 1f, new SpatialId(3), out nearest), Is.True);
            Assert.That(nearest, Is.EqualTo(new SpatialId(5)));
            Assert.That(index.TryFindNearest(new SpatialPoint(0f, 0f), 0.5f, out _), Is.False);
            Assert.That(index.TryFindNearest(new SpatialPoint(0f, 0f), 1f, new SpatialId(99), out nearest), Is.True);
            Assert.That(nearest, Is.EqualTo(new SpatialId(3)));
            Assert.That(index.Count, Is.EqualTo(countBefore));
        }

        [Test]
        public void ClearRetainsUsabilityAndRemovedSlotsCanBeReused()
        {
            var index = new SpatialIndex2D(1f, 2);
            Assert.That(index.TryInsert(new SpatialId(1), new SpatialPoint(0f, 0f)).Success, Is.True);
            Assert.That(index.TryInsert(new SpatialId(2), new SpatialPoint(1f, 0f)).Success, Is.True);
            Assert.That(index.TryRemove(new SpatialId(1)).Success, Is.True);
            Assert.That(index.TryInsert(new SpatialId(3), new SpatialPoint(2f, 0f)).Success, Is.True);
            Assert.That(index.Count, Is.EqualTo(2));

            index.Clear();
            Assert.That(index.Count, Is.EqualTo(0));
            Assert.That(index.Contains(new SpatialId(2)), Is.False);
            Assert.That(index.QueryCircle(new SpatialPoint(0f, 0f), 100f, Span<SpatialId>.Empty),
                Is.EqualTo(new SpatialQueryResult(0, 0)));
            Assert.That(index.TryFindNearest(new SpatialPoint(0f, 0f), 100f, out _), Is.False);
            Assert.That(index.TryInsert(new SpatialId(4), new SpatialPoint(-0.5f, -0.5f)).Success, Is.True);
            Assert.That(index.Count, Is.EqualTo(1));
        }

        [Test]
        public void ExtremeQueryRangeFailsBeforeAnUnsafeBucketLoop()
        {
            var index = new SpatialIndex2D(1f);
            Assert.That(index.TryInsert(new SpatialId(1), new SpatialPoint(0f, 0f)).Success, Is.True);
            Assert.That(() => index.QueryRect(new SpatialRect(float.MinValue, -1f, float.MaxValue, 1f),
                Span<SpatialId>.Empty), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => index.QueryCircle(new SpatialPoint(0f, 0f), float.MaxValue,
                Span<SpatialId>.Empty), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => index.TryFindNearest(new SpatialPoint(0f, 0f), float.MaxValue, out _),
                Throws.TypeOf<ArgumentOutOfRangeException>());

            var emptyIndex = new SpatialIndex2D(1f);
            Assert.That(() => emptyIndex.QueryRect(new SpatialRect(float.MinValue, -1f, float.MaxValue, 1f),
                Span<SpatialId>.Empty), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => emptyIndex.QueryCircle(new SpatialPoint(0f, 0f), float.MaxValue,
                Span<SpatialId>.Empty), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        private static int[] Sorted(SpatialId[] values, int count)
        {
            return values.Take(count).Select(value => value.Value).OrderBy(value => value).ToArray();
        }
    }
}
