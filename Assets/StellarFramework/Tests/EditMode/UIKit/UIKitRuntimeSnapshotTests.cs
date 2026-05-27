using NUnit.Framework;
using StellarFramework.UI;

namespace StellarFramework.Tests.UIKit
{
    public sealed class UIKitRuntimeSnapshotTests
    {
        [Test]
        public void EmptySnapshotKeepsReasonAndZeroCounts()
        {
            UIKitRuntimeSnapshot snapshot = UIKitRuntimeSnapshot.Empty("UIKit instance is null");

            Assert.That(snapshot.LoadStrategyName, Is.EqualTo("UIKit instance is null"));
            Assert.That(snapshot.CachedPanelCount, Is.Zero);
            Assert.That(snapshot.ActivePanelCount, Is.Zero);
            Assert.That(snapshot.LoadingPanelCount, Is.Zero);
        }

        [Test]
        public void ToMultilineStringReportsCountsAndPanelNames()
        {
            UIKitRuntimeSnapshot snapshot = new UIKitRuntimeSnapshot
            {
                IsInitialized = true,
                HasRootCanvas = true,
                HasStaticCanvas = true,
                HasDynamicCanvas = true,
                LoadStrategyName = "TestStrategy"
            };
            snapshot.CachedPanels.Add("InventoryPanel");
            snapshot.ActivePanels.Add("InventoryPanel");
            snapshot.LoadingPanels.Add("RewardPanel");

            string report = snapshot.ToMultilineString();

            Assert.That(report, Does.Contain("Initialized=True"));
            Assert.That(report, Does.Contain("Strategy=TestStrategy"));
            Assert.That(report, Does.Contain("Cached=1"));
            Assert.That(report, Does.Contain("Active=1"));
            Assert.That(report, Does.Contain("Loading=1"));
            Assert.That(report, Does.Contain("InventoryPanel"));
            Assert.That(report, Does.Contain("RewardPanel"));
        }
    }
}
