using System;
using NUnit.Framework;
using StellarFramework.WorldGenKit;
using StellarFramework.WorldGenKit.Authoring;
using StellarFramework.WorldGenKit.Builtins;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class WorldGenKitAuthoringOperationsRegionTests
    {
        [Test]
        public void HeightRaiseLowerSetAndFlattenComposeWithoutMutatingBase()
        {
            WorldPlanarSampleLayout layout = new WorldPlanarSampleLayout(4, 4);
            float[] baseValues = new float[layout.Count];
            for (int i = 0; i < baseValues.Length; i++) baseValues[i] = i;
            float[] originalBase = (float[])baseValues.Clone();
            float[] finalValues = new float[layout.Count];
            WorldDenseOverrideLayer<float> layer = new WorldDenseOverrideLayer<float>(layout);
            WorldSampleRect edit = new WorldSampleRect(1, 1, 2, 2);

            WorldHeightAuthoringOperations.Raise(baseValues.AsSpan(), layer, in edit, 5f);
            WorldHeightAuthoringOperations.Lower(baseValues.AsSpan(), layer, in edit, 2f);
            Assert.That(layer.GetComposedValue(baseValues.AsSpan(), 1, 1),
                Is.EqualTo(baseValues[layout.GetIndex(1, 1)] + 3f));

            WorldSampleRect one = new WorldSampleRect(2, 2, 1, 1);
            WorldHeightAuthoringOperations.SetHeight(layer, in one, 42f);
            Assert.That(layer.GetComposedValue(baseValues.AsSpan(), 2, 2), Is.EqualTo(42f));
            WorldHeightAuthoringOperations.Flatten(layer, in one, 7f);
            Assert.That(layer.GetComposedValue(baseValues.AsSpan(), 2, 2), Is.EqualTo(7f));

            layer.Compose(baseValues.AsSpan(), finalValues.AsSpan());
            Assert.That(baseValues, Is.EqualTo(originalBase));
            Assert.That(layer.TryGetDirtyBounds(out WorldSampleRect dirty), Is.True);
            Assert.That(dirty, Is.EqualTo(edit));
            Assert.That(finalValues[layout.GetIndex(0, 0)], Is.EqualTo(baseValues[0]));
        }

        [Test]
        public void SmoothUsesSnapshotAndCallerOwnedScratch()
        {
            WorldPlanarSampleLayout layout = new WorldPlanarSampleLayout(3, 3);
            float[] baseValues = new float[layout.Count];
            baseValues[layout.GetIndex(1, 1)] = 10f;
            WorldDenseOverrideLayer<float> layer = new WorldDenseOverrideLayer<float>(layout);
            WorldSampleRect center = new WorldSampleRect(1, 1, 1, 1);
            float[] scratch = new float[1];

            WorldHeightAuthoringOperations.Smooth(
                baseValues.AsSpan(),
                layer,
                in center,
                1f,
                scratch.AsSpan());

            // Center + four cardinal neighbors => (10 + 0 + 0 + 0 + 0) / 5 = 2.
            Assert.That(layer.GetComposedValue(baseValues.AsSpan(), 1, 1), Is.EqualTo(2f));
            Assert.That(baseValues[layout.GetIndex(1, 1)], Is.EqualTo(10f));
            Assert.That(layer.TryConsumeDirtyBounds(out WorldSampleRect dirty), Is.True);
            Assert.That(dirty, Is.EqualTo(center));

            WorldDenseOverrideLayer<float> failureLayer = new WorldDenseOverrideLayer<float>(layout);
            Assert.That(
                () => WorldHeightAuthoringOperations.Smooth(
                    baseValues.AsSpan(),
                    failureLayer,
                    in center,
                    1f,
                    Span<float>.Empty),
                Throws.TypeOf<ArgumentException>());
            Assert.That(failureLayer.Count, Is.EqualTo(0));
            Assert.That(failureLayer.HasDirtyBounds, Is.False);
        }

        [Test]
        public void GenericPaintSupportsSemanticIndicesAndMasks()
        {
            WorldPlanarSampleLayout layout = new WorldPlanarSampleLayout(4, 3);
            WorldSampleRect bounds = new WorldSampleRect(1, 1, 2, 2);
            WorldDenseOverrideLayer<int> biomePaint = new WorldDenseOverrideLayer<int>(layout);
            WorldDenseOverrideLayer<byte> maskPaint = new WorldDenseOverrideLayer<byte>(layout);
            int[] biomeBase = new int[layout.Count];
            byte[] maskBase = new byte[layout.Count];
            int[] biomeFinal = new int[layout.Count];
            byte[] maskFinal = new byte[layout.Count];

            WorldAuthoringPaint.Fill(biomePaint, in bounds, 7);
            WorldAuthoringPaint.Fill(maskPaint, in bounds, (byte)1);
            biomePaint.Compose(biomeBase.AsSpan(), biomeFinal.AsSpan());
            maskPaint.Compose(maskBase.AsSpan(), maskFinal.AsSpan());

            for (int y = 0; y < layout.Height; y++)
            {
                for (int x = 0; x < layout.Width; x++)
                {
                    int index = layout.GetIndex(x, y);
                    bool painted = bounds.Contains(x, y);
                    Assert.That(biomeFinal[index], Is.EqualTo(painted ? 7 : 0));
                    Assert.That(maskFinal[index], Is.EqualTo(painted ? (byte)1 : (byte)0));
                }
            }
        }

        [Test]
        public void StableIdSemanticPaintResolvesBeforeMutatingSparseOverrides()
        {
            WorldPlanarSampleLayout layout = new WorldPlanarSampleLayout(3, 2);
            WorldSampleRect bounds = new WorldSampleRect(1, 0, 2, 2);
            WorldBiomeDefinition grass = new WorldBiomeDefinition(
                WorldBiomeId.From("biome.grass"),
                WorldSurfaceId.From("surface.grass"),
                new WorldBiomeCriteria());
            WorldBiomeDefinition water = new WorldBiomeDefinition(
                WorldBiomeId.From("biome.water"),
                WorldSurfaceId.From("surface.water"),
                new WorldBiomeCriteria());
            WorldBiomeCatalog biomes = new WorldBiomeCatalog(new[] { grass, water }.AsSpan(), grass.Id);
            WorldSurfaceCatalog surfaces = new WorldSurfaceCatalog(
                new[] { WorldSurfaceId.From("surface.grass"), WorldSurfaceId.From("surface.water") }.AsSpan());

            WorldDenseOverrideLayer<int> biomeLayer = new WorldDenseOverrideLayer<int>(layout);
            WorldDenseOverrideLayer<int> surfaceLayer = new WorldDenseOverrideLayer<int>(layout);
            WorldSemanticAuthoringPaint.PaintBiome(biomeLayer, in bounds, water.Id, biomes);
            WorldSemanticAuthoringPaint.PaintSurface(
                surfaceLayer,
                in bounds,
                WorldSurfaceId.From("surface.water"),
                surfaces);

            biomes.TryGetIndex(water.Id, out int waterBiomeIndex);
            surfaces.TryGetIndex(WorldSurfaceId.From("surface.water"), out int waterSurfaceIndex);
            int[] emptyBase = new int[layout.Count];
            Assert.That(biomeLayer.GetComposedValue(emptyBase.AsSpan(), 2, 1), Is.EqualTo(waterBiomeIndex));
            Assert.That(surfaceLayer.GetComposedValue(emptyBase.AsSpan(), 2, 1), Is.EqualTo(waterSurfaceIndex));

            WorldDenseOverrideLayer<int> failureLayer = new WorldDenseOverrideLayer<int>(layout);
            Assert.That(
                () => WorldSemanticAuthoringPaint.PaintBiome(
                    failureLayer,
                    in bounds,
                    WorldBiomeId.From("biome.missing"),
                    biomes),
                Throws.TypeOf<ArgumentException>());
            Assert.That(failureLayer.Count, Is.EqualTo(0));
            Assert.That(failureLayer.HasDirtyBounds, Is.False);
        }

        [Test]
        public void DirtyPropagationOnlyIncludesTrueBuiltinDependencies()
        {
            WorldPlanarSampleLayout layout = new WorldPlanarSampleLayout(8, 8);
            WorldSampleRect edit = new WorldSampleRect(3, 3, 2, 2);

            WorldBuiltinDerivedDirtyRegions height = WorldAuthoringDirtyPropagation.FromHeightEdit(in edit, in layout);
            Assert.That(height.WaterDepth, Is.EqualTo(edit));
            Assert.That(height.Slope, Is.EqualTo(new WorldSampleRect(2, 2, 4, 4)));
            Assert.That(height.Biome, Is.EqualTo(height.Slope));
            Assert.That(height.Surface, Is.EqualTo(height.Slope));
            Assert.That(height.Buildable, Is.EqualTo(height.Slope));

            WorldBuiltinDerivedDirtyRegions moisture = WorldAuthoringDirtyPropagation.FromMoistureEdit(in edit);
            Assert.That(moisture.WaterDepth.HasValue, Is.False);
            Assert.That(moisture.Slope.HasValue, Is.False);
            Assert.That(moisture.Biome, Is.EqualTo(edit));
            Assert.That(moisture.Surface, Is.EqualTo(edit));
            Assert.That(moisture.Buildable, Is.EqualTo(edit));

            WorldBuiltinDerivedDirtyRegions biome = WorldAuthoringDirtyPropagation.FromBiomePaint(in edit);
            Assert.That(biome.Biome.HasValue, Is.False);
            Assert.That(biome.Surface, Is.EqualTo(edit));
            Assert.That(biome.Buildable, Is.EqualTo(edit));

            WorldBuiltinDerivedDirtyRegions surface = WorldAuthoringDirtyPropagation.FromSurfacePaint();
            Assert.That(surface.WaterDepth.HasValue, Is.False);
            Assert.That(surface.Slope.HasValue, Is.False);
            Assert.That(surface.Biome.HasValue, Is.False);
            Assert.That(surface.Surface.HasValue, Is.False);
            Assert.That(surface.Buildable.HasValue, Is.False);
        }

        [Test]
        public void RegionalRecomputeAfterHeightOverrideDoesNotTouchOutsideDirtyCascade()
        {
            WorldPlanarSampleLayout layout = new WorldPlanarSampleLayout(5, 5);
            WorldChannelRegistryBuilder registryBuilder = new WorldChannelRegistryBuilder();
            WorldChannelStorageDescriptor dense = new WorldChannelStorageDescriptor(
                WorldChannelStorageKind.Dense,
                WorldChannelScope.Sample);
            ChannelHandle<float> heightHandle = registryBuilder.Register<float>(WorldDataChannelId.From("terrain.height"), dense);
            ChannelHandle<float> waterHandle = registryBuilder.Register<float>(WorldDataChannelId.From("terrain.water_depth"), dense);
            ChannelHandle<float> slopeHandle = registryBuilder.Register<float>(WorldDataChannelId.From("terrain.slope"), dense);
            ChannelHandle<int> biomeHandle = registryBuilder.Register<int>(WorldDataChannelId.From("terrain.biome"), dense);
            ChannelHandle<int> surfaceHandle = registryBuilder.Register<int>(WorldDataChannelId.From("terrain.surface"), dense);
            ChannelHandle<byte> buildableHandle = registryBuilder.Register<byte>(WorldDataChannelId.From("terrain.buildable"), dense);
            WorldChannelRegistry registry = registryBuilder.Build();

            WorldBiomeDefinition waterBiome = new WorldBiomeDefinition(
                WorldBiomeId.From("biome.water"),
                WorldSurfaceId.From("surface.water"),
                new WorldBiomeCriteria(waterDepth: new WorldRangeRule(0.001d, 1000d)),
                100);
            WorldBiomeDefinition landBiome = new WorldBiomeDefinition(
                WorldBiomeId.From("biome.land"),
                WorldSurfaceId.From("surface.grass"),
                new WorldBiomeCriteria());
            WorldBiomeCatalog biomes = new WorldBiomeCatalog(new[] { waterBiome, landBiome }.AsSpan(), landBiome.Id);
            WorldSurfaceCatalog surfaces = new WorldSurfaceCatalog(
                new[] { WorldSurfaceId.From("surface.grass"), WorldSurfaceId.From("surface.water") }.AsSpan());

            DenseChannelStorage<float> finalHeight = new DenseChannelStorage<float>(layout.Count);
            finalHeight.Fill(10f);
            DenseChannelStorage<float> water = new DenseChannelStorage<float>(layout.Count);
            DenseChannelStorage<float> slope = new DenseChannelStorage<float>(layout.Count);
            DenseChannelStorage<int> biome = new DenseChannelStorage<int>(layout.Count);
            DenseChannelStorage<int> surface = new DenseChannelStorage<int>(layout.Count);
            DenseChannelStorage<byte> buildable = new DenseChannelStorage<byte>(layout.Count);
            WorldGenerationDataSet data = new WorldGenerationDataSet(registry);
            data.Bind(heightHandle, finalHeight);
            data.Bind(waterHandle, water);
            data.Bind(slopeHandle, slope);
            data.Bind(biomeHandle, biome);
            data.Bind(surfaceHandle, surface);
            data.Bind(buildableHandle, buildable);

            WorldWaterDepthStage waterStage = new WorldWaterDepthStage(heightHandle, waterHandle, layout, 0f);
            WorldSlopeStage slopeStage = new WorldSlopeStage(heightHandle, slopeHandle, layout);
            WorldBiomeStage biomeStage = new WorldBiomeStage(heightHandle, biomeHandle, layout, biomes, waterDepth: waterHandle, slope: slopeHandle);
            WorldSurfaceStage surfaceStage = new WorldSurfaceStage(biomeHandle, surfaceHandle, layout, biomes, surfaces);
            WorldBuildableStage buildableStage = new WorldBuildableStage(
                slopeHandle,
                waterHandle,
                buildableHandle,
                layout,
                new WorldBuildableSettings(5f, 0f, new[] { waterBiome.Id }.AsSpan()),
                biomeHandle,
                biomes);

            WorldPlanarSampleRegion whole = WorldPlanarSampleRegion.Whole(in layout);
            Assert.That(waterStage.ExecuteRegion(data, in whole).Success, Is.True);
            Assert.That(slopeStage.ExecuteRegion(data, in whole).Success, Is.True);
            Assert.That(biomeStage.ExecuteRegion(data, in whole).Success, Is.True);
            Assert.That(surfaceStage.ExecuteRegion(data, in whole).Success, Is.True);
            Assert.That(buildableStage.ExecuteRegion(data, in whole).Success, Is.True);

            float[] oldWater = water.AsReadOnlySpan().ToArray();
            float[] oldSlope = slope.AsReadOnlySpan().ToArray();
            int[] oldBiome = biome.AsReadOnlySpan().ToArray();
            int[] oldSurface = surface.AsReadOnlySpan().ToArray();
            byte[] oldBuildable = buildable.AsReadOnlySpan().ToArray();

            float[] baseHeight = new float[layout.Count];
            Array.Fill(baseHeight, 10f);
            WorldDenseOverrideLayer<float> overrides = new WorldDenseOverrideLayer<float>(layout);
            WorldSampleRect center = new WorldSampleRect(2, 2, 1, 1);
            WorldHeightAuthoringOperations.SetHeight(overrides, in center, -10f);
            overrides.Compose(baseHeight.AsSpan(), finalHeight.AsSpan());
            Assert.That(baseHeight[layout.GetIndex(2, 2)], Is.EqualTo(10f));

            WorldBuiltinDerivedDirtyRegions dirty = WorldAuthoringDirtyPropagation.FromHeightEdit(in center, in layout);
            WorldSampleRect waterDirty = dirty.WaterDepth.Value;
            WorldSampleRect slopeDirty = dirty.Slope.Value;
            WorldSampleRect biomeDirty = dirty.Biome.Value;
            WorldSampleRect surfaceDirty = dirty.Surface.Value;
            WorldSampleRect buildableDirty = dirty.Buildable.Value;
            WorldPlanarSampleRegion waterRegion = WorldAuthoringDirtyPropagation.ToRegion(in waterDirty);
            WorldPlanarSampleRegion slopeRegion = WorldAuthoringDirtyPropagation.ToRegion(in slopeDirty);
            WorldPlanarSampleRegion biomeRegion = WorldAuthoringDirtyPropagation.ToRegion(in biomeDirty);
            WorldPlanarSampleRegion surfaceRegion = WorldAuthoringDirtyPropagation.ToRegion(in surfaceDirty);
            WorldPlanarSampleRegion buildableRegion = WorldAuthoringDirtyPropagation.ToRegion(in buildableDirty);

            Assert.That(waterStage.ExecuteRegion(data, in waterRegion).Success, Is.True);
            Assert.That(slopeStage.ExecuteRegion(data, in slopeRegion).Success, Is.True);
            Assert.That(biomeStage.ExecuteRegion(data, in biomeRegion).Success, Is.True);
            Assert.That(surfaceStage.ExecuteRegion(data, in surfaceRegion).Success, Is.True);
            Assert.That(buildableStage.ExecuteRegion(data, in buildableRegion).Success, Is.True);

            int centerIndex = layout.GetIndex(2, 2);
            Assert.That(water[centerIndex], Is.EqualTo(10f));
            Assert.That(biomes.GetDefinition(biome[centerIndex]).Id, Is.EqualTo(waterBiome.Id));
            Assert.That(surfaces.GetId(surface[centerIndex]).Value, Is.EqualTo("surface.water"));
            Assert.That(buildable[centerIndex], Is.EqualTo((byte)0));

            WorldSampleRect cascade = slopeDirty;
            for (int y = 0; y < layout.Height; y++)
            {
                for (int x = 0; x < layout.Width; x++)
                {
                    if (cascade.Contains(x, y)) continue;
                    int index = layout.GetIndex(x, y);
                    Assert.That(water[index], Is.EqualTo(oldWater[index]), "water outside dirty cascade");
                    Assert.That(slope[index], Is.EqualTo(oldSlope[index]), "slope outside dirty cascade");
                    Assert.That(biome[index], Is.EqualTo(oldBiome[index]), "biome outside dirty cascade");
                    Assert.That(surface[index], Is.EqualTo(oldSurface[index]), "surface outside dirty cascade");
                    Assert.That(buildable[index], Is.EqualTo(oldBuildable[index]), "buildable outside dirty cascade");
                }
            }
        }
    }
}
