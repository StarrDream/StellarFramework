using System;
using NUnit.Framework;
using StellarFramework.WorldGenKit.Resources;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class WorldGenKitResourceExposureTests
    {
        [Test]
        public void ExposureProfileAcceptsAllowedGlobalCategoryAndResourceOverrides()
        {
            WorldResourceCategoryId vegetation = WorldResourceCategoryId.From("resource_category.vegetation");
            WorldResourceId tree = WorldResourceId.From("resource.tree");
            WorldResourceGenerationExposure exposure = Exposure(
                occurrenceMin: 0.5d,
                occurrenceMax: 2d,
                clusterMin: 0.5d,
                clusterMax: 3d,
                richnessMin: 0.25d,
                richnessMax: 4d);
            WorldResourceGenerationExposureProfile profile = new WorldResourceGenerationExposureProfile(
                exposure,
                new[] { new WorldResourceCategoryExposureEntry(vegetation, exposure) }.AsSpan(),
                new[] { new WorldResourceExposureEntry(tree, exposure) }.AsSpan());
            WorldResourceGenerationSettings settings = new WorldResourceGenerationSettings(
                new WorldResourceGenerationMultiplier(1.25d, 1d, 1d),
                new[]
                {
                    new WorldResourceCategoryModifierEntry(
                        vegetation,
                        new WorldResourceGenerationMultiplier(1d, 2d, 1d))
                }.AsSpan(),
                new[]
                {
                    new WorldResourceModifierEntry(
                        tree,
                        new WorldResourceGenerationMultiplier(1.5d, 1d, 2d))
                }.AsSpan());

            Assert.That(profile.Validate(settings, out WorldResourceExposureValidationError error, out int entryIndex), Is.True);
            Assert.That(error, Is.EqualTo(WorldResourceExposureValidationError.None));
            Assert.That(entryIndex, Is.EqualTo(-1));
        }

        [Test]
        public void UnexposedResourceOverrideIsRejected()
        {
            WorldResourceId tree = WorldResourceId.From("resource.tree");
            WorldResourceGenerationExposureProfile profile = new WorldResourceGenerationExposureProfile(
                LockedExposure());
            WorldResourceGenerationSettings settings = new WorldResourceGenerationSettings(
                WorldResourceGenerationMultiplier.Identity,
                resourceModifiers: new[]
                {
                    new WorldResourceModifierEntry(
                        tree,
                        new WorldResourceGenerationMultiplier(2d, 1d, 1d))
                }.AsSpan());

            Assert.That(profile.Validate(settings, out WorldResourceExposureValidationError error, out int entryIndex), Is.False);
            Assert.That(error, Is.EqualTo(WorldResourceExposureValidationError.ResourceNotExposed));
            Assert.That(entryIndex, Is.EqualTo(0));
        }

        [Test]
        public void OutOfRangeExposedMultiplierIsRejected()
        {
            WorldResourceId tree = WorldResourceId.From("resource.tree");
            WorldResourceGenerationExposure treeExposure = new WorldResourceGenerationExposure(
                new WorldResourceMultiplierExposure(true, 0.5d, 2d, 1d),
                new WorldResourceMultiplierExposure(false),
                new WorldResourceMultiplierExposure(false));
            WorldResourceGenerationExposureProfile profile = new WorldResourceGenerationExposureProfile(
                LockedExposure(),
                resourceExposure: new[] { new WorldResourceExposureEntry(tree, treeExposure) }.AsSpan());
            WorldResourceGenerationSettings settings = new WorldResourceGenerationSettings(
                WorldResourceGenerationMultiplier.Identity,
                resourceModifiers: new[]
                {
                    new WorldResourceModifierEntry(
                        tree,
                        new WorldResourceGenerationMultiplier(3d, 1d, 1d))
                }.AsSpan());

            Assert.That(profile.Validate(settings, out WorldResourceExposureValidationError error, out int entryIndex), Is.False);
            Assert.That(error, Is.EqualTo(WorldResourceExposureValidationError.ResourceMultiplierNotAllowed));
            Assert.That(entryIndex, Is.EqualTo(0));
        }

        [Test]
        public void LockedParameterStillAllowsIdentityWhileOtherParameterIsExposed()
        {
            WorldResourceId tree = WorldResourceId.From("resource.tree");
            WorldResourceGenerationExposure treeExposure = new WorldResourceGenerationExposure(
                new WorldResourceMultiplierExposure(true, 0.5d, 2d, 1d),
                new WorldResourceMultiplierExposure(false),
                new WorldResourceMultiplierExposure(false));
            WorldResourceGenerationExposureProfile profile = new WorldResourceGenerationExposureProfile(
                LockedExposure(),
                resourceExposure: new[] { new WorldResourceExposureEntry(tree, treeExposure) }.AsSpan());
            WorldResourceGenerationSettings settings = new WorldResourceGenerationSettings(
                WorldResourceGenerationMultiplier.Identity,
                resourceModifiers: new[]
                {
                    new WorldResourceModifierEntry(
                        tree,
                        new WorldResourceGenerationMultiplier(1.5d, 1d, 1d))
                }.AsSpan());

            Assert.That(profile.Validate(settings, out _, out _), Is.True);
        }

        [Test]
        public void ExposureRangeRejectsDefaultOutsideRangeAtConstruction()
        {
            Assert.That(
                () => new WorldResourceMultiplierExposure(true, 0.5d, 2d, 3d),
                Throws.TypeOf<System.ArgumentOutOfRangeException>());
        }

        private static WorldResourceGenerationExposure LockedExposure() =>
            new WorldResourceGenerationExposure(
                new WorldResourceMultiplierExposure(false),
                new WorldResourceMultiplierExposure(false),
                new WorldResourceMultiplierExposure(false));

        private static WorldResourceGenerationExposure Exposure(
            double occurrenceMin,
            double occurrenceMax,
            double clusterMin,
            double clusterMax,
            double richnessMin,
            double richnessMax) =>
            new WorldResourceGenerationExposure(
                new WorldResourceMultiplierExposure(true, occurrenceMin, occurrenceMax, 1d),
                new WorldResourceMultiplierExposure(true, clusterMin, clusterMax, 1d),
                new WorldResourceMultiplierExposure(true, richnessMin, richnessMax, 1d));
    }
}
