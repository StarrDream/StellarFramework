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
            var index = new SpatialIndex2D(bucketSize, entityCount);
            var positions = new SpatialPoint[entityCount];
            var queryBuffer = new SpatialId[256];
            long checksum = 0L;
            long allocatedBefore = GC.GetTotalMemory(false);

            Stopwatch insertWatch = Stopwatch.StartNew();
            for (int i = 0; i < entityCount; i++)
            {
                SpatialPoint position = MakePosition(i);
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
                SpatialPoint next = new SpatialPoint(old.X + 0.5f, old.Y + 0.5f);
                if (index.TryUpdatePosition(new SpatialId(i + 1), next).Success) positions[i] = next;
            }
            sameBucketWatch.Stop();

            Stopwatch crossBucketWatch = Stopwatch.StartNew();
            for (int i = 0; i < entityCount; i++)
            {
                SpatialPoint old = positions[i];
                SpatialPoint next = new SpatialPoint(old.X + bucketSize + 0.25f, old.Y);
                if (index.TryUpdatePosition(new SpatialId(i + 1), next).Success) positions[i] = next;
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

            string message = string.Format(
                "SpatialKit 100k env={0} insertMs={1:F3} lookupMs={2:F3} sameBucketUpdateMs={3:F3} crossBucketUpdateMs={4:F3} rect10kMs={5:F3} circle10kMs={6:F3} nearest10kMs={7:F3} removeMs={8:F3} clearMs={9:F3} checksum={10} allocationDelta={11}",
                Application.unityVersion, insertWatch.Elapsed.TotalMilliseconds, lookupWatch.Elapsed.TotalMilliseconds,
                sameBucketWatch.Elapsed.TotalMilliseconds, crossBucketWatch.Elapsed.TotalMilliseconds,
                rectWatch.Elapsed.TotalMilliseconds, circleWatch.Elapsed.TotalMilliseconds,
                nearestWatch.Elapsed.TotalMilliseconds, removeWatch.Elapsed.TotalMilliseconds,
                clearWatch.Elapsed.TotalMilliseconds, checksum, allocatedDelta);
            TestContext.Progress.WriteLine(message);
            UnityEngine.Debug.Log(message);
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
                "SpatialKit 1M env={0} insertMs={1:F3} lookup62.5kMs={2:F3} movement31.25kMs={3:F3} clearMs={4:F3} checksum={5} allocationDelta={6}",
                Application.unityVersion, insertWatch.Elapsed.TotalMilliseconds, lookupWatch.Elapsed.TotalMilliseconds,
                movementWatch.Elapsed.TotalMilliseconds, clearWatch.Elapsed.TotalMilliseconds, checksum, allocatedDelta);
            TestContext.Progress.WriteLine(message);
            UnityEngine.Debug.Log(message);
            Assert.That(index.Count, Is.EqualTo(0));
        }

        private static SpatialPoint MakePosition(int index)
        {
            int x = index % 1000 - 500;
            int y = index / 1000 - 50;
            return new SpatialPoint(x + 0.25f, y + 0.5f);
        }
    }
}
