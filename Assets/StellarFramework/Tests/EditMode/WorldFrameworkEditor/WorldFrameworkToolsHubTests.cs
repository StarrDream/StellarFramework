using System;
using NUnit.Framework;
using StellarFramework.PlacementKit;
using StellarFramework.WorldGenKit;
using StellarFramework.WorldGenKit.Builtins;
using StellarFramework.WorldGenKit.Feature;
using StellarFramework.WorldGenKit.Resources;
using StellarFramework.WorldKit;
using UnityEngine;

namespace StellarFramework.Editor.Modules.WorldFramework.Tests
{
    public sealed class WorldFrameworkToolsHubTests
    {
        [SetUp]
        public void SetUp() => WorldFrameworkDiagnosticsRegistry.Clear();

        [TearDown]
        public void TearDown() => WorldFrameworkDiagnosticsRegistry.Clear();

        [Test]
        public void DiagnosticsRegistryRequiresExplicitUniqueSources()
        {
            TestSource source = new TestSource("Primary");
            WorldFrameworkDiagnosticsRegistry.Register(source);

            Assert.That(WorldFrameworkDiagnosticsRegistry.Count, Is.EqualTo(1));
            Assert.That(WorldFrameworkDiagnosticsRegistry.GetAt(0), Is.SameAs(source));
            Assert.Throws<InvalidOperationException>(() => WorldFrameworkDiagnosticsRegistry.Register(source));
            Assert.That(WorldFrameworkDiagnosticsRegistry.Unregister(source), Is.True);
            Assert.That(WorldFrameworkDiagnosticsRegistry.Count, Is.EqualTo(0));
        }

        [Test]
        public void RuntimeSnapshotRejectsInconsistentLifecycleCounts()
        {
            Assert.Throws<ArgumentException>(() => new WorldFrameworkRuntimeSnapshot(
                WorldId.From("tools.world"),
                WorldExtent.Infinite,
                2,
                1,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0L));
        }

        [Test]
        public void InspectorReadsCompiledChannelsAndStagesWithoutReflection()
        {
            WorldGenerationPipelineBuilder builder = new WorldGenerationPipelineBuilder();
            ChannelHandle<float> height = builder.Channels.Register<float>(
                WorldDataChannelId.From("terrain.height"),
                new WorldChannelStorageDescriptor(WorldChannelStorageKind.Dense, WorldChannelScope.Sample));
            builder.AddStage(new TestStage(height));
            WorldGenerationCompileResult compile = builder.Compile();
            Assert.That(compile.Success, Is.True);

            WorldGenerationChannelInspection[] channels = new WorldGenerationChannelInspection[1];
            WorldGenerationStageInspection[] stages = new WorldGenerationStageInspection[1];
            Assert.That(WorldGenerationInspectorModel.WriteChannels(compile.Plan, channels), Is.EqualTo(1));
            Assert.That(WorldGenerationInspectorModel.WriteStages(compile.Plan, stages), Is.EqualTo(1));
            Assert.That(channels[0].Id.Value, Is.EqualTo("terrain.height"));
            Assert.That(stages[0].StageId.Value, Is.EqualTo("tools.test_stage"));
            Assert.That(stages[0].ProducedCount, Is.EqualTo(1));
        }

        [Test]
        public void InspectorPreflightsDestinationBeforeWriting()
        {
            WorldGenerationPipelineBuilder builder = new WorldGenerationPipelineBuilder();
            builder.Channels.Register<float>(
                WorldDataChannelId.From("terrain.height"),
                new WorldChannelStorageDescriptor(WorldChannelStorageKind.Dense, WorldChannelScope.Sample),
                WorldChannelSourceMode.ProvidedInput);
            WorldGenerationCompileResult compile = builder.Compile();
            Assert.That(compile.Success, Is.True);

            Assert.Throws<ArgumentException>(() =>
                WorldGenerationInspectorModel.WriteChannels(
                    compile.Plan,
                    Array.Empty<WorldGenerationChannelInspection>()));
        }

