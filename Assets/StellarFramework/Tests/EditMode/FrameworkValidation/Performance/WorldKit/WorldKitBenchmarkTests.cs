using System;
using System.Diagnostics;
using NUnit.Framework;
using StellarFramework.WorldKit;
using UnityEngine;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class WorldKitBenchmarkTests
    {
        [Test, Category("Benchmark")]
        public void WorldKitBenchmark_100kChunkRegistryLayerAndDirtyOperations()
        {
            const int count = 100000;

            WorldId worldId = WorldId.From("world.benchmark");
            WorldChunkRegistry chunks = new WorldChunkRegistry(worldId, WorldExtent.Infinite, count);

            WorldDataLayerRegistryBuilder layerBuilder = new WorldDataLayerRegistryBuilder(1);
            WorldDataLayerHandle<int> valueHandle = layerBuilder.Register<int>(
                WorldDataLayerId.From("chunk.benchmark_value"),
                WorldDataLayerScope.Chunk);
            WorldDataLayerRegistry layerRegistry = layerBuilder.Build();
            WorldDataLayerStore<int> values = new WorldDataLayerStore<int>(layerRegistry, valueHandle, count);

            WorldDirtyChunkTracker dirty = new WorldDirtyChunkTracker(count);
            WorldChunkCoord[] output = new WorldChunkCoord[count];

            long allocatedBefore = GC.GetTotalMemory(false);
            long checksum = 0L;

            Stopwatch registerWatch = Stopwatch.StartNew();
            for (int i = 0; i < count; i++)
            {
                WorldChunkCoord coord = Coord(i);
                if (!chunks.TryRegister(coord).Success) Assert.Fail("Benchmark register failed at " + i);
            }
            registerWatch.Stop();

            Stopwatch transitionWatch = Stopwatch.StartNew();
            for (int i = 0; i < count; i++)
            {
                WorldChunkCoord coord = Coord(i);
                if (!chunks.TryTransition(coord, WorldChunkState.Metadata).Success) Assert.Fail("Metadata transition failed.");
                if (!chunks.TryTransition(coord, WorldChunkState.DataReady).Success) Assert.Fail("DataReady transition failed.");
            }
            transitionWatch.Stop();

            Stopwatch layerWriteWatch = Stopwatch.StartNew();
            for (int i = 0; i < count; i++) values.SetChunk(Coord(i), i);
            layerWriteWatch.Stop();

            Stopwatch layerReadWatch = Stopwatch.StartNew();
            for (int i = 0; i < count; i++)
            {
                if (!values.TryGetChunk(Coord(i), out int value)) Assert.Fail("Layer read failed.");
                checksum += value;
            }
            layerReadWatch.Stop();

            Stopwatch dirtyWatch = Stopwatch.StartNew();
            for (int i = 0; i < count; i++) dirty.MarkDirty(Coord(i));
            int dirtyWritten = dirty.WriteDirty(output.AsSpan());
            dirtyWatch.Stop();

            Stopwatch unloadWatch = Stopwatch.StartNew();
            for (int i = 0; i < count; i++)
            {
                WorldChunkCoord coord = Coord(i);
                chunks.TryTransition(coord, WorldChunkState.Metadata);
                chunks.TryTransition(coord, WorldChunkState.Unloaded);
                chunks.TryRemove(coord);
                values.RemoveChunk(coord);
            }
            unloadWatch.Stop();

            long allocatedDelta = GC.GetTotalMemory(false) - allocatedBefore;
            string message = string.Format(
                "WorldKit benchmark env={0} chunks={1} registerMs={2:F3} transition2xMs={3:F3} layerWriteMs={4:F3} layerReadMs={5:F3} dirtyMarkWriteMs={6:F3} unloadRemoveMs={7:F3} checksum={8} dirtyWritten={9} allocationDelta={10}",
                Application.unityVersion,
                count,
                registerWatch.Elapsed.TotalMilliseconds,
                transitionWatch.Elapsed.TotalMilliseconds,
                layerWriteWatch.Elapsed.TotalMilliseconds,
                layerReadWatch.Elapsed.TotalMilliseconds,
                dirtyWatch.Elapsed.TotalMilliseconds,
                unloadWatch.Elapsed.TotalMilliseconds,
                checksum,
                dirtyWritten,
                allocatedDelta);

            TestContext.Progress.WriteLine(message);
            UnityEngine.Debug.Log(message);

            Assert.That(checksum, Is.EqualTo(4999950000L));
            Assert.That(dirtyWritten, Is.EqualTo(count));
            Assert.That(chunks.Count, Is.EqualTo(0));
            Assert.That(values.Count, Is.EqualTo(0));
        }

        private static WorldChunkCoord Coord(int index)
        {
            return new WorldChunkCoord(index - 50000L, ((long)index * 17L) - 850000L);
        }
    }
}
