using System.Collections;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using StellarFrameworkVerification.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace StellarFramework.Tests.ReleaseGate
{
    [Category("StellarFramework.ReleaseGate")]
    public sealed class YooAssetHotUpdateEndToEndTests
    {
        private const int TimeoutMs = 120000;
        private const string PrepareMenuPath =
            "Tools/StellarFramework/Verification/Prepare HotUpdate PlayMode Release Gate";
        private const string HotUpdateEntryLog = "<color=red>Hello HybridCLR , 热更成功 ;</color>";
        private static readonly Regex ExpectedInterruptedBundleLog = new Regex(
            @"^URL : http://127\.0\.0\.1:\d+/[A-Za-z0-9_.-]+\.bundle Error : Unknown Error$");

        [UnityTest]
        [Timeout(TimeoutMs)]
        public IEnumerator PreparedPackageResumesRangeAndEntersHotUpdate()
        {
            HotUpdateVerificationConfig config = LoadPreparedConfig();

            // Test Runner's diagnostic prefix is not part of the raw Unity log condition.
            LogAssert.Expect(LogType.Error, ExpectedInterruptedBundleLog);
            LogAssert.Expect(LogType.Log, HotUpdateEntryLog);
            return RunPreparedGateAsync(config).ToCoroutine();
        }

        private static HotUpdateVerificationConfig LoadPreparedConfig()
        {
            string configPath = HotUpdateVerificationPaths.RuntimeConfigFilePath;
            Assert.That(
                File.Exists(configPath),
                Is.True,
                $"precondition missing: run '{PrepareMenuPath}' before this PlayMode release gate.");

            HotUpdateVerificationConfig config = JsonUtility.FromJson<HotUpdateVerificationConfig>(
                File.ReadAllText(configPath, Encoding.UTF8));
            Assert.That(config, Is.Not.Null, "precondition missing: prepared runtime-config.json is invalid.");
            Assert.That(
                Directory.Exists(config.packageDirectory),
                Is.True,
                $"precondition missing: prepared YooAsset package directory does not exist: {config.packageDirectory}");

            return config;
        }

        private static async UniTask RunPreparedGateAsync(HotUpdateVerificationConfig config)
        {
            HotUpdateVerificationResult result =
                await HotUpdateRuntimeVerification.RunAsync(config);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.success, Is.True, result.error);
            Assert.That(result.packageVersion, Is.EqualTo(config.expectedPackageVersion));
            Assert.That(result.largeBundleSize, Is.GreaterThan(1024L * 1024L));
            Assert.That(result.interruptedBytes, Is.GreaterThan(0));
            Assert.That(result.resumeOffset, Is.GreaterThan(0));
            Assert.That(result.resumeOffset, Is.LessThanOrEqualTo(result.interruptedBytes));
            Assert.That(result.manifestSource, Does.StartWith("ResKit:YooAsset:"));
            Assert.That(result.loadedAssemblyFullName, Does.Contain("HotUpdate"));
        }
    }
}
