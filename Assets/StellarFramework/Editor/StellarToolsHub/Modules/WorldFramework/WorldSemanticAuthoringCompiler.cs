using System;
using System.Collections.Generic;
using StellarFramework.PlacementKit;
using StellarFramework.WorldGenKit.Feature;
using StellarFramework.WorldGenKit.Resources;

namespace StellarFramework.Editor.Modules.WorldFramework
{
    public sealed class WorldSemanticAuthoringCompileResult
    {
        private readonly string[] _messages;

        public bool Success { get; }
        public WorldOccupancyRegistry OccupancyRegistry { get; }
        public WorldResourceCatalog ResourceCatalog { get; }
        public WorldFeatureCatalog FeatureCatalog { get; }
        public IReadOnlyList<string> Messages => _messages;

        internal WorldSemanticAuthoringCompileResult(
            bool success,
            WorldOccupancyRegistry occupancyRegistry,
            WorldResourceCatalog resourceCatalog,
            WorldFeatureCatalog featureCatalog,
            string[] messages)
        {
            Success = success;
            OccupancyRegistry = occupancyRegistry;
            ResourceCatalog = resourceCatalog;
            FeatureCatalog = featureCatalog;
            _messages = messages ?? Array.Empty<string>();
        }
    }

    public readonly struct WorldPlacementProbeResult
    {
        private readonly PlacementFailureRecord[] _failures;

        public PlacementEvaluationResult Evaluation { get; }
        public IReadOnlyList<PlacementFailureRecord> Failures => _failures;

        internal WorldPlacementProbeResult(
            PlacementEvaluationResult evaluation,
            PlacementFailureRecord[] failures)
        {
            Evaluation = evaluation;
            _failures = failures ?? Array.Empty<PlacementFailureRecord>();
        }
    }

    public static class WorldSemanticAuthoringCompiler
    {
        public static WorldSemanticAuthoringCompileResult Compile(
            WorldGenerationAuthoringProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            List<string> messages = new List<string>();

            WorldOccupancyRegistry occupancy = BuildOccupancyRegistry(profile.Resources, messages);
            WorldResourceCatalog resources = occupancy == null
                ? null
                : BuildResourceCatalog(profile.Resources, occupancy, messages);
            WorldFeatureCatalog features = BuildFeatureCatalog(profile.Features, messages);

            return new WorldSemanticAuthoringCompileResult(
                messages.Count == 0 && occupancy != null && resources != null && features != null,
                occupancy,
                resources,
                features,
                messages.ToArray());
        }

        public static WorldPlacementProbeResult EvaluatePlacement(
            WorldPlacementProbeSettings source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            PlacementTypeId typeId = PlacementTypeId.From(source.TypeId);
            PlacementFootprint footprint = source.FootprintKind == PlacementFootprintKind.Circle
                ? PlacementFootprint.Circle(source.SizeX)
                : PlacementFootprint.Rectangle(source.SizeX, source.SizeY);
            PlacementRequest request = new PlacementRequest(
                typeId,
                source.X,
                source.Y,
                source.RotationDegrees,
                footprint);
            PlacementSiteFacts facts = new PlacementSiteFacts(
                source.SiteMaxSlopeDegrees,
                source.SiteMinWaterDepth,
                source.SiteMaxWaterDepth,
                source.SiteZoneMask,
                source.SiteConflictMask,
                source.SiteConnectionMask,
                source.BaseSuitability);

            List<IPlacementRule<PlacementSiteFacts>> rules =
                new List<IPlacementRule<PlacementSiteFacts>>(6);
            if (source.UseSlopeRule)
                rules.Add(new PlacementSlopeRule(source.MaxAllowedSlopeDegrees));
            if (source.UseWaterRule)
                rules.Add(new PlacementWaterDepthRule(
                    source.MinAllowedWaterDepth,
                    source.MaxAllowedWaterDepth));
            if (source.RequiredZoneMask != 0UL)
                rules.Add(new PlacementRequiredZoneRule(
                    source.RequiredZoneMask,
                    source.RequireAllZones));
            if (source.ForbiddenConflictMask != 0UL)
                rules.Add(new PlacementConflictRule(source.ForbiddenConflictMask));
            if (source.RequiredConnectionMask != 0UL)
                rules.Add(new PlacementConnectionRule(
                    source.RequiredConnectionMask,
                    source.RequireAllConnections));
            if (source.UseBaseSuitability)
                rules.Add(new PlacementBaseSuitabilityRule());

            IPlacementRule<PlacementSiteFacts>[] ruleArray = rules.ToArray();
            PlacementFailureRecord[] failureBuffer =
                new PlacementFailureRecord[ruleArray.Length];
            PlacementEvaluationResult result = PlacementEvaluator.Evaluate(
                in request,
                in facts,
                ruleArray,
                failureBuffer,
                true);

            PlacementFailureRecord[] failures =
                new PlacementFailureRecord[result.FailureCount];
            Array.Copy(failureBuffer, failures, failures.Length);
            return new WorldPlacementProbeResult(result, failures);
        }

