#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using StellarFrameworkVerification.Runtime;
using YooAsset;
using YooAsset.Editor;

namespace StellarFrameworkVerification.Editor
{
    /// <summary>
    /// Builds the small verification-only YooAsset package used by content-resume and
    /// HybridCLR end-to-end gates. It is not a production content pipeline.
    /// </summary>
    public static class YooAssetHotUpdateVerificationBuilder
    {
        public const string PackageName = "StellarHotUpdateVerification";
        public const string PackageVersion = "verification-v1";

        public static string DefaultOutputRoot => Path.Combine(
            Directory.GetParent(UnityEngine.Application.dataPath)?.FullName ?? UnityEngine.Application.dataPath,
            "Temp",
            "StellarHotUpdateVerification",
            "Bundles").Replace('\\', '/');

        public static string GetPackageOutputDirectory(string packageVersion = PackageVersion)
        {
            string resolvedVersion = string.IsNullOrWhiteSpace(packageVersion)
                ? PackageVersion
                : packageVersion.Trim();
            return Path.Combine(
                    DefaultOutputRoot,
                    EditorUserBuildSettings.activeBuildTarget.ToString(),
                    PackageName,
                    resolvedVersion)
                .Replace('\\', '/');
        }

        public static BuildResult Build(string outputRoot = null, string packageVersion = PackageVersion)
        {
            string resolvedOutputRoot = string.IsNullOrWhiteSpace(outputRoot)
                ? DefaultOutputRoot
                : outputRoot.Replace('\\', '/');

            AssetBundleCollectorSetting setting = AssetBundleCollectorSettingData.Setting;
            setting.CheckPackageConfigError(PackageName);

            var parameters = new BuiltinBuildParameters
            {
                BuildOutputRoot = resolvedOutputRoot,
                BuildinFileRoot = AssetBundleBuilderHelper.GetStreamingAssetsRoot(),
                BuildPipeline = EBuildPipeline.BuiltinBuildPipeline.ToString(),
                BuildBundleType = (int)EBuildBundleType.AssetBundle,
                BuildTarget = EditorUserBuildSettings.activeBuildTarget,
                PackageName = PackageName,
                PackageVersion = string.IsNullOrWhiteSpace(packageVersion) ? PackageVersion : packageVersion.Trim(),
                PackageNote = "StellarFramework hot-update verification",
                EnableSharePackRule = false,
                VerifyBuildingResult = true,
                FileNameStyle = EFileNameStyle.HashName,
                BuildinFileCopyOption = EBuildinFileCopyOption.None,
                BuildinFileCopyParams = string.Empty,
                CompressOption = ECompressOption.LZ4,
                ClearBuildCacheFiles = true,
                UseAssetDependencyDB = true
            };

            Directory.CreateDirectory(resolvedOutputRoot);
            var pipeline = new BuiltinBuildPipeline();
            BuildResult result = pipeline.Run(parameters, false);
            if (!result.Success)
            {
                throw new InvalidOperationException("YooAsset verification package build failed: " + result.ErrorInfo);
            }

            return result;
        }

        [MenuItem("StellarFramework/Verification/Build YooAsset HotUpdate Package")]
        private static void BuildFromMenu()
        {
            BuildResult result = Build();
            UnityEngine.Debug.Log(
                $"[HotUpdateVerification] YooAsset package built: {result.OutputPackageDirectory}");
        }

        [MenuItem("StellarFramework/Verification/Prepare Runtime HotUpdate E2E")]
        private static void PrepareRuntimeE2E()
        {
            BuildResult buildResult = Build();
            string verificationRoot = Path.Combine(
                Directory.GetParent(UnityEngine.Application.dataPath)?.FullName ?? UnityEngine.Application.dataPath,
                "Temp",
                "StellarHotUpdateVerification");
            Directory.CreateDirectory(verificationRoot);

            string remoteCdnRoot = Path.Combine(verificationRoot, "RemoteCDN");
            DeleteDirectorySafe(remoteCdnRoot);
            CopyDirectory(buildResult.OutputPackageDirectory, remoteCdnRoot);

            string resultPath = Path.Combine(verificationRoot, "runtime-result.json");
            if (File.Exists(resultPath)) File.Delete(resultPath);

            var config = new HotUpdateVerificationConfig
            {
                packageDirectory = remoteCdnRoot.Replace('\\', '/'),
                cacheRoot = Path.Combine(verificationRoot, "ClientCache").Replace('\\', '/'),
                packageName = PackageName,
                expectedPackageVersion = PackageVersion,
                interruptAfterBytes = 256 * 1024
            };

            File.WriteAllText(
                Path.Combine(verificationRoot, "runtime-config.json"),
                UnityEngine.JsonUtility.ToJson(config, true),
                System.Text.Encoding.UTF8);
            UnityEngine.Debug.Log(
                $"[HotUpdateVerification] Runtime E2E armed. RemoteCDN={remoteCdnRoot}, ClientCache={config.cacheRoot}. Enter Play Mode once.");
        }

        private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
        {
            Directory.CreateDirectory(destinationDirectory);
            foreach (string filePath in Directory.GetFiles(sourceDirectory, "*", SearchOption.TopDirectoryOnly))
            {
                File.Copy(
                    filePath,
                    Path.Combine(destinationDirectory, Path.GetFileName(filePath)),
                    true);
            }

            foreach (string childDirectory in Directory.GetDirectories(sourceDirectory, "*", SearchOption.TopDirectoryOnly))
            {
                CopyDirectory(
                    childDirectory,
                    Path.Combine(destinationDirectory, Path.GetFileName(childDirectory)));
            }
        }

        private static void DeleteDirectorySafe(string path)
        {
            if (!Directory.Exists(path)) return;
            Directory.Delete(path, true);
        }
    }
}
#endif