        [Test]
        public void DefaultAuthoringProfileCompilesToDeterministicTerrainPlan()
        {
            WorldGenerationAuthoringProfile profile =
                ScriptableObject.CreateInstance<WorldGenerationAuthoringProfile>();
            try
            {
                profile.ResetToDefaults();
                WorldGenerationAuthoringCompileResult first =
                    WorldGenerationAuthoringCompiler.Compile(profile);
                WorldGenerationAuthoringCompileResult second =
                    WorldGenerationAuthoringCompiler.Compile(profile);

                Assert.That(first.Success, Is.True, string.Join("\n", first.Messages));
                Assert.That(second.Success, Is.True, string.Join("\n", second.Messages));
                Assert.That(first.Plan.PlanHash, Is.EqualTo(second.Plan.PlanHash));
                Assert.That(first.Plan.Channels.Count, Is.EqualTo(7));
                Assert.That(first.Plan.StageCount, Is.EqualTo(7));
                Assert.That(first.Layout, Is.EqualTo(new WorldPlanarSampleLayout(128, 128, 1L)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void AuthoringCompilerRejectsDuplicateChannelBeforePipelineCompile()
        {
            WorldGenerationAuthoringProfile profile =
                ScriptableObject.CreateInstance<WorldGenerationAuthoringProfile>();
            try
            {
                profile.ResetToDefaults();
                profile.Channels.Add(new WorldAuthoringChannelDefinition(
                    "terrain.height",
                    WorldAuthoringChannelValueType.Float));

                WorldGenerationAuthoringCompileResult result =
                    WorldGenerationAuthoringCompiler.Compile(profile);

                Assert.That(result.Success, Is.False);
                Assert.That(result.Plan, Is.Null);
                Assert.That(string.Join("\n", result.Messages), Does.Contain("duplicates ID 'terrain.height'"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void AuthoringCompilerRejectsWrongTypedBuiltinChannel()
        {
            WorldGenerationAuthoringProfile profile =
                ScriptableObject.CreateInstance<WorldGenerationAuthoringProfile>();
            try
            {
                profile.ResetToDefaults();
                profile.Channels[0].ValueType = WorldAuthoringChannelValueType.Int;

                WorldGenerationAuthoringCompileResult result =
                    WorldGenerationAuthoringCompiler.Compile(profile);

                Assert.That(result.Success, Is.False);
                Assert.That(string.Join("\n", result.Messages), Does.Contain("requires float Channel 'terrain.height'"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void AuthoringCompilerSurfacesCoreMissingProducerDiagnostics()
        {
            WorldGenerationAuthoringProfile profile =
                ScriptableObject.CreateInstance<WorldGenerationAuthoringProfile>();
            try
            {
                profile.ResetToDefaults();
                profile.Terrain.HeightEnabled = false;

                WorldGenerationAuthoringCompileResult result =
                    WorldGenerationAuthoringCompiler.Compile(profile);

                Assert.That(result.Success, Is.False);
                Assert.That(string.Join("\n", result.Messages), Does.Contain("missing_producer").IgnoreCase);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void BiomeAuthoringRejectsConditionalFallbackBiome()
        {
            WorldGenerationAuthoringProfile profile =
                ScriptableObject.CreateInstance<WorldGenerationAuthoringProfile>();
            try
            {
                profile.ResetToDefaults();
                profile.BiomeSurface.Biomes[0].Height.Enabled = true;
                profile.BiomeSurface.Biomes[0].Height.Min = -1f;
                profile.BiomeSurface.Biomes[0].Height.Max = 1f;

                WorldGenerationAuthoringCompileResult result =
                    WorldGenerationAuthoringCompiler.Compile(profile);

                Assert.That(result.Success, Is.False);
                Assert.That(
                    string.Join("\n", result.Messages),
                    Does.Contain("Fallback biome criteria must be unconditional"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void BiomeAuthoringRejectsMissingSurfaceMapping()
        {
            WorldGenerationAuthoringProfile profile =
                ScriptableObject.CreateInstance<WorldGenerationAuthoringProfile>();
            try
            {
                profile.ResetToDefaults();
                profile.BiomeSurface.Biomes[0].SurfaceId = "surface.missing";

                WorldGenerationAuthoringCompileResult result =
                    WorldGenerationAuthoringCompiler.Compile(profile);

                Assert.That(result.Success, Is.False);
                Assert.That(
                    string.Join("\n", result.Messages),
                    Does.Contain("references missing Surface 'surface.missing'"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void BuildableAuthoringRejectsUnknownBlockedBiome()
        {
            WorldGenerationAuthoringProfile profile =
                ScriptableObject.CreateInstance<WorldGenerationAuthoringProfile>();
            try
            {
                profile.ResetToDefaults();
                profile.Buildable.BlockedBiomeIds.Add("biome.unknown");

                WorldGenerationAuthoringCompileResult result =
                    WorldGenerationAuthoringCompiler.Compile(profile);

                Assert.That(result.Success, Is.False);
                Assert.That(
                    string.Join("\n", result.Messages),
                    Does.Contain("Blocked biome is missing from catalog"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void DefaultSemanticAuthoringCompilesRealResourceAndFeatureCatalogs()
        {
            WorldGenerationAuthoringProfile profile =
                ScriptableObject.CreateInstance<WorldGenerationAuthoringProfile>();
            try
            {
                profile.ResetToDefaults();

                WorldSemanticAuthoringCompileResult result =
                    WorldSemanticAuthoringCompiler.Compile(profile);

                Assert.That(result.Success, Is.True, string.Join("\n", result.Messages));
                Assert.That(result.OccupancyRegistry.Count, Is.EqualTo(3));
                Assert.That(result.ResourceCatalog.Count, Is.EqualTo(1));
                Assert.That(result.FeatureCatalog.Count, Is.EqualTo(1));
                Assert.That(
                    result.ResourceCatalog.GetDefinition(0).Id.Value,
                    Is.EqualTo("resource.tree"));
                Assert.That(
                    result.FeatureCatalog.GetDefinition(0).Id.Value,
                    Is.EqualTo("feature.landmark"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void SemanticAuthoringRejectsDuplicateOccupancyAndInvalidDistribution()
        {
            WorldGenerationAuthoringProfile profile =
                ScriptableObject.CreateInstance<WorldGenerationAuthoringProfile>();
            try
            {
                profile.ResetToDefaults();
                profile.Resources.OccupancyTypeIds.Add("occupancy.vegetation");

                WorldSemanticAuthoringCompileResult duplicate =
                    WorldSemanticAuthoringCompiler.Compile(profile);
                Assert.That(duplicate.Success, Is.False);
                Assert.That(
                    string.Join("\n", duplicate.Messages),
                    Does.Contain("Duplicate occupancy ID"));

                profile.ResetToDefaults();
                profile.Resources.Resources[0].Occurrence = 2f;
                WorldSemanticAuthoringCompileResult invalidDistribution =
                    WorldSemanticAuthoringCompiler.Compile(profile);
                Assert.That(invalidDistribution.Success, Is.False);
                Assert.That(
                    string.Join("\n", invalidDistribution.Messages),
                    Does.Contain("Occurrence must be in [0,1]"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void SemanticAuthoringRejectsDuplicateFeatureIds()
        {
            WorldGenerationAuthoringProfile profile =
                ScriptableObject.CreateInstance<WorldGenerationAuthoringProfile>();
            try
            {
                profile.ResetToDefaults();
                profile.Features.Features.Add(new WorldFeatureAuthoringDefinition
                {
                    Id = "feature.landmark",
                    CategoryId = "feature.other"
                });

                WorldSemanticAuthoringCompileResult result =
                    WorldSemanticAuthoringCompiler.Compile(profile);

                Assert.That(result.Success, Is.False);
                Assert.That(
                    string.Join("\n", result.Messages),
                    Does.Contain("Duplicate feature ID"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void ResourcePreviewReportsAcceptedAndOccupancyRejectedCandidates()
        {
            WorldGenerationAuthoringProfile profile =
                ScriptableObject.CreateInstance<WorldGenerationAuthoringProfile>();
            try
            {
                profile.ResetToDefaults();
                WorldSemanticAuthoringCompileResult semantic =
                    WorldSemanticAuthoringCompiler.Compile(profile);
                Assert.That(semantic.Success, Is.True, string.Join("\n", semantic.Messages));

                WorldSpawnCandidate[] candidates =
                {
                    new WorldSpawnCandidate(0, 0, 0L, 0L, 2d, 1UL, 1d),
                    new WorldSpawnCandidate(0, 0, 0L, 0L, 1d, 2UL, 1d)
                };
                WorldResourcePreviewResult preview =
                    WorldSemanticPreviewModel.ResolveResources(
                        candidates,
                        semantic.ResourceCatalog,
                        1);

                Assert.That(preview.Accepted, Is.EqualTo(1));
                Assert.That(preview.RejectedOccupancy, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void FeaturePreviewReportsReservationRejection()
        {
            WorldGenerationAuthoringProfile profile =
                ScriptableObject.CreateInstance<WorldGenerationAuthoringProfile>();
            try
            {
                profile.ResetToDefaults();
                WorldSemanticAuthoringCompileResult semantic =
                    WorldSemanticAuthoringCompiler.Compile(profile);
                Assert.That(semantic.Success, Is.True, string.Join("\n", semantic.Messages));

                WorldFeatureCandidate[] candidates =
                {
                    new WorldFeatureCandidate(0, 0d, 0d, 0d, 2d, 1UL),
                    new WorldFeatureCandidate(0, 0d, 0d, 0d, 1d, 2UL)
                };
                WorldFeaturePreviewResult preview =
                    WorldSemanticPreviewModel.ResolveFeatures(
                        candidates,
                        semantic.FeatureCatalog);

                Assert.That(preview.Accepted, Is.EqualTo(1));
                Assert.That(preview.RejectedQuota, Is.EqualTo(0));
                Assert.That(preview.RejectedReservation, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void PlacementProbeUsesRealRulesAndReturnsFailureIds()
        {
            WorldPlacementProbeSettings settings = new WorldPlacementProbeSettings
            {
                SiteMaxSlopeDegrees = 60d,
                MaxAllowedSlopeDegrees = 45d,
                SiteMinWaterDepth = 0d,
                SiteMaxWaterDepth = 0d,
                MinAllowedWaterDepth = 0d,
                MaxAllowedWaterDepth = 0d
            };

            WorldPlacementProbeResult result =
                WorldSemanticAuthoringCompiler.EvaluatePlacement(settings);

            Assert.That(result.Evaluation.Allowed, Is.False);
            Assert.That(result.Evaluation.FailureCount, Is.EqualTo(1));
            Assert.That(result.Failures.Count, Is.EqualTo(1));
            Assert.That(
                result.Failures[0].FailureId,
                Is.EqualTo(PlacementBuiltInFailureIds.Slope));
        }

        [Test]
        public void ConsolidatedValidatorPassesDefaultProfileAndReportsKnownMemory()
        {
            WorldGenerationAuthoringProfile profile =
                ScriptableObject.CreateInstance<WorldGenerationAuthoringProfile>();
            try
            {
                profile.ResetToDefaults();

                WorldAuthoringValidationReport report =
                    WorldAuthoringDiagnosticsModel.Validate(profile);

                Assert.That(report.Success, Is.True);
                Assert.That(report.ErrorCount, Is.EqualTo(0));
                Assert.That(report.Memory.SampleCount, Is.EqualTo(128L * 128L));
                Assert.That(report.Memory.DenseChannelCount, Is.EqualTo(7));
                Assert.That(report.Memory.ConstantChannelCount, Is.EqualTo(0));
                Assert.That(report.Memory.VariableStorageChannelCount, Is.EqualTo(0));
                Assert.That(report.Memory.KnownFixedChannelBytes, Is.EqualTo(409600L));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void ValidatorWarnsAboutUnusedChannelAndIncludesItsDenseMemory()
        {
            WorldGenerationAuthoringProfile profile =
                ScriptableObject.CreateInstance<WorldGenerationAuthoringProfile>();
            try
            {
                profile.ResetToDefaults();
                profile.Channels.Add(new WorldAuthoringChannelDefinition(
                    "game.magic_density",
                    WorldAuthoringChannelValueType.Float));

                WorldAuthoringValidationReport report =
                    WorldAuthoringDiagnosticsModel.Validate(profile);

                Assert.That(report.Success, Is.True);
                Assert.That(report.WarningCount, Is.EqualTo(1));
                Assert.That(report.Memory.KnownFixedChannelBytes, Is.EqualTo(475136L));
                Assert.That(report.Issues.Count, Is.EqualTo(1));
                Assert.That(report.Issues[0].Code, Is.EqualTo("channel.unused"));
                Assert.That(report.Issues[0].Message, Does.Contain("game.magic_density"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void ResourceHeatmapIsDeterministicForSameSeedAndProfile()
        {
            WorldGenerationAuthoringProfile profile =
                ScriptableObject.CreateInstance<WorldGenerationAuthoringProfile>();
            try
            {
                profile.ResetToDefaults();
                WorldSemanticAuthoringCompileResult semantics =
                    WorldSemanticAuthoringCompiler.Compile(profile);
                Assert.That(semantics.Success, Is.True, string.Join("\n", semantics.Messages));

                WorldCandidateHeatmapSnapshot first =
                    WorldAuthoringDiagnosticsModel.BuildResourceHeatmap(
                        profile, semantics, 0, 32, 32, 12345UL);
                WorldCandidateHeatmapSnapshot second =
                    WorldAuthoringDiagnosticsModel.BuildResourceHeatmap(
                        profile, semantics, 0, 32, 32, 12345UL);

                Assert.That(first.GeneratedCandidates, Is.EqualTo(second.GeneratedCandidates));
                Assert.That(first.AcceptedCandidates, Is.EqualTo(second.AcceptedCandidates));
                Assert.That(first.RejectedOccupancy, Is.EqualTo(second.RejectedOccupancy));
                Assert.That(first.RejectedSpacing, Is.EqualTo(second.RejectedSpacing));
                for (int y = 0; y < first.Height; y++)
                {
                    for (int x = 0; x < first.Width; x++)
                    {
                        Assert.That(
                            first.GetCandidateCount(x, y),
                            Is.EqualTo(second.GetCandidateCount(x, y)));
                        Assert.That(
                            first.GetAcceptedCount(x, y),
                            Is.EqualTo(second.GetAcceptedCount(x, y)));
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void ResourceHeatmapReportsRealMinSpacingRejections()
        {
            WorldGenerationAuthoringProfile profile =
                ScriptableObject.CreateInstance<WorldGenerationAuthoringProfile>();
            try
            {
                profile.ResetToDefaults();
                profile.Resources.Resources[0].Occurrence = 1f;
                profile.Resources.Resources[0].MinSpacing = 2f;
                WorldSemanticAuthoringCompileResult semantics =
                    WorldSemanticAuthoringCompiler.Compile(profile);
                Assert.That(semantics.Success, Is.True, string.Join("\n", semantics.Messages));

                WorldCandidateHeatmapSnapshot heatmap =
                    WorldAuthoringDiagnosticsModel.BuildResourceHeatmap(
                        profile, semantics, 0, 16, 16, 1UL);

                Assert.That(heatmap.GeneratedCandidates, Is.EqualTo(256));
                Assert.That(heatmap.AcceptedCandidates, Is.LessThan(256));
                Assert.That(heatmap.RejectedSpacing, Is.GreaterThan(0));
                Assert.That(
                    heatmap.AcceptedCandidates +
                    heatmap.RejectedOccupancy +
                    heatmap.RejectedBudget +
                    heatmap.RejectedSpacing,
                    Is.EqualTo(heatmap.GeneratedCandidates));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void ValidatorRejectsInvalidPlacementProbeConfiguration()
        {
            WorldGenerationAuthoringProfile profile =
                ScriptableObject.CreateInstance<WorldGenerationAuthoringProfile>();
            try
            {
                profile.ResetToDefaults();
                profile.PlacementProbe.MinAllowedWaterDepth = 5d;
                profile.PlacementProbe.MaxAllowedWaterDepth = 1d;

                WorldAuthoringValidationReport report =
                    WorldAuthoringDiagnosticsModel.Validate(profile);

                Assert.That(report.Success, Is.False);
                Assert.That(report.ErrorCount, Is.EqualTo(1));
                Assert.That(
                    report.Issues[0].Code,
                    Is.EqualTo("placement.probe_configuration"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void DetailSnapshotOwnsImmutableCopiesOfChunkLayerAndDeltaRows()
        {
            WorldId worldId = WorldId.From("world.tools");
            WorldChunkDiagnosticSnapshot[] chunks =
            {
                new WorldChunkDiagnosticSnapshot(
                    new WorldChunkCoord(-2, 3),
                    new WorldRegionCoord(-1, 0),
                    WorldChunkState.Active,
                    "Presentation",
                    true,
                    1024L)
            };
            WorldDataLayerDiagnosticSnapshot[] layers =
            {
                new WorldDataLayerDiagnosticSnapshot(
                    WorldDataLayerId.From("world.height"),
                    WorldDataLayerScope.Chunk,
                    "Chunk[-2,3]",
                    "Single[]",
                    2048L)
            };
            WorldDeltaDiagnosticSnapshot[] deltas =
            {
                new WorldDeltaDiagnosticSnapshot(
                    1UL,
                    WorldDeltaTypeId.From("delta.height"),
                    new WorldDeltaVersion(1),
                    WorldDeltaTarget.ForChunk(worldId, new WorldChunkCoord(-2, 3)),
                    64L)
            };

            WorldFrameworkDetailSnapshot snapshot =
                new WorldFrameworkDetailSnapshot(chunks, layers, deltas);
            chunks[0] = default(WorldChunkDiagnosticSnapshot);
            layers[0] = default(WorldDataLayerDiagnosticSnapshot);
            deltas[0] = default(WorldDeltaDiagnosticSnapshot);

            Assert.That(snapshot.ChunkCount, Is.EqualTo(1));
            Assert.That(snapshot.GetChunk(0).Coord, Is.EqualTo(new WorldChunkCoord(-2, 3)));
            Assert.That(snapshot.GetDataLayer(0).LayerId.Value, Is.EqualTo("world.height"));
            Assert.That(snapshot.GetDelta(0).TypeId.Value, Is.EqualTo("delta.height"));
        }

        [Test]
        public void DetailRowsRejectInvalidNegativeMemoryEstimates()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new WorldChunkDiagnosticSnapshot(
                    new WorldChunkCoord(0, 0),
                    new WorldRegionCoord(0, 0),
                    WorldChunkState.Metadata,
                    "Metadata",
                    false,
                    -1L));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new WorldDataLayerDiagnosticSnapshot(
                    WorldDataLayerId.From("world.height"),
                    WorldDataLayerScope.World,
                    "World",
                    "Single[]",
                    -1L));
        }

        private sealed class TestSource : IWorldFrameworkDiagnosticsSource
        {
            public string DisplayName { get; }
            public WorldGenerationPlan GenerationPlan => null;
            public WorldGenerationReport LastGenerationReport => null;

            internal TestSource(string displayName) => DisplayName = displayName;

            public bool TryCaptureRuntimeSnapshot(out WorldFrameworkRuntimeSnapshot snapshot, out string error)
            {
                snapshot = default(WorldFrameworkRuntimeSnapshot);
                error = "No runtime attached in this test source.";
                return false;
            }
        }

        private sealed class TestStage : IWorldGenerationStage
        {
            private readonly ChannelHandle<float> _height;
            public WorldGenerationStageId Id => WorldGenerationStageId.From("tools.test_stage");

            internal TestStage(ChannelHandle<float> height) => _height = height;

            public void Describe(WorldGenerationStageDescriptorBuilder builder)
            {
                builder.SetSeedScope(WorldGenerationSeedScope.Chunk);
                builder.Produce(_height);
            }

            public WorldGenerationStageResult Execute(in WorldGenerationContext context) =>
                WorldGenerationStageResult.Succeeded();
        }
    }
}
