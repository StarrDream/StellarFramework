using System;
using NUnit.Framework;
using StellarFramework.WorldGenKit.Feature;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class WorldGenKitCompoundFeatureTests
    {
        [Test]
        public void TemplateRejectsDuplicateSlotIds()
        {
            WorldCompoundSlotId slot = WorldCompoundSlotId.From("compound.slot.house_a");
            WorldCompoundElementTypeId house = WorldCompoundElementTypeId.From("compound.element.house");
            WorldCompoundFeatureElement[] elements =
            {
                new WorldCompoundFeatureElement(slot, house, 0d, 0d),
                new WorldCompoundFeatureElement(slot, house, 5d, 0d)
            };

            Assert.That(
                () => new WorldCompoundFeatureTemplate(WorldCompoundTemplateId.From("compound.village"), elements.AsSpan()),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void CompoundProfileRejectsBindingTemplateToNonCompoundFeature()
        {
            WorldFeatureCategoryId category = WorldFeatureCategoryId.From("feature_category.test");
            WorldFeatureDefinition tower = new WorldFeatureDefinition(
                WorldFeatureId.From("feature.tower"),
                category,
                WorldFeatureKind.Landmark,
                WorldFeatureFootprint.Rectangle(4d, 4d),
                WorldFeatureQuota.Unlimited());
            WorldFeatureCatalog catalog = new WorldFeatureCatalog(new[] { tower }.AsSpan());
            WorldCompoundFeatureTemplate template = CreateVillageTemplate();
            WorldCompoundFeatureProfile profile = new WorldCompoundFeatureProfile(new[]
            {
                new WorldCompoundFeatureBinding(tower.Id, template)
            }.AsSpan());

            Assert.That(() => profile.Compile(catalog), Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void VillageTemplateTransformsLocalMembersByParentPoseDeterministically()
        {
            CreateVillageFixture(out WorldFeatureDefinition village, out WorldFeatureCatalog catalog, out WorldCompiledCompoundFeatureProfile profile);
            catalog.TryGetIndex(village.Id, out int villageIndex);
            WorldFeatureCandidate parent = new WorldFeatureCandidate(
                villageIndex,
                x: 100d,
                y: 200d,
                rotationDegrees: 90d,
                score: 1d,
                deterministicKey: 123456UL);
            WorldCompoundElementInstanceData[] first = new WorldCompoundElementInstanceData[3];
            WorldCompoundElementInstanceData[] second = new WorldCompoundElementInstanceData[3];

            int firstCount = WorldCompoundFeatureLayoutBuilder.Build(in parent, catalog, profile, first.AsSpan());
            int secondCount = WorldCompoundFeatureLayoutBuilder.Build(in parent, catalog, profile, second.AsSpan());

            Assert.That(firstCount, Is.EqualTo(3));
            Assert.That(secondCount, Is.EqualTo(3));
            Assert.That(first[0].X, Is.EqualTo(100d).Within(0.0000001d));
            Assert.That(first[0].Y, Is.EqualTo(210d).Within(0.0000001d));
            Assert.That(first[0].RotationDegrees, Is.EqualTo(90d));
            Assert.That(first[1].X, Is.EqualTo(95d).Within(0.0000001d));
            Assert.That(first[1].Y, Is.EqualTo(200d).Within(0.0000001d));
            Assert.That(first[1].RotationDegrees, Is.EqualTo(180d));
            Assert.That(first[2].X, Is.EqualTo(100d).Within(0.0000001d));
            Assert.That(first[2].Y, Is.EqualTo(190d).Within(0.0000001d));
            Assert.That(first[2].RotationDegrees, Is.EqualTo(270d));
            for (int i = 0; i < firstCount; i++)
            {
                Assert.That(first[i].SlotId, Is.EqualTo(second[i].SlotId));
                Assert.That(first[i].ElementTypeId, Is.EqualTo(second[i].ElementTypeId));
                Assert.That(first[i].X, Is.EqualTo(second[i].X));
                Assert.That(first[i].Y, Is.EqualTo(second[i].Y));
                Assert.That(first[i].RotationDegrees, Is.EqualTo(second[i].RotationDegrees));
                Assert.That(first[i].ParentDeterministicKey, Is.EqualTo(123456UL));
                Assert.That(first[i].ElementIndex, Is.EqualTo(i));
            }
        }

        [Test]
        public void DestinationCapacityIsValidatedBeforeAnyMemberWrite()
        {
            CreateVillageFixture(out WorldFeatureDefinition village, out WorldFeatureCatalog catalog, out WorldCompiledCompoundFeatureProfile profile);
            catalog.TryGetIndex(village.Id, out int villageIndex);
            WorldFeatureCandidate parent = new WorldFeatureCandidate(villageIndex, 0d, 0d, 0d, 1d, 1UL);
            WorldCompoundElementInstanceData[] output = new WorldCompoundElementInstanceData[2];

            Assert.That(
                () => WorldCompoundFeatureLayoutBuilder.Build(in parent, catalog, profile, output.AsSpan()),
                Throws.TypeOf<ArgumentException>());
            Assert.That(output[0].SlotId.IsValid, Is.False);
            Assert.That(output[1].SlotId.IsValid, Is.False);
        }

        [Test]
        public void UnboundCompoundFeatureReturnsZeroWithoutOutputMutation()
        {
            WorldFeatureCategoryId category = WorldFeatureCategoryId.From("feature_category.settlement");
            WorldFeatureDefinition village = new WorldFeatureDefinition(
                WorldFeatureId.From("feature.village"),
                category,
                WorldFeatureKind.Compound,
                WorldFeatureFootprint.Rectangle(60d, 60d),
                WorldFeatureQuota.Unlimited());
            WorldFeatureCatalog catalog = new WorldFeatureCatalog(new[] { village }.AsSpan());
            WorldCompiledCompoundFeatureProfile profile = new WorldCompoundFeatureProfile(
                ReadOnlySpan<WorldCompoundFeatureBinding>.Empty).Compile(catalog);
            WorldFeatureCandidate parent = new WorldFeatureCandidate(0, 0d, 0d, 0d, 1d, 1UL);
            WorldCompoundElementInstanceData[] output = new WorldCompoundElementInstanceData[1];

            int count = WorldCompoundFeatureLayoutBuilder.Build(in parent, catalog, profile, output.AsSpan());

            Assert.That(count, Is.EqualTo(0));
            Assert.That(output[0].SlotId.IsValid, Is.False);
        }

        private static void CreateVillageFixture(
            out WorldFeatureDefinition village,
            out WorldFeatureCatalog catalog,
            out WorldCompiledCompoundFeatureProfile profile)
        {
            village = new WorldFeatureDefinition(
                WorldFeatureId.From("feature.village"),
                WorldFeatureCategoryId.From("feature_category.settlement"),
                WorldFeatureKind.Compound,
                WorldFeatureFootprint.Rectangle(60d, 60d),
                new WorldFeatureQuota(maxPerWorld: -1, maxPerRegion: 2),
                priority: 50);
            catalog = new WorldFeatureCatalog(new[] { village }.AsSpan());
            profile = new WorldCompoundFeatureProfile(new[]
            {
                new WorldCompoundFeatureBinding(village.Id, CreateVillageTemplate())
            }.AsSpan()).Compile(catalog);
        }

        private static WorldCompoundFeatureTemplate CreateVillageTemplate()
        {
            WorldCompoundElementTypeId house = WorldCompoundElementTypeId.From("compound.element.house");
            WorldCompoundElementTypeId well = WorldCompoundElementTypeId.From("compound.element.well");
            return new WorldCompoundFeatureTemplate(
                WorldCompoundTemplateId.From("compound.template.village.small"),
                new[]
                {
                    new WorldCompoundFeatureElement(WorldCompoundSlotId.From("compound.slot.house_a"), house, 10d, 0d, 0d),
                    new WorldCompoundFeatureElement(WorldCompoundSlotId.From("compound.slot.well"), well, 0d, 5d, 90d),
                    new WorldCompoundFeatureElement(WorldCompoundSlotId.From("compound.slot.house_b"), house, -10d, 0d, 180d)
                }.AsSpan());
        }
    }
}
