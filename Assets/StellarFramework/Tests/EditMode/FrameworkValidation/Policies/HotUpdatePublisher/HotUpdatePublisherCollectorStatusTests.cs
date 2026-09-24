using NUnit.Framework;
using StellarFramework.Editor.HotUpdatePublisher;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class HotUpdatePublisherCollectorStatusTests
    {
        [TearDown]
        public void TearDown()
        {
            HotUpdatePublisherCollectorStatus.Provider = null;
        }

        [TestCase(null)]
        [TestCase("")]
        public void Check_ReportsMissingBusinessPackage(string packageName)
        {
            HotUpdatePublisherCollectorStatus status = HotUpdatePublisherCollectorStatus.Check(packageName);

            Assert.That(status.IsReady, Is.False);
            Assert.That(status.Message, Does.Contain("缺少 YooAsset 业务 Package"));
        }

        [Test]
        public void Check_RejectsVerificationPackageBeforeConsultingProvider()
        {
            bool providerCalled = false;
            HotUpdatePublisherCollectorStatus.Provider = _ =>
            {
                providerCalled = true;
                return new HotUpdatePublisherCollectorStatus(true, "unexpected");
            };

            HotUpdatePublisherCollectorStatus status =
                HotUpdatePublisherCollectorStatus.Check("StellarHotUpdateVerification");

            Assert.That(status.IsReady, Is.False);
            Assert.That(status.Message, Does.Contain("Verification"));
            Assert.That(providerCalled, Is.False);
        }

        [Test]
        public void Check_ExplainsUnavailableYooAssetAdapter()
        {
            HotUpdatePublisherCollectorStatus.Provider = null;

            HotUpdatePublisherCollectorStatus status =
                HotUpdatePublisherCollectorStatus.Check("BusinessPackage");

            Assert.That(status.IsReady, Is.False);
            Assert.That(status.Message, Does.Contain("YooAsset Editor Adapter"));
        }
    }
}
