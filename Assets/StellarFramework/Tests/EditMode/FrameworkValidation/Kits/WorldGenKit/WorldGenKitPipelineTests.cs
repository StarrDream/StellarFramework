using System;
using System.Collections.Generic;
using NUnit.Framework;
using StellarFramework.WorldGenKit;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class WorldGenKitPipelineTests
    {
        [Test]
        public void PipelineCompilesDependencyOrderAndExecutesDeterministically()
        {
            WorldGenerationPipelineBuilder builder = new WorldGenerationPipelineBuilder();
            ChannelHandle<float> height = RegisterDenseFloat(builder, "terrain.height");
            ChannelHandle<float> slope = RegisterDenseFloat(builder, "terrain.slope");
            ChannelHandle<int> biome = builder.Channels.Register<int>(
                WorldDataChannelId.From("terrain.biome"),
                new WorldChannelStorageDescriptor(WorldChannelStorageKind.Dense, WorldChannelScope.Sample));

            List<string> executionOrder = new List<string>();

            TestStage biomeStage = new TestStage(
                "stage.biome",
                descriptor =>
                {
                    descriptor.SetSeedScope(WorldGenerationSeedScope.Chunk);
                    descriptor.Require(slope);
                    descriptor.Produce(biome);
                },
                context =>
                {
                    executionOrder.Add("biome");
                    DenseChannelStorage<float> slopeStore = context.Data.GetStorage<float, DenseChannelStorage<float>>(slope);
                    DenseChannelStorage<int> biomeStore = context.Data.GetStorage<int, DenseChannelStorage<int>>(biome);
                    biomeStore[0] = slopeStore[0] >= 2f ? 7 : 0;
                    return WorldGenerationStageResult.Succeeded();
                });

            TestStage slopeStage = new TestStage(
                "stage.slope",
                descriptor =>
                {
                    descriptor.SetSeedScope(WorldGenerationSeedScope.Chunk);
                    descriptor.Require(height);
                    descriptor.Produce(slope);
                },
                context =>
                {
                    executionOrder.Add("slope");
                    DenseChannelStorage<float> heightStore = context.Data.GetStorage<float, DenseChannelStorage<float>>(height);
                    DenseChannelStorage<float> slopeStore = context.Data.GetStorage<float, DenseChannelStorage<float>>(slope);
                    slopeStore[0] = heightStore[0] * 2f;
                    return WorldGenerationStageResult.Succeeded();
                });

            TestStage heightStage = new TestStage(
                "stage.height",
                descriptor =>
                {
                    descriptor.SetSeedScope(WorldGenerationSeedScope.Chunk);
                    descriptor.Produce(height);
                },
                context =>
                {
                    executionOrder.Add("height");
                    context.Data.GetStorage<float, DenseChannelStorage<float>>(height)[0] = 1.25f;
                    return WorldGenerationStageResult.Succeeded();
                });

            // Deliberately add consumers before producers. Compile order must come from dependencies.
            builder.AddStage(biomeStage);
            builder.AddStage(slopeStage);
            builder.AddStage(heightStage);

            WorldGenerationCompileResult compile = builder.Compile();
            Assert.That(compile.Success, Is.True, JoinDiagnostics(compile.Diagnostics));
            Assert.That(compile.Plan, Is.Not.Null);
            Assert.That(compile.Plan.StageCount, Is.EqualTo(3));
            Assert.That(compile.Plan.GetStageDescriptor(0).StageId.Value, Is.EqualTo("stage.height"));
            Assert.That(compile.Plan.GetStageDescriptor(1).StageId.Value, Is.EqualTo("stage.slope"));
            Assert.That(compile.Plan.GetStageDescriptor(2).StageId.Value, Is.EqualTo("stage.biome"));
            Assert.That(compile.Plan.PlanHash, Is.Not.EqualTo(0UL));

            WorldGenerationDataSet data = new WorldGenerationDataSet(compile.Plan.Channels);
            DenseChannelStorage<float> heightStorage = new DenseChannelStorage<float>(1);
            DenseChannelStorage<float> slopeStorage = new DenseChannelStorage<float>(1);
            DenseChannelStorage<int> biomeStorage = new DenseChannelStorage<int>(1);
            data.Bind(height, heightStorage);
            data.Bind(slope, slopeStorage);
            data.Bind(biome, biomeStorage);

            WorldGenerationStageExecutionRecord[] records = new WorldGenerationStageExecutionRecord[3];
            WorldGenerationRunKey runKey = new WorldGenerationRunKey(-8, 17, 33UL);
            WorldGenerationRunResult run = compile.Plan.Execute(
                data,
                new WorldGenerationSeed(55UL),
                runKey,
                records.AsSpan());

            Assert.That(run.Success, Is.True);
            Assert.That(run.StagesWritten, Is.EqualTo(3));
            Assert.That(executionOrder, Is.EqualTo(new[] { "height", "slope", "biome" }));
            Assert.That(heightStorage[0], Is.EqualTo(1.25f));
            Assert.That(slopeStorage[0], Is.EqualTo(2.5f));
            Assert.That(biomeStorage[0], Is.EqualTo(7));

            WorldGenerationSeed firstSeed = records[0].StageSeed;
            executionOrder.Clear();
            WorldGenerationStageExecutionRecord[] secondRecords = new WorldGenerationStageExecutionRecord[3];
            WorldGenerationRunResult secondRun = compile.Plan.Execute(
                data,
                new WorldGenerationSeed(55UL),
                runKey,
                secondRecords.AsSpan());
            Assert.That(secondRun.Success, Is.True);
            Assert.That(secondRecords[0].StageSeed, Is.EqualTo(firstSeed));
        }

        [Test]
        public void CompileRejectsMissingRequiredProducer()
        {
            WorldGenerationPipelineBuilder builder = new WorldGenerationPipelineBuilder();
            ChannelHandle<float> missing = RegisterDenseFloat(builder, "terrain.missing");
            ChannelHandle<float> output = RegisterDenseFloat(builder, "terrain.output");

            builder.AddStage(new TestStage(
                "stage.consumer",
                descriptor =>
                {
                    descriptor.SetSeedScope(WorldGenerationSeedScope.Chunk);
                    descriptor.Require(missing);
                    descriptor.Produce(output);
                }));

            WorldGenerationCompileResult result = builder.Compile();
            Assert.That(result.Success, Is.False);
            AssertHasDiagnostic(result, WorldGenerationDiagnosticIds.MissingProducer, "terrain.missing");
        }

        [Test]
        public void CompileRejectsDuplicateProducerAndDuplicateMutator()
        {
            WorldGenerationPipelineBuilder producerBuilder = new WorldGenerationPipelineBuilder();
            ChannelHandle<float> produced = RegisterDenseFloat(producerBuilder, "terrain.shared");
            producerBuilder.AddStage(Producer("stage.one", produced));
            producerBuilder.AddStage(Producer("stage.two", produced));

            WorldGenerationCompileResult duplicateProducer = producerBuilder.Compile();
            Assert.That(duplicateProducer.Success, Is.False);
            AssertHasDiagnostic(duplicateProducer, WorldGenerationDiagnosticIds.DuplicateProducer, "terrain.shared");

            WorldGenerationPipelineBuilder mutatorBuilder = new WorldGenerationPipelineBuilder();
            ChannelHandle<float> provided = mutatorBuilder.Channels.Register<float>(
                WorldDataChannelId.From("terrain.imported"),
                new WorldChannelStorageDescriptor(WorldChannelStorageKind.External, WorldChannelScope.Sample),
                WorldChannelSourceMode.ProvidedInput);
            mutatorBuilder.AddStage(Mutator("stage.mutate_a", provided));
            mutatorBuilder.AddStage(Mutator("stage.mutate_b", provided));

            WorldGenerationCompileResult duplicateMutator = mutatorBuilder.Compile();
            Assert.That(duplicateMutator.Success, Is.False);
            AssertHasDiagnostic(duplicateMutator, WorldGenerationDiagnosticIds.DuplicateMutator, "terrain.imported");
        }

        [Test]
        public void CompileRejectsCycleAndProvidedInputProducer()
        {
            WorldGenerationPipelineBuilder cycleBuilder = new WorldGenerationPipelineBuilder();
            ChannelHandle<float> a = RegisterDenseFloat(cycleBuilder, "channel.a");
            ChannelHandle<float> b = RegisterDenseFloat(cycleBuilder, "channel.b");
            cycleBuilder.AddStage(new TestStage(
                "stage.a",
                descriptor =>
                {
                    descriptor.SetSeedScope(WorldGenerationSeedScope.Chunk);
                    descriptor.Require(b);
                    descriptor.Produce(a);
                }));
            cycleBuilder.AddStage(new TestStage(
                "stage.b",
                descriptor =>
                {
                    descriptor.SetSeedScope(WorldGenerationSeedScope.Chunk);
                    descriptor.Require(a);
                    descriptor.Produce(b);
                }));

            WorldGenerationCompileResult cycle = cycleBuilder.Compile();
            Assert.That(cycle.Success, Is.False);
            AssertHasDiagnostic(cycle, WorldGenerationDiagnosticIds.DependencyCycle);

            WorldGenerationPipelineBuilder providedBuilder = new WorldGenerationPipelineBuilder();
            ChannelHandle<float> provided = providedBuilder.Channels.Register<float>(
                WorldDataChannelId.From("terrain.height"),
                new WorldChannelStorageDescriptor(WorldChannelStorageKind.External, WorldChannelScope.Sample),
                WorldChannelSourceMode.ProvidedInput);
            providedBuilder.AddStage(Producer("stage.illegal", provided));

            WorldGenerationCompileResult providedResult = providedBuilder.Compile();
            Assert.That(providedResult.Success, Is.False);
            AssertHasDiagnostic(providedResult, WorldGenerationDiagnosticIds.ProvidedChannelProduced, "terrain.height");
        }

        [Test]
        public void CompileRejectsForeignHandleDuplicateReferenceAndMissingSeedScope()
        {
            WorldGenerationPipelineBuilder owner = new WorldGenerationPipelineBuilder();
            ChannelHandle<float> own = RegisterDenseFloat(owner, "channel.own");
            WorldGenerationPipelineBuilder foreignBuilder = new WorldGenerationPipelineBuilder();
            ChannelHandle<float> foreign = RegisterDenseFloat(foreignBuilder, "channel.foreign");

            owner.AddStage(new TestStage(
                "stage.invalid",
                descriptor =>
                {
                    descriptor.Require(foreign);
                    descriptor.Require(own);
                    descriptor.Optional(own);
                }));

            WorldGenerationCompileResult result = owner.Compile();
            Assert.That(result.Success, Is.False);
            AssertHasDiagnostic(result, WorldGenerationDiagnosticIds.InvalidChannelHandle);
            AssertHasDiagnostic(result, WorldGenerationDiagnosticIds.DuplicateChannelReference, "channel.own");
            AssertHasDiagnostic(result, WorldGenerationDiagnosticIds.MissingSeedScope);
        }

        [Test]
        public void OptionalInputMayRemainUnboundButRequiredProducedAndMutatedMayNot()
        {
            WorldGenerationPipelineBuilder builder = new WorldGenerationPipelineBuilder();
            ChannelHandle<float> optional = RegisterDenseFloat(builder, "terrain.optional");
            ChannelHandle<float> output = RegisterDenseFloat(builder, "terrain.output");

            builder.AddStage(new TestStage(
                "stage.optional_consumer",
                descriptor =>
                {
                    descriptor.SetSeedScope(WorldGenerationSeedScope.Chunk);
                    descriptor.Optional(optional);
                    descriptor.Produce(output);
                },
                context =>
                {
                    bool hasOptional = context.Data.TryGetStorage<float, DenseChannelStorage<float>>(optional, out _);
                    context.Data.GetStorage<float, DenseChannelStorage<float>>(output)[0] = hasOptional ? 2f : 1f;
                    return WorldGenerationStageResult.Succeeded();
                }));

            WorldGenerationCompileResult compile = builder.Compile();
            Assert.That(compile.Success, Is.True, JoinDiagnostics(compile.Diagnostics));

            WorldGenerationDataSet data = new WorldGenerationDataSet(compile.Plan.Channels);
            DenseChannelStorage<float> outputStorage = new DenseChannelStorage<float>(1);
            data.Bind(output, outputStorage);

            WorldGenerationStageExecutionRecord[] records = new WorldGenerationStageExecutionRecord[1];
            WorldGenerationRunResult run = compile.Plan.Execute(
                data,
                new WorldGenerationSeed(1UL),
                new WorldGenerationRunKey(0, 0),
                records.AsSpan());

            Assert.That(run.Success, Is.True);
            Assert.That(outputStorage[0], Is.EqualTo(1f));

            data.Unbind(output);
            WorldGenerationRunResult missingOutput = compile.Plan.Execute(
                data,
                new WorldGenerationSeed(1UL),
                new WorldGenerationRunKey(0, 0),
                records.AsSpan());
            Assert.That(missingOutput.Status, Is.EqualTo(WorldGenerationRunStatus.UnboundChannel));
        }

        [Test]
        public void ExecuteRejectsSmallBufferRegistryMismatchAndStopsOnStageFailure()
        {
            WorldGenerationPipelineBuilder builder = new WorldGenerationPipelineBuilder();
            ChannelHandle<float> a = RegisterDenseFloat(builder, "channel.a");
            ChannelHandle<float> b = RegisterDenseFloat(builder, "channel.b");
            WorldGenerationDiagnosticId failureCode = WorldGenerationDiagnosticId.From("stage.intentional_failure");

            builder.AddStage(new TestStage(
                "stage.fail",
                descriptor =>
                {
                    descriptor.SetSeedScope(WorldGenerationSeedScope.Chunk);
                    descriptor.Produce(a);
                },
                context => WorldGenerationStageResult.Failed(failureCode, "intentional")));
            builder.AddStage(new TestStage(
                "stage.after",
                descriptor =>
                {
                    descriptor.SetSeedScope(WorldGenerationSeedScope.Chunk);
                    descriptor.Require(a);
                    descriptor.Produce(b);
                }));

            WorldGenerationCompileResult compile = builder.Compile();
            Assert.That(compile.Success, Is.True, JoinDiagnostics(compile.Diagnostics));

            WorldGenerationDataSet data = new WorldGenerationDataSet(compile.Plan.Channels);
            data.Bind(a, new DenseChannelStorage<float>(1));
            data.Bind(b, new DenseChannelStorage<float>(1));

            WorldGenerationStageExecutionRecord sentinel = default(WorldGenerationStageExecutionRecord);
            WorldGenerationStageExecutionRecord[] tooSmall = { sentinel };
            WorldGenerationRunResult small = compile.Plan.Execute(
                data,
                new WorldGenerationSeed(1UL),
                default(WorldGenerationRunKey),
                tooSmall.AsSpan());
            Assert.That(small.Status, Is.EqualTo(WorldGenerationRunStatus.BufferTooSmall));
            Assert.That(small.StagesWritten, Is.EqualTo(0));

            WorldChannelRegistryBuilder foreignRegistryBuilder = new WorldChannelRegistryBuilder();
            foreignRegistryBuilder.Register<float>(
                WorldDataChannelId.From("channel.a"),
                new WorldChannelStorageDescriptor(WorldChannelStorageKind.Dense, WorldChannelScope.Sample));
            WorldGenerationDataSet foreignData = new WorldGenerationDataSet(foreignRegistryBuilder.Build());
            WorldGenerationStageExecutionRecord[] records = new WorldGenerationStageExecutionRecord[2];
            WorldGenerationRunResult mismatch = compile.Plan.Execute(
                foreignData,
                new WorldGenerationSeed(1UL),
                default(WorldGenerationRunKey),
                records.AsSpan());
            Assert.That(mismatch.Status, Is.EqualTo(WorldGenerationRunStatus.RegistryMismatch));

            WorldGenerationRunResult failed = compile.Plan.Execute(
                data,
                new WorldGenerationSeed(1UL),
                default(WorldGenerationRunKey),
                records.AsSpan());
            Assert.That(failed.Status, Is.EqualTo(WorldGenerationRunStatus.Failed));
            Assert.That(failed.StagesWritten, Is.EqualTo(1));
            Assert.That(failed.TerminalStageIndex, Is.EqualTo(0));
            Assert.That(failed.Code, Is.EqualTo(failureCode));
            Assert.That(records[0].StageId.Value, Is.EqualTo("stage.fail"));
        }

        [Test]
        public void CustomMagicChannelCanDriveRuleWithoutTemperatureChannel()
        {
            WorldGenerationPipelineBuilder builder = new WorldGenerationPipelineBuilder();
            ChannelHandle<float> magic = RegisterDenseFloat(builder, "game.magic_density");
            ChannelHandle<int> biome = builder.Channels.Register<int>(
                WorldDataChannelId.From("terrain.biome"),
                new WorldChannelStorageDescriptor(WorldChannelStorageKind.Dense, WorldChannelScope.Sample));

            builder.AddStage(new TestStage(
                "stage.magic_source",
                descriptor =>
                {
                    descriptor.SetSeedScope(WorldGenerationSeedScope.Chunk);
                    descriptor.Produce(magic);
                },
                context =>
                {
                    context.Data.GetStorage<float, DenseChannelStorage<float>>(magic)[0] = 0.75f;
                    return WorldGenerationStageResult.Succeeded();
                }));

            builder.AddStage(new TestStage(
                "stage.magic_biome",
                descriptor =>
                {
                    descriptor.SetSeedScope(WorldGenerationSeedScope.Chunk);
                    descriptor.Require(magic);
                    descriptor.Produce(biome);
                },
                context =>
                {
                    float value = context.Data.GetStorage<float, DenseChannelStorage<float>>(magic)[0];
                    WorldThresholdRule magicRule = new WorldThresholdRule(0.5d, WorldThresholdComparison.GreaterOrEqual);
                    context.Data.GetStorage<int, DenseChannelStorage<int>>(biome)[0] = magicRule.Evaluate(value) ? 99 : 0;
                    return WorldGenerationStageResult.Succeeded();
                }));

            WorldGenerationCompileResult compile = builder.Compile();
            Assert.That(compile.Success, Is.True, JoinDiagnostics(compile.Diagnostics));
            Assert.That(compile.Plan.Channels.TryResolve<float>(
                WorldDataChannelId.From("terrain.temperature"),
                out _,
                out WorldChannelResolveError missingTemperature), Is.False);
            Assert.That(missingTemperature, Is.EqualTo(WorldChannelResolveError.NotFound));

            WorldGenerationDataSet data = new WorldGenerationDataSet(compile.Plan.Channels);
            data.Bind(magic, new DenseChannelStorage<float>(1));
            DenseChannelStorage<int> biomeStorage = new DenseChannelStorage<int>(1);
            data.Bind(biome, biomeStorage);

            WorldGenerationStageExecutionRecord[] records = new WorldGenerationStageExecutionRecord[2];
            WorldGenerationRunResult run = compile.Plan.Execute(
                data,
                new WorldGenerationSeed(123UL),
                new WorldGenerationRunKey(4, -9),
                records.AsSpan());

            Assert.That(run.Success, Is.True);
            Assert.That(biomeStorage[0], Is.EqualTo(99));
        }

        [Test]
        public void ReportSnapshotsRunAndEquivalentDependencyPlanHasStableHash()
        {
            WorldGenerationPipelineBuilder first = CreateTwoStagePlan(true, out ChannelHandle<float> firstA, out ChannelHandle<float> firstB);
            WorldGenerationCompileResult firstCompile = first.Compile();
            Assert.That(firstCompile.Success, Is.True, JoinDiagnostics(firstCompile.Diagnostics));

            WorldGenerationPipelineBuilder second = CreateTwoStagePlan(false, out _, out _);
            WorldGenerationCompileResult secondCompile = second.Compile();
            Assert.That(secondCompile.Success, Is.True, JoinDiagnostics(secondCompile.Diagnostics));
            Assert.That(secondCompile.Plan.PlanHash, Is.EqualTo(firstCompile.Plan.PlanHash));

            WorldGenerationDataSet data = new WorldGenerationDataSet(firstCompile.Plan.Channels);
            data.Bind(firstA, new DenseChannelStorage<float>(1));
            data.Bind(firstB, new DenseChannelStorage<float>(1));

            WorldGenerationSeed seed = new WorldGenerationSeed(555UL);
            WorldGenerationRunKey runKey = new WorldGenerationRunKey(-10, 20, 88UL);
            WorldGenerationStageExecutionRecord[] records = new WorldGenerationStageExecutionRecord[2];
            WorldGenerationRunResult run = firstCompile.Plan.Execute(data, seed, runKey, records.AsSpan());
            Assert.That(run.Success, Is.True);

            WorldGenerationReport report = WorldGenerationReport.Capture(
                firstCompile.Plan,
                seed,
                runKey,
                run,
                records.AsSpan());

            Assert.That(report.PlanHash, Is.EqualTo(firstCompile.Plan.PlanHash));
            Assert.That(report.WorldSeed, Is.EqualTo(seed));
            Assert.That(report.RunKey, Is.EqualTo(runKey));
            Assert.That(report.Status, Is.EqualTo(WorldGenerationRunStatus.Succeeded));
            Assert.That(report.StageCount, Is.EqualTo(2));
            Assert.That(report.GetStage(0).StageId.Value, Is.EqualTo("stage.a"));
            Assert.That(report.GetStage(1).StageId.Value, Is.EqualTo("stage.b"));

            WorldGenerationStageExecutionRecord[] copied = new WorldGenerationStageExecutionRecord[2];
            Assert.That(report.WriteStages(copied.AsSpan()), Is.EqualTo(2));
            Assert.That(copied[1].StageId.Value, Is.EqualTo("stage.b"));
        }

        private static ChannelHandle<float> RegisterDenseFloat(WorldGenerationPipelineBuilder builder, string id)
        {
            return builder.Channels.Register<float>(
                WorldDataChannelId.From(id),
                new WorldChannelStorageDescriptor(WorldChannelStorageKind.Dense, WorldChannelScope.Sample));
        }

        private static TestStage Producer(string id, ChannelHandle<float> output)
        {
            return new TestStage(
                id,
                descriptor =>
                {
                    descriptor.SetSeedScope(WorldGenerationSeedScope.Chunk);
                    descriptor.Produce(output);
                });
        }

        private static TestStage Mutator(string id, ChannelHandle<float> channel)
        {
            return new TestStage(
                id,
                descriptor =>
                {
                    descriptor.SetSeedScope(WorldGenerationSeedScope.Chunk);
                    descriptor.Mutate(channel);
                });
        }

        private static WorldGenerationPipelineBuilder CreateTwoStagePlan(
            bool reverseAddOrder,
            out ChannelHandle<float> a,
            out ChannelHandle<float> b)
        {
            WorldGenerationPipelineBuilder builder = new WorldGenerationPipelineBuilder();
            a = RegisterDenseFloat(builder, "channel.a");
            b = RegisterDenseFloat(builder, "channel.b");

            ChannelHandle<float> capturedA = a;
            ChannelHandle<float> capturedB = b;
            TestStage stageA = new TestStage(
                "stage.a",
                descriptor =>
                {
                    descriptor.SetSeedScope(WorldGenerationSeedScope.Chunk);
                    descriptor.Produce(capturedA);
                });
            TestStage stageB = new TestStage(
                "stage.b",
                descriptor =>
                {
                    descriptor.SetSeedScope(WorldGenerationSeedScope.Chunk);
                    descriptor.Require(capturedA);
                    descriptor.Produce(capturedB);
                });

            if (reverseAddOrder)
            {
                builder.AddStage(stageB);
                builder.AddStage(stageA);
            }
            else
            {
                builder.AddStage(stageA);
                builder.AddStage(stageB);
            }

            return builder;
        }

        private static void AssertHasDiagnostic(
            WorldGenerationCompileResult result,
            WorldGenerationDiagnosticId code,
            string channelId = null)
        {
            for (int i = 0; i < result.Diagnostics.Length; i++)
            {
                WorldGenerationDiagnostic diagnostic = result.Diagnostics[i];
                if (diagnostic.Code != code) continue;
                if (channelId != null && diagnostic.ChannelId.Value != channelId) continue;
                return;
            }

            Assert.Fail("Missing diagnostic " + code + (channelId == null ? string.Empty : " for " + channelId) + ". " + JoinDiagnostics(result.Diagnostics));
        }

        private static string JoinDiagnostics(WorldGenerationDiagnostic[] diagnostics)
        {
            if (diagnostics == null || diagnostics.Length == 0) return "<no diagnostics>";
            string result = string.Empty;
            for (int i = 0; i < diagnostics.Length; i++)
            {
                if (i > 0) result += " | ";
                result += diagnostics[i].Code + ": " + diagnostics[i].Message;
            }
            return result;
        }

        private sealed class TestStage : IWorldGenerationStage
        {
            private readonly Action<WorldGenerationStageDescriptorBuilder> _describe;
            private readonly Func<WorldGenerationContext, WorldGenerationStageResult> _execute;

            public WorldGenerationStageId Id { get; }

            internal TestStage(
                string id,
                Action<WorldGenerationStageDescriptorBuilder> describe,
                Func<WorldGenerationContext, WorldGenerationStageResult> execute = null)
            {
                Id = WorldGenerationStageId.From(id);
                _describe = describe ?? throw new ArgumentNullException(nameof(describe));
                _execute = execute;
            }

            public void Describe(WorldGenerationStageDescriptorBuilder builder) => _describe(builder);

            public WorldGenerationStageResult Execute(in WorldGenerationContext context)
            {
                return _execute == null
                    ? WorldGenerationStageResult.Succeeded()
                    : _execute(context);
            }
        }
    }
}
