using System;
using NUnit.Framework;
using StellarFramework.WorldGenKit;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class WorldGenKitStorageRuleTests
    {
        [Test]
        public void DenseSparseChunkedConstantComputedAndExternalStorageKeepDistinctSemantics()
        {
            DenseChannelStorage<int> dense = new DenseChannelStorage<int>(4);
            dense.Fill(3);
            dense[2] = 9;
            Assert.That(dense.AsReadOnlySpan()[0], Is.EqualTo(3));
            Assert.That(dense.AsReadOnlySpan()[2], Is.EqualTo(9));

            SparseChannelStorage<int> sparse = new SparseChannelStorage<int>(-1);
            Assert.That(sparse.Get(10), Is.EqualTo(-1));
            sparse.Set(10, 5);
            Assert.That(sparse.Get(10), Is.EqualTo(5));
            Assert.That(sparse.StoredCount, Is.EqualTo(1));
            Assert.That(sparse.Remove(10), Is.True);
            Assert.That(sparse.Get(10), Is.EqualTo(-1));

            ChunkedChannelStorage<TestPageKey, string> chunked = new ChunkedChannelStorage<TestPageKey, string>();
            TestPageKey key = new TestPageKey(-7, 12);
            chunked.Set(key, "page");
            Assert.That(chunked.TryGet(key, out string page), Is.True);
            Assert.That(page, Is.EqualTo("page"));

            ConstantChannelStorage<double> constant = new ConstantChannelStorage<double>(4.25d);
            Assert.That(constant.Value, Is.EqualTo(4.25d));

            ComputedChannelStorage<TestComputeContext, double> computed =
                new ComputedChannelStorage<TestComputeContext, double>(
                    (in TestComputeContext context) => context.A + context.B);
            TestComputeContext computeContext = new TestComputeContext(2d, 6.5d);
            Assert.That(computed.Evaluate(in computeContext), Is.EqualTo(8.5d));

            TestExternalSource source = new TestExternalSource("imported");
            ExternalChannelStorage<TestExternalSource, float> external =
                new ExternalChannelStorage<TestExternalSource, float>(source);
            Assert.That(external.Source, Is.SameAs(source));
        }

        [Test]
        public void DataSetBindsByTypedHandleAndDeclaredStorageKind()
        {
            WorldChannelRegistryBuilder builder = new WorldChannelRegistryBuilder();
            ChannelHandle<float> denseHandle = builder.Register<float>(
                WorldDataChannelId.From("terrain.height"),
                new WorldChannelStorageDescriptor(WorldChannelStorageKind.Dense, WorldChannelScope.Sample));
            ChannelHandle<float> sparseHandle = builder.Register<float>(
                WorldDataChannelId.From("terrain.moisture"),
                new WorldChannelStorageDescriptor(WorldChannelStorageKind.Sparse, WorldChannelScope.Sample));
            WorldChannelRegistry registry = builder.Build();
            WorldGenerationDataSet data = new WorldGenerationDataSet(registry);

            DenseChannelStorage<float> dense = new DenseChannelStorage<float>(8);
            data.Bind(denseHandle, dense);
            Assert.That(data.GetStorage<float, DenseChannelStorage<float>>(denseHandle), Is.SameAs(dense));
            Assert.That(data.IsBound(denseHandle), Is.True);

            Assert.That(data.TryBind(sparseHandle, new DenseChannelStorage<float>(8), out WorldChannelBindingError error), Is.False);
            Assert.That(error, Is.EqualTo(WorldChannelBindingError.StorageKindMismatch));
            Assert.That(data.TryGetStorage<float, SparseChannelStorage<float>>(sparseHandle, out _), Is.False);

            SparseChannelStorage<float> sparse = new SparseChannelStorage<float>();
            data.Bind(sparseHandle, sparse);
            Assert.That(data.BoundCount, Is.EqualTo(2));

            Assert.That(data.TryBind(denseHandle, new DenseChannelStorage<float>(8), out error), Is.False);
            Assert.That(error, Is.EqualTo(WorldChannelBindingError.AlreadyBound));

            Assert.That(data.Unbind(denseHandle), Is.True);
            Assert.That(data.IsBound(denseHandle), Is.False);
            data.ClearBindings();
            Assert.That(data.BoundCount, Is.EqualTo(0));
        }

        [Test]
        public void RangeThresholdCurveDistanceAndCompositionRulesAreDeterministic()
        {
            WorldRangeRule range = new WorldRangeRule(10d, 20d);
            Assert.That(range.Contains(10d), Is.True);
            Assert.That(range.Contains(20d), Is.True);
            Assert.That(range.Contains(20.1d), Is.False);
            Assert.That(range.NormalizeClamped(15d), Is.EqualTo(0.5d));

            WorldThresholdRule threshold = new WorldThresholdRule(0.4d, WorldThresholdComparison.GreaterOrEqual);
            Assert.That(threshold.Evaluate(0.4d), Is.True);
            Assert.That(threshold.Evaluate(0.399d), Is.False);

            WorldCurvePoint[] points =
            {
                new WorldCurvePoint(0d, 0d),
                new WorldCurvePoint(10d, 1d),
                new WorldCurvePoint(20d, 0d)
            };
            WorldCurve curve = new WorldCurve(points.AsSpan());
            Assert.That(curve.Evaluate(5d), Is.EqualTo(0.5d));
            Assert.That(curve.Evaluate(15d), Is.EqualTo(0.5d));
            Assert.That(curve.Evaluate(-100d), Is.EqualTo(0d));
            Assert.That(curve.Evaluate(100d), Is.EqualTo(0d));

            double[] scores = { 0.25d, 0.5d, 1d };
            double[] weights = { 1d, 2d, 1d };
            Assert.That(WorldRuleMath.WeightedSum(scores.AsSpan(), weights.AsSpan()), Is.EqualTo(0.5625d));
            Assert.That(WorldRuleMath.Multiply(scores.AsSpan()), Is.EqualTo(0.125d));
            Assert.That(WorldRuleMath.Min(scores.AsSpan()), Is.EqualTo(0.25d));
            Assert.That(WorldRuleMath.Max(scores.AsSpan()), Is.EqualTo(1d));
            Assert.That(WorldRuleMath.Inverse01(0.25d), Is.EqualTo(0.75d));
            Assert.That(WorldRuleMath.Distance2D(0d, 0d, 3d, 4d), Is.EqualTo(5d));

            bool[] andValues = { true, true, false };
            bool[] orValues = { false, false, true };
            Assert.That(WorldRuleMath.And(andValues.AsSpan()), Is.False);
            Assert.That(WorldRuleMath.Or(orValues.AsSpan()), Is.True);
        }

        [Test]
        public void NoiseAndTagsUseStableIdsWithoutGlobalRandomState()
        {
            WorldGenerationSeed seed = new WorldGenerationSeed(998877UL);
            WorldRuleId noiseId = WorldRuleId.From("rule.resource_noise");
            double a = WorldNoiseRule.Sample01(seed, -123, 456, noiseId, 8UL);
            double b = WorldNoiseRule.Sample01(seed, -123, 456, noiseId, 8UL);
            double c = WorldNoiseRule.Sample01(seed, -122, 456, noiseId, 8UL);
            Assert.That(a, Is.GreaterThanOrEqualTo(0d).And.LessThan(1d));
            Assert.That(a, Is.EqualTo(b));
            Assert.That(c, Is.Not.EqualTo(a));

            WorldNoiseKey compiled = WorldNoiseRule.Compile(noiseId);
            double compiledA = WorldNoiseRule.Sample01(seed, -123, 456, compiled, 8UL);
            double compiledB = WorldNoiseRule.Sample01(seed, -123, 456, compiled, 8UL);
            double compiledC = WorldNoiseRule.Sample01(seed, -122, 456, compiled, 8UL);
            Assert.That(compiledA, Is.GreaterThanOrEqualTo(0d).And.LessThan(1d));
            Assert.That(compiledA, Is.EqualTo(compiledB));
            Assert.That(compiledC, Is.Not.EqualTo(compiledA));

            WorldRuleTagId forest = WorldRuleTagId.From("tag.forest");
            WorldRuleTagId wet = WorldRuleTagId.From("tag.wet");
            WorldRuleTagSet tags = new WorldRuleTagSet(new[] { wet, forest }.AsSpan());
            Assert.That(tags.Contains(forest), Is.True);
            Assert.That(tags.Contains(WorldRuleTagId.From("tag.desert")), Is.False);

            Assert.That(
                () => new WorldRuleTagSet(new[] { forest, forest }.AsSpan()),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void RuleInputsRejectInvalidNumbersInsteadOfSilentlyPropagatingNaN()
        {
            Assert.That(() => new WorldRangeRule(double.NaN, 1d), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new WorldRuleEvaluation(true, double.PositiveInfinity), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => WorldRuleMath.Distance2D(0d, 0d, double.NaN, 0d), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => WorldRuleMath.WeightedSum(new[] { 1d }.AsSpan(), new[] { -1d }.AsSpan()), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        private readonly struct TestPageKey : IEquatable<TestPageKey>
        {
            private readonly int _x;
            private readonly int _y;

            internal TestPageKey(int x, int y)
            {
                _x = x;
                _y = y;
            }

            public bool Equals(TestPageKey other) => _x == other._x && _y == other._y;
            public override bool Equals(object obj) => obj is TestPageKey other && Equals(other);
            public override int GetHashCode() => unchecked((_x * 397) ^ _y);
        }

        private readonly struct TestComputeContext
        {
            internal double A { get; }
            internal double B { get; }

            internal TestComputeContext(double a, double b)
            {
                A = a;
                B = b;
            }
        }

        private sealed class TestExternalSource
        {
            internal string Name { get; }
            internal TestExternalSource(string name) => Name = name;
        }
    }
}
