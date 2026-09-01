using System;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;

namespace StellarFramework.Tests.FrameworkValidation
{
    /// <summary>SpatialKit 性能趋势基准；只记录环境和耗时，不设置固定毫秒门槛。</summary>
    public sealed class SpatialKitBenchmarkTests
    {
        [Test, Category("Benchmark")]
        public void SpatialKitBenchmark_100kDynamicOperations()
        {
            const int entityCount = 100000;
            const int queryIterations = 10000;
            const float bucketSize = 8f;
            const int initialCapacity = entityCount;
            var index = new SpatialIndex2D(bucketSize, initialCapacity);
            var positions = new SpatialPoint[entityCount];
            var queryBuffer = new SpatialId[256];
            long checksum = 0L;
            long sameBucketChecksum = 0L;
            long crossBucketChecksum = 0L;
            int sameBucketUpdates = 0;
            int crossBucketUpdates = 0;
            long allocatedBefore = GC.GetTotalMemory(false);

            ValidateBenchmarkDataset(entityCount, bucketSize);

            Stopwatch insertWatch = Stopwatch.StartNew();
            for (int i = 0; i < entityCount; i++)
            {
                SpatialPoint position = MakePosition(i, bucketSize);
                positions[i] = position;
                SpatialMutationResult result = index.TryInsert(new SpatialId(i + 1), position);
                if (!result.Success) checksum += (int)result.Error;
            }
            insertWatch.Stop();

            Stopwatch lookupWatch = Stopwatch.StartNew();
            for (int i = 0; i < entityCount; i++)
            {
                SpatialId id = new SpatialId(i + 1);
                if (index.Contains(id)) checksum += id.Value;
                if (index.TryGetPosition(id, out SpatialPoint position)) checksum += position.X.GetHashCode();
            }
            lookupWatch.Stop();

            Stopwatch sameBucketWatch = Stopwatch.StartNew();
            for (int i = 0; i < entityCount; i++)
            {
                SpatialPoint old = positions[i];
                SpatialPoint next = MakeSameBucketTarget(old);
                if (index.TryUpdatePosition(new SpatialId(i + 1), next).Success)
                {
                    positions[i] = next;
                    sameBucketUpdates++;
                    sameBucketChecksum += i + 1;
                }
            }
            sameBucketWatch.Stop();

            Stopwatch crossBucketWatch = Stopwatch.StartNew();
            for (int i = 0; i < entityCount; i++)
            {
                SpatialPoint old = positions[i];
                SpatialPoint next = MakeCrossBucketTarget(old, bucketSize);
                if (index.TryUpdatePosition(new SpatialId(i + 1), next).Success)
                {
                    positions[i] = next;
                    crossBucketUpdates++;
                    crossBucketChecksum += i + 1;
                }
            }
            crossBucketWatch.Stop();

            Stopwatch rectWatch = Stopwatch.StartNew();
            for (int i = 0; i < queryIterations; i++)
            {
                SpatialPoint center = positions[(i * 17) % entityCount];
                SpatialQueryResult result = index.QueryRect(
                    new SpatialRect(center.X - 4f, center.Y - 4f, center.X + 4f, center.Y + 4f), queryBuffer);
                checksum += result.MatchCount + result.WrittenCount;
            }
            rectWatch.Stop();

            Stopwatch circleWatch = Stopwatch.StartNew();
            for (int i = 0; i < queryIterations; i++)
            {
                SpatialPoint center = positions[(i * 23) % entityCount];
                SpatialQueryResult result = index.QueryCircle(center, 4f, queryBuffer);
                checksum += result.MatchCount + result.WrittenCount;
            }
            circleWatch.Stop();

            Stopwatch nearestWatch = Stopwatch.StartNew();
            for (int i = 0; i < queryIterations; i++)
            {
                SpatialPoint center = positions[(i * 29) % entityCount];
                if (index.TryFindNearest(center, 12f, out SpatialId nearest)) checksum += nearest.Value;
            }
            nearestWatch.Stop();

            Stopwatch removeWatch = Stopwatch.StartNew();
            for (int i = 0; i < entityCount; i++)
            {
                if (index.TryRemove(new SpatialId(i + 1)).Success) checksum += i + 1;
            }
            removeWatch.Stop();

            Stopwatch clearWatch = Stopwatch.StartNew();
            index.Clear();
            clearWatch.Stop();
            long allocatedDelta = GC.GetTotalMemory(false) - allocatedBefore;

            checksum += sameBucketChecksum + crossBucketChecksum;
            string message = string.Format(
                "SpatialKit 100k env={0} EntityCount={1} BucketSize={2} InitialCapacity={3} InsertMs={4:F3} LookupMs={5:F3} SameBucketUpdate={6} SameBucketUpdateMs={7:F3} CrossBucketUpdate={8} CrossBucketUpdateMs={9:F3} RectQuery={10} RectQueryMs={11:F3} CircleQuery={12} CircleQueryMs={13:F3} Nearest={14} NearestMs={15:F3} RemoveMs={16:F3} ClearMs={17:F3} SameBucketChecksum={18} CrossBucketChecksum={19} Checksum={20} ManagedHeapDelta={21}",
                Application.unityVersion, entityCount, bucketSize, initialCapacity,
                insertWatch.Elapsed.TotalMilliseconds, lookupWatch.Elapsed.TotalMilliseconds,
                sameBucketUpdates, sameBucketWatch.Elapsed.TotalMilliseconds,
                crossBucketUpdates, crossBucketWatch.Elapsed.TotalMilliseconds,
                queryIterations, rectWatch.Elapsed.TotalMilliseconds,
                queryIterations, circleWatch.Elapsed.TotalMilliseconds,
                queryIterations, nearestWatch.Elapsed.TotalMilliseconds,
                removeWatch.Elapsed.TotalMilliseconds, clearWatch.Elapsed.TotalMilliseconds,
                sameBucketChecksum, crossBucketChecksum, checksum, allocatedDelta);
            TestContext.Progress.WriteLine(message);
            UnityEngine.Debug.Log(message);
            Assert.That(sameBucketUpdates, Is.EqualTo(entityCount));
            Assert.That(crossBucketUpdates, Is.EqualTo(entityCount));
            Assert.That(index.Count, Is.EqualTo(0));
        }

