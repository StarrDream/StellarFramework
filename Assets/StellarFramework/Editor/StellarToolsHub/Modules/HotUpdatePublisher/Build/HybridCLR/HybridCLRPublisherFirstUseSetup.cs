using System;
using System.IO;
using System.Linq;
using StellarFramework.Editor.Modules;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using HybridCLR.Editor.Commands;
using UPMInfo = UnityEditor.PackageManager.PackageInfo;

namespace StellarFramework.Editor.HotUpdatePublisher
{
    /// <summary>Runs the existing HybridCLR BaseRelease creation path from the first-use guide.</summary>
    internal static class HybridCLRPublisherFirstUseSetup
    {
        private const string BaseAppVersionPrefsSuffix = ".baseAppVersion";
        private const string PackagePrefix = "StellarFramework.HotUpdatePublisher.";
        private const string YooAssetPackageName = "com.tuyoogame.yooasset";

        [MenuItem("Tools/StellarFramework/HotUpdate Publisher/Create Android Base Release")]
        private static void CreateAndroidBaseRelease()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
                UnityEngine.Debug.Log("[HotUpdatePublisher] Switched active target to Android. Wait for import/compile, then run Create Android Base Release again.");
                return;
            }

            BuildTargetGroup targetGroup = BuildTargetGroup.Android;
            ScriptingImplementation backend = PlayerSettings.GetScriptingBackend(targetGroup);
            AndroidArchitecture architectures = PlayerSettings.Android.targetArchitectures;
            if (backend != ScriptingImplementation.IL2CPP ||
                (architectures & AndroidArchitecture.X86_64) == 0)
            {
                PlayerSettings.SetScriptingBackend(targetGroup, ScriptingImplementation.IL2CPP);
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.X86_64;
                AssetDatabase.SaveAssets();
                UnityEngine.Debug.Log("[HotUpdatePublisher] Configured Android for IL2CPP/x86_64. Wait for import/compile, then run Create Android Base Release again. No BaseRelease was written in this step.");
                return;
            }

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string prefsSuffix = projectRoot.Replace('\\', '/');
            string baseAppVersion = EditorPrefs.GetString(
                PackagePrefix + prefsSuffix + BaseAppVersionPrefsSuffix,
                PlayerSettings.bundleVersion).Trim();
            if (string.IsNullOrWhiteSpace(baseAppVersion))
                throw new InvalidOperationException("Set a non-empty Base App version in HotUpdate Publisher Overview before creating the BaseRelease.");

            try
            {
                var repository = new HotUpdateBaseReleaseRepository();
                var adapter = new HybridCLRHotUpdateBuildAdapter(repository);
                var request = new HotUpdateBaseReleaseCreateRequest
                {
                    BaseAppVersion = baseAppVersion,
                    Platform = BuildTarget.Android,
                    Architecture = "x86_64",
                    UnityVersion = Application.unityVersion,
                    HybridCLRVersion = GetHybridCLRVersion(),
                    YooAssetVersion = GetPackageVersion(YooAssetPackageName),
                    ScriptingBackend = ScriptingImplementation.IL2CPP,
                    GitCommit = new GitHotUpdateSnapshotProvider(projectRoot).ReadSnapshot().Commit
                };

                // This is the existing formal path: Generate/All emits target-platform AOT DLLs,
                // then the repository copies and hashes those exact outputs into an immutable record.
                HotUpdateBaseRelease release = adapter.CreateBaseRelease(request);
                UnityEngine.Debug.Log(
                    $"[HotUpdatePublisher] Created formal Android/{release.BaseAppVersion} BaseRelease from HybridCLR Generate/All. AOT metadata: {release.AotMetadata.Length} files. Repository: {repository.RepositoryRoot}");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                throw;
            }
        }

        private static string GetHybridCLRVersion()
        {
            UPMInfo package = UPMInfo.FindForAssembly(typeof(CompileDllCommand).Assembly);
            if (package == null || string.IsNullOrWhiteSpace(package.version))
                throw new InvalidOperationException("HybridCLR package version could not be read from the loaded Editor assembly.");
            return package.version;
        }

        private static string GetPackageVersion(string packageName)
        {
            UPMInfo package = UPMInfo.GetAllRegisteredPackages()
                .FirstOrDefault(item => string.Equals(item.name, packageName, StringComparison.Ordinal));
            if (package == null || string.IsNullOrWhiteSpace(package.version))
                throw new InvalidOperationException($"Required UPM package '{packageName}' is not registered in this project.");
            return package.version;
        }
    }
}
