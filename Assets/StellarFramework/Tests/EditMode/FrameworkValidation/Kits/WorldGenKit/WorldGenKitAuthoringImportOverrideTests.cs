using System;
using NUnit.Framework;
using StellarFramework.WorldGenKit;
using StellarFramework.WorldGenKit.Authoring;
using StellarFramework.WorldGenKit.Builtins;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class WorldGenKitAuthoringImportOverrideTests
    {
        [Test]
        public void SampleRectSupportsUnionExpansionAndLayoutValidation()
        {
            WorldPlanarSampleLayout layout = new WorldPlanarSampleLayout(10, 8);
            WorldSampleRect a = new WorldSampleRect(2, 3, 3, 2);
            WorldSampleRect b = new WorldSampleRect(6, 1, 2, 4);

            Assert.That(a.Contains(2, 3), Is.True);
            Assert.That(a.Contains(5, 3), Is.False);
            Assert.That(a.FitsWithin(in layout), Is.True);
            Assert.That(a.Union(in b), Is.EqualTo(new WorldSampleRect(2, 1, 6, 4)));
            Assert.That(a.ExpandClamped(3, in layout), Is.EqualTo(new WorldSampleRect(0, 0, 8, 8)));
            Assert.That(new WorldSampleRect(9, 7, 1, 1).ExpandClamped(5, in layout),
                Is.EqualTo(new WorldSampleRect(4, 2, 6, 6)));
        }

        [Test]
        public void DenseImportCopiesTypedHeightAndMaskAndRejectsLengthMismatchBeforeWrite()
        {
            float[] heights = { -1f, 2f, 3.5f, 9f };
            DenseChannelStorage<float> heightStorage = new DenseChannelStorage<float>(4);
            heightStorage.Fill(99f);
            WorldDenseChannelImport.CopyExact(heights.AsSpan(), heightStorage);
            Assert.That(heightStorage.AsReadOnlySpan().ToArray(), Is.EqualTo(heights));

            byte[] mask = { 0, 1, 1, 0 };
            DenseChannelStorage<byte> maskStorage = new DenseChannelStorage<byte>(4);
            WorldDenseChannelImport.CopyExact(mask.AsSpan(), maskStorage);
            Assert.That(maskStorage.AsReadOnlySpan().ToArray(), Is.EqualTo(mask));

            float[] beforeFailure = heightStorage.AsReadOnlySpan().ToArray();
            Assert.That(
                () => WorldDenseChannelImport.CopyExact(new[] { 1f, 2f, 3f }.AsSpan(), heightStorage),
                Throws.TypeOf<ArgumentException>());
            Assert.That(heightStorage.AsReadOnlySpan().ToArray(), Is.EqualTo(beforeFailure));
        }

        [Test]
        public void SemanticBiomeImportResolvesStableIdsAndUnknownIdIsFailureAtomic()
        {
            WorldBiomeDefinition grass = Biome("biome.grass", "surface.grass");
            WorldBiomeDefinition water = Biome("biome.water", "surface.water");
            WorldBiomeCatalog catalog = new WorldBiomeCatalog(new[] { water, grass }.AsSpan(), grass.Id);
            DenseChannelStorage<int> destination = new DenseChannelStorage<int>(4);
            destination.Fill(77);

            WorldBiomeId[] ids = { grass.Id, water.Id, grass.Id, water.Id };
            Assert.That(WorldSemanticChannelImport.TryImportBiomeIds(
                ids.AsSpan(), catalog, destination, out int errorIndex, out WorldSemanticImportError error), Is.True);
            Assert.That(error, Is.EqualTo(WorldSemanticImportError.None));
            Assert.That(errorIndex, Is.EqualTo(-1));
            catalog.TryGetIndex(grass.Id, out int grassIndex);
            catalog.TryGetIndex(water.Id, out int waterIndex);
            Assert.That(destination.AsReadOnlySpan().ToArray(),
                Is.EqualTo(new[] { grassIndex, waterIndex, grassIndex, waterIndex }));

            destination.Fill(123);
            WorldBiomeId[] invalid =
            {
                grass.Id,
                WorldBiomeId.From("biome.missing"),
                water.Id,
                grass.Id
            };
            Assert.That(WorldSemanticChannelImport.TryImportBiomeIds(
                invalid.AsSpan(), catalog, destination, out errorIndex, out error), Is.False);
            Assert.That(error, Is.EqualTo(WorldSemanticImportError.UnknownStableId));
            Assert.That(errorIndex, Is.EqualTo(1));
            Assert.That(destination.AsReadOnlySpan().ToArray(), Is.EqualTo(new[] { 123, 123, 123, 123 }));
        }

        [Test]
        public void SemanticSurfaceImportIsTypedAndLengthMismatchIsFailureAtomic()
        {
            WorldSurfaceCatalog catalog = new WorldSurfaceCatalog(
                new[] { WorldSurfaceId.From("surface.grass"), WorldSurfaceId.From("surface.mud") }.AsSpan());
            DenseChannelStorage<int> destination = new DenseChannelStorage<int>(2);
            destination.Fill(55);
            WorldSurfaceId[] ids = { WorldSurfaceId.From("surface.mud"), WorldSurfaceId.From("surface.grass") };

            Assert.That(WorldSemanticChannelImport.TryImportSurfaceIds(
                ids.AsSpan(), catalog, destination, out _, out WorldSemanticImportError error), Is.True);
            Assert.That(error, Is.EqualTo(WorldSemanticImportError.None));
            Assert.That(catalog.GetId(destination[0]).Value, Is.EqualTo("surface.mud"));
            Assert.That(catalog.GetId(destination[1]).Value, Is.EqualTo("surface.grass"));

            destination.Fill(88);
            Assert.That(WorldSemanticChannelImport.TryImportSurfaceIds(
                new[] { WorldSurfaceId.From("surface.grass") }.AsSpan(),
                catalog,
                destination,
                out int errorIndex,
                out error), Is.False);
            Assert.That(error, Is.EqualTo(WorldSemanticImportError.LengthMismatch));
            Assert.That(errorIndex, Is.EqualTo(-1));
            Assert.That(destination.AsReadOnlySpan().ToArray(), Is.EqualTo(new[] { 88, 88 }));
        }

        [Test]
        public void OverrideLayerComposesOverBaseWithoutMutatingBaseAndUnionsDirtyBounds()
        {
            WorldPlanarSampleLayout layout = new WorldPlanarSampleLayout(4, 3);
            float[] baseValues = new float[layout.Count];
            for (int i = 0; i < baseValues.Length; i++) baseValues[i] = i;
            float[] originalBase = (float[])baseValues.Clone();
            float[] finalValues = new float[layout.Count];
            WorldDenseOverrideLayer<float> layer = new WorldDenseOverrideLayer<float>(layout);

            layer.Set(1, 1, 100f);
            layer.Set(3, 2, 200f);
            layer.SetByIndex(0, -5f);
            layer.Compose(baseValues.AsSpan(), finalValues.AsSpan());

            Assert.That(baseValues, Is.EqualTo(originalBase));
            Assert.That(finalValues[layout.GetIndex(1, 1)], Is.EqualTo(100f));
            Assert.That(finalValues[layout.GetIndex(3, 2)], Is.EqualTo(200f));
            Assert.That(finalValues[0], Is.EqualTo(-5f));
            Assert.That(layer.Count, Is.EqualTo(3));
            Assert.That(layer.TryGetDirtyBounds(out WorldSampleRect dirty), Is.True);
            Assert.That(dirty, Is.EqualTo(new WorldSampleRect(0, 0, 4, 3)));
            Assert.That(layer.GetComposedValue(baseValues.AsSpan(), 1, 1), Is.EqualTo(100f));
            Assert.That(layer.GetComposedValue(baseValues.AsSpan(), 2, 1), Is.EqualTo(baseValues[6]));
        }

        [Test]
        public void RemovingAndClearingOverridesRevealsBaseAndTracksDirtyRegion()
        {
            WorldPlanarSampleLayout layout = new WorldPlanarSampleLayout(5, 4);
            int[] baseValues = new int[layout.Count];
            for (int i = 0; i < baseValues.Length; i++) baseValues[i] = i + 10;
            int[] finalValues = new int[layout.Count];
            WorldDenseOverrideLayer<int> layer = new WorldDenseOverrideLayer<int>(layout);

            layer.Set(2, 1, 999);
            Assert.That(layer.TryConsumeDirtyBounds(out WorldSampleRect setDirty), Is.True);
            Assert.That(setDirty, Is.EqualTo(new WorldSampleRect(2, 1, 1, 1)));
            Assert.That(layer.TryConsumeDirtyBounds(out _), Is.False);

            Assert.That(layer.Remove(2, 1), Is.True);
            layer.Compose(baseValues.AsSpan(), finalValues.AsSpan());
            Assert.That(finalValues, Is.EqualTo(baseValues));
            Assert.That(layer.TryConsumeDirtyBounds(out WorldSampleRect removeDirty), Is.True);
            Assert.That(removeDirty, Is.EqualTo(new WorldSampleRect(2, 1, 1, 1)));

            layer.Set(0, 0, 100);
            layer.Set(4, 3, 200);
            layer.TryConsumeDirtyBounds(out _);
            layer.Clear();
            Assert.That(layer.Count, Is.EqualTo(0));
            Assert.That(layer.TryConsumeDirtyBounds(out WorldSampleRect clearDirty), Is.True);
            Assert.That(clearDirty, Is.EqualTo(new WorldSampleRect(0, 0, 5, 4)));
            layer.Compose(baseValues.AsSpan(), finalValues.AsSpan());
            Assert.That(finalValues, Is.EqualTo(baseValues));
        }

        private static WorldBiomeDefinition Biome(string biomeId, string surfaceId) =>
            new WorldBiomeDefinition(
                WorldBiomeId.From(biomeId),
                WorldSurfaceId.From(surfaceId),
                new WorldBiomeCriteria());
    }
}
