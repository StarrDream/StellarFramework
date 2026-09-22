#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using StellarFramework.Editor;
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

        public static void BuildAndLog()
        {
            BuildResult result = Build();
            UnityEngine.Debug.Log(
                $"[HotUpdateVerification] YooAsset package built: {result.OutputPackageDirectory}");
        }

        public static void PrepareRuntimeE2E()
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

    [StellarTool("HotUpdate 发布验证", "热更新", -10,
        RequiredAssemblyNames = new[]
        {
            "StellarFramework.Verification.Runtime",
            "StellarFramework.ResKit.YooAsset",
            "YooAsset.Editor"
        })]
    public sealed class HotUpdateVerificationHubModule : ToolModule
    {
        public override string Icon => "d_TestPassed";
        public override string Description =>
            "维护者专用：构建 YooAsset 验证包，并准备 ResKit + YooAsset + HybridCLR Runtime E2E。";

        public override void OnGUI()
        {
            Section("维护者发布验证");
            EditorGUILayout.HelpBox(
                "这里只服务 StellarFramework 源码工程的发布 Gate，不属于业务项目 Runtime，也不会随 Kit 分发。",
                MessageType.Info);

            if (PrimaryButton("构建 YooAsset HotUpdate 验证包", GUILayout.Height(32)))
            {
                YooAssetHotUpdateVerificationBuilder.BuildAndLog();
            }

            if (PrimaryButton("准备 Runtime HotUpdate E2E", GUILayout.Height(32)))
            {
                YooAssetHotUpdateVerificationBuilder.PrepareRuntimeE2E();
            }

            EditorGUILayout.HelpBox(
                "Runtime E2E 会准备 RemoteCDN / ClientCache 配置；随后进入 Play Mode 执行对应验证 Gate。",
                MessageType.None);
        }
    }
}
#endif
