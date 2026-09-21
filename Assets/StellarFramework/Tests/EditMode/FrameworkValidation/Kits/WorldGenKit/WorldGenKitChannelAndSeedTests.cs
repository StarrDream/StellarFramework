using System;
using NUnit.Framework;
using StellarFramework.WorldGenKit;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class WorldGenKitChannelAndSeedTests
    {
        [Test]
        public void StableIdsUseCanonicalFormat()
        {
            Assert.That(WorldDataChannelId.From("terrain.height").Value, Is.EqualTo("terrain.height"));
            Assert.That(WorldGenerationStageId.From("stage.height").Value, Is.EqualTo("stage.height"));
            Assert.That(WorldRuleId.From("rule.height_range").Value, Is.EqualTo("rule.height_range"));

            string[] invalid = { null, "", "Terrain.Height", "terrain-height", ".terrain", "terrain.", "terrain..height", " terrain.height" };
            for (int i = 0; i < invalid.Length; i++)
            {
                Assert.That(WorldDataChannelId.TryCreate(invalid[i], out _, out string error), Is.False, invalid[i]);
                Assert.That(error, Is.Not.Null.And.Not.Empty, invalid[i]);
            }
        }

        [Test]
        public void ChannelRegistryProtectsTypeAndRegistryOwnership()
        {
            WorldChannelRegistryBuilder builder = new WorldChannelRegistryBuilder();
            WorldDataChannelId heightId = WorldDataChannelId.From("terrain.height");
            WorldDataChannelId biomeId = WorldDataChannelId.From("terrain.biome");

            ChannelHandle<float> height = builder.Register<float>(
                heightId,
                new WorldChannelStorageDescriptor(WorldChannelStorageKind.Dense, WorldChannelScope.Sample));
            ChannelHandle<int> biome = builder.Register<int>(
                biomeId,
                new WorldChannelStorageDescriptor(WorldChannelStorageKind.External, WorldChannelScope.Sample),
                WorldChannelSourceMode.ProvidedInput);

            WorldChannelRegistry registry = builder.Build();
            Assert.That(registry.Count, Is.EqualTo(2));
            Assert.That(registry.IsHandleValid(height), Is.True);
            Assert.That(registry.IsHandleValid(biome), Is.True);
            Assert.That(default(ChannelHandle<float>).IsValid, Is.False);

            Assert.That(registry.TryResolve(heightId, out ChannelHandle<float> resolved, out WorldChannelResolveError error), Is.True);
            Assert.That(error, Is.EqualTo(WorldChannelResolveError.None));
            Assert.That(resolved, Is.EqualTo(height));

            Assert.That(registry.TryResolve(heightId, out ChannelHandle<int> wrongType, out error), Is.False);
            Assert.That(wrongType.IsValid, Is.False);
            Assert.That(error, Is.EqualTo(WorldChannelResolveError.TypeMismatch));

            Assert.That(registry.TryGetDescriptor(biome, out WorldChannelDescriptor descriptor), Is.True);
            Assert.That(descriptor.Storage.Kind, Is.EqualTo(WorldChannelStorageKind.External));
            Assert.That(descriptor.Storage.Scope, Is.EqualTo(WorldChannelScope.Sample));
            Assert.That(descriptor.SourceMode, Is.EqualTo(WorldChannelSourceMode.ProvidedInput));

            WorldChannelRegistryBuilder otherBuilder = new WorldChannelRegistryBuilder();
            ChannelHandle<float> foreign = otherBuilder.Register<float>(
                heightId,
                new WorldChannelStorageDescriptor(WorldChannelStorageKind.Dense, WorldChannelScope.Sample));
            WorldChannelRegistry other = otherBuilder.Build();
            Assert.That(other.Generation, Is.Not.EqualTo(registry.Generation));
            Assert.That(registry.IsHandleValid(foreign), Is.False);
            Assert.That(other.IsHandleValid(height), Is.False);
        }

        [Test]
        public void ChannelRegistryRejectsDuplicateInvalidAndPostBuildRegistration()
        {
            WorldChannelRegistryBuilder builder = new WorldChannelRegistryBuilder();
            WorldDataChannelId id = WorldDataChannelId.From("terrain.height");
            WorldChannelStorageDescriptor dense = new WorldChannelStorageDescriptor(
                WorldChannelStorageKind.Dense,
                WorldChannelScope.Sample);

            builder.Register<float>(id, dense);
            Assert.That(builder.TryRegister(id, dense, WorldChannelSourceMode.ProducedByStage,
                out ChannelHandle<int> duplicate, out WorldChannelRegistrationError error), Is.False);
            Assert.That(duplicate.IsValid, Is.False);
            Assert.That(error, Is.EqualTo(WorldChannelRegistrationError.DuplicateId));

            Assert.That(builder.TryRegister(default(WorldDataChannelId), dense, WorldChannelSourceMode.ProducedByStage,
                out ChannelHandle<float> invalidId, out error), Is.False);
            Assert.That(invalidId.IsValid, Is.False);
            Assert.That(error, Is.EqualTo(WorldChannelRegistrationError.InvalidId));

            Assert.That(builder.TryRegister(WorldDataChannelId.From("terrain.slope"), default(WorldChannelStorageDescriptor),
                WorldChannelSourceMode.ProducedByStage, out ChannelHandle<float> invalidStorage, out error), Is.False);
            Assert.That(invalidStorage.IsValid, Is.False);
            Assert.That(error, Is.EqualTo(WorldChannelRegistrationError.InvalidStorage));

            builder.Build();
            Assert.That(builder.TryRegister(WorldDataChannelId.From("terrain.slope"), dense,
                WorldChannelSourceMode.ProducedByStage, out ChannelHandle<float> frozen, out error), Is.False);
            Assert.That(frozen.IsValid, Is.False);
            Assert.That(error, Is.EqualTo(WorldChannelRegistrationError.RegistryFrozen));
            Assert.That(() => builder.Build(), Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void StableSeedDerivationHasLockedCrossSessionTestVector()
        {
            WorldGenerationSeed seed = new WorldGenerationSeed(123456789UL);
            WorldGenerationStageId stage = WorldGenerationStageId.From("stage.height");

            WorldGenerationSeed derived = seed.Derive(-42L, 77L, stage, 999UL);
            Assert.That(derived.Value, Is.EqualTo(0x56D9FA3612E0585DUL));
            Assert.That(seed.Derive(-42L, 77L, stage, 999UL), Is.EqualTo(derived));

            Assert.That(seed.Derive(-41L, 77L, stage, 999UL), Is.Not.EqualTo(derived));
            Assert.That(seed.Derive(-42L, 78L, stage, 999UL), Is.Not.EqualTo(derived));
            Assert.That(seed.Derive(-42L, 77L, WorldGenerationStageId.From("stage.water"), 999UL), Is.Not.EqualTo(derived));
            Assert.That(seed.Derive(-42L, 77L, stage, 1000UL), Is.Not.EqualTo(derived));
        }
    }
}
