using System;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;

namespace StellarFramework.Tests.FrameworkValidation
{
    /// <summary>只记录性能趋势，不设置固定毫秒门槛；运行 Unity Test Runner 的 Benchmark 分类。</summary>
    public sealed class GridKitBenchmarkTests
    {
        [Test, Category("Benchmark")]
        public void GridKitBenchmark_1MStorageGeometryAndOccupancy()
        {
            const int width = 1000;
            const int height = 1000;
            const int occupancyIterations = 100000;
            var bounds = new GridRect(new GridCoord(-500, -500), new GridSize(width, height));
            var grid = new DenseGrid<int>(bounds);
            long allocatedBefore = GC.GetTotalMemory(false);

            Stopwatch fillWatch = Stopwatch.StartNew();
            grid.Fill(1);
            fillWatch.Stop();

            long linearChecksum = 0L;
            Stopwatch linearReadWatch = Stopwatch.StartNew();
            var span = grid.AsReadOnlySpan();
            for (int i = 0; i < span.Length; i++) linearChecksum += span[i];
            linearReadWatch.Stop();

            Stopwatch linearWriteWatch = Stopwatch.StartNew();
            Span<int> writable = grid.AsSpan();
            for (int i = 0; i < writable.Length; i++) writable[i] = i;
            linearWriteWatch.Stop();

            long coordReadChecksum = 0L;
            Stopwatch coordReadWatch = Stopwatch.StartNew();
            for (int y = bounds.Min.Y; y < bounds.MaxExclusiveY; y++)
            {
                for (int x = bounds.Min.X; x < bounds.MaxExclusiveX; x++)
                {
                    coordReadChecksum += grid[new GridCoord(x, y)];
                }
            }
            coordReadWatch.Stop();

            Stopwatch coordWriteWatch = Stopwatch.StartNew();
            for (int y = bounds.Min.Y; y < bounds.MaxExclusiveY; y++)
            {
                for (int x = bounds.Min.X; x < bounds.MaxExclusiveX; x++)
                {
                    grid[new GridCoord(x, y)] = x + y;
                }
            }
            coordWriteWatch.Stop();

            int rectCount = 0;
            Stopwatch rectWatch = Stopwatch.StartNew();
            foreach (GridCoord ignored in bounds) rectCount++;
            rectWatch.Stop();

            int roundTripChecksum = 0;
            Stopwatch coordIndexWatch = Stopwatch.StartNew();
            for (int i = 0; i < grid.Count; i++)
            {
                GridCoord coord = grid.GetCoord(i);
                roundTripChecksum += grid.GetIndex(coord);
            }
            coordIndexWatch.Stop();

            var occupancy = new GridOccupancy(bounds);
            var single = new GridFootprint(new GridOffset(0, 0));
            var owner = new GridOccupantId(1);
            int canOccupySuccess = 0;
            Stopwatch canOccupyWatch = Stopwatch.StartNew();
            for (int i = 0; i < occupancyIterations; i++)
            {
                GridCoord coord = new GridCoord(i % width - 500, (i / width) % height - 500);
                if (occupancy.CanOccupy(owner, coord, single, GridTransform.Identity).Success) canOccupySuccess++;
            }
            canOccupyWatch.Stop();

            int occupySuccess = 0;
            Stopwatch occupyWatch = Stopwatch.StartNew();
            for (int i = 0; i < occupancyIterations; i++)
            {
                GridCoord coord = new GridCoord(i % width - 500, (i / width) % height - 500);
                if (occupancy.TryOccupy(owner, coord, single, GridTransform.Identity).Success) occupySuccess++;
                occupancy.TryRelease(owner, coord, single, GridTransform.Identity);
            }
            occupyWatch.Stop();

            long allocatedDelta = GC.GetTotalMemory(false) - allocatedBefore;
            string message = string.Format(
                "GridKit benchmark env={0} cells={1} fillMs={2:F3} linearReadMs={3:F3} linearWriteMs={4:F3} coordReadMs={5:F3} coordWriteMs={6:F3} coordIndexMs={7:F3} rectMs={8:F3} canOccupy100kMs={9:F3} occupyRelease100kMs={10:F3} checksums={11}/{12}/{13} rectCount={14} canOccupy={15} occupy={16} allocationDelta={17}",
                Application.unityVersion, grid.Count, fillWatch.Elapsed.TotalMilliseconds,
                linearReadWatch.Elapsed.TotalMilliseconds, linearWriteWatch.Elapsed.TotalMilliseconds,
                coordReadWatch.Elapsed.TotalMilliseconds, coordWriteWatch.Elapsed.TotalMilliseconds,
                coordIndexWatch.Elapsed.TotalMilliseconds, rectWatch.Elapsed.TotalMilliseconds,
                canOccupyWatch.Elapsed.TotalMilliseconds, occupyWatch.Elapsed.TotalMilliseconds,
                linearChecksum, coordReadChecksum, roundTripChecksum, rectCount, canOccupySuccess,
                occupySuccess, allocatedDelta);
            TestContext.Progress.WriteLine(message);
            UnityEngine.Debug.Log(message);
            Assert.That(rectCount, Is.EqualTo(width * height));
            Assert.That(canOccupySuccess, Is.EqualTo(occupancyIterations));
            Assert.That(occupySuccess, Is.EqualTo(occupancyIterations));
        }
    }
}
