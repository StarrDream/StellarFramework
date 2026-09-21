using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using NUnit.Framework;
using StellarFramework.WorldGenKit;
using StellarFramework.WorldGenKit.Builtins;
using StellarFramework.WorldGenKit.Feature;
using StellarFramework.WorldGenKit.Resources;
using StellarFramework.WorldGenKit.StreamingAdapter;
using StellarFramework.WorldKit;
using StellarFramework.WorldKit.Streaming.SaveKitAdapter;
using UnityEngine;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class WorldFrameworkReleaseBenchmarkTests
    {
        [Test, Category("Benchmark")]
        public void ChunkGenerationBenchmark_64x64Across64Chunks()
        {
            WorldPlanarSampleLayout layout =
                new WorldPlanarSampleLayout(64, 64, 1L);
            BenchmarkPipeline pipeline = BuildPipeline(layout);
            DenseChannelStorage<float> height =
                new DenseChannelStorage<float>(layout.Count);
            DenseChannelStorage<float> moisture =
                new DenseChannelStorage<float>(layout.Count);
            DenseChannelStorage<float> water =
                new DenseChannelStorage<float>(layout.Count);
            DenseChannelStorage<float> slope =
                new DenseChannelStorage<float>(layout.Count);
            DenseChannelStorage<int> biome =
                new DenseChannelStorage<int>(layout.Count);
            DenseChannelStorage<int> surface =
                new DenseChannelStorage<int>(layout.Count);
            DenseChannelStorage<byte> buildable =
                new DenseChannelStorage<byte>(layout.Count);
            WorldGenerationDataSet data =
                new WorldGenerationDataSet(pipeline.Plan.Channels);
            data.Bind(pipeline.Height, height);
            data.Bind(pipeline.Moisture, moisture);
            data.Bind(pipeline.Water, water);
            data.Bind(pipeline.Slope, slope);
            data.Bind(pipeline.Biome, biome);
            data.Bind(pipeline.Surface, surface);
            data.Bind(pipeline.Buildable, buildable);
            WorldGenerationStageExecutionRecord[] records =
                new WorldGenerationStageExecutionRecord[pipeline.Plan.StageCount];
            WorldGenerationSeed seed =
                new WorldGenerationSeed(0xA1202026UL);

            Assert.That(
                WorldChunkGenerationAdapter.TryCreateRunKey(
                    new WorldChunkCoord(0, 0),
                    in layout,
                    0UL,
                    out WorldGenerationRunKey warmRunKey),
                Is.True);
            WorldGenerationRunResult warmup =
                pipeline.Plan.Execute(
                    data,
                    seed,
                    warmRunKey,
                    records.AsSpan());
            Assert.That(warmup.Success, Is.True);

            const int chunksPerIteration = 64;
            const int measuredIterations = 5;
            double[] elapsedMs = new double[measuredIterations];
            long checksum = 0L;

            long allocatedBefore =
                GC.GetAllocatedBytesForCurrentThread();
            for (int iteration = 0;
                 iteration < measuredIterations;
                 iteration++)
            {
                long start = Stopwatch.GetTimestamp();
                for (int i = 0; i < chunksPerIteration; i++)
                {
                    WorldChunkCoord chunk =
                        new WorldChunkCoord(
                            -500000L + i,
                            700000L + (iteration * 97L));
                    if (!WorldChunkGenerationAdapter.TryCreateRunKey(
                            chunk,
                            in layout,
                            0UL,
                            out WorldGenerationRunKey runKey))
                        Assert.Fail("Chunk run-key mapping failed.");

                    WorldGenerationRunResult run =
                        pipeline.Plan.Execute(
                            data,
                            seed,
                            runKey,
                            records.AsSpan());
                    if (!run.Success)
                        Assert.Fail(
                            "Chunk generation failed: " +
                            run.Code);

                    checksum +=
                        (long)(height[0] * 1000f) +
                        (long)(moisture[layout.Count - 1] * 1000f) +
                        buildable[i % layout.Count];
                }

                long end = Stopwatch.GetTimestamp();
                elapsedMs[iteration] =
                    (end - start) * 1000d /
                    Stopwatch.Frequency;
            }
            long exactAllocatedBytes =
                GC.GetAllocatedBytesForCurrentThread() -
                allocatedBefore;

            Array.Sort(elapsedMs);
            double minMs = elapsedMs[0];
            double medianMs =
                elapsedMs[measuredIterations / 2];
            string message = string.Format(
                CultureInfo.InvariantCulture,
                "World Framework chunk generation env={0} layout={1}x{2} chunksPerIteration={3} measuredIterations={4} minMs={5:F3} medianMs={6:F3} exactHotPathAllocatedBytes={7} checksum={8}",
                Application.unityVersion,
                layout.Width,
                layout.Height,
                chunksPerIteration,
                measuredIterations,
                minMs,
                medianMs,
                exactAllocatedBytes,
                checksum);
            TestContext.Progress.WriteLine(message);
            UnityEngine.Debug.Log(message);

            Assert.That(checksum, Is.Not.EqualTo(0L));
            Assert.That(
                exactAllocatedBytes,
                Is.EqualTo(0L),
                "Chunk generation hot path must be allocation-free after warmup with reusable data/scratch.");
        }

        [Test, Category("Benchmark")]
        public void SaveDeltaSizeBenchmark_1kAnd10kTypedDeltas()
        {
            UnityJsonSaveSerializer serializer =
                new UnityJsonSaveSerializer();

            long oneThousandBytes =
                MeasureDeltaSnapshot(
                    1000,
                    serializer,
                    out double oneThousandCaptureMs,
                    out double oneThousandSerializeMs);
            long tenThousandBytes =
                MeasureDeltaSnapshot(
                    10000,
                    serializer,
                    out double tenThousandCaptureMs,
                    out double tenThousandSerializeMs);

            double bytesPerDelta =
                tenThousandBytes / 10000d;
            double growth =
                tenThousandBytes /
                (double)oneThousandBytes;
            string message = string.Format(
                CultureInfo.InvariantCulture,
                "World Framework save delta size env={0} deltas1kBytes={1} capture1kMs={2:F3} serialize1kMs={3:F3} deltas10kBytes={4} capture10kMs={5:F3} serialize10kMs={6:F3} bytesPerDelta10k={7:F2} growth10x={8:F3}",
                Application.unityVersion,
                oneThousandBytes,
                oneThousandCaptureMs,
                oneThousandSerializeMs,
                tenThousandBytes,
                tenThousandCaptureMs,
                tenThousandSerializeMs,
                bytesPerDelta,
                growth);
            TestContext.Progress.WriteLine(message);
            UnityEngine.Debug.Log(message);

            Assert.That(oneThousandBytes, Is.GreaterThan(0L));
            Assert.That(tenThousandBytes, Is.GreaterThan(oneThousandBytes));
            Assert.That(
                growth,
                Is.InRange(8d, 12d),
                "Delta snapshot growth should remain approximately linear.");
        }

        [Test, Category("Benchmark")]
        public void HotPathAllocationBenchmark_DenseSparseResourceFeature()
        {
            const int denseCount = 65536;
            const int sparseCount = 8192;
            const int iterations = 128;
            DenseChannelStorage<float> dense =
                new DenseChannelStorage<float>(denseCount);
            SparseChannelStorage<int> sparse =
                new SparseChannelStorage<int>(-1, sparseCount);
            Span<float> denseSpan = dense.AsSpan();
            for (int i = 0; i < denseCount; i++)
                denseSpan[i] = i * 0.5f;
            for (int i = 0; i < sparseCount; i++)
                sparse.Set(i * 3, i);

            WorldResourceDefinition resource =
                new WorldResourceDefinition(
                    WorldResourceId.From("resource.benchmark.alloc"),
                    WorldResourceCategoryId.From("category.benchmark.alloc"),
                    new WorldResourceDistributionDefinition(
                        WorldResourceDistributionMode.Density,
                        1d),
                    WorldOccupancyMask.None,
                    WorldOccupancyMask.None,
                    10);
            WorldResourceCatalog resourceCatalog =
                new WorldResourceCatalog(
                    new[] { resource }.AsSpan());
            const int candidateCount = 4096;
            WorldSpawnCandidate[] resourceCandidates =
                new WorldSpawnCandidate[candidateCount];
            for (int i = 0; i < candidateCount; i++)
            {
                resourceCandidates[i] =
                    new WorldSpawnCandidate(
                        0,
                        i,
                        i & 63,
                        i >> 6,
                        1d - (i * 0.000001d),
                        (ulong)(i + 1),
                        1d);
            }
            WorldOccupancyCellState[] occupancy =
                new WorldOccupancyCellState[candidateCount];
            int[] resourceOrder =
                new int[candidateCount];
            WorldSpawnRecord[] resourceOutput =
                new WorldSpawnRecord[candidateCount];

            WorldFeatureDefinition feature =
                new WorldFeatureDefinition(
                    WorldFeatureId.From("feature.benchmark.alloc"),
                    WorldFeatureCategoryId.From("feature_category.benchmark.alloc"),
                    WorldFeatureKind.Landmark,
                    WorldFeatureFootprint.Rectangle(1d, 1d),
                    WorldFeatureQuota.Unlimited(),
                    10);
            WorldFeatureCatalog featureCatalog =
                new WorldFeatureCatalog(
                    new[] { feature }.AsSpan());
            WorldFeatureCandidate[] featureCandidates =
                new WorldFeatureCandidate[candidateCount];
            for (int i = 0; i < candidateCount; i++)
            {
                featureCandidates[i] =
                    new WorldFeatureCandidate(
                        0,
                        (i & 63) * 2d,
                        (i >> 6) * 2d,
                        0d,
                        1d - (i * 0.000001d),
                        (ulong)(i + 1));
            }
            int[] featureOrder =
                new int[candidateCount];
            int[] acceptedPerFeature =
                new int[featureCatalog.Count];
            WorldFeatureReservation[] reservations =
                new WorldFeatureReservation[candidateCount];
            WorldFeatureInstanceData[] featureOutput =
                new WorldFeatureInstanceData[candidateCount];

            WorldResourceScatterResolver.Resolve(
                resourceCandidates,
                resourceCatalog,
                occupancy,
                resourceOrder,
                resourceOutput);
            WorldFeatureResolver.Resolve(
                featureCandidates,
                featureCatalog,
                ReadOnlySpan<int>.Empty,
                ReadOnlySpan<int>.Empty,
                ReadOnlySpan<WorldFeatureReservation>.Empty,
                featureOrder,
                acceptedPerFeature,
                reservations,
                featureOutput);

            long checksum = 0L;
            long allocatedBefore =
                GC.GetAllocatedBytesForCurrentThread();
            long start = Stopwatch.GetTimestamp();
            for (int iteration = 0;
                 iteration < iterations;
                 iteration++)
            {
                int denseIndex =
                    (iteration * 509) & (denseCount - 1);
                denseSpan[denseIndex] += 1f;
                checksum += (long)denseSpan[denseIndex];

                int sparseKey =
                    (iteration % sparseCount) * 3;
                int sparseValue =
                    sparse.Get(sparseKey);
                sparse.Set(
                    sparseKey,
                    sparseValue + 1);
                checksum += sparse.Get(sparseKey);

                Array.Clear(
                    occupancy,
                    0,
                    occupancy.Length);
                WorldScatterResolveResult resourceResult =
                    WorldResourceScatterResolver.Resolve(
                        resourceCandidates,
                        resourceCatalog,
                        occupancy,
                        resourceOrder,
                        resourceOutput);
                checksum += resourceResult.AcceptedCount;

                WorldFeatureResolveResult featureResult =
                    WorldFeatureResolver.Resolve(
                        featureCandidates,
                        featureCatalog,
                        ReadOnlySpan<int>.Empty,
                        ReadOnlySpan<int>.Empty,
                        ReadOnlySpan<WorldFeatureReservation>.Empty,
                        featureOrder,
                        acceptedPerFeature,
                        reservations,
                        featureOutput);
                checksum += featureResult.AcceptedCount;
            }
            long end = Stopwatch.GetTimestamp();
            long exactAllocatedBytes =
                GC.GetAllocatedBytesForCurrentThread() -
                allocatedBefore;
            double elapsedMs =
                (end - start) * 1000d /
                Stopwatch.Frequency;

            string message = string.Format(
                CultureInfo.InvariantCulture,
                "World Framework hot-path allocation env={0} iterations={1} dense={2} sparse={3} candidates={4} elapsedMs={5:F3} exactAllocatedBytes={6} checksum={7}",
                Application.unityVersion,
                iterations,
                denseCount,
                sparseCount,
                candidateCount,
                elapsedMs,
                exactAllocatedBytes,
                checksum);
            TestContext.Progress.WriteLine(message);
            UnityEngine.Debug.Log(message);

            Assert.That(checksum, Is.Not.EqualTo(0L));
            Assert.That(
                exactAllocatedBytes,
                Is.EqualTo(0L),
                "Reusable dense/sparse/resource/feature hot paths must not allocate after warmup.");
        }

        private static long MeasureDeltaSnapshot(
            int deltaCount,
            UnityJsonSaveSerializer serializer,
            out double captureMs,
            out double serializeMs)
        {
            WorldId worldId =
                WorldId.From("world.benchmark.delta_benchmark");
            WorldDeltaCodecRegistry codecs =
                new WorldDeltaCodecRegistry();
            Assert.That(
                codecs.TryRegister(
                    new BenchmarkDeltaCodec(),
                    out string codecError),
                Is.True,
                codecError);
            WorldDeltaPersistenceState state =
                new WorldDeltaPersistenceState(
                    worldId,
                    codecs,
                    deltaCount);
            for (int i = 0; i < deltaCount; i++)
            {
                WorldChunkCoord chunk =
                    new WorldChunkCoord(
                        i % 128,
                        i / 128);
                state.DeltaSet.Append(
                    new BenchmarkDelta(
                        WorldDeltaTarget.ForChunk(
                            worldId,
                            chunk),
                        i,
                        (i * 17) ^ 0x55AA));
            }

            long captureStart =
                Stopwatch.GetTimestamp();
            WorldDeltaSnapshot snapshot =
                state.CaptureSnapshot();
            long captureEnd =
                Stopwatch.GetTimestamp();
            using (MemoryStream stream =
                   new MemoryStream())
            {
                long serializeStart =
                    Stopwatch.GetTimestamp();
                serializer.SerializeAsync(
                        typeof(WorldDeltaSnapshot),
                        snapshot,
                        stream,
                        CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
                long serializeEnd =
                    Stopwatch.GetTimestamp();

                captureMs =
                    (captureEnd - captureStart) *
                    1000d /
                    Stopwatch.Frequency;
                serializeMs =
                    (serializeEnd - serializeStart) *
                    1000d /
                    Stopwatch.Frequency;
                return stream.Length;
            }
        }

        private static BenchmarkPipeline BuildPipeline(
            WorldPlanarSampleLayout layout)
        {
            WorldBiomeDefinition waterBiome =
                new WorldBiomeDefinition(
                    WorldBiomeId.From("biome.benchmark.water"),
                    WorldSurfaceId.From("surface.benchmark.water"),
                    new WorldBiomeCriteria(
                        waterDepth:
                            new WorldRangeRule(
                                0.00001d,
                                1000d)),
                    100);
            WorldBiomeDefinition landBiome =
                new WorldBiomeDefinition(
                    WorldBiomeId.From("biome.benchmark.land"),
                    WorldSurfaceId.From("surface.benchmark.land"),
                    new WorldBiomeCriteria());
            WorldBiomeCatalog biomes =
                new WorldBiomeCatalog(
                    new[]
                    {
                        landBiome,
                        waterBiome
                    }.AsSpan(),
                    landBiome.Id);
            WorldSurfaceCatalog surfaces =
                new WorldSurfaceCatalog(
                    new[]
                    {
                        WorldSurfaceId.From("surface.benchmark.land"),
                        WorldSurfaceId.From("surface.benchmark.water")
                    }.AsSpan());

            WorldGenerationPipelineBuilder builder =
                new WorldGenerationPipelineBuilder();
            WorldChannelStorageDescriptor dense =
                new WorldChannelStorageDescriptor(
                    WorldChannelStorageKind.Dense,
                    WorldChannelScope.Sample);
            ChannelHandle<float> height =
                builder.Channels.Register<float>(
                    WorldDataChannelId.From("benchmark.height"),
                    dense);
            ChannelHandle<float> moisture =
                builder.Channels.Register<float>(
                    WorldDataChannelId.From("benchmark.moisture"),
                    dense);
            ChannelHandle<float> water =
                builder.Channels.Register<float>(
                    WorldDataChannelId.From("benchmark.water"),
                    dense);
            ChannelHandle<float> slope =
                builder.Channels.Register<float>(
                    WorldDataChannelId.From("benchmark.slope"),
                    dense);
            ChannelHandle<int> biome =
                builder.Channels.Register<int>(
                    WorldDataChannelId.From("benchmark.biome"),
                    dense);
            ChannelHandle<int> surface =
                builder.Channels.Register<int>(
                    WorldDataChannelId.From("benchmark.surface"),
                    dense);
            ChannelHandle<byte> buildable =
                builder.Channels.Register<byte>(
                    WorldDataChannelId.From("benchmark.buildable"),
                    dense);

            builder.AddStage(
                new WorldBuildableStage(
                    slope,
                    water,
                    buildable,
                    layout,
                    new WorldBuildableSettings(
                        10f,
                        0f,
                        new[]
                        {
                            WorldBiomeId.From("biome.benchmark.water")
                        }.AsSpan()),
                    biome,
                    biomes));
            builder.AddStage(
                new WorldSurfaceStage(
                    biome,
                    surface,
                    layout,
                    biomes,
                    surfaces));
            builder.AddStage(
                new WorldBiomeStage(
                    height,
                    biome,
                    layout,
                    biomes,
                    moisture,
                    water,
                    slope));
            builder.AddStage(
                new WorldSlopeStage(
                    height,
                    slope,
                    layout));
            builder.AddStage(
                new WorldWaterDepthStage(
                    height,
                    water,
                    layout,
                    0f));
            builder.AddStage(
                new WorldMoistureStage(
                    moisture,
                    layout,
                    new WorldFractalNoiseSettings(
                        WorldRuleId.From("benchmark.noise.moisture"),
                        48L,
                        4,
                        2,
                        0.55d)));
            builder.AddStage(
                new WorldHeightStage(
                    height,
                    layout,
                    new WorldFractalNoiseSettings(
                        WorldRuleId.From("benchmark.noise.height"),
                        64L,
                        5,
                        2,
                        0.5d),
                    -20f,
                    30f));

            WorldGenerationCompileResult compile =
                builder.Compile();
            Assert.That(compile.Success, Is.True);
            return new BenchmarkPipeline(
                compile.Plan,
                height,
                moisture,
                water,
                slope,
                biome,
                surface,
                buildable);
        }

        private readonly struct BenchmarkPipeline
        {
            internal WorldGenerationPlan Plan { get; }
            internal ChannelHandle<float> Height { get; }
            internal ChannelHandle<float> Moisture { get; }
            internal ChannelHandle<float> Water { get; }
            internal ChannelHandle<float> Slope { get; }
            internal ChannelHandle<int> Biome { get; }
            internal ChannelHandle<int> Surface { get; }
            internal ChannelHandle<byte> Buildable { get; }

            internal BenchmarkPipeline(
                WorldGenerationPlan plan,
                ChannelHandle<float> height,
                ChannelHandle<float> moisture,
                ChannelHandle<float> water,
                ChannelHandle<float> slope,
                ChannelHandle<int> biome,
                ChannelHandle<int> surface,
                ChannelHandle<byte> buildable)
            {
                Plan = plan;
                Height = height;
                Moisture = moisture;
                Water = water;
                Slope = slope;
                Biome = biome;
                Surface = surface;
                Buildable = buildable;
            }
        }

        private sealed class BenchmarkDelta : IWorldDelta
        {
            internal static readonly WorldDeltaTypeId StableTypeId =
                WorldDeltaTypeId.From("benchmark.cell_delta");

            public WorldDeltaTypeId TypeId => StableTypeId;
            public WorldDeltaVersion Version =>
                new WorldDeltaVersion(1);
            public WorldDeltaTarget Target { get; }
            public int Index { get; }
            public int Value { get; }

            internal BenchmarkDelta(
                WorldDeltaTarget target,
                int index,
                int value)
            {
                Target = target;
                Index = index;
                Value = value;
            }
        }

        private sealed class BenchmarkDeltaCodec :
            IWorldDeltaCodec
        {
            public WorldDeltaTypeId TypeId =>
                BenchmarkDelta.StableTypeId;

            public bool TryEncode(
                IWorldDelta delta,
                out string payload,
                out string error)
            {
                BenchmarkDelta typed =
                    delta as BenchmarkDelta;
                if (typed == null)
                {
                    payload = null;
                    error = "Wrong delta type.";
                    return false;
                }

                payload =
                    typed.Index.ToString(
                        CultureInfo.InvariantCulture) +
                    "," +
                    typed.Value.ToString(
                        CultureInfo.InvariantCulture);
                error = null;
                return true;
            }

            public bool TryDecode(
                WorldDeltaTarget target,
                WorldDeltaVersion version,
                string payload,
                out IWorldDelta delta,
                out string error)
            {
                delta = null;
                error = null;
                if (version.Value != 1)
                {
                    error = "Unsupported version.";
                    return false;
                }

                string[] parts =
                    (payload ?? string.Empty).Split(',');
                if (parts.Length != 2 ||
                    !int.TryParse(
                        parts[0],
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int index) ||
                    !int.TryParse(
                        parts[1],
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int value))
                {
                    error = "Invalid payload.";
                    return false;
                }

                delta =
                    new BenchmarkDelta(
                        target,
                        index,
                        value);
                return true;
            }
        }
    }
}
