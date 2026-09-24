#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HybridCLR.Editor.Commands;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using StellarFramework.Editor;
using StellarFramework.Editor.Modules;
using StellarFramework.HybridCLR;
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
        public const string PackageName = HotUpdateVerificationPaths.PackageName;
        public const string PackageVersion = HotUpdateVerificationPaths.PackageVersion;

        public static string DefaultOutputRoot => HotUpdateVerificationPaths.PackageOutputRoot;

        [Serializable]
        private sealed class AndroidPreparationState
        {
            public string status;
            public string startedAtUtc;
            public string completedAtUtc;
            public string buildTarget;
            public string scriptingBackend;
            public string hotUpdateDllSource;
            public string hotUpdateDllSha256;
            public string manifestBuildTarget;
            public string manifestAssemblyKey;
            public string manifestAssemblySha256;
            public string[] aotMetadataKeys;
            public string[] regeneratedAotMetadataSources;
            public string packageName;
            public string packageVersion;
            public string packageDirectory;
            public int packageFileCount;
            public int packageBundleCount;
            public string error;
        }

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

        [MenuItem("Tools/StellarFramework/Verification/Prepare HotUpdate PlayMode Release Gate")]
        public static void PrepareRuntimeE2E()
        {
            BuildResult buildResult = Build();
            string verificationRoot = HotUpdateVerificationPaths.RootDirectoryPath;
            Directory.CreateDirectory(verificationRoot);

            string remoteCdnRoot = HotUpdateVerificationPaths.RemoteCdnDirectoryPath;
            DeleteDirectorySafe(remoteCdnRoot);
            CopyDirectory(buildResult.OutputPackageDirectory, remoteCdnRoot);

            string resultPath = HotUpdateVerificationPaths.RuntimeResultFilePath;
            if (File.Exists(resultPath)) File.Delete(resultPath);

            var config = new HotUpdateVerificationConfig
            {
                packageDirectory = remoteCdnRoot.Replace('\\', '/'),
                cacheRoot = HotUpdateVerificationPaths.ClientCacheDirectoryPath,
                packageName = PackageName,
                expectedPackageVersion = PackageVersion,
                interruptAfterBytes = 256 * 1024
            };

            File.WriteAllText(
                HotUpdateVerificationPaths.RuntimeConfigFilePath,
                UnityEngine.JsonUtility.ToJson(config, true),
                System.Text.Encoding.UTF8);
            UnityEngine.Debug.Log(
                $"[HotUpdateVerification] Runtime E2E armed. RemoteCDN={remoteCdnRoot}, ClientCache={config.cacheRoot}. Enter Play Mode once.");
        }

        /// <summary>
        /// Regenerates Android HybridCLR artifacts, exports their manifest/assets, and builds
        /// the Android YooAsset package. All temporary Player settings are restored in finally.
        /// </summary>
        [MenuItem("Tools/StellarFramework/Verification/Prepare Android HotUpdate Release Gate")]
        public static void PrepareAndroidReleaseHotUpdate()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                throw new BuildFailedException(
                    "Android HotUpdate preparation requires the active BuildTarget to be Android. " +
                    "Switch target in Build Settings first; this gate does not leave a target switch behind.");
            }

            string statePath = HotUpdateVerificationPaths.AndroidPreparationResultFilePath;
            var state = new AndroidPreparationState
            {
                status = "RUNNING",
                startedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                buildTarget = EditorUserBuildSettings.activeBuildTarget.ToString(),
                packageName = PackageName,
                packageVersion = PackageVersion
            };
            WritePreparationState(statePath, state);

            NamedBuildTarget android = NamedBuildTarget.Android;
            ScriptingImplementation previousBackend = PlayerSettings.GetScriptingBackend(android);
            AndroidArchitecture previousArchitectures = PlayerSettings.Android.targetArchitectures;
            bool previousDevelopment = EditorUserBuildSettings.development;
            bool previousBuildAppBundle = EditorUserBuildSettings.buildAppBundle;

            try
            {
                PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.X86_64;
                EditorUserBuildSettings.development = false;
                EditorUserBuildSettings.buildAppBundle = false;
                state.scriptingBackend = PlayerSettings.GetScriptingBackend(android).ToString();

                // GenerateAll compiles HotUpdate.dll and builds a scripts-only Android IL2CPP
                // player to regenerate AssembliesPostIl2CppStrip/Android from this target.
                PrebuildCommand.GenerateAll();
                state.hotUpdateDllSource = HybridCLRHotUpdateAssetExporter
                    .GetGeneratedHotUpdateSourceDirectory(BuildTarget.Android) + "/HotUpdate.dll";
                state.regeneratedAotMetadataSources = ValidateAndroidHybridClrOutputs(
                    state.hotUpdateDllSource,
                    out string hotUpdateSha256);
                state.hotUpdateDllSha256 = hotUpdateSha256;

                HybridCLRHotUpdateExportReport export =
                    HybridCLRHotUpdateAssetExporter.ExportGeneratedAssets(BuildTarget.Android);
                if (export == null || !export.Success)
                {
                    string errors = export == null
                        ? "Exporter returned no report."
                        : string.Join(" | ", export.Errors);
                    throw new BuildFailedException("Android HybridCLR asset export failed: " + errors);
                }

                HotUpdateManifest manifest = HotUpdateManifest.FromJson(export.ManifestJson);
                if (manifest == null || !string.Equals(manifest.buildTarget, "Android", StringComparison.Ordinal))
                {
                    throw new BuildFailedException(
                        "Generated HotUpdateManifest does not identify the Android BuildTarget.");
                }
                if (!string.Equals(
                        HotUpdateManifest.NormalizeSha256(manifest.hotUpdateAssemblySha256),
                        state.hotUpdateDllSha256,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new BuildFailedException(
                        "Android Manifest SHA256 does not match the freshly compiled HotUpdate.dll.");
                }

                var regeneratedSources = new HashSet<string>(
                    state.regeneratedAotMetadataSources,
                    StringComparer.OrdinalIgnoreCase);
                foreach (string metadataKey in manifest.aotMetadataKeys)
                {
                    string sourceFileName = Path.GetFileName(metadataKey);
                    if (sourceFileName.EndsWith(".bytes", StringComparison.OrdinalIgnoreCase))
                    {
                        sourceFileName = sourceFileName.Substring(0, sourceFileName.Length - ".bytes".Length);
                    }

                    string sourcePath = Path.Combine(
                        HybridCLRHotUpdateAssetExporter.GetGeneratedAotSourceDirectory(BuildTarget.Android),
                        sourceFileName).Replace('\\', '/');
                    if (!regeneratedSources.Contains(sourcePath))
                    {
                        throw new BuildFailedException(
                            $"Manifest AOT metadata key is not backed by the regenerated Android output: {metadataKey}");
                    }
                }

                state.manifestBuildTarget = manifest.buildTarget;
                state.manifestAssemblyKey = manifest.hotUpdateAssemblyKey;
                state.manifestAssemblySha256 = manifest.hotUpdateAssemblySha256;
                state.aotMetadataKeys = manifest.aotMetadataKeys.ToArray();

                BuildResult package = Build(packageVersion: PackageVersion);
                state.packageDirectory = package.OutputPackageDirectory.Replace('\\', '/');
                if (!Directory.Exists(state.packageDirectory))
                {
                    throw new DirectoryNotFoundException(
                        "Android YooAsset verification package output is missing: " + state.packageDirectory);
                }

                state.packageFileCount = Directory.GetFiles(
                    state.packageDirectory,
                    "*",
                    SearchOption.AllDirectories).Length;
                state.packageBundleCount = Directory.GetFiles(
                    state.packageDirectory,
                    "*.bundle",
                    SearchOption.TopDirectoryOnly).Length;
                if (state.packageFileCount == 0 || state.packageBundleCount == 0)
                {
                    throw new BuildFailedException(
                        "Android YooAsset verification package contains no files or bundle payloads.");
                }

                state.status = "PASS";
                Debug.Log(
                    $"[HotUpdateVerification] Android preparation PASS. Target=Android, " +
                    $"HotUpdateSHA256={state.hotUpdateDllSha256}, AOT={state.aotMetadataKeys.Length}, " +
                    $"Package={state.packageDirectory}, Bundles={state.packageBundleCount}.");
            }
            catch (Exception exception)
            {
                state.status = "FAIL";
                state.error = exception.ToString();
                throw;
            }
            finally
            {
                PlayerSettings.Android.targetArchitectures = previousArchitectures;
                PlayerSettings.SetScriptingBackend(android, previousBackend);
                EditorUserBuildSettings.development = previousDevelopment;
                EditorUserBuildSettings.buildAppBundle = previousBuildAppBundle;

                state.completedAtUtc = DateTimeOffset.UtcNow.ToString("O");
                WritePreparationState(statePath, state);
                Debug.Log(
                    $"[HotUpdateVerification] Restored Android preparation settings: " +
                    $"backend={previousBackend}, architectures={previousArchitectures}, " +
                    $"development={previousDevelopment}, appBundle={previousBuildAppBundle}.");
            }
        }

        private static string[] ValidateAndroidHybridClrOutputs(
            string hotUpdateDllPath,
            out string hotUpdateSha256)
        {
            if (!File.Exists(hotUpdateDllPath))
            {
                throw new FileNotFoundException(
                    "HybridCLR Generate/All did not produce the Android HotUpdate.dll.",
                    hotUpdateDllPath);
            }

            string generatedAotDirectory = HybridCLRHotUpdateAssetExporter
                .GetGeneratedAotSourceDirectory(BuildTarget.Android);
            string[] expectedFiles =
            {
                "mscorlib.dll",
                "System.dll",
                "System.Core.dll",
                "UnityEngine.CoreModule.dll"
            };
            var generatedFiles = new List<string>(expectedFiles.Length);
            for (int i = 0; i < expectedFiles.Length; i++)
            {
                string fullPath = Path.Combine(generatedAotDirectory, expectedFiles[i]).Replace('\\', '/');
                if (!File.Exists(fullPath))
                {
                    throw new FileNotFoundException(
                        "HybridCLR Generate/All did not produce required Android AOT metadata.",
                        fullPath);
                }

                generatedFiles.Add(fullPath);
            }

            hotUpdateSha256 = HybridCLRHotUpdateAssetExporter.ComputeSha256Hex(
                File.ReadAllBytes(hotUpdateDllPath));
            return generatedFiles.ToArray();
        }

        private static void WritePreparationState(string path, AndroidPreparationState state)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

            string tempPath = path + ".tmp";
            File.WriteAllText(tempPath, JsonUtility.ToJson(state, true), System.Text.Encoding.UTF8);
            if (File.Exists(path)) File.Delete(path);
            File.Move(tempPath, path);
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
