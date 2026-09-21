using NUnit.Framework;
using StellarFramework.Res;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class YooAssetContentUpdaterPolicyTests
    {
        [Test]
        public void InvalidOptionsReturnStableErrorCode()
        {
            YooAssetContentUpdateResult result =
                YooAssetContentUpdater.UpdateHostPackageAsync(null).GetAwaiter().GetResult();

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(YooAssetContentUpdateErrorCode.InvalidOptions));
            Assert.That(result.FailureStage, Is.EqualTo(YooAssetContentUpdateStage.None));
            Assert.That(result.RetryCount, Is.EqualTo(0));
        }

        [Test]
        public void DefaultPolicyRetriesOnlyVersionAndManifestControlPlaneFailures()
        {
            IYooAssetContentUpdateRetryPolicy policy = new YooAssetContentUpdateRetryPolicy(2, 123);

            Assert.That(policy.ShouldRetry(
                Failure(YooAssetContentUpdateErrorCode.VersionRequestFailed, YooAssetContentUpdateStage.RequestingVersion),
                1,
                out int versionDelay), Is.True);
            Assert.That(versionDelay, Is.EqualTo(123));

            Assert.That(policy.ShouldRetry(
                Failure(YooAssetContentUpdateErrorCode.ManifestUpdateFailed, YooAssetContentUpdateStage.UpdatingManifest),
                2,
                out int manifestDelay), Is.True);
            Assert.That(manifestDelay, Is.EqualTo(123));

            Assert.That(policy.ShouldRetry(
                Failure(YooAssetContentUpdateErrorCode.ManifestUpdateFailed, YooAssetContentUpdateStage.UpdatingManifest),
                3,
                out _), Is.False);

            Assert.That(policy.ShouldRetry(
                Failure(YooAssetContentUpdateErrorCode.DownloadFailed, YooAssetContentUpdateStage.Downloading),
                1,
                out _), Is.False,
                "Bundle retry ownership must stay inside YooAsset ResourceDownloaderOperation.");

            Assert.That(policy.ShouldRetry(
                Failure(YooAssetContentUpdateErrorCode.PackageInitializationFailed, YooAssetContentUpdateStage.InitializingPackage),
                1,
                out _), Is.False,
                "A failed initialization is stateful and must not be blindly retried by the thin helper.");
        }

        [Test]
        public void NullRetryPolicyIsAllowedForFailFastProjects()
        {
            var options = new YooAssetContentUpdateOptions
            {
                RetryPolicy = null
            };

            Assert.That(options.RetryPolicy, Is.Null);
        }

        private static YooAssetContentUpdateFailure Failure(
            YooAssetContentUpdateErrorCode errorCode,
            YooAssetContentUpdateStage stage)
        {
            return new YooAssetContentUpdateFailure(errorCode, stage, "test");
        }
    }
}