        private static WorldOccupancyRegistry BuildOccupancyRegistry(
            WorldResourceAuthoringSettings source,
            List<string> messages)
        {
            if (source == null)
            {
                messages.Add("Resource settings are missing.");
                return null;
            }
            if (source.OccupancyTypeIds == null || source.OccupancyTypeIds.Count == 0)
            {
                messages.Add("At least one occupancy type is required.");
                return null;
            }

            WorldOccupancyRegistryBuilder builder = new WorldOccupancyRegistryBuilder();
            for (int i = 0; i < source.OccupancyTypeIds.Count; i++)
            {
                try
                {
                    builder.Register(WorldOccupancyTypeId.From(source.OccupancyTypeIds[i]));
                }
                catch (Exception exception) when (
                    exception is ArgumentException ||
                    exception is InvalidOperationException)
                {
                    messages.Add($"Occupancy[{i}]: {exception.Message}");
                }
            }
            return messages.Count == 0 ? builder.Build() : null;
        }

        private static WorldResourceCatalog BuildResourceCatalog(
            WorldResourceAuthoringSettings source,
            WorldOccupancyRegistry occupancy,
            List<string> messages)
        {
            if (source.Resources == null || source.Resources.Count == 0)
            {
                messages.Add("At least one Resource definition is required.");
                return null;
            }

            WorldResourceDefinition[] definitions =
                new WorldResourceDefinition[source.Resources.Count];
            for (int i = 0; i < definitions.Length; i++)
            {
                WorldResourceAuthoringDefinition item = source.Resources[i];
                if (item == null)
                {
                    messages.Add($"Resource[{i}] is null.");
                    continue;
                }

                try
                {
                    WorldResourceId id = WorldResourceId.From(item.Id);
                    WorldResourceCategoryId category =
                        WorldResourceCategoryId.From(item.CategoryId);
                    WorldResourceDistributionDefinition distribution =
                        new WorldResourceDistributionDefinition(
                            item.Mode,
                            item.Occurrence,
                            item.ClusterSize,
                            item.Richness,
                            item.MinSpacing);
                    WorldOccupancyMask occupies =
                        CreateOccupancyMask(occupancy, item.Occupies, $"Resource[{i}] Occupies");
                    WorldOccupancyMask excludes =
                        CreateOccupancyMask(occupancy, item.Excludes, $"Resource[{i}] Excludes");
                    definitions[i] = new WorldResourceDefinition(
                        id,
                        category,
                        distribution,
                        occupies,
                        excludes,
                        item.Priority);
                }
                catch (Exception exception) when (
                    exception is ArgumentException ||
                    exception is InvalidOperationException)
                {
                    messages.Add($"Resource[{i}]: {exception.Message}");
                }
            }

            if (messages.Count > 0) return null;
            try
            {
                return new WorldResourceCatalog(definitions);
            }
            catch (ArgumentException exception)
            {
                messages.Add("Resource catalog: " + exception.Message);
                return null;
            }
        }

        private static WorldFeatureCatalog BuildFeatureCatalog(
            WorldFeatureAuthoringSettings source,
            List<string> messages)
        {
            if (source == null || source.Features == null || source.Features.Count == 0)
            {
                messages.Add("At least one Feature definition is required.");
                return null;
            }

            WorldFeatureDefinition[] definitions =
                new WorldFeatureDefinition[source.Features.Count];
            for (int i = 0; i < definitions.Length; i++)
            {
                WorldFeatureAuthoringDefinition item = source.Features[i];
                if (item == null)
                {
                    messages.Add($"Feature[{i}] is null.");
                    continue;
                }

                try
                {
                    WorldFeatureFootprint footprint =
                        item.FootprintKind == WorldFeatureFootprintKind.Circle
                            ? WorldFeatureFootprint.Circle(item.SizeX)
                            : WorldFeatureFootprint.Rectangle(item.SizeX, item.SizeY);
                    WorldFeatureQuota quota =
                        new WorldFeatureQuota(item.MaxPerWorld, item.MaxPerRegion);
                    definitions[i] = new WorldFeatureDefinition(
                        WorldFeatureId.From(item.Id),
                        WorldFeatureCategoryId.From(item.CategoryId),
                        item.Kind,
                        footprint,
                        quota,
                        item.Priority);
                }
                catch (Exception exception) when (
                    exception is ArgumentException ||
                    exception is InvalidOperationException)
                {
                    messages.Add($"Feature[{i}]: {exception.Message}");
                }
            }

            if (messages.Count > 0) return null;
            try
            {
                return new WorldFeatureCatalog(definitions);
            }
            catch (ArgumentException exception)
            {
                messages.Add("Feature catalog: " + exception.Message);
                return null;
            }
        }

        private static WorldOccupancyMask CreateOccupancyMask(
            WorldOccupancyRegistry registry,
            List<string> source,
            string label)
        {
            if (source == null || source.Count == 0)
                return WorldOccupancyMask.None;

            WorldOccupancyTypeId[] ids = new WorldOccupancyTypeId[source.Count];
            for (int i = 0; i < ids.Length; i++)
                ids[i] = WorldOccupancyTypeId.From(source[i]);
            try
            {
                return registry.CreateMask(ids);
            }
            catch (ArgumentException exception)
            {
                throw new ArgumentException(label + ": " + exception.Message, exception);
            }
        }
    }
}
