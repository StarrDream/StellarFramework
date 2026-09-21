using NUnit.Framework;
using StellarFramework.WorldKit;
using StellarFramework.WorldKit.Streaming.UnityAdapter;
using UnityEngine;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class WorldFloatingOriginAdapterTests
    {
        [Test]
        public void HugeLogicalCoordinatesRemainSmallRelativeToCurrentOrigin()
        {
            WorldPoint2D origin = new WorldPoint2D(1_000_000_000_000d, -1_000_000_000_000d);
            WorldPoint2D logical = new WorldPoint2D(origin.X + 12.25d, origin.Y - 7.5d);

            Vector3 unity = WorldFloatingOriginAdapter.ToUnityPosition(logical, origin, 3f);
            Assert.That(unity.x, Is.EqualTo(12.25f));
            Assert.That(unity.y, Is.EqualTo(3f));
            Assert.That(unity.z, Is.EqualTo(-7.5f));

            WorldPoint2D roundTrip = WorldFloatingOriginAdapter.ToLogicalPosition(unity, origin);
            Assert.That(roundTrip.X, Is.EqualTo(logical.X));
            Assert.That(roundTrip.Y, Is.EqualTo(logical.Y));
        }

        [Test]
        public void RecenterReturnsSceneDeltaThatPreservesLogicalPosition()
        {
            WorldFloatingOriginSettings settings = new WorldFloatingOriginSettings(1000d, 100d);
            WorldPoint2D currentOrigin = new WorldPoint2D(0d, 0d);
            WorldPoint2D focus = new WorldPoint2D(1250d, -2340d);
            WorldPoint2D objectLogical = new WorldPoint2D(1300d, -2300d);
            Vector3 before = WorldFloatingOriginAdapter.ToUnityPosition(objectLogical, currentOrigin);

            Assert.That(WorldFloatingOriginAdapter.TryComputeRecenter(
                focus, currentOrigin, in settings, out WorldPoint2D nextOrigin, out Vector3 sceneDelta), Is.True);
            Assert.That(nextOrigin, Is.EqualTo(new WorldPoint2D(1300d, -2300d)));

            Vector3 afterByShift = before + sceneDelta;
            Vector3 afterByRemap = WorldFloatingOriginAdapter.ToUnityPosition(objectLogical, nextOrigin);
            Assert.That(afterByShift, Is.EqualTo(afterByRemap));
            Assert.That(afterByRemap, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void FocusInsideThresholdDoesNotRequestRecenter()
        {
            WorldFloatingOriginSettings settings = new WorldFloatingOriginSettings(500d, 100d);
            WorldPoint2D origin = new WorldPoint2D(10_000d, -20_000d);
            WorldPoint2D focus = new WorldPoint2D(10_499d, -20_500d);

            Assert.That(WorldFloatingOriginAdapter.TryComputeRecenter(
                focus, origin, in settings, out WorldPoint2D nextOrigin, out Vector3 sceneDelta), Is.False);
            Assert.That(nextOrigin, Is.EqualTo(origin));
            Assert.That(sceneDelta, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void UnrepresentableRelativeCoordinateFailsExplicitly()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                WorldFloatingOriginAdapter.ToUnityPosition(
                    new WorldPoint2D(double.MaxValue / 2d, 0d),
                    new WorldPoint2D(-double.MaxValue / 2d, 0d)));
        }

        [Test]
        public void RepeatedRecentersKeepHugeLogicalTravelInSmallUnitySpace()
        {
            WorldFloatingOriginSettings settings = new WorldFloatingOriginSettings(10_000d, 1_000d);
            WorldPoint2D origin = new WorldPoint2D(0d, 0d);
            WorldPoint2D[] focuses =
            {
                new WorldPoint2D(25_250d, -14_750d),
                new WorldPoint2D(10_000_250d, -20_000_750d),
                new WorldPoint2D(1_000_000_250d, -2_000_000_750d),
                new WorldPoint2D(1_000_000_000_250d, -1_000_000_000_750d)
            };

            for (int i = 0; i < focuses.Length; i++)
            {
                WorldPoint2D focus = focuses[i];
                Assert.That(WorldFloatingOriginAdapter.TryComputeRecenter(
                    focus,
                    origin,
                    in settings,
                    out WorldPoint2D nextOrigin,
                    out Vector3 sceneDelta), Is.True);

                Vector3 relative = WorldFloatingOriginAdapter.ToUnityPosition(focus, nextOrigin);
                Assert.That(Mathf.Abs(relative.x), Is.LessThanOrEqualTo(500f));
                Assert.That(Mathf.Abs(relative.z), Is.LessThanOrEqualTo(500f));
                Assert.That(float.IsNaN(sceneDelta.x) || float.IsInfinity(sceneDelta.x), Is.False);
                Assert.That(float.IsNaN(sceneDelta.z) || float.IsInfinity(sceneDelta.z), Is.False);
                origin = nextOrigin;
            }
        }
    }
}
