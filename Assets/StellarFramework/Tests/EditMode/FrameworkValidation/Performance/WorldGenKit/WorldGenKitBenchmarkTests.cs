using System;
using System.Diagnostics;
using NUnit.Framework;
using StellarFramework.WorldGenKit;
using UnityEngine;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class WorldGenKitBenchmarkTests
    {
        [Test, Category("Benchmark")]
        public void WorldGenKitBenchmark_DenseSparseChunkedHandlesAndPipeline()
        {
            const int denseCount = 1000000;
            const int sparseCount = 100000;
            const int chunkCount = 100000;
            const int handleResolveCount = 1000000;
            const int pipelineRuns = 100000;

            WorldChannelRegistryBuilder registryBuilder = new WorldChannelRegistryBuilder(3);
            ChannelHandle<float> denseHandle = registryBuilder.Register<float>(
                WorldDataChannelId.From("benchmark.dense"),
                new WorldChannelStorageDescriptor(WorldChannelStorageKind.Dense, WorldChannelScope.Sample));
            ChannelHandle<int> sparseHandle = registryBuilder.Register<int>(
                WorldDataChannelId.From("benchmark.sparse"),
                new WorldChannelStorageDescriptor(WorldChannelStorageKind.Sparse, WorldChannelScope.Sample));
            ChannelHandle<int> chunkHandle = registryBuilder.Register<int>(
                WorldDataChannelId.From("benchmark.chunked"),
                new WorldChannelStorageDescriptor(WorldChannelStorageKind.Chunked, WorldChannelScope.Chunk));
            WorldChannelRegistry registry = registryBuilder.Build();

            DenseChannelStorage<float> dense = new DenseChannelStorage<float>(denseCount);
            SparseChannelStorage<int> sparse = new SparseChannelStorage<int>(-1, sparseCount);
            ChunkedChannelStorage<BenchmarkPageKey, int> chunked =
                new ChunkedChannelStorage<BenchmarkPageKey, int>(chunkCount);
            WorldGenerationDataSet data = new WorldGenerationDataSet(registry);
            data.Bind(denseHandle, dense);
            data.Bind(sparseHandle, sparse);
            data.Bind(chunkHandle, chunked);

            long heapBefore = GC.GetTotalMemory(false);
            long checksum = 0L;

            Stopwatch denseWrite = Stopwatch.StartNew();
            Span<float> denseSpan = dense.AsSpan();
            for (int i = 0; i < denseSpan.Length; i++) denseSpan[i] = i * 0.25f;
            denseWrite.Stop();

            Stopwatch denseRead = Stopwatch.StartNew();
            for (int i = 0; i < denseSpan.Length; i += 4) checksum += (long)denseSpan[i];
            denseRead.Stop();

            Stopwatch sparseWrite = Stopwatch.StartNew();
            for (int i = 0; i < sparseCount; i++) sparse.Set(i * 3, i);
            sparseWrite.Stop();

            Stopwatch sparseRead = Stopwatch.StartNew();
            for (int i = 0; i < sparseCount; i++) checksum += sparse.Get(i * 3);
            sparseRead.Stop();

            Stopwatch chunkWrite = Stopwatch.StartNew();
            for (int i = 0; i < chunkCount; i++)
                chunked.Set(new BenchmarkPageKey(i - 50000L, (i * 13L) - 650000L), i);
            chunkWrite.Stop();

            Stopwatch chunkRead = Stopwatch.StartNew();
            for (int i = 0; i < chunkCount; i++)
            {
                if (!chunked.TryGet(new BenchmarkPageKey(i - 50000L, (i * 13L) - 650000L), out int value))
                    Assert.Fail("Chunked benchmark lookup failed at " + i);
                checksum += value;
            }
            chunkRead.Stop();

            Stopwatch handleResolve = Stopwatch.StartNew();
            for (int i = 0; i < handleResolveCount; i++)
            {
                if (!data.TryGetStorage<float, DenseChannelStorage<float>>(denseHandle, out DenseChannelStorage<float> resolved))
                    Assert.Fail("Compiled handle resolution failed.");
                checksum += resolved.Length;
            }
            handleResolve.Stop();

            WorldGenerationCompileResult pipelineCompile = BuildBenchmarkPipeline(out ChannelHandle<float> a, out ChannelHandle<float> b);
            Assert.That(pipelineCompile.Success, Is.True);
            WorldGenerationDataSet pipelineData = new WorldGenerationDataSet(pipelineCompile.Plan.Channels);
            DenseChannelStorage<float> aStorage = new DenseChannelStorage<float>(1);
            DenseChannelStorage<float> bStorage = new DenseChannelStorage<float>(1);
            pipelineData.Bind(a, aStorage);
            pipelineData.Bind(b, bStorage);
            WorldGenerationStageExecutionRecord[] records = new WorldGenerationStageExecutionRecord[2];

            Stopwatch pipelineWatch = Stopwatch.StartNew();
            WorldGenerationSeed seed = new WorldGenerationSeed(12345UL);
            for (int i = 0; i < pipelineRuns; i++)
            {
                WorldGenerationRunResult run = pipelineCompile.Plan.Execute(
                    pipelineData,
                    seed,
                    new WorldGenerationRunKey(i, -i),
                    records.AsSpan());
                if (!run.Success) Assert.Fail("Pipeline benchmark failed at " + i + " with " + run.Status);
                checksum += (long)bStorage[0];
            }
            pipelineWatch.Stop();

            long heapDelta = GC.GetTotalMemory(false) - heapBefore;
            string message = string.Format(
                "WorldGenKit benchmark env={0} dense={1} sparse={2} chunks={3} handleResolve={4} pipelineRuns={5} denseWriteMs={6:F3} denseReadMs={7:F3} sparseWriteMs={8:F3} sparseReadMs={9:F3} chunkWriteMs={10:F3} chunkReadMs={11:F3} handleResolveMs={12:F3} pipelineMs={13:F3} checksum={14} allocationDelta={15}",
                Application.unityVersion,
                denseCount,
                sparseCount,
                chunkCount,
                handleResolveCount,
                pipelineRuns,
                denseWrite.Elapsed.TotalMilliseconds,
                denseRead.Elapsed.TotalMilliseconds,
                sparseWrite.Elapsed.TotalMilliseconds,
                sparseRead.Elapsed.TotalMilliseconds,
                chunkWrite.Elapsed.TotalMilliseconds,
                chunkRead.Elapsed.TotalMilliseconds,
                handleResolve.Elapsed.TotalMilliseconds,
                pipelineWatch.Elapsed.TotalMilliseconds,
                checksum,
                heapDelta);

            TestContext.Progress.WriteLine(message);
            UnityEngine.Debug.Log(message);

            Assert.That(sparse.StoredCount, Is.EqualTo(sparseCount));
            Assert.That(chunked.Count, Is.EqualTo(chunkCount));
            Assert.That(checksum, Is.GreaterThan(0L));
        }

        private static WorldGenerationCompileResult BuildBenchmarkPipeline(
            out ChannelHandle<float> a,
            out ChannelHandle<float> b)
        {
            WorldGenerationPipelineBuilder builder = new WorldGenerationPipelineBuilder();
            WorldChannelStorageDescriptor dense = new WorldChannelStorageDescriptor(
                WorldChannelStorageKind.Dense,
                WorldChannelScope.Sample);
            a = builder.Channels.Register<float>(WorldDataChannelId.From("benchmark.pipeline_a"), dense);
            b = builder.Channels.Register<float>(WorldDataChannelId.From("benchmark.pipeline_b"), dense);

            ChannelHandle<float> capturedA = a;
            ChannelHandle<float> capturedB = b;
            builder.AddStage(new BenchmarkStageA(capturedA));
            builder.AddStage(new BenchmarkStageB(capturedA, capturedB));
            return builder.Compile();
        }

        private readonly struct BenchmarkPageKey : IEquatable<BenchmarkPageKey>
        {
            private readonly long _x;
            private readonly long _y;

            internal BenchmarkPageKey(long x, long y)
            {
                _x = x;
                _y = y;
            }

            public bool Equals(BenchmarkPageKey other) => _x == other._x && _y == other._y;
            public override bool Equals(object obj) => obj is BenchmarkPageKey other && Equals(other);
            public override int GetHashCode() => unchecked((_x.GetHashCode() * 397) ^ _y.GetHashCode());
        }

        private sealed class BenchmarkStageA : IWorldGenerationStage
        {
            private readonly ChannelHandle<float> _output;
            public WorldGenerationStageId Id { get; } = WorldGenerationStageId.From("benchmark.stage_a");

            internal BenchmarkStageA(ChannelHandle<float> output) => _output = output;

            public void Describe(WorldGenerationStageDescriptorBuilder builder)
            {
                builder.SetSeedScope(WorldGenerationSeedScope.Chunk);
                builder.Produce(_output);
            }

            public WorldGenerationStageResult Execute(in WorldGenerationContext context)
            {
                context.Data.GetStorage<float, DenseChannelStorage<float>>(_output)[0] =
                    (float)(context.RunKey.X & 1023L);
                return WorldGenerationStageResult.Succeeded();
            }
        }

        private sealed class BenchmarkStageB : IWorldGenerationStage
        {
            private readonly ChannelHandle<float> _input;
            private readonly ChannelHandle<float> _output;
            public WorldGenerationStageId Id { get; } = WorldGenerationStageId.From("benchmark.stage_b");

            internal BenchmarkStageB(ChannelHandle<float> input, ChannelHandle<float> output)
            {
                _input = input;
                _output = output;
            }

            public void Describe(WorldGenerationStageDescriptorBuilder builder)
            {
                builder.SetSeedScope(WorldGenerationSeedScope.Chunk);
                builder.Require(_input);
                builder.Produce(_output);
            }

            public WorldGenerationStageResult Execute(in WorldGenerationContext context)
            {
                float input = context.Data.GetStorage<float, DenseChannelStorage<float>>(_input)[0];
                context.Data.GetStorage<float, DenseChannelStorage<float>>(_output)[0] = input + 1f;
                return WorldGenerationStageResult.Succeeded();
            }
        }
    }
}