        [Test, Category("Benchmark")]
        public void SpatialKitBenchmark_1MStorageStress()
        {
            const int entityCount = 1000000;
            const float bucketSize = 8f;
            var index = new SpatialIndex2D(bucketSize, entityCount);
            long allocatedBefore = GC.GetTotalMemory(false);
            long checksum = 0L;

            Stopwatch insertWatch = Stopwatch.StartNew();
            for (int i = 0; i < entityCount; i++)
            {
                int x = i % 2000 - 1000;
                int y = i / 2000 - 250;
                SpatialMutationResult result = index.TryInsert(new SpatialId(i + 1),
                    new SpatialPoint(x + 0.25f, y + 0.75f));
                if (!result.Success) checksum += (int)result.Error;
            }
            insertWatch.Stop();
            Assert.That(index.Count, Is.EqualTo(entityCount));

            Stopwatch lookupWatch = Stopwatch.StartNew();
            for (int i = 0; i < entityCount; i += 16)
            {
                SpatialId id = new SpatialId(i + 1);
                if (index.Contains(id)) checksum += id.Value;
                if (index.TryGetPosition(id, out SpatialPoint position)) checksum += position.Y.GetHashCode();
            }
            lookupWatch.Stop();

            Stopwatch movementWatch = Stopwatch.StartNew();
            for (int i = 0; i < entityCount; i += 32)
            {
                int x = i % 2000 - 1000;
                int y = i / 2000 - 250;
                if (index.TryUpdatePosition(new SpatialId(i + 1),
                    new SpatialPoint(x + bucketSize + 0.25f, y + 0.75f)).Success) checksum++;
            }
            movementWatch.Stop();

            Stopwatch clearWatch = Stopwatch.StartNew();
            index.Clear();
            clearWatch.Stop();
            long allocatedDelta = GC.GetTotalMemory(false) - allocatedBefore;
            string message = string.Format(
                "SpatialKit 1M env={0} insertMs={1:F3} lookup62.5kMs={2:F3} movement31.25kMs={3:F3} clearMs={4:F3} checksum={5} ManagedHeapDelta={6}",
                Application.unityVersion, insertWatch.Elapsed.TotalMilliseconds, lookupWatch.Elapsed.TotalMilliseconds,
                movementWatch.Elapsed.TotalMilliseconds, clearWatch.Elapsed.TotalMilliseconds, checksum, allocatedDelta);
            TestContext.Progress.WriteLine(message);
            UnityEngine.Debug.Log(message);
            Assert.That(index.Count, Is.EqualTo(0));
        }

        private static void ValidateBenchmarkDataset(int entityCount, float bucketSize)
        {
            for (int i = 0; i < entityCount; i++)
            {
                SpatialPoint initial = MakePosition(i, bucketSize);
                SpatialPoint sameBucket = MakeSameBucketTarget(initial);
                SpatialPoint crossBucket = MakeCrossBucketTarget(sameBucket, bucketSize);
                int initialBucketX = DatasetBucket(initial.X, bucketSize);
                int initialBucketY = DatasetBucket(initial.Y, bucketSize);
                int sameBucketX = DatasetBucket(sameBucket.X, bucketSize);
                int sameBucketY = DatasetBucket(sameBucket.Y, bucketSize);
                int crossBucketX = DatasetBucket(crossBucket.X, bucketSize);
                int crossBucketY = DatasetBucket(crossBucket.Y, bucketSize);
                if (initialBucketX != sameBucketX || initialBucketY != sameBucketY ||
                    crossBucketX != sameBucketX + 1 || crossBucketY != sameBucketY)
                {
                    Assert.Fail(string.Format(
                        "Benchmark dataset crossed an unexpected bucket at index {0}: initial=({1},{2}) same=({3},{4}) cross=({5},{6})",
                        i, initialBucketX, initialBucketY, sameBucketX, sameBucketY, crossBucketX, crossBucketY));
                }
            }
        }

        private static SpatialPoint MakePosition(int index, float bucketSize)
        {
            int bucketX = index % 1000 - 500;
            int bucketY = index / 1000 - 50;
            return new SpatialPoint(bucketX * bucketSize + 2f, bucketY * bucketSize + 2f);
        }

        private static SpatialPoint MakeSameBucketTarget(SpatialPoint old)
        {
            return new SpatialPoint(old.X + 0.5f, old.Y + 0.5f);
        }

        private static SpatialPoint MakeCrossBucketTarget(SpatialPoint old, float bucketSize)
        {
            return new SpatialPoint(old.X + bucketSize, old.Y);
        }

        private static int DatasetBucket(float coordinate, float bucketSize)
        {
            return (int)Math.Floor(coordinate / (double)bucketSize);
        }
    }
}
