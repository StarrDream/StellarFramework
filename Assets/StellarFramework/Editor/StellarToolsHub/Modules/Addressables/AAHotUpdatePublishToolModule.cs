using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using StellarFramework.Editor;
using StellarFramework.HotUpdate;
using StellarFramework.Res;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace StellarFramework.Editor.Modules
{
    public enum AAWorkflowMode
    {
        LocalBuiltIn,
        RemoteHotUpdate
    }

    [Serializable]
    public sealed class AAWorkflowConfig
    {
        public string Name = "本地内置 AA";
        public AAWorkflowMode Mode = AAWorkflowMode.LocalBuiltIn;
        public string BuildTargetName = "StandaloneWindows64";
        public string AddressablesProfileName = "";
        public bool CreateAddressablesProfileIfMissing = true;
        public bool ApplyAddressablesProfile = true;
        public bool ConfigureBundledGroups = true;
        public List<string> IncludedGroupNames = new List<string>();
        public string LocalOutputDirectory = "[StreamingAssets]/aa";
        public string RemoteBuildDirectory = "D:/HotUpdate/[BuildTarget]";
        public string RemotePublishDirectory = "D:/HotUpdate/[BuildTarget]";
        public string RemoteLoadPathOrUrl = "";
        public string ManifestPathOrUrl = "";
        public string TestPlayerRootDirectory = "";
        public bool EnableRemoteCatalog;
        public bool AllowStreamingAssetsFallback = true;
        public bool AllowResourcesFallback = true;
        public bool CleanPublishDirectory = true;
        public bool CopyMetaFiles;
        public bool WriteRuntimeSettings = true;

        public static AAWorkflowConfig CreateLocalBuiltInDefault()
        {
            return new AAWorkflowConfig
            {
                Name = "本地内置 AA",
                Mode = AAWorkflowMode.LocalBuiltIn,
                AddressablesProfileName = "Stellar Local Built-in",
                LocalOutputDirectory = "[StreamingAssets]/aa",
                RemoteBuildDirectory = "",
                RemotePublishDirectory = "",
                RemoteLoadPathOrUrl = "",
                ManifestPathOrUrl = "",
                EnableRemoteCatalog = false,
                AllowStreamingAssetsFallback = true,
                AllowResourcesFallback = true,
                CleanPublishDirectory = true,
                CopyMetaFiles = false,
                WriteRuntimeSettings = true
            };
        }

        public static AAWorkflowConfig CreateRemoteHotUpdateDefault()
        {
            return new AAWorkflowConfig
            {
                Name = "远端热更 AA",
                Mode = AAWorkflowMode.RemoteHotUpdate,
                AddressablesProfileName = "Stellar Remote HotUpdate",
                LocalOutputDirectory = "[StreamingAssets]/aa",
                RemoteBuildDirectory = "D:/HotUpdate/[BuildTarget]",
                RemotePublishDirectory = "D:/HotUpdate/[BuildTarget]",
                RemoteLoadPathOrUrl = "",
                ManifestPathOrUrl = "",
                EnableRemoteCatalog = true,
                AllowStreamingAssetsFallback = false,
                AllowResourcesFallback = false,
                CleanPublishDirectory = true,
                CopyMetaFiles = false,
                WriteRuntimeSettings = true
            };
        }

        public AAWorkflowConfig Clone()
        {
            return new AAWorkflowConfig
            {
                Name = Name,
                Mode = Mode,
                BuildTargetName = BuildTargetName,
                AddressablesProfileName = AddressablesProfileName,
                CreateAddressablesProfileIfMissing = CreateAddressablesProfileIfMissing,
                ApplyAddressablesProfile = ApplyAddressablesProfile,
                ConfigureBundledGroups = ConfigureBundledGroups,
                IncludedGroupNames = IncludedGroupNames == null
                    ? new List<string>()
                    : new List<string>(IncludedGroupNames),
                LocalOutputDirectory = LocalOutputDirectory,
                RemoteBuildDirectory = RemoteBuildDirectory,
                RemotePublishDirectory = RemotePublishDirectory,
                RemoteLoadPathOrUrl = RemoteLoadPathOrUrl,
                ManifestPathOrUrl = ManifestPathOrUrl,
                TestPlayerRootDirectory = TestPlayerRootDirectory,
                EnableRemoteCatalog = EnableRemoteCatalog,
                AllowStreamingAssetsFallback = AllowStreamingAssetsFallback,
                AllowResourcesFallback = AllowResourcesFallback,
                CleanPublishDirectory = CleanPublishDirectory,
                CopyMetaFiles = CopyMetaFiles,
                WriteRuntimeSettings = WriteRuntimeSettings
            };
        }

        public void Normalize()
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                Name = Mode == AAWorkflowMode.LocalBuiltIn ? "本地内置 AA" : "远端热更 AA";
            }

            if (string.IsNullOrWhiteSpace(BuildTargetName))
            {
                BuildTargetName = EditorUserBuildSettings.activeBuildTarget.ToString();
            }

            if (string.IsNullOrWhiteSpace(LocalOutputDirectory))
            {
                LocalOutputDirectory = "[StreamingAssets]/aa";
            }
            else if (string.Equals(
                         LocalOutputDirectory.Replace('\\', '/').Trim(),
                         "[StreamingAssets]/aa/[BuildTarget]",
                         StringComparison.OrdinalIgnoreCase))
            {
                LocalOutputDirectory = "[StreamingAssets]/aa";
            }

            if (Mode == AAWorkflowMode.RemoteHotUpdate)
            {
                if (string.IsNullOrWhiteSpace(RemotePublishDirectory))
                {
                    RemotePublishDirectory = "D:/HotUpdate/[BuildTarget]";
                }

                if (string.IsNullOrWhiteSpace(RemoteBuildDirectory))
                {
                    RemoteBuildDirectory = RemotePublishDirectory;
                }
            }

            if (IncludedGroupNames == null)
            {
                IncludedGroupNames = new List<string>();
            }
        }
    }

    public sealed partial class AAWorkflowConfigSet : ScriptableObject
    {
        public const string DefaultAssetPath =
            "Assets/StellarFramework/Editor/StellarToolsHub/Configs/AAWorkflowConfigSet.asset";

        [SerializeField] private int selectedConfigIndex;
        [SerializeField] private List<AAWorkflowConfig> configs = new List<AAWorkflowConfig>();

        public int SelectedConfigIndex
        {
            get => selectedConfigIndex;
            set => selectedConfigIndex = Mathf.Clamp(value, 0, Mathf.Max(0, Configs.Count - 1));
        }

        public List<AAWorkflowConfig> Configs => configs;

        public AAWorkflowConfig SelectedConfig
        {
            get
            {
                EnsureDefaults();
                return configs[selectedConfigIndex];
            }
        }

        public AAWorkflowConfig GetFirstConfig(AAWorkflowMode mode)
        {
            EnsureDefaults();
            for (int i = 0; i < configs.Count; i++)
            {
                if (configs[i] != null && configs[i].Mode == mode)
                {
                    return configs[i];
                }
            }

            AAWorkflowConfig config = mode == AAWorkflowMode.LocalBuiltIn
                ? AAWorkflowConfig.CreateLocalBuiltInDefault()
                : AAWorkflowConfig.CreateRemoteHotUpdateDefault();
            configs.Add(config);
            return config;
        }

        public bool SelectFirstConfig(AAWorkflowMode mode)
        {
            EnsureDefaults();
            for (int i = 0; i < configs.Count; i++)
            {
                if (configs[i] != null && configs[i].Mode == mode)
                {
                    bool changed = selectedConfigIndex != i;
                    selectedConfigIndex = i;
                    return changed;
                }
            }

            GetFirstConfig(mode);
            selectedConfigIndex = configs.Count - 1;
            return true;
        }

        public static AAWorkflowConfigSet CreateDefault()
        {
            AAWorkflowConfigSet configSet = CreateInstance<AAWorkflowConfigSet>();
            configSet.configs = new List<AAWorkflowConfig>
            {
                AAWorkflowConfig.CreateLocalBuiltInDefault(),
                AAWorkflowConfig.CreateRemoteHotUpdateDefault()
            };
            configSet.selectedConfigIndex = 0;
            return configSet;
        }

        public void EnsureDefaults()
        {
            if (configs == null)
            {
                configs = new List<AAWorkflowConfig>();
            }

            if (configs.Count == 0)
            {
                configs.Add(AAWorkflowConfig.CreateLocalBuiltInDefault());
                configs.Add(AAWorkflowConfig.CreateRemoteHotUpdateDefault());
            }

            for (int i = 0; i < configs.Count; i++)
            {
                if (configs[i] == null)
                {
                    configs[i] = i == 0
                        ? AAWorkflowConfig.CreateLocalBuiltInDefault()
                        : AAWorkflowConfig.CreateRemoteHotUpdateDefault();
                }

                configs[i].Normalize();
            }

            selectedConfigIndex = Mathf.Clamp(selectedConfigIndex, 0, configs.Count - 1);
        }

        public AAWorkflowConfig Add(AAWorkflowMode mode)
        {
            EnsureDefaults();
            AAWorkflowConfig config = mode == AAWorkflowMode.LocalBuiltIn
                ? AAWorkflowConfig.CreateLocalBuiltInDefault()
                : AAWorkflowConfig.CreateRemoteHotUpdateDefault();
            config.Name += " " + (configs.Count + 1);
            configs.Add(config);
            selectedConfigIndex = configs.Count - 1;
            return config;
        }

        public AAWorkflowConfig DuplicateSelected()
        {
            EnsureDefaults();
            AAWorkflowConfig copy = SelectedConfig.Clone();
            copy.Name += " 副本";
            configs.Add(copy);
            selectedConfigIndex = configs.Count - 1;
            return copy;
        }

        public void DeleteSelected()
        {
            EnsureDefaults();
            if (configs.Count <= 1)
            {
                return;
            }

            configs.RemoveAt(selectedConfigIndex);
            selectedConfigIndex = Mathf.Clamp(selectedConfigIndex, 0, configs.Count - 1);
        }
    }

    public static class AAWorkflowConfigStore
    {
        private const string SelectedIndexKey = "StellarFramework.AAWorkflow.SelectedIndex";
        private static AAWorkflowConfigSet _configSet;

        public static AAWorkflowConfigSet ConfigSet
        {
            get
            {
                if (_configSet == null)
                {
                    _configSet = LoadOrCreate();
                }

                return _configSet;
            }
        }

        public static void Reload()
        {
            _configSet = LoadOrCreate();
        }

        public static void Save()
        {
            ConfigSet.EnsureDefaults();
            EditorUtility.SetDirty(ConfigSet);
            AssetDatabase.SaveAssets();
            EditorPrefs.SetInt(SelectedIndexKey, ConfigSet.SelectedConfigIndex);
        }

        private static AAWorkflowConfigSet LoadOrCreate()
        {
            AAWorkflowConfigSet configSet =
                AssetDatabase.LoadAssetAtPath<AAWorkflowConfigSet>(AAWorkflowConfigSet.DefaultAssetPath);
            if (configSet == null)
            {
                string directory = Path.GetDirectoryName(AAWorkflowConfigSet.DefaultAssetPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                configSet = AAWorkflowConfigSet.CreateDefault();
                AssetDatabase.CreateAsset(configSet, AAWorkflowConfigSet.DefaultAssetPath);
                AssetDatabase.SaveAssets();
            }

            configSet.EnsureDefaults();
            configSet.SelectedConfigIndex = EditorPrefs.GetInt(SelectedIndexKey, configSet.SelectedConfigIndex);
            return configSet;
        }
    }

    public static class AAWorkflowPathUtility
    {
        public const string ManifestFileName = "HotUpdateManifest.json";

        public static string BuildManifestDisplayPath(AAWorkflowConfig config, BuildTarget target)
        {
            if (config == null)
            {
                return string.Empty;
            }

            return config.Mode == AAWorkflowMode.LocalBuiltIn
                ? "StreamingAssets:" + AppendPathOrUrl(ExpandPathTokens(config.LocalOutputDirectory, target), ManifestFileName)
                : BuildRuntimeManifestPathOrUrl(config, target);
        }

        public static string BuildRuntimeManifestPathOrUrl(AAWorkflowConfig config, BuildTarget target)
        {
            if (config == null || config.Mode == AAWorkflowMode.LocalBuiltIn)
            {
                return string.Empty;
            }

            string explicitValue = ExpandPathTokens(config.ManifestPathOrUrl, target);
            if (!string.IsNullOrWhiteSpace(explicitValue))
            {
                return LooksLikeLocalPath(explicitValue) ? ToFileUri(explicitValue) : explicitValue;
            }

            return AppendPathOrUrl(BuildRemoteLoadPath(config, target), ManifestFileName);
        }

        public static string BuildRemoteLoadPath(AAWorkflowConfig config, BuildTarget target)
        {
            if (config == null)
            {
                return string.Empty;
            }

            string configured = ExpandPathTokens(config.RemoteLoadPathOrUrl, target);
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return LooksLikeLocalPath(configured)
                    ? ToFileUri(configured).TrimEnd('/')
                    : configured.TrimEnd('/');
            }

            return ToFileUri(ExpandPathTokens(config.RemotePublishDirectory, target)).TrimEnd('/');
        }

        public static string BuildRemoteBuildPath(AAWorkflowConfig config, BuildTarget target)
        {
            if (config == null)
            {
                return string.Empty;
            }

            string value = ExpandPathTokens(config.RemoteBuildDirectory, target);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            return ExpandPathTokens(config.RemotePublishDirectory, target);
        }

        public static string GetAddressablesLocalBuildDirectory(BuildTarget target)
        {
            string buildPath = GetAddressablesBuildPath();
            if (string.IsNullOrWhiteSpace(buildPath))
            {
                string projectRoot = Path.GetDirectoryName(Application.dataPath) ?? string.Empty;
                buildPath = Path.Combine(projectRoot, "Library", "com.unity.addressables", "aa", GetAddressablesPlatformFolder(target));
            }

            return NormalizeAbsolutePath(buildPath);
        }

        public static string GetAddressablesLocalStreamingAssetsDirectory(BuildTarget target)
        {
            return NormalizeAbsolutePath(Path.Combine(
                Application.streamingAssetsPath,
                "aa",
                GetAddressablesPlatformFolder(target)));
        }

        public static string GetStreamingAssetsAaRootDirectory()
        {
            return NormalizeAbsolutePath(Path.Combine(Application.streamingAssetsPath, "aa"));
        }

        public static string GetProjectStreamingAssetsManifestPath()
        {
            return NormalizeAbsolutePath(Path.Combine(
                Application.streamingAssetsPath,
                "aa",
                ManifestFileName));
        }

        public static string GetPublishDirectory(AAWorkflowConfig config, BuildTarget target)
        {
            if (config == null)
            {
                return string.Empty;
            }

            return config.Mode == AAWorkflowMode.LocalBuiltIn
                ? ExpandPathTokens(config.LocalOutputDirectory, target)
                : ExpandPathTokens(config.RemotePublishDirectory, target);
        }

        public static string ResolveTestPlayerStreamingAssetsAaDirectory(AAWorkflowConfig config, BuildTarget target)
        {
            if (config == null || string.IsNullOrWhiteSpace(config.TestPlayerRootDirectory))
            {
                return string.Empty;
            }

            string root = NormalizeAbsolutePath(ExpandPathTokens(config.TestPlayerRootDirectory, target));
            if (!Directory.Exists(root))
            {
                return string.Empty;
            }

            string dataRoot = root.EndsWith("_Data", StringComparison.OrdinalIgnoreCase)
                ? root
                : Directory.GetDirectories(root, "*_Data", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(dataRoot))
            {
                return string.Empty;
            }

            return NormalizeAbsolutePath(Path.Combine(dataRoot, "StreamingAssets", "aa"));
        }

        public static bool IsSafeTestPlayerStreamingAssetsAaDirectory(string directory, BuildTarget target)
        {
            string normalized = NormalizeAbsolutePath(directory);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            string suffix = "/StreamingAssets/aa";
            if (!normalized.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string dataRoot = normalized.Substring(0, normalized.Length - suffix.Length).TrimEnd('/');
            return dataRoot.EndsWith("_Data", StringComparison.OrdinalIgnoreCase);
        }

        public static string GetLegacyLocalBuiltInDirectory(BuildTarget target)
        {
            return NormalizeAbsolutePath(Path.Combine(
                Application.streamingAssetsPath,
                "aa",
                target.ToString()));
        }

        public static string ResolveBuiltPlayerStreamingAssetsAaDirectory(string outputPath)
        {
            string normalized = NormalizeAbsolutePath(outputPath);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return string.Empty;
            }

            if (Directory.Exists(normalized) && normalized.EndsWith("_Data", StringComparison.OrdinalIgnoreCase))
            {
                return NormalizeAbsolutePath(Path.Combine(normalized, "StreamingAssets", "aa"));
            }

            if (File.Exists(normalized))
            {
                string directory = Path.GetDirectoryName(normalized) ?? string.Empty;
                string productName = Path.GetFileNameWithoutExtension(normalized);
                string dataRoot = Path.Combine(directory, productName + "_Data");
                return NormalizeAbsolutePath(Path.Combine(dataRoot, "StreamingAssets", "aa"));
            }

            string extension = Path.GetExtension(normalized);
            if (!string.IsNullOrWhiteSpace(extension))
            {
                string directory = Path.GetDirectoryName(normalized) ?? string.Empty;
                string productName = Path.GetFileNameWithoutExtension(normalized);
                return NormalizeAbsolutePath(Path.Combine(directory, productName + "_Data", "StreamingAssets", "aa"));
            }

            string[] dataDirectories = Directory.Exists(normalized)
                ? Directory.GetDirectories(normalized, "*_Data", SearchOption.TopDirectoryOnly)
                : Array.Empty<string>();
            if (dataDirectories.Length > 0)
            {
                return NormalizeAbsolutePath(Path.Combine(dataDirectories[0], "StreamingAssets", "aa"));
            }

            return string.Empty;
        }

        public static bool IsSafeLegacyLocalBuiltInDirectory(string directory, BuildTarget target)
        {
            string normalized = NormalizeAbsolutePath(directory);
            string expected = GetLegacyLocalBuiltInDirectory(target);
            return !string.IsNullOrWhiteSpace(normalized) &&
                   string.Equals(normalized, expected, StringComparison.OrdinalIgnoreCase);
        }

        public static string ExpandPathTokens(string value, BuildTarget target)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string projectRoot = ProjectRoot;
            string assetsPath = Application.dataPath.Replace('\\', '/');
            string streamingAssets = Application.streamingAssetsPath.Replace('\\', '/');
            string buildTarget = target.ToString();

            return value
                .Replace("\\", "/")
                .Replace("[ProjectRoot]", projectRoot)
                .Replace("{ProjectRoot}", projectRoot)
                .Replace("[Assets]", assetsPath)
                .Replace("[AssetsPath]", assetsPath)
                .Replace("{Assets}", assetsPath)
                .Replace("[StreamingAssets]", streamingAssets)
                .Replace("{StreamingAssets}", streamingAssets)
                .Replace("[BuildTarget]", buildTarget)
                .Replace("{BuildTarget}", buildTarget)
                .Replace("[ProductName]", PlayerSettings.productName)
                .Replace("{ProductName}", PlayerSettings.productName)
                .Trim();
        }

        public static string ToFileUri(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            string expanded = path.Replace('\\', '/').Trim();
            if (expanded.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                return expanded;
            }

            return new Uri(Path.GetFullPath(expanded).Replace('\\', '/')).AbsoluteUri;
        }

        public static string AppendPathOrUrl(string basePathOrUrl, string fileName)
        {
            if (string.IsNullOrWhiteSpace(basePathOrUrl))
            {
                return fileName;
            }

            return basePathOrUrl.Trim().TrimEnd('/') + "/" + fileName.TrimStart('/');
        }

        public static string NormalizeAbsolutePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            return Path.GetFullPath(path.Replace('\\', '/')).Replace('\\', '/').TrimEnd('/');
        }

        public static bool LooksLikeLocalPath(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string trimmed = value.Trim();
            if (trimmed.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return Path.IsPathRooted(trimmed) ||
                   trimmed.StartsWith("./", StringComparison.Ordinal) ||
                   trimmed.StartsWith("../", StringComparison.Ordinal);
        }

        private static string ProjectRoot
        {
            get
            {
                DirectoryInfo parent = Directory.GetParent(Application.dataPath);
                return (parent == null ? Application.dataPath : parent.FullName).Replace('\\', '/');
            }
        }

        private static string GetAddressablesBuildPath()
        {
            Type addressablesType = Type.GetType(
                "UnityEngine.AddressableAssets.Addressables, Unity.Addressables");
            PropertyInfo property = addressablesType?.GetProperty(
                "BuildPath",
                BindingFlags.Public | BindingFlags.Static);
            return property?.GetValue(null, null) as string;
        }

        private static string GetAddressablesPlatformFolder(BuildTarget target)
        {
            switch (target)
            {
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    return "Windows";
                case BuildTarget.StandaloneOSX:
                    return "OSX";
                case BuildTarget.StandaloneLinux64:
                    return "Linux";
                case BuildTarget.Android:
                    return "Android";
                case BuildTarget.iOS:
                    return "iOS";
                case BuildTarget.WebGL:
                    return "WebGL";
                default:
                    return target.ToString();
            }
        }
    }

    public sealed class AAHotUpdatePublishValidationReport
    {
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public int ManifestCount;
        public int CatalogJsonCount;
        public int CatalogHashCount;
        public int SettingsJsonCount;
        public int BundleCount;
        public int MetaFileCount;

        public bool IsValid => Errors.Count == 0;
    }

    public sealed class AAWorkflowPackagingStatus
    {
        public AAWorkflowMode Mode { get; private set; }
        public string ModeLabel { get; private set; }
        public string BadgeText { get; private set; }
        public string TargetLabel { get; private set; }
        public string ManifestDisplayPath { get; private set; }
        public string RemoteCatalogLabel { get; private set; }
        public string StreamingAssetsFallbackLabel { get; private set; }
        public string ResourcesFallbackLabel { get; private set; }
        public string RuntimeSettingsLabel { get; private set; }
        public string PackageHint { get; private set; }
        public bool IsRemoteHotUpdate { get; private set; }

        public static AAWorkflowPackagingStatus Build(AAWorkflowConfig config, BuildTarget target)
        {
            config = config ?? AAWorkflowConfig.CreateLocalBuiltInDefault();
            bool isRemote = config.Mode == AAWorkflowMode.RemoteHotUpdate;

            return new AAWorkflowPackagingStatus
            {
                Mode = config.Mode,
                ModeLabel = isRemote ? "远端热更 AA" : "本地内置 AA",
                BadgeText = isRemote ? "Player 将优先读取远端 Manifest" : "Player 将从包内 StreamingAssets 加载",
                TargetLabel = target.ToString(),
                ManifestDisplayPath = AAWorkflowPathUtility.BuildManifestDisplayPath(config, target),
                RemoteCatalogLabel = config.EnableRemoteCatalog ? "开启" : "关闭",
                StreamingAssetsFallbackLabel = config.AllowStreamingAssetsFallback ? "开启" : "关闭",
                ResourcesFallbackLabel = config.AllowResourcesFallback ? "开启" : "关闭",
                RuntimeSettingsLabel = config.WriteRuntimeSettings ? "构建时写入" : "手动维护",
                PackageHint = isRemote
                    ? "打包前执行“一键远端热更发布”，Player 会内置远端 Manifest 地址。"
                    : "打包前执行“一键本地内置构建”，Player 会随包带上 StreamingAssets/aa。",
                IsRemoteHotUpdate = isRemote
            };
        }
    }

    public sealed class AAHotUpdatePublishRunReport
    {
        public readonly List<string> Messages = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Errors = new List<string>();
        public int CopiedFiles;
        public string SourceDirectory;
        public string PublishDirectory;
        public string ManifestPathOrUrl;

        public bool Success => Errors.Count == 0;

        public void AddMessage(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                Messages.Add(message);
                Debug.Log("[AAWorkflowPublish] " + message);
            }
        }

        public void AddWarning(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                Warnings.Add(message);
                Debug.LogWarning("[AAWorkflowPublish] " + message);
            }
        }

        public void AddError(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                Errors.Add(message);
                Debug.LogError("[AAWorkflowPublish] " + message);
            }
        }
    }

    public static class AAHotUpdatePublishLogic
    {
        public const string ManifestFileName = AAWorkflowPathUtility.ManifestFileName;

        public static string ExpandPathTokens(string value, BuildTarget target)
        {
            return AAWorkflowPathUtility.ExpandPathTokens(value, target);
        }

        public static string GetDefaultSourceDirectory(BuildTarget target)
        {
            return ExpandPathTokens("[StreamingAssets]/aa", target);
        }

        public static string ToFileUri(string path)
        {
            return AAWorkflowPathUtility.ToFileUri(path);
        }

        public static string AppendPathOrUrl(string basePathOrUrl, string fileName)
        {
            return AAWorkflowPathUtility.AppendPathOrUrl(basePathOrUrl, fileName);
        }

        public static int CopyDirectory(
            string sourceDirectory,
            string destinationDirectory,
            bool cleanDestination,
            bool copyMetaFiles)
        {
            string source = NormalizeAbsolutePath(sourceDirectory);
            string destination = NormalizeAbsolutePath(destinationDirectory);

            if (!Directory.Exists(source))
            {
                throw new DirectoryNotFoundException("AA 源目录不存在：" + source);
            }

            if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("AA 源目录和目标目录相同：" + source);
            }

            if (cleanDestination && IsDangerousCleanDirectory(destination))
            {
                throw new InvalidOperationException("拒绝清空危险的目标目录：" + destination);
            }

            if (cleanDestination && Directory.Exists(destination))
            {
                foreach (string file in Directory.GetFiles(destination))
                {
                    File.Delete(file);
                }

                foreach (string directory in Directory.GetDirectories(destination))
                {
                    Directory.Delete(directory, true);
                }
            }

            Directory.CreateDirectory(destination);
            int copied = 0;
            foreach (string sourceFile in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                if (!copyMetaFiles && sourceFile.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string relative = sourceFile.Substring(source.Length).TrimStart('\\', '/');
                string destinationFile = Path.Combine(destination, relative);
                string destinationFolder = Path.GetDirectoryName(destinationFile);
                if (!string.IsNullOrEmpty(destinationFolder))
                {
                    Directory.CreateDirectory(destinationFolder);
                }

                File.Copy(sourceFile, destinationFile, true);
                copied++;
            }

            return copied;
        }

        public static bool AreSameDirectory(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            {
                return false;
            }

            return string.Equals(
                NormalizeAbsolutePath(left),
                NormalizeAbsolutePath(right),
                StringComparison.OrdinalIgnoreCase);
        }

        public static AAHotUpdatePublishValidationReport ValidatePublishDirectory(string publishDirectory)
        {
            return ValidatePublishDirectory(
                publishDirectory,
                requireCatalogHash: true,
                requireSettingsJson: false,
                warnAboutMetaFiles: true);
        }

        public static AAHotUpdatePublishValidationReport ValidatePublishDirectory(
            string publishDirectory,
            bool requireCatalogHash,
            bool requireSettingsJson)
        {
            return ValidatePublishDirectory(
                publishDirectory,
                requireCatalogHash,
                requireSettingsJson,
                warnAboutMetaFiles: true);
        }

        public static AAHotUpdatePublishValidationReport ValidatePublishDirectory(
            string publishDirectory,
            bool requireCatalogHash,
            bool requireSettingsJson,
            bool warnAboutMetaFiles)
        {
            AAHotUpdatePublishValidationReport report = new AAHotUpdatePublishValidationReport();
            string directory = NormalizeAbsolutePath(publishDirectory);

            if (!Directory.Exists(directory))
            {
                report.Errors.Add("发布目录不存在：" + directory);
                return report;
            }

            string[] files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
            report.ManifestCount = files.Count(file =>
                string.Equals(Path.GetFileName(file), ManifestFileName, StringComparison.OrdinalIgnoreCase));
            report.CatalogJsonCount = files.Count(file => IsCatalogJson(Path.GetFileName(file)));
            report.CatalogHashCount = files.Count(file =>
                Path.GetExtension(file).Equals(".hash", StringComparison.OrdinalIgnoreCase));
            report.SettingsJsonCount = files.Count(file =>
                string.Equals(Path.GetFileName(file), "settings.json", StringComparison.OrdinalIgnoreCase));
            report.BundleCount = files.Count(file =>
                Path.GetExtension(file).Equals(".bundle", StringComparison.OrdinalIgnoreCase));
            report.MetaFileCount = files.Count(file =>
                Path.GetExtension(file).Equals(".meta", StringComparison.OrdinalIgnoreCase));

            if (report.ManifestCount == 0)
            {
                report.Errors.Add("发布目录中没有找到 HotUpdateManifest.json。");
            }

            if (report.CatalogJsonCount == 0)
            {
                report.Errors.Add("发布目录中没有找到 Addressables catalog json。");
            }

            if (requireCatalogHash && report.CatalogHashCount == 0)
            {
                report.Errors.Add("发布目录中没有找到 Addressables catalog hash。");
            }

            if (requireSettingsJson && report.SettingsJsonCount == 0)
            {
                report.Errors.Add("发布目录中没有找到 Addressables settings.json。");
            }

            if (report.BundleCount == 0)
            {
                report.Errors.Add("发布目录中没有找到 Addressables .bundle 文件。");
            }

            if (warnAboutMetaFiles && report.MetaFileCount > 0)
            {
                report.Warnings.Add("发布目录中包含 Unity .meta 文件，Player 运行时不需要这些文件。");
            }

            return report;
        }

        public static AddressablesPlayerBuildResult BuildAddressablesPlayerContent()
        {
            using (new AddressablesBuildReportScope(clearBuildReportList: true))
            {
                AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
                return result;
            }
        }

        private static string NormalizeAbsolutePath(string path)
        {
            return AAWorkflowPathUtility.NormalizeAbsolutePath(path);
        }

        private static bool IsCatalogJson(string fileName)
        {
            if (string.IsNullOrEmpty(fileName) ||
                !fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return fileName.StartsWith("catalog", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsDangerousCleanDirectory(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                return true;
            }

            string normalized = NormalizeAbsolutePath(directory);
            string root = Path.GetPathRoot(normalized)?.Replace('\\', '/').TrimEnd('/') ?? string.Empty;
            if (string.Equals(normalized, root, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string projectRoot = ProjectRoot.TrimEnd('/');
            if (string.Equals(normalized, projectRoot, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return normalized.Length <= root.Length + 3;
        }

        private static string ProjectRoot
        {
            get
            {
                DirectoryInfo parent = Directory.GetParent(Application.dataPath);
                return (parent == null ? Application.dataPath : parent.FullName).Replace('\\', '/');
            }
        }

    }

    public static class AAWorkflowValidator
    {
        public static AAHotUpdatePublishValidationReport ValidatePublishDirectory(
            string publishDirectory,
            string hotUpdateDllBytesPath = null)
        {
            return ValidatePublishDirectory(
                publishDirectory,
                hotUpdateDllBytesPath,
                requireCatalogHash: true,
                requireSettingsJson: false,
                warnAboutMetaFiles: true);
        }

        public static AAHotUpdatePublishValidationReport ValidatePublishDirectory(
            string publishDirectory,
            string hotUpdateDllBytesPath,
            bool requireCatalogHash,
            bool requireSettingsJson)
        {
            return ValidatePublishDirectory(
                publishDirectory,
                hotUpdateDllBytesPath,
                requireCatalogHash,
                requireSettingsJson,
                warnAboutMetaFiles: true);
        }

        public static AAHotUpdatePublishValidationReport ValidatePublishDirectory(
            string publishDirectory,
            string hotUpdateDllBytesPath,
            bool requireCatalogHash,
            bool requireSettingsJson,
            bool warnAboutMetaFiles)
        {
            AAHotUpdatePublishValidationReport report =
                AAHotUpdatePublishLogic.ValidatePublishDirectory(
                    publishDirectory,
                    requireCatalogHash,
                    requireSettingsJson,
                    warnAboutMetaFiles);
            string directory = AAWorkflowPathUtility.NormalizeAbsolutePath(publishDirectory);
            string manifestPath = Path.Combine(directory, AAWorkflowPathUtility.ManifestFileName);
            if (!File.Exists(manifestPath))
            {
                return report;
            }

            HotUpdateManifest manifest = null;
            try
            {
                manifest = HotUpdateManifest.FromJson(File.ReadAllText(manifestPath, Encoding.UTF8));
            }
            catch (Exception exception)
            {
                report.Errors.Add("Manifest JSON 解析失败：" + exception.GetBaseException().Message);
            }

            if (manifest == null)
            {
                return report;
            }

            HotUpdateManifestValidationReport manifestReport = manifest.Validate();
            foreach (string warning in manifestReport.Warnings)
            {
                report.Warnings.Add("Manifest: " + warning);
            }

            foreach (string error in manifestReport.Errors)
            {
                report.Errors.Add("Manifest: " + error);
            }

            if (!string.IsNullOrWhiteSpace(hotUpdateDllBytesPath) && File.Exists(hotUpdateDllBytesPath))
            {
                string actual = ComputeSha256(hotUpdateDllBytesPath);
                string expected = (manifest.hotUpdateAssemblySha256 ?? string.Empty)
                    .Trim()
                    .Replace("-", string.Empty);
                if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                {
                    report.Errors.Add(
                        "HotUpdate.dll.bytes SHA256 不匹配。Expected=" + expected + ", Actual=" + actual);
                }
            }

            return report;
        }

        private static string ComputeSha256(string path)
        {
            using (SHA256 sha256 = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                byte[] hash = sha256.ComputeHash(stream);
                StringBuilder builder = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                {
                    builder.Append(hash[i].ToString("x2"));
                }

                return builder.ToString();
            }
        }
    }

    public sealed class AAGroupPathStatus
    {
        public string GroupName;
        public bool HasBundledSchema;
        public bool Included;
        public string BuildPathVariable;
        public string LoadPathVariable;
        public string EvaluatedBuildPath;
        public string EvaluatedLoadPath;
    }

    public static class AAAddressablesConfigurator
    {
        public static bool TryApply(
            AAWorkflowConfig config,
            BuildTarget target,
            AAHotUpdatePublishRunReport report)
        {
            if (config == null)
            {
                report?.AddError("AA 工作流配置为空。");
                return false;
            }

            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                report?.AddError("没有找到 AddressableAssetSettings。");
                return false;
            }

            if (!config.ApplyAddressablesProfile)
            {
                settings.BuildRemoteCatalog = config.EnableRemoteCatalog;
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
                report?.AddMessage("已跳过 Addressables Profile 切换，仅更新 Build Remote Catalog。");
                return true;
            }

            string profileName = string.IsNullOrWhiteSpace(config.AddressablesProfileName)
                ? settings.profileSettings.GetProfileName(settings.activeProfileId)
                : config.AddressablesProfileName.Trim();
            if (string.IsNullOrWhiteSpace(profileName))
            {
                profileName = config.Mode == AAWorkflowMode.LocalBuiltIn
                    ? "Stellar Local Built-in"
                    : "Stellar Remote HotUpdate";
            }

            string profileId = settings.profileSettings.GetProfileId(profileName);
            if (string.IsNullOrEmpty(profileId) && config.CreateAddressablesProfileIfMissing)
            {
                profileId = settings.profileSettings.AddProfile(profileName, settings.activeProfileId);
                report?.AddMessage("已创建 Addressables Profile：" + profileName);
            }

            if (string.IsNullOrEmpty(profileId))
            {
                report?.AddError("没有找到 Addressables Profile：" + profileName);
                return false;
            }

            settings.activeProfileId = profileId;
            EnsureProfileVariable(settings, AddressableAssetSettings.kLocalBuildPath,
                AddressableAssetSettings.kLocalBuildPathValue);
            EnsureProfileVariable(settings, AddressableAssetSettings.kLocalLoadPath,
                AddressableAssetSettings.kLocalLoadPathValue);
            EnsureProfileVariable(settings, AddressableAssetSettings.kRemoteBuildPath,
                AAWorkflowPathUtility.BuildRemoteBuildPath(config, target));
            EnsureProfileVariable(settings, AddressableAssetSettings.kRemoteLoadPath,
                AAWorkflowPathUtility.BuildRemoteLoadPath(config, target));

            settings.profileSettings.SetValue(
                profileId,
                AddressableAssetSettings.kLocalBuildPath,
                AddressableAssetSettings.kLocalBuildPathValue);
            settings.profileSettings.SetValue(
                profileId,
                AddressableAssetSettings.kLocalLoadPath,
                AddressableAssetSettings.kLocalLoadPathValue);
            settings.profileSettings.SetValue(
                profileId,
                AddressableAssetSettings.kRemoteBuildPath,
                AAWorkflowPathUtility.BuildRemoteBuildPath(config, target));
            settings.profileSettings.SetValue(
                profileId,
                AddressableAssetSettings.kRemoteLoadPath,
                AAWorkflowPathUtility.BuildRemoteLoadPath(config, target));

            settings.BuildRemoteCatalog = config.EnableRemoteCatalog;
            ConfigureBundledGroups(config, settings);
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.ProfileModified, null, true, true);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            report?.AddMessage("已应用 Addressables 配置：" + profileName);
            return true;
        }

        public static List<AAGroupPathStatus> GetGroupPathStatuses(AAWorkflowConfig config)
        {
            List<AAGroupPathStatus> statuses = new List<AAGroupPathStatus>();
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null || settings.groups == null)
            {
                return statuses;
            }

            foreach (AddressableAssetGroup group in settings.groups)
            {
                if (group == null)
                {
                    continue;
                }

                BundledAssetGroupSchema schema = group.GetSchema<BundledAssetGroupSchema>();
                bool included = ShouldIncludeGroup(config, group, schema);
                statuses.Add(new AAGroupPathStatus
                {
                    GroupName = group.Name,
                    HasBundledSchema = schema != null,
                    Included = included,
                    BuildPathVariable = schema != null ? schema.BuildPath.GetName(settings) : "",
                    LoadPathVariable = schema != null ? schema.LoadPath.GetName(settings) : "",
                    EvaluatedBuildPath = schema != null ? schema.BuildPath.GetValue(settings) : "",
                    EvaluatedLoadPath = schema != null ? schema.LoadPath.GetValue(settings) : ""
                });
            }

            return statuses;
        }

        private static void ConfigureBundledGroups(AAWorkflowConfig config, AddressableAssetSettings settings)
        {
            if (!config.ConfigureBundledGroups || settings.groups == null)
            {
                return;
            }

            foreach (AddressableAssetGroup group in settings.groups)
            {
                if (group == null)
                {
                    continue;
                }

                BundledAssetGroupSchema schema = group.GetSchema<BundledAssetGroupSchema>();
                if (!ShouldIncludeGroup(config, group, schema))
                {
                    continue;
                }

                if (config.Mode == AAWorkflowMode.LocalBuiltIn)
                {
                    schema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
                    schema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
                }
                else
                {
                    schema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteBuildPath);
                    schema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteLoadPath);
                }

                EditorUtility.SetDirty(group);
            }
        }

        private static bool ShouldIncludeGroup(
            AAWorkflowConfig config,
            AddressableAssetGroup group,
            BundledAssetGroupSchema schema)
        {
            if (group == null || schema == null)
            {
                return false;
            }

            if (string.Equals(group.Name, "Built In Data", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (config == null || config.IncludedGroupNames == null || config.IncludedGroupNames.Count == 0)
            {
                return true;
            }

            return config.IncludedGroupNames.Contains(group.Name);
        }

        private static void EnsureProfileVariable(
            AddressableAssetSettings settings,
            string variableName,
            string defaultValue)
        {
            if (settings.profileSettings.GetProfileDataByName(variableName) == null)
            {
                settings.profileSettings.CreateValue(variableName, defaultValue);
            }
        }
    }

    public static class AAWorkflowRuntimeSettingsWriter
    {
        public static bool TryWrite(AAWorkflowConfig config, BuildTarget target, out string message)
        {
            message = string.Empty;
            if (config == null)
            {
                message = "AA 工作流配置为空。";
                return false;
            }

            HotUpdateSettings settings = LoadOrCreateRuntimeSettingsAsset();
            if (settings == null)
            {
                message = "无法加载或创建 HotUpdateSettings 资源。";
                return false;
            }

            string manifestPathOrUrl = AAWorkflowPathUtility.BuildRuntimeManifestPathOrUrl(config, target);
            SerializedObject serialized = new SerializedObject(settings);
            SetString(serialized, "hotUpdateManifestPathOrUrl", manifestPathOrUrl);
            SetBool(serialized, "hotUpdateManifestFallbackToStreamingAssets", config.AllowStreamingAssetsFallback);
            SetBool(serialized, "hotUpdateManifestFallbackToResources", config.AllowResourcesFallback);
            SetBool(serialized, "addressablesUpdateCatalogsOnCheck", config.Mode == AAWorkflowMode.RemoteHotUpdate);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            message = config.Mode == AAWorkflowMode.LocalBuiltIn
                ? "已写入本地内置 AA 运行时设置：Manifest URL 留空，启用 StreamingAssets。"
                : "已写入远端热更 AA Manifest 地址：" + manifestPathOrUrl;
            return true;
        }

        private static void SetString(SerializedObject serialized, string propertyName, string value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.stringValue = value ?? string.Empty;
            }
        }

        private static void SetBool(SerializedObject serialized, string propertyName, bool value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.boolValue = value;
            }
        }

        private static HotUpdateSettings LoadOrCreateRuntimeSettingsAsset()
        {
            const string assetPath = AAWorkflowWorkspaceInitializer.HotUpdateSettingsAssetPath;
            HotUpdateSettings settings = AssetDatabase.LoadAssetAtPath<HotUpdateSettings>(assetPath);
            if (settings != null)
            {
                return settings;
            }

            string[] guids = AssetDatabase.FindAssets("t:HotUpdateSettings");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/Resources/", StringComparison.OrdinalIgnoreCase))
                {
                    settings = AssetDatabase.LoadAssetAtPath<HotUpdateSettings>(path);
                    if (settings != null)
                    {
                        return settings;
                    }
                }
            }

            Directory.CreateDirectory("Assets/Resources");
            settings = ScriptableObject.CreateInstance<HotUpdateSettings>();
            AssetDatabase.CreateAsset(settings, assetPath);
            AssetDatabase.SaveAssets();
            return settings;
        }
    }

    public static class AAWorkflowPublishService
    {
        public static AAHotUpdatePublishRunReport BuildLocalBuiltIn(AAWorkflowConfig config, BuildTarget target)
        {
            AAHotUpdatePublishRunReport report = CreateReport(config, target);
            try
            {
                if (!AAAddressablesConfigurator.TryApply(config, target, report))
                {
                    return report;
                }

                if (!ExportHybridClr(target, report))
                {
                    return report;
                }

                if (!BuildAddressables(report))
                {
                    return report;
                }

                CopyLocalBuildToStreamingAssets(config, target, report);

                if (config.WriteRuntimeSettings && !WriteRuntimeSettings(config, target, report))
                {
                    return report;
                }

                AddValidation(report, AAWorkflowValidator.ValidatePublishDirectory(
                    AAWorkflowPathUtility.GetPublishDirectory(config, target),
                    GetHotUpdateDllBytesPath(),
                    requireCatalogHash: false,
                    requireSettingsJson: true,
                    warnAboutMetaFiles: false));
            }
            catch (Exception exception)
            {
                report.AddError(exception.GetBaseException().Message);
            }

            return report;
        }

        public static AAHotUpdatePublishRunReport PublishRemoteHotUpdate(AAWorkflowConfig config, BuildTarget target)
        {
            AAHotUpdatePublishRunReport report = CreateReport(config, target);
            try
            {
                if (!AAAddressablesConfigurator.TryApply(config, target, report))
                {
                    return report;
                }

                if (!ExportHybridClr(target, report))
                {
                    return report;
                }

                if (!BuildAddressables(report))
                {
                    return report;
                }

                string source = AAWorkflowPathUtility.BuildRemoteBuildPath(config, target);
                string destination = AAWorkflowPathUtility.ExpandPathTokens(config.RemotePublishDirectory, target);
                CopyIfNeeded(report, source, destination, config.CleanPublishDirectory, config.CopyMetaFiles);
                if (!SyncManifestToPublishDirectory(report, destination))
                {
                    return report;
                }

                if (config.WriteRuntimeSettings && !WriteRuntimeSettings(config, target, report))
                {
                    return report;
                }

                AddValidation(report, AAWorkflowValidator.ValidatePublishDirectory(
                    destination,
                    GetHotUpdateDllBytesPath(),
                    requireCatalogHash: true,
                    requireSettingsJson: false,
                    warnAboutMetaFiles: true));
            }
            catch (Exception exception)
            {
                report.AddError(exception.GetBaseException().Message);
            }

            return report;
        }

        public static AAHotUpdatePublishRunReport CoverTestPlayerStreamingAssets(
            AAWorkflowConfig config,
            BuildTarget target)
        {
            AAHotUpdatePublishRunReport report = CreateReport(config, target);
            try
            {
                string source = AAWorkflowPathUtility.ExpandPathTokens(config.LocalOutputDirectory, target);
                string destination = AAWorkflowPathUtility.ResolveTestPlayerStreamingAssetsAaDirectory(config, target);
                report.SourceDirectory = source;
                report.PublishDirectory = destination;

                if (!AAWorkflowPathUtility.IsSafeTestPlayerStreamingAssetsAaDirectory(destination, target))
                {
                    report.AddError("测试 Player 目录无效。请选择已打包 Player 根目录，工具只允许覆盖 *_Data/StreamingAssets/aa。");
                    return report;
                }

                CopyIfNeeded(report, source, destination, config.CleanPublishDirectory, config.CopyMetaFiles);
                AddValidation(report, AAWorkflowValidator.ValidatePublishDirectory(
                    destination,
                    GetHotUpdateDllBytesPath(),
                    requireCatalogHash: false,
                    requireSettingsJson: true,
                    warnAboutMetaFiles: false));
            }
            catch (Exception exception)
            {
                report.AddError(exception.GetBaseException().Message);
            }

            return report;
        }

        private static AAHotUpdatePublishRunReport CreateReport(AAWorkflowConfig config, BuildTarget target)
        {
            AAHotUpdatePublishRunReport report = new AAHotUpdatePublishRunReport();
            report.SourceDirectory = config != null && config.Mode == AAWorkflowMode.RemoteHotUpdate
                ? AAWorkflowPathUtility.BuildRemoteBuildPath(config, target)
                : AAWorkflowPathUtility.ExpandPathTokens(config?.LocalOutputDirectory, target);
            report.PublishDirectory = AAWorkflowPathUtility.GetPublishDirectory(config, target);
            report.ManifestPathOrUrl = AAWorkflowPathUtility.BuildManifestDisplayPath(config, target);
            return report;
        }

        private static bool ExportHybridClr(BuildTarget target, AAHotUpdatePublishRunReport report)
        {
            HybridCLRHotUpdateExportReport exportReport =
                HybridCLRHotUpdateAssetExporter.ExportGeneratedAssets(target);
            HybridCLRHotUpdateAssetExporter.LogReport(exportReport);
            foreach (string warning in exportReport.Warnings) report.AddWarning(warning);
            foreach (string error in exportReport.Errors) report.AddError(error);
            return exportReport.Success;
        }

        private static bool BuildAddressables(AAHotUpdatePublishRunReport report)
        {
            AddressablesPlayerBuildResult buildResult = AAHotUpdatePublishLogic.BuildAddressablesPlayerContent();
            if (buildResult != null && !string.IsNullOrEmpty(buildResult.Error))
            {
                report.AddError("Addressables 构建失败：" + buildResult.Error);
                return false;
            }

            report.AddMessage("Addressables 构建完成。");
            return true;
        }

        private static void CopyLocalBuildToStreamingAssets(
            AAWorkflowConfig config,
            BuildTarget target,
            AAHotUpdatePublishRunReport report)
        {
            string source = AAWorkflowPathUtility.GetAddressablesLocalBuildDirectory(target);
            string destination = AAWorkflowPathUtility.GetAddressablesLocalStreamingAssetsDirectory(target);
            CopyIfNeeded(report, source, destination, config.CleanPublishDirectory, config.CopyMetaFiles);
            CleanLegacyLocalBuiltInDirectory(target, report);
            report.AddMessage("Addressables 本地构建产物已同步到 StreamingAssets/aa。");
            report.PublishDirectory = AAWorkflowPathUtility.ExpandPathTokens(config.LocalOutputDirectory, target);
        }

        private static void CleanLegacyLocalBuiltInDirectory(
            BuildTarget target,
            AAHotUpdatePublishRunReport report)
        {
            string legacyDirectory = AAWorkflowPathUtility.GetLegacyLocalBuiltInDirectory(target);
            if (!Directory.Exists(legacyDirectory))
            {
                return;
            }

            if (!AAWorkflowPathUtility.IsSafeLegacyLocalBuiltInDirectory(legacyDirectory, target))
            {
                report.AddWarning("跳过旧本地 AA 目录清理，路径未通过安全检查：" + legacyDirectory);
                return;
            }

            Directory.Delete(legacyDirectory, true);
            string metaPath = legacyDirectory + ".meta";
            if (File.Exists(metaPath))
            {
                File.Delete(metaPath);
            }

            AssetDatabase.Refresh();
            report.AddMessage("已清理旧版本地 AA 目录：" + legacyDirectory);
        }

        private static void CopyIfNeeded(
            AAHotUpdatePublishRunReport report,
            string source,
            string destination,
            bool cleanDestination,
            bool copyMetaFiles)
        {
            report.SourceDirectory = source;
            report.PublishDirectory = destination;
            if (AAHotUpdatePublishLogic.AreSameDirectory(source, destination))
            {
                report.AddMessage("AA 源目录和目标目录相同，已跳过复制。");
                return;
            }

            report.CopiedFiles = AAHotUpdatePublishLogic.CopyDirectory(
                source,
                destination,
                cleanDestination,
                copyMetaFiles);
            report.AddMessage("已复制 AA 文件数量：" + report.CopiedFiles);
        }

        public static bool SyncManifestToPublishDirectory(
            string sourceManifestPath,
            string publishDirectory,
            out string message)
        {
            message = string.Empty;
            if (string.IsNullOrWhiteSpace(sourceManifestPath) || !File.Exists(sourceManifestPath))
            {
                message = "Manifest 源文件不存在：" + sourceManifestPath;
                return false;
            }

            if (string.IsNullOrWhiteSpace(publishDirectory))
            {
                message = "发布目录为空，无法同步 Manifest。";
                return false;
            }

            Directory.CreateDirectory(publishDirectory);
            string destinationPath = Path.Combine(publishDirectory, AAWorkflowPathUtility.ManifestFileName);
            File.Copy(sourceManifestPath, destinationPath, true);
            message = "已同步 Manifest 到发布目录：" + destinationPath.Replace('\\', '/');
            return true;
        }

        private static bool SyncManifestToPublishDirectory(
            AAHotUpdatePublishRunReport report,
            string publishDirectory)
        {
            string sourceManifestPath = AAWorkflowPathUtility.GetProjectStreamingAssetsManifestPath();
            if (SyncManifestToPublishDirectory(sourceManifestPath, publishDirectory, out string message))
            {
                report.AddMessage(message);
                return true;
            }

            report.AddError(message);
            return false;
        }

        private static bool WriteRuntimeSettings(
            AAWorkflowConfig config,
            BuildTarget target,
            AAHotUpdatePublishRunReport report)
        {
            if (AAWorkflowRuntimeSettingsWriter.TryWrite(config, target, out string message))
            {
                report.AddMessage(message);
                return true;
            }

            report.AddError(message);
            return false;
        }

        private static void AddValidation(
            AAHotUpdatePublishRunReport report,
            AAHotUpdatePublishValidationReport validation)
        {
            foreach (string warning in validation.Warnings) report.AddWarning(warning);
            foreach (string error in validation.Errors) report.AddError(error);
            if (validation.IsValid)
            {
                report.AddMessage("目录校验通过。");
            }
        }

        private static string GetHotUpdateDllBytesPath()
        {
            return Path.Combine(Application.dataPath, "GameHotUpdate", "Code", "HotUpdate.dll.bytes");
        }
    }

    [StellarTool("AA 配置与发布", "资源管理", 1)]
    public sealed class AAWorkflowPublishHubModule : ToolModule
    {
        private static readonly string[] TabNames =
        {
            "配置列表",
            "本地内置 AA",
            "远端热更 AA",
            "校验与诊断"
        };

        private Vector2 _scroll;
        private int _tabIndex;
        private string _diagnosticDirectory = "";
        private AAHotUpdatePublishRunReport _lastRunReport;
        private AAHotUpdatePublishValidationReport _lastValidationReport;
        private bool _showConfigAdvanced;
        private bool _showLocalAdvanced;
        private bool _showRemoteAdvanced;
        private bool _showLocalManualActions;
        private bool _showRemoteManualActions;
        private bool _showInlineDiagnostics;
        private bool _showGroupPathDetails;
        private bool _workspaceInitializationQueued;
        private bool _workspaceInitializationRunning;
        private EditorApplication.CallbackFunction _pendingWorkspaceInitializationAction;

        public override string Icon => "d_Folder Icon";
        public override string Description => "管理本地内置 AA 与远端热更 AA 配置，构建、发布、覆盖测试 Player 并诊断 Manifest。";

        public override void OnEnable()
        {
            AAWorkflowConfigStore.Reload();
        }

        public override void OnDisable()
        {
            if (_pendingWorkspaceInitializationAction != null)
            {
                EditorApplication.delayCall -= _pendingWorkspaceInitializationAction;
                _pendingWorkspaceInitializationAction = null;
            }

            _workspaceInitializationQueued = false;
            _workspaceInitializationRunning = false;
        }

        public override void OnGUI()
        {
            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            AAWorkflowWorkspaceStatus workspaceStatus = AAWorkflowWorkspaceInitializer.Evaluate(target);

            if (!workspaceStatus.IsReady)
            {
                DrawInitializationHeader();
                DrawWorkspaceInitialization(target, workspaceStatus);
                DrawLastReports();
                return;
            }

            AAWorkflowConfigSet configSet = AAWorkflowConfigStore.ConfigSet;
            configSet.EnsureDefaults();
            SyncSelectedConfigToTab(configSet);
            AAWorkflowConfig config = configSet.SelectedConfig;

            DrawHeader(configSet, config, target);

            AAWorkflowConfig localConfig = configSet.GetFirstConfig(AAWorkflowMode.LocalBuiltIn);
            AAWorkflowConfig remoteConfig = configSet.GetFirstConfig(AAWorkflowMode.RemoteHotUpdate);
            DrawTabBar();
            GUILayout.Space(6);

            bool reportsInSidebar = _tabIndex == 1 || _tabIndex == 2;
            switch (_tabIndex)
            {
                case 0:
                    DrawConfigList(configSet, config, target);
                    break;
                case 1:
                    DrawLocalBuiltIn(localConfig, target);
                    break;
                case 2:
                    DrawRemoteHotUpdate(remoteConfig, target);
                    break;
                case 3:
                    DrawDiagnostics(config, target);
                    break;
            }

            if (!reportsInSidebar)
            {
                DrawLastReports();
            }
        }

        private static void DrawInitializationHeader()
        {
            using (new GUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.MinHeight(64), GUILayout.ExpandWidth(true)))
            {
                EditorGUILayout.LabelField("AA 配置与发布", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    "第一次接入前，先初始化一次热更工作区。初始化完成后再进入完整的 AA 配置、发布和诊断面板。",
                    EditorStyles.miniLabel);
            }

            GUILayout.Space(6);
        }

        private void DrawWorkspaceInitialization(
            BuildTarget target,
            AAWorkflowWorkspaceStatus workspaceStatus)
        {
            Section("初始化热更工作区");
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.HelpBox(
                    "第一次接入热更时，先初始化一次工作区。这个步骤会创建 Addressables Settings、AAWorkflowConfigSet、默认 Local/Remote Profile，并写入默认运行时热更配置。初始化完成后，才会进入正常的 AA 配置与发布面板。",
                    MessageType.Info);

                if (workspaceStatus.MissingItems.Count > 0)
                {
                    string missing = string.Join("\n- ", workspaceStatus.MissingItems);
                    EditorGUILayout.HelpBox("当前缺少：\n- " + missing, MessageType.Warning);
                }

                if (_workspaceInitializationQueued || _workspaceInitializationRunning)
                {
                    EditorGUILayout.HelpBox(
                        _workspaceInitializationRunning
                            ? "热更工作区初始化正在执行，请等待当前任务完成。"
                            : "热更工作区初始化已加入队列，稍后会在编辑器空闲时执行。",
                        MessageType.Info);
                }

                using (new GUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(_workspaceInitializationQueued || _workspaceInitializationRunning))
                    {
                        if (PrimaryButton("初始化热更工作区", GUILayout.Height(34)))
                        {
                            QueueWorkspaceInitialization(target);
                            GUI.FocusControl(null);
                        }
                    }

                    if (GUILayout.Button(new GUIContent("刷新状态", "重新检查当前工程的热更工作区状态。"), GUILayout.Height(30)))
                    {
                        AAWorkflowConfigStore.Reload();
                        GUI.FocusControl(null);
                    }
                }
            }

            Section("初始化后你会得到什么");
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("1. Addressables Settings", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("2. Local / Remote 两套默认 AA 工作流", EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.LabelField("3. HotUpdateSettings / ResKitRuntimeSettings 运行时资产", EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.LabelField("4. 进入完整的本地内置 AA / 远端热更 AA / 诊断页签", EditorStyles.wordWrappedMiniLabel);
            }
        }

        private void QueueWorkspaceInitialization(BuildTarget target)
        {
            if (_workspaceInitializationQueued || _workspaceInitializationRunning)
            {
                return;
            }

            _workspaceInitializationQueued = true;
            _pendingWorkspaceInitializationAction = () =>
            {
                EditorApplication.delayCall -= _pendingWorkspaceInitializationAction;
                _pendingWorkspaceInitializationAction = null;
                _workspaceInitializationQueued = false;
                RunWorkspaceInitialization(target);
            };

            EditorApplication.delayCall += _pendingWorkspaceInitializationAction;
            Window.ShowNotification(new GUIContent("热更工作区初始化已排队"));
            Window.Repaint();
        }

        private void RunWorkspaceInitialization(BuildTarget target)
        {
            _workspaceInitializationRunning = true;
            try
            {
                if (AAWorkflowWorkspaceInitializer.TryInitialize(target, out List<string> messages, out List<string> errors))
                {
                    _lastRunReport = new AAHotUpdatePublishRunReport();
                    foreach (string message in messages)
                    {
                        _lastRunReport.AddMessage(message);
                    }

                    Window.ShowNotification(new GUIContent("热更工作区初始化完成"));
                }
                else
                {
                    _lastRunReport = new AAHotUpdatePublishRunReport();
                    foreach (string message in messages)
                    {
                        _lastRunReport.AddMessage(message);
                    }

                    foreach (string error in errors)
                    {
                        _lastRunReport.AddError(error);
                    }

                    Window.ShowNotification(new GUIContent("热更工作区初始化失败"));
                }
            }
            catch (Exception exception)
            {
                _lastRunReport = new AAHotUpdatePublishRunReport();
                _lastRunReport.AddError(exception.GetBaseException().Message);
                Window.ShowNotification(new GUIContent("热更工作区初始化失败"));
            }
            finally
            {
                AAWorkflowConfigStore.Reload();
                _workspaceInitializationRunning = false;
                Window.Repaint();
            }
        }

        private void SyncSelectedConfigToTab(AAWorkflowConfigSet configSet)
        {
            if (configSet == null)
            {
                return;
            }

            bool changed = false;
            switch (_tabIndex)
            {
                case 1:
                    changed = configSet.SelectFirstConfig(AAWorkflowMode.LocalBuiltIn);
                    break;
                case 2:
                    changed = configSet.SelectFirstConfig(AAWorkflowMode.RemoteHotUpdate);
                    break;
            }

            if (changed)
            {
                AAWorkflowConfigStore.Save();
            }
        }

        private void DrawHeader(AAWorkflowConfigSet configSet, AAWorkflowConfig config, BuildTarget target)
        {
            using (new GUILayout.HorizontalScope())
            {
                using (new GUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.MinHeight(64), GUILayout.ExpandWidth(true)))
                {
                    EditorGUILayout.LabelField("AA 配置与发布", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(
                        "构建、发布、校验本地内置 AA 与远端热更 AA",
                        EditorStyles.miniLabel);

                    EditorGUI.BeginChangeCheck();
                    string[] names = configSet.Configs.Select(item => item == null ? "AA 配置" : item.Name).ToArray();
                    int selected = EditorGUILayout.Popup(
                        new GUIContent("当前工作流", "选择当前要查看和执行的 AA 工作流配置。配置保存在项目资源中，团队可共享。"),
                        configSet.SelectedConfigIndex,
                        names);
                    if (selected != configSet.SelectedConfigIndex)
                    {
                        configSet.SelectedConfigIndex = selected;
                        GUI.FocusControl(null);
                    }

                    if (EditorGUI.EndChangeCheck())
                    {
                        AAWorkflowConfigStore.Save();
                    }
                }

                GUILayout.Space(8);
                DrawPackagingStatusBar(config, target);
            }

            GUILayout.Space(6);
        }

        private void DrawPackagingStatusBar(AAWorkflowConfig config, BuildTarget target)
        {
            AAWorkflowPackagingStatus status = AAWorkflowPackagingStatus.Build(config, target);

            using (new GUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(390), GUILayout.MinHeight(64)))
            {
                using (new GUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("当前状态", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField("Remote Catalog " + status.RemoteCatalogLabel, EditorStyles.miniLabel);
                }

                DrawCompactStatusRow("模式", status.ModeLabel + " / " + status.TargetLabel);
                DrawCompactStatusRow("Manifest", status.ManifestDisplayPath);
                DrawCompactStatusRow(
                    "兜底",
                    "StreamingAssets " + status.StreamingAssetsFallbackLabel + " / Resources " + status.ResourcesFallbackLabel);
            }
        }

        private static void DrawCompactStatusRow(string label, string value)
        {
            using (new GUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, EditorStyles.miniLabel, GUILayout.Width(54));
                EditorGUILayout.LabelField(new GUIContent(value, value), EditorStyles.miniLabel);
            }
        }

        private void DrawTabBar()
        {
            GUIStyle tabStyle = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold,
                fixedHeight = 30,
                margin = new RectOffset(2, 2, 0, 0)
            };

            using (new GUILayout.HorizontalScope())
            {
                for (int i = 0; i < TabNames.Length; i++)
                {
                    bool active = i == _tabIndex;
                    Color oldColor = GUI.backgroundColor;
                    GUI.backgroundColor = active ? new Color(0.28f, 0.55f, 1f) : Color.white;
                    string label = active ? "● " + TabNames[i] : TabNames[i];
                    if (GUILayout.Button(new GUIContent(label), tabStyle, GUILayout.Height(active ? 34 : 30)))
                    {
                        _tabIndex = i;
                        GUI.FocusControl(null);
                    }

                    GUI.backgroundColor = oldColor;
                }
            }
        }

        private void DrawConfigList(AAWorkflowConfigSet configSet, AAWorkflowConfig config, BuildTarget target)
        {
            Section("配置管理");
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new GUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("新增本地内置", "创建一套用于 StreamingAssets 随包资源的 AA 配置。"), GUILayout.Height(24)))
                    {
                        configSet.Add(AAWorkflowMode.LocalBuiltIn);
                        AAWorkflowConfigStore.Save();
                    }

                    if (GUILayout.Button(new GUIContent("新增远端热更", "创建一套用于 D 盘、HTTP、CDN 等远端发布的 AA 配置。"), GUILayout.Height(24)))
                    {
                        configSet.Add(AAWorkflowMode.RemoteHotUpdate);
                        AAWorkflowConfigStore.Save();
                    }

                    if (GUILayout.Button(new GUIContent("复制当前", "复制当前配置，适合为不同目录、不同服务器快速派生。"), GUILayout.Height(24)))
                    {
                        configSet.DuplicateSelected();
                        AAWorkflowConfigStore.Save();
                    }

                    GUI.enabled = configSet.Configs.Count > 1;
                    if (GUILayout.Button(new GUIContent("删除当前", "删除当前配置。至少会保留一套配置。"), GUILayout.Height(24)))
                    {
                        if (EditorUtility.DisplayDialog("删除 AA 配置", "确定要删除当前 AA 配置吗？", "删除", "取消"))
                        {
                            configSet.DeleteSelected();
                            AAWorkflowConfigStore.Save();
                        }
                    }
                    GUI.enabled = true;
                }

                EditorGUILayout.HelpBox(
                    "配置资产保存在 Assets/StellarFramework/Editor/StellarToolsHub/Configs/AAWorkflowConfigSet.asset。当前选中项使用 EditorPrefs 记录，方便每个开发者保留自己的工作状态。",
                    MessageType.None);
            }

            DrawConfigSummary(config, target);
            _showConfigAdvanced = EditorGUILayout.Foldout(
                _showConfigAdvanced,
                new GUIContent("高级配置", "编辑配置名称、复制策略、Addressables Profile 等低频选项。"),
                true);
            if (_showConfigAdvanced)
            {
                DrawCommonFields(config, target);
                DrawAddressablesOptions(config, target);
            }
        }

        private void DrawLocalBuiltIn(AAWorkflowConfig config, BuildTarget target)
        {
            if (config == null)
            {
                EditorGUILayout.HelpBox("没有找到本地内置 AA 配置。", MessageType.Warning);
                return;
            }

            if (config.Mode != AAWorkflowMode.LocalBuiltIn)
            {
                EditorGUILayout.HelpBox("当前配置不是本地内置 AA。可以在配置列表中切换，或把当前配置模式改成本地内置。", MessageType.Warning);
            }

            DrawWorkflowColumns(
                () =>
                {
                    DrawLocalMainPanel(config, target);
                    DrawLocalFoldouts(config, target);
                    DrawInlineDiagnostics(config, target);
                },
                () => DrawWorkflowSidebar(config, target, includeRemoteActions: false));
        }

        private void DrawRemoteHotUpdate(AAWorkflowConfig config, BuildTarget target)
        {
            if (config == null)
            {
                EditorGUILayout.HelpBox("没有找到远端热更 AA 配置。", MessageType.Warning);
                return;
            }

            if (config.Mode != AAWorkflowMode.RemoteHotUpdate)
            {
                EditorGUILayout.HelpBox("当前配置不是远端热更 AA。可以在配置列表中切换，或把当前配置模式改成远端热更。", MessageType.Warning);
            }

            DrawWorkflowColumns(
                () =>
                {
                    DrawRemoteMainPanel(config, target);
                    DrawRemoteFoldouts(config, target);
                    DrawInlineDiagnostics(config, target);
                },
                () => DrawWorkflowSidebar(config, target, includeRemoteActions: true));
        }

        private void DrawWorkflowColumns(Action drawMain, Action drawSidebar)
        {
            using (new GUILayout.HorizontalScope())
            {
                using (new GUILayout.VerticalScope(GUILayout.MinWidth(0), GUILayout.ExpandWidth(true)))
                {
                    drawMain?.Invoke();
                }

                GUILayout.Space(8);

                using (new GUILayout.VerticalScope(GUILayout.Width(300)))
                {
                    drawSidebar?.Invoke();
                }
            }
        }

        private void DrawLocalMainPanel(AAWorkflowConfig config, BuildTarget target)
        {
            Section("本地内置 AA");
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUI.BeginChangeCheck();
                config.Mode = AAWorkflowMode.LocalBuiltIn;
                config.LocalOutputDirectory = DrawFolderTextField(
                    new GUIContent("本地 AA 输出目录", "本地内置 AA 构建输出目录。默认是 Assets/StreamingAssets/aa。Addressables 会在里面生成平台目录。"),
                    config.LocalOutputDirectory,
                    target);
                config.TestPlayerRootDirectory = DrawFolderTextField(
                    new GUIContent("测试 Player 根目录", "选择已经打出来的 Player 根目录，工具会自动定位 *_Data/StreamingAssets/aa。"),
                    config.TestPlayerRootDirectory,
                    target);
                if (EditorGUI.EndChangeCheck())
                {
                    AAWorkflowConfigStore.Save();
                }

                DrawInfoRow("编辑器输出", AAWorkflowPathUtility.ExpandPathTokens(config.LocalOutputDirectory, target));
                DrawInfoRow("测试覆盖目标", AAWorkflowPathUtility.ResolveTestPlayerStreamingAssetsAaDirectory(config, target));

                using (new GUILayout.HorizontalScope())
                {
                    if (PrimaryButton(new GUIContent(
                            "一键本地内置构建",
                            "自动完成：1. 写入本地内置 AA 配置；2. 导出热更 DLL 与 Manifest；3. 构建 Addressables 资源包；4. 同步到 StreamingAssets/aa；5. 写入 Player 运行时设置。"),
                            GUILayout.Height(36)))
                    {
                        RunLocalBuild(config, target);
                    }

                    if (GUILayout.Button(new GUIContent("覆盖测试 Player", "把编辑器中的 StreamingAssets/aa 整套复制到已打包 Player 的 *_Data/StreamingAssets/aa。"), GUILayout.Height(32)))
                    {
                        CoverTestPlayer(config, target);
                    }

                    if (GUILayout.Button(new GUIContent("校验当前目录", "校验本地内置 AA 输出目录。"), GUILayout.Height(32)))
                    {
                        ValidateWorkflowDirectory(config, target);
                    }
                }
            }
        }

        private void DrawRemoteMainPanel(AAWorkflowConfig config, BuildTarget target)
        {
            Section("远端热更 AA");
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUI.BeginChangeCheck();
                config.Mode = AAWorkflowMode.RemoteHotUpdate;
                config.RemotePublishDirectory = DrawFolderTextField(
                    new GUIContent("远端发布目录", "最终给 file://、HTTP 服务器或 CDN 使用的本机目录。"),
                    config.RemotePublishDirectory,
                    target);
                config.RemoteLoadPathOrUrl = EditorGUILayout.TextField(
                    new GUIContent("远端加载路径/URL", "Addressables Remote.LoadPath。留空时从发布目录推导 file:/// 地址，也可以填 https://...。"),
                    config.RemoteLoadPathOrUrl);
                if (EditorGUI.EndChangeCheck())
                {
                    AAWorkflowConfigStore.Save();
                }

                DrawInfoRow("Remote.LoadPath", AAWorkflowPathUtility.BuildRemoteLoadPath(config, target));
                DrawInfoRow("运行时 Manifest", AAWorkflowPathUtility.BuildRuntimeManifestPathOrUrl(config, target));

                using (new GUILayout.HorizontalScope())
                {
                    if (PrimaryButton(new GUIContent(
                            "一键远端热更发布",
                            "自动完成：1. 写入远端 AA 构建配置；2. 导出热更 DLL 与 Manifest；3. 构建 Addressables 资源包；4. 复制到远端发布目录；5. 写入 Player 运行时读取的 Manifest 地址。"),
                            GUILayout.Height(36)))
                    {
                        RunRemotePublish(config, target);
                    }

                    if (GUILayout.Button(new GUIContent("仅发布目录", "只把 Remote.BuildPath 的输出复制到远端发布目录，并做目录校验。"), GUILayout.Height(32)))
                    {
                        PublishRemoteOnly(config, target);
                    }

                    if (GUILayout.Button(new GUIContent("校验当前目录", "校验远端发布目录。"), GUILayout.Height(32)))
                    {
                        ValidateWorkflowDirectory(config, target);
                    }

                    if (GUILayout.Button(new GUIContent("打开远端目录", "打开远端发布目录。"), GUILayout.Height(32)))
                    {
                        EditorUtility.RevealInFinder(AAWorkflowPathUtility.ExpandPathTokens(config.RemotePublishDirectory, target));
                    }
                }
            }
        }

        private void DrawLocalFoldouts(AAWorkflowConfig config, BuildTarget target)
        {
            _showLocalManualActions = EditorGUILayout.Foldout(
                _showLocalManualActions,
                new GUIContent("手动步骤", "展开后可以单独执行导出、构建、校验等步骤。"),
                true);
            if (_showLocalManualActions)
            {
                using (new GUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    DrawSharedActionButtons(config, target, false);
                }
            }

            _showLocalAdvanced = EditorGUILayout.Foldout(
                _showLocalAdvanced,
                new GUIContent("高级设置", "一般保持默认。需要改 Profile、兜底策略或复制规则时再展开。"),
                true);
            if (_showLocalAdvanced)
            {
                using (new GUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    DrawCommonFields(config, target);
                    DrawLocalAdvancedFields(config);
                    DrawAddressablesOptions(config, target);
                }
            }
        }

        private void DrawRemoteFoldouts(AAWorkflowConfig config, BuildTarget target)
        {
            _showRemoteManualActions = EditorGUILayout.Foldout(
                _showRemoteManualActions,
                new GUIContent("手动步骤", "展开后可以单独执行导出、构建、发布目录、校验等步骤。"),
                true);
            if (_showRemoteManualActions)
            {
                using (new GUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    DrawSharedActionButtons(config, target, true);
                }
            }

            _showRemoteAdvanced = EditorGUILayout.Foldout(
                _showRemoteAdvanced,
                new GUIContent("高级设置", "一般保持默认。需要改 Profile、Manifest 覆盖地址或兜底策略时再展开。"),
                true);
            if (_showRemoteAdvanced)
            {
                using (new GUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    DrawCommonFields(config, target);
                    DrawRemoteAdvancedFields(config, target);
                    DrawAddressablesOptions(config, target);
                }
            }
        }

        private void DrawInlineDiagnostics(AAWorkflowConfig config, BuildTarget target)
        {
            _showInlineDiagnostics = EditorGUILayout.Foldout(
                _showInlineDiagnostics,
                new GUIContent("诊断工具", "展开后校验目录、打开 Addressables Groups 或查看路径明细。"),
                true);
            if (!_showInlineDiagnostics)
            {
                return;
            }

            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new GUILayout.HorizontalScope())
                {
                    _diagnosticDirectory = EditorGUILayout.TextField(
                        new GUIContent("指定目录", "要检查的 AA 输出或发布目录。留空时使用当前配置目录。"),
                        _diagnosticDirectory);
                    if (GUILayout.Button(new GUIContent("...", "选择要校验的目录。"), GUILayout.Width(32)))
                    {
                        string selected = EditorUtility.OpenFolderPanel("选择 AA 校验目录", _diagnosticDirectory, "");
                        if (!string.IsNullOrWhiteSpace(selected))
                        {
                            _diagnosticDirectory = selected.Replace('\\', '/');
                        }
                    }
                }

                using (new GUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("校验当前配置目录", "校验当前模式对应的输出或发布目录。"), GUILayout.Height(28)))
                    {
                        ValidateWorkflowDirectory(config, target);
                    }

                    if (GUILayout.Button(new GUIContent("校验指定目录", "校验上方指定目录。"), GUILayout.Height(28)))
                    {
                        string directory = string.IsNullOrWhiteSpace(_diagnosticDirectory)
                            ? AAWorkflowPathUtility.GetPublishDirectory(config, target)
                            : AAWorkflowPathUtility.ExpandPathTokens(_diagnosticDirectory, target);
                        ValidateDirectory(config, directory);
                    }

                    if (GUILayout.Button(new GUIContent("打开 AA Groups", "打开 Unity Addressables Groups 窗口。"), GUILayout.Height(28)))
                    {
                        EditorApplication.ExecuteMenuItem("Window/Asset Management/Addressables/Groups");
                    }
                }
            }
        }

        private void DrawWorkflowSidebar(AAWorkflowConfig config, BuildTarget target, bool includeRemoteActions)
        {
            DrawConfigSnapshot(config, target);
            DrawQuickLinks(config, target, includeRemoteActions);
            DrawLastReportsCompact();
        }

        private void DrawConfigSnapshot(AAWorkflowConfig config, BuildTarget target)
        {
            AAWorkflowPackagingStatus status = AAWorkflowPackagingStatus.Build(config, target);

            Section("当前配置摘要");
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawInfoRow("配置名称", config.Name, 84);
                DrawInfoRow("模式", status.ModeLabel, 84);
                DrawInfoRow("Manifest", status.ManifestDisplayPath, 84);
                DrawInfoRow("Catalog", status.RemoteCatalogLabel, 84);
                DrawInfoRow("兜底策略", "StreamingAssets " + status.StreamingAssetsFallbackLabel +
                                      " / Resources " + status.ResourcesFallbackLabel, 84);
            }
        }

        private void DrawQuickLinks(AAWorkflowConfig config, BuildTarget target, bool includeRemoteActions)
        {
            Section("快速入口");
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new GUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("打开输出", "打开当前配置对应的输出或发布目录。"), GUILayout.Height(26)))
                    {
                        EditorUtility.RevealInFinder(AAWorkflowPathUtility.GetPublishDirectory(config, target));
                    }

                    if (GUILayout.Button(new GUIContent("AA Groups", "打开 Unity Addressables Groups 窗口。"), GUILayout.Height(26)))
                    {
                        EditorApplication.ExecuteMenuItem("Window/Asset Management/Addressables/Groups");
                    }
                }

                using (new GUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("写入设置", "把当前工作流的 Manifest 来源、兜底策略和 catalog 检查开关写入 HotUpdateSettings。"), GUILayout.Height(26)))
                    {
                        WriteWorkflowRuntimeSettings(config, target);
                    }

                    GUI.enabled = includeRemoteActions;
                    if (GUILayout.Button(new GUIContent("远端目录", "打开远端发布目录。"), GUILayout.Height(26)))
                    {
                        EditorUtility.RevealInFinder(AAWorkflowPathUtility.ExpandPathTokens(config.RemotePublishDirectory, target));
                    }
                    GUI.enabled = true;
                }
            }
        }

        private void DrawLastReportsCompact()
        {
            Section("最近一次运行");
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (_lastRunReport == null && _lastValidationReport == null)
                {
                    EditorGUILayout.LabelField("暂无运行结果。", EditorStyles.miniLabel);
                    return;
                }

                _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MinHeight(82), GUILayout.MaxHeight(150));

                if (_lastRunReport != null)
                {
                    EditorGUILayout.LabelField(
                        _lastRunReport.Success ? "状态：成功" : "状态：失败",
                        _lastRunReport.Success ? EditorStyles.boldLabel : EditorStyles.label);
                    DrawInfoRow("文件数", _lastRunReport.CopiedFiles.ToString(), 60);
                    DrawInfoRow("发布目录", _lastRunReport.PublishDirectory, 60);
                    foreach (string message in _lastRunReport.Messages)
                    {
                        EditorGUILayout.LabelField(new GUIContent(message, message), EditorStyles.miniLabel);
                    }

                    foreach (string warning in _lastRunReport.Warnings)
                    {
                        EditorGUILayout.HelpBox(warning, MessageType.Warning);
                    }

                    foreach (string error in _lastRunReport.Errors)
                    {
                        EditorGUILayout.HelpBox(error, MessageType.Error);
                    }
                }

                if (_lastValidationReport != null)
                {
                    EditorGUILayout.LabelField(
                        $"Manifest={_lastValidationReport.ManifestCount}, Catalog={_lastValidationReport.CatalogJsonCount}, Bundle={_lastValidationReport.BundleCount}",
                        EditorStyles.miniLabel);
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private static void DrawInfoRow(string label, string value, float labelWidth = 112f)
        {
            using (new GUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(new GUIContent(label), GUILayout.Width(labelWidth));
                EditorGUILayout.SelectableLabel(
                    string.IsNullOrWhiteSpace(value) ? "-" : value,
                    EditorStyles.textField,
                    GUILayout.Height(18));
            }
        }

        private void DrawDiagnostics(AAWorkflowConfig config, BuildTarget target)
        {
            _showGroupPathDetails = EditorGUILayout.Foldout(
                _showGroupPathDetails,
                new GUIContent("高级：Addressables Group 路径明细", "用于排查每个 Group 当前指向 Local 还是 Remote。日常使用不需要展开。"),
                true);
            if (_showGroupPathDetails)
            {
                DrawGroupStatus(config);
            }

            Section("目录校验");
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.HelpBox(
                    "校验会检查 HotUpdateManifest.json、catalog json、catalog hash、bundle，并用当前工程中的 HotUpdate.dll.bytes 对 Manifest SHA256 做一致性检查。",
                    MessageType.None);
                using (new GUILayout.HorizontalScope())
                {
                    _diagnosticDirectory = EditorGUILayout.TextField(
                        new GUIContent("校验目录", "要检查的 AA 输出或发布目录。留空时使用当前配置的默认目录。"),
                        _diagnosticDirectory);
                    if (GUILayout.Button(new GUIContent("...", "选择要校验的目录。"), GUILayout.Width(32)))
                    {
                        string selected = EditorUtility.OpenFolderPanel("选择 AA 校验目录", _diagnosticDirectory, "");
                        if (!string.IsNullOrWhiteSpace(selected))
                        {
                            _diagnosticDirectory = selected.Replace('\\', '/');
                        }
                    }
                }

                using (new GUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("校验当前配置目录", "校验当前模式对应的本地输出目录或远端发布目录。"), GUILayout.Height(28)))
                    {
                        ValidateWorkflowDirectory(config, target);
                    }

                    if (GUILayout.Button(new GUIContent("校验指定目录", "校验上方指定目录。"), GUILayout.Height(28)))
                    {
                        string directory = string.IsNullOrWhiteSpace(_diagnosticDirectory)
                            ? AAWorkflowPathUtility.GetPublishDirectory(config, target)
                            : AAWorkflowPathUtility.ExpandPathTokens(_diagnosticDirectory, target);
                        ValidateDirectory(config, directory);
                    }
                }
            }

            Section("快捷入口");
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new GUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("打开本地输出", "打开当前配置本地输出目录。"), GUILayout.Height(24)))
                    {
                        EditorUtility.RevealInFinder(AAWorkflowPathUtility.ExpandPathTokens(config.LocalOutputDirectory, target));
                    }

                    if (GUILayout.Button(new GUIContent("打开远端目录", "打开远端发布目录。"), GUILayout.Height(24)))
                    {
                        EditorUtility.RevealInFinder(AAWorkflowPathUtility.ExpandPathTokens(config.RemotePublishDirectory, target));
                    }

                    if (GUILayout.Button(new GUIContent("打开 AA Groups", "打开 Unity Addressables Groups 窗口。"), GUILayout.Height(24)))
                    {
                        EditorApplication.ExecuteMenuItem("Window/Asset Management/Addressables/Groups");
                    }
                }
            }
        }

        private void DrawCommonFields(AAWorkflowConfig config, BuildTarget target)
        {
            Section("基础配置");
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUI.BeginChangeCheck();
                config.Name = EditorGUILayout.TextField(
                    new GUIContent("配置名称", "当前 AA 工作流配置的显示名称。"),
                    config.Name);
                config.Mode = (AAWorkflowMode)EditorGUILayout.EnumPopup(
                    new GUIContent("模式", "本地内置 AA 随包加载；远端热更 AA 从 file/http 等远端位置更新。"),
                    config.Mode);
                config.BuildTargetName = target.ToString();
                config.CleanPublishDirectory = EditorGUILayout.Toggle(
                    new GUIContent("复制前清空目标", "复制发布目录或覆盖测试 Player 前清空目标目录，避免旧 bundle 残留。"),
                    config.CleanPublishDirectory);
                config.CopyMetaFiles = EditorGUILayout.Toggle(
                    new GUIContent("复制 .meta 文件", "正式发布不需要 .meta。只有调试目录内容时才建议开启。"),
                    config.CopyMetaFiles);
                config.WriteRuntimeSettings = EditorGUILayout.Toggle(
                    new GUIContent("写入运行时设置", "把当前工作流的 Manifest 策略写入 Resources/HotUpdateSettings.asset。"),
                    config.WriteRuntimeSettings);
                if (EditorGUI.EndChangeCheck())
                {
                    config.Normalize();
                    AAWorkflowConfigStore.Save();
                }
            }
        }

        private void DrawConfigSummary(AAWorkflowConfig config, BuildTarget target)
        {
            Section("当前配置概览");
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(new GUIContent("配置名称"), new GUIContent(config.Name));
                EditorGUILayout.LabelField(new GUIContent("模式"), new GUIContent(ModeLabel(config.Mode)));
                EditorGUILayout.LabelField(
                    new GUIContent("输出/发布目录"),
                    new GUIContent(AAWorkflowPathUtility.GetPublishDirectory(config, target)));
                EditorGUILayout.LabelField(
                    new GUIContent("Manifest 来源"),
                    new GUIContent(AAWorkflowPathUtility.BuildManifestDisplayPath(config, target)));
                EditorGUILayout.HelpBox("新手只需要选择一套配置，然后去对应的“本地内置 AA”或“远端热更 AA”页签点一键按钮。", MessageType.Info);
            }
        }

        private void DrawLocalAdvancedFields(AAWorkflowConfig config)
        {
            Section("本地高级策略");
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUI.BeginChangeCheck();
                config.EnableRemoteCatalog = EditorGUILayout.Toggle(
                    new GUIContent("启用远端 Catalog", "本地内置模式默认关闭。只有混合测试远端 catalog 时才开启。"),
                    config.EnableRemoteCatalog);
                config.AllowStreamingAssetsFallback = EditorGUILayout.Toggle(
                    new GUIContent("允许 StreamingAssets Manifest", "本地内置模式应开启，运行时从包内 StreamingAssets/aa/HotUpdateManifest.json 读取，并兼容旧的 aa/<BuildTarget> 路径。"),
                    config.AllowStreamingAssetsFallback);
                config.AllowResourcesFallback = EditorGUILayout.Toggle(
                    new GUIContent("允许 Resources 兜底", "Manifest 缺失时是否允许回退到 ResKitRuntimeSettings 里的旧字段。"),
                    config.AllowResourcesFallback);
                if (EditorGUI.EndChangeCheck())
                {
                    AAWorkflowConfigStore.Save();
                }
            }
        }

        private void DrawRemoteAdvancedFields(AAWorkflowConfig config, BuildTarget target)
        {
            Section("远端高级策略");
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUI.BeginChangeCheck();
                config.EnableRemoteCatalog = EditorGUILayout.Toggle(
                    new GUIContent("启用远端 Catalog", "远端热更模式默认开启。旧包通过远端 catalog/hash 发现资源更新。"),
                    config.EnableRemoteCatalog);
                config.AllowStreamingAssetsFallback = EditorGUILayout.Toggle(
                    new GUIContent("开发期允许 StreamingAssets 兜底", "远端模式默认关闭，避免远端失败时误以为热更成功。开发临时调试才建议开启。"),
                    config.AllowStreamingAssetsFallback);
                config.AllowResourcesFallback = EditorGUILayout.Toggle(
                    new GUIContent("允许 Resources 兜底", "远端模式默认关闭。开启后 Manifest 失败时可能回退到包内旧配置。"),
                    config.AllowResourcesFallback);
                config.RemoteBuildDirectory = DrawFolderTextField(
                    new GUIContent("远端构建目录", "Addressables Remote.BuildPath。可以直接构建到 D 盘发布目录。"),
                    config.RemoteBuildDirectory,
                    target);
                config.ManifestPathOrUrl = EditorGUILayout.TextField(
                    new GUIContent("Manifest 覆盖地址", "可选。显式指定 HotUpdateManifest.json 地址；留空时从远端加载路径/URL 推导。"),
                    config.ManifestPathOrUrl);
                if (EditorGUI.EndChangeCheck())
                {
                    AAWorkflowConfigStore.Save();
                }

                EditorGUILayout.LabelField(
                    new GUIContent("Remote.BuildPath", "将写入 Addressables Profile 的远端构建路径。"),
                    new GUIContent(AAWorkflowPathUtility.BuildRemoteBuildPath(config, target)));
            }
        }

        private void DrawAddressablesOptions(AAWorkflowConfig config, BuildTarget target)
        {
            Section("Addressables 配置");
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUI.BeginChangeCheck();
                config.ApplyAddressablesProfile = EditorGUILayout.Toggle(
                    new GUIContent("应用 AA Profile", "执行构建/发布前切换或创建指定 Addressables Profile。"),
                    config.ApplyAddressablesProfile);
                config.AddressablesProfileName = EditorGUILayout.TextField(
                    new GUIContent("AA Profile 名称", "要切换到的 Addressables Profile 名称。留空时使用当前 Active Profile。"),
                    config.AddressablesProfileName);
                config.CreateAddressablesProfileIfMissing = EditorGUILayout.Toggle(
                    new GUIContent("缺失时创建 Profile", "Profile 不存在时，自动基于当前 Profile 创建。"),
                    config.CreateAddressablesProfileIfMissing);
                config.ConfigureBundledGroups = EditorGUILayout.Toggle(
                    new GUIContent("批量设置 Group 路径", "把 BundledAssetGroupSchema 批量切换到 Local 或 Remote BuildPath/LoadPath。"),
                    config.ConfigureBundledGroups);
                if (EditorGUI.EndChangeCheck())
                {
                    AAWorkflowConfigStore.Save();
                }

                using (new GUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("应用当前 AA 配置", "立即把当前配置写入 Addressables Profile、Remote/Local 路径和 Build Remote Catalog。"), GUILayout.Height(28)))
                    {
                        _lastRunReport = new AAHotUpdatePublishRunReport();
                        AAAddressablesConfigurator.TryApply(config, target, _lastRunReport);
                        Window.ShowNotification(new GUIContent(_lastRunReport.Success ? "AA 配置已应用" : "AA 配置失败"));
                    }

                    if (GUILayout.Button(new GUIContent("写入运行时设置", "把当前工作流的 Manifest 来源、兜底策略和 catalog 检查开关写入 HotUpdateSettings。"), GUILayout.Height(28)))
                    {
                        WriteWorkflowRuntimeSettings(config, target);
                    }
                }
            }
        }

        private void DrawGroupStatus(AAWorkflowConfig config)
        {
            Section("Addressables Group 明细");
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.HelpBox(
                    "这里用于排查每个 Addressables Group 当前指向本地还是远端。日常构建只需要使用本地/远端页的一键按钮。",
                    MessageType.None);
                List<AAGroupPathStatus> statuses = AAAddressablesConfigurator.GetGroupPathStatuses(config);
                if (statuses.Count == 0)
                {
                    EditorGUILayout.HelpBox("没有读取到 Addressables Group。", MessageType.Warning);
                    return;
                }

                foreach (AAGroupPathStatus status in statuses)
                {
                    using (new GUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        EditorGUILayout.LabelField(status.GroupName, EditorStyles.boldLabel);
                        if (!status.HasBundledSchema)
                        {
                            EditorGUILayout.LabelField(new GUIContent("类型"), new GUIContent("非 BundledAssetGroupSchema"));
                            continue;
                        }

                        EditorGUILayout.LabelField(new GUIContent("是否纳入批量设置"), new GUIContent(status.Included ? "是" : "否"));
                        EditorGUILayout.LabelField(new GUIContent("BuildPath"), new GUIContent(status.BuildPathVariable));
                        EditorGUILayout.LabelField(new GUIContent("LoadPath"), new GUIContent(status.LoadPathVariable));
                        EditorGUILayout.SelectableLabel(status.EvaluatedBuildPath, EditorStyles.textField, GUILayout.Height(18));
                        EditorGUILayout.SelectableLabel(status.EvaluatedLoadPath, EditorStyles.textField, GUILayout.Height(18));
                    }
                }
            }
        }

        private void DrawSharedActionButtons(AAWorkflowConfig config, BuildTarget target, bool includeOpenRemote)
        {
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("导出 DLL + Manifest", "复制 HybridCLR 生成的 DLL 为 .dll.bytes，并生成 HotUpdateManifest.json。"), GUILayout.Height(28)))
                {
                    HybridCLRHotUpdateExportReport report =
                        HybridCLRHotUpdateAssetExporter.ExportGeneratedAssets(target);
                    HybridCLRHotUpdateAssetExporter.LogReport(report);
                    Window.ShowNotification(new GUIContent(report.Success ? "导出完成" : "导出失败"));
                }

                if (GUILayout.Button(new GUIContent("构建 Addressables", "调用 AddressableAssetSettings.BuildPlayerContent() 构建当前 AA 内容。"), GUILayout.Height(28)))
                {
                    BuildAddressablesOnly();
                }

                if (GUILayout.Button(new GUIContent("校验目录", "校验当前配置对应的输出/发布目录。"), GUILayout.Height(28)))
                {
                    ValidateWorkflowDirectory(config, target);
                }

                if (includeOpenRemote && GUILayout.Button(new GUIContent("打开远端目录", "打开远端发布目录。"), GUILayout.Height(28)))
                {
                    EditorUtility.RevealInFinder(AAWorkflowPathUtility.ExpandPathTokens(config.RemotePublishDirectory, target));
                }
            }
        }

        private string DrawFolderTextField(GUIContent label, string value, BuildTarget target)
        {
            using (new GUILayout.HorizontalScope())
            {
                value = EditorGUILayout.TextField(label, value);
                if (GUILayout.Button(new GUIContent("...", "选择本机文件夹。"), GUILayout.Width(32)))
                {
                    string expanded = AAWorkflowPathUtility.ExpandPathTokens(value, target);
                    string selected = EditorUtility.OpenFolderPanel(label.text, expanded, "");
                    if (!string.IsNullOrWhiteSpace(selected))
                    {
                        value = selected.Replace('\\', '/');
                    }
                }
            }

            return value;
        }

        private void RunLocalBuild(AAWorkflowConfig config, BuildTarget target)
        {
            try
            {
                EditorUtility.DisplayProgressBar("AA 本地内置构建", "正在构建本地内置 AA...", 0.3f);
                _lastRunReport = AAWorkflowPublishService.BuildLocalBuiltIn(config, target);
                _lastValidationReport = _lastRunReport.Success
                    ? ValidateDirectoryForConfig(config, _lastRunReport.PublishDirectory)
                    : null;
                Window.ShowNotification(new GUIContent(_lastRunReport.Success ? "本地内置构建完成" : "本地内置构建失败"));
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void RunRemotePublish(AAWorkflowConfig config, BuildTarget target)
        {
            try
            {
                EditorUtility.DisplayProgressBar("AA 远端热更发布", "正在发布远端热更 AA...", 0.3f);
                _lastRunReport = AAWorkflowPublishService.PublishRemoteHotUpdate(config, target);
                _lastValidationReport = _lastRunReport.Success
                    ? ValidateDirectoryForConfig(config, _lastRunReport.PublishDirectory)
                    : null;
                Window.ShowNotification(new GUIContent(_lastRunReport.Success ? "远端发布完成" : "远端发布失败"));
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void CoverTestPlayer(AAWorkflowConfig config, BuildTarget target)
        {
            _lastRunReport = AAWorkflowPublishService.CoverTestPlayerStreamingAssets(config, target);
            _lastValidationReport = _lastRunReport.Success
                ? ValidateDirectoryForConfig(config, _lastRunReport.PublishDirectory)
                : null;
            Window.ShowNotification(new GUIContent(_lastRunReport.Success ? "测试 Player 覆盖完成" : "测试 Player 覆盖失败"));
        }

        private void PublishRemoteOnly(AAWorkflowConfig config, BuildTarget target)
        {
            _lastRunReport = new AAHotUpdatePublishRunReport();
            try
            {
                string source = AAWorkflowPathUtility.BuildRemoteBuildPath(config, target);
                string destination = AAWorkflowPathUtility.ExpandPathTokens(config.RemotePublishDirectory, target);
                _lastRunReport.SourceDirectory = source;
                _lastRunReport.PublishDirectory = destination;
                _lastRunReport.ManifestPathOrUrl = AAWorkflowPathUtility.BuildManifestDisplayPath(config, target);
                if (AAHotUpdatePublishLogic.AreSameDirectory(source, destination))
                {
                    _lastRunReport.AddMessage("AA 源目录和远端发布目录相同，已跳过复制。");
                }
                else
                {
                    _lastRunReport.CopiedFiles = AAHotUpdatePublishLogic.CopyDirectory(
                        source,
                        destination,
                        config.CleanPublishDirectory,
                        config.CopyMetaFiles);
                    _lastRunReport.AddMessage("已发布 AA 文件数量：" + _lastRunReport.CopiedFiles);
                }

                _lastValidationReport = ValidateDirectoryForConfig(config, destination);
                foreach (string warning in _lastValidationReport.Warnings) _lastRunReport.AddWarning(warning);
                foreach (string error in _lastValidationReport.Errors) _lastRunReport.AddError(error);
                Window.ShowNotification(new GUIContent(_lastRunReport.Success ? "远端目录发布完成" : "远端目录发布失败"));
            }
            catch (Exception exception)
            {
                _lastRunReport.AddError(exception.GetBaseException().Message);
                Window.ShowNotification(new GUIContent("远端目录发布失败"));
            }
        }

        private void BuildAddressablesOnly()
        {
            _lastRunReport = new AAHotUpdatePublishRunReport();
            AddressablesPlayerBuildResult result = AAHotUpdatePublishLogic.BuildAddressablesPlayerContent();
            if (result != null && !string.IsNullOrEmpty(result.Error))
            {
                _lastRunReport.AddError("Addressables 构建失败：" + result.Error);
                Window.ShowNotification(new GUIContent("Addressables 构建失败"));
                return;
            }

            _lastRunReport.AddMessage("Addressables 构建完成。");
            Window.ShowNotification(new GUIContent("Addressables 构建完成"));
        }

        private void WriteWorkflowRuntimeSettings(AAWorkflowConfig config, BuildTarget target)
        {
            _lastRunReport = new AAHotUpdatePublishRunReport();
            if (AAWorkflowRuntimeSettingsWriter.TryWrite(config, target, out string message))
            {
                _lastRunReport.AddMessage(message);
                Window.ShowNotification(new GUIContent("运行时设置已保存"));
            }
            else
            {
                _lastRunReport.AddError(message);
                Window.ShowNotification(new GUIContent("运行时设置保存失败"));
            }
        }

        private void ValidateWorkflowDirectory(AAWorkflowConfig config, BuildTarget target)
        {
            ValidateDirectory(config, AAWorkflowPathUtility.GetPublishDirectory(config, target));
        }

        private void ValidateDirectory(AAWorkflowConfig config, string directory)
        {
            _lastValidationReport = ValidateDirectoryForConfig(config, directory);
            _lastRunReport = new AAHotUpdatePublishRunReport
            {
                PublishDirectory = directory
            };
            foreach (string warning in _lastValidationReport.Warnings) _lastRunReport.AddWarning(warning);
            foreach (string error in _lastValidationReport.Errors) _lastRunReport.AddError(error);
            if (_lastValidationReport.IsValid)
            {
                _lastRunReport.AddMessage("目录校验通过：" + directory);
            }
        }

        private static AAHotUpdatePublishValidationReport ValidateDirectoryForConfig(
            AAWorkflowConfig config,
            string directory)
        {
            bool isLocalBuiltIn = config != null && config.Mode == AAWorkflowMode.LocalBuiltIn;
            return AAWorkflowValidator.ValidatePublishDirectory(
                directory,
                GetHotUpdateDllBytesPath(),
                requireCatalogHash: !isLocalBuiltIn,
                requireSettingsJson: isLocalBuiltIn,
                warnAboutMetaFiles: !isLocalBuiltIn);
        }

        private void DrawLastReports()
        {
            if (_lastRunReport == null && _lastValidationReport == null)
            {
                return;
            }

            Section("上次结果");
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MinHeight(120), GUILayout.MaxHeight(260));

            if (_lastRunReport != null)
            {
                EditorGUILayout.HelpBox(
                    $"成功={_lastRunReport.Success}, 复制文件数={_lastRunReport.CopiedFiles}\n" +
                    $"源目录={_lastRunReport.SourceDirectory}\n" +
                    $"发布目录={_lastRunReport.PublishDirectory}\n" +
                    $"Manifest={_lastRunReport.ManifestPathOrUrl}",
                    _lastRunReport.Success ? MessageType.Info : MessageType.Error);

                foreach (string message in _lastRunReport.Messages)
                {
                    EditorGUILayout.HelpBox(message, MessageType.None);
                }

                foreach (string warning in _lastRunReport.Warnings)
                {
                    EditorGUILayout.HelpBox(warning, MessageType.Warning);
                }

                foreach (string error in _lastRunReport.Errors)
                {
                    EditorGUILayout.HelpBox(error, MessageType.Error);
                }
            }

            if (_lastValidationReport != null)
            {
                EditorGUILayout.HelpBox(
                    $"Manifest={_lastValidationReport.ManifestCount}, CatalogJson={_lastValidationReport.CatalogJsonCount}, " +
                    $"CatalogHash={_lastValidationReport.CatalogHashCount}, SettingsJson={_lastValidationReport.SettingsJsonCount}, " +
                    $"Bundle={_lastValidationReport.BundleCount}, Meta={_lastValidationReport.MetaFileCount}",
                    _lastValidationReport.IsValid ? MessageType.Info : MessageType.Error);
            }

            EditorGUILayout.EndScrollView();
        }

        private static string ModeLabel(AAWorkflowMode mode)
        {
            return mode == AAWorkflowMode.LocalBuiltIn ? "本地内置 AA" : "远端热更 AA";
        }

        private static string GetHotUpdateDllBytesPath()
        {
            return Path.Combine(Application.dataPath, "GameHotUpdate", "Code", "HotUpdate.dll.bytes");
        }
    }

    [InitializeOnLoad]
    internal static class HybridCLRAddressablesBuildReportGuard
    {
        static HybridCLRAddressablesBuildReportGuard()
        {
            AddressablesPlayerBuildProcessor.BuildAddressablesOverride = BuildAddressablesForPlayer;
            AddressablesBuildReportScope.DisableReportsAndClearReportList();
        }

        private static AddressablesPlayerBuildResult BuildAddressablesForPlayer(AddressableAssetSettings settings)
        {
            bool isHybridClrScriptsOnlyBuild = EditorUserBuildSettings.buildScriptsOnly;
            if (!isHybridClrScriptsOnlyBuild)
            {
                AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult normalResult);
                return normalResult;
            }

            using (new AddressablesBuildReportScope(clearBuildReportList: true))
            {
                AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult guardedResult);
                return guardedResult;
            }
        }
    }

    internal sealed class AAWorkflowPlayerBuildPostprocessor : IPostprocessBuildWithReport
    {
        public int callbackOrder => 1000;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report == null || report.summary.result != BuildResult.Succeeded)
            {
                return;
            }

            AAWorkflowConfigSet configSet = AAWorkflowConfigStore.ConfigSet;
            configSet.EnsureDefaults();
            AAWorkflowConfig config = configSet.SelectedConfig;
            if (config == null || config.Mode != AAWorkflowMode.LocalBuiltIn)
            {
                return;
            }

            string source = AAWorkflowPathUtility.GetStreamingAssetsAaRootDirectory();
            string destination = AAWorkflowPathUtility.ResolveBuiltPlayerStreamingAssetsAaDirectory(
                report.summary.outputPath);
            if (string.IsNullOrWhiteSpace(destination))
            {
                Debug.LogWarning("[AAWorkflowPublish] 无法定位 Player StreamingAssets/aa，跳过本地内置 AA 构建后同步。Output=" +
                                 report.summary.outputPath);
                return;
            }

            try
            {
                int copied = AAHotUpdatePublishLogic.CopyDirectory(
                    source,
                    destination,
                    cleanDestination: true,
                    copyMetaFiles: false);
                Debug.Log("[AAWorkflowPublish] Player 本地内置 AA 已同步。Files=" + copied +
                          ", Source=" + source + ", Destination=" + destination);
            }
            catch (Exception exception)
            {
                Debug.LogError("[AAWorkflowPublish] Player 本地内置 AA 同步失败：" +
                               exception.GetBaseException().Message);
            }
        }
    }

    internal sealed class AddressablesBuildReportScope : IDisposable
    {
        private const string ProjectConfigDataTypeName =
            "UnityEditor.AddressableAssets.Settings.ProjectConfigData, Unity.Addressables.Editor";

        private readonly Type _projectConfigDataType;
        private readonly bool? _generateBuildLayout;
        private readonly bool? _autoOpenAddressablesReport;
        private readonly bool? _userHasBeenInformed;

        public AddressablesBuildReportScope(bool clearBuildReportList = false)
        {
            _projectConfigDataType = Type.GetType(ProjectConfigDataTypeName);
            if (_projectConfigDataType == null)
            {
                return;
            }

            _generateBuildLayout = GetBoolProperty("GenerateBuildLayout");
            _autoOpenAddressablesReport = GetBoolProperty("AutoOpenAddressablesReport");
            _userHasBeenInformed = GetBoolProperty("UserHasBeenInformedAboutBuildReportSettingPreBuild");

            SetBoolProperty("GenerateBuildLayout", false);
            SetBoolProperty("AutoOpenAddressablesReport", false);
            SetBoolProperty("UserHasBeenInformedAboutBuildReportSettingPreBuild", true);

            if (clearBuildReportList)
            {
                ClearBuildReportFilePaths(_projectConfigDataType);
            }
        }

        public void Dispose()
        {
            if (_projectConfigDataType == null)
            {
                return;
            }

            RestoreBoolProperty("GenerateBuildLayout", _generateBuildLayout);
            RestoreBoolProperty("AutoOpenAddressablesReport", _autoOpenAddressablesReport);
            RestoreBoolProperty("UserHasBeenInformedAboutBuildReportSettingPreBuild", _userHasBeenInformed);
        }

        public static void DisableReportsAndClearReportList()
        {
            Type type = Type.GetType(ProjectConfigDataTypeName);
            if (type == null)
            {
                return;
            }

            SetBoolProperty(type, "GenerateBuildLayout", false);
            SetBoolProperty(type, "AutoOpenAddressablesReport", false);
            SetBoolProperty(type, "UserHasBeenInformedAboutBuildReportSettingPreBuild", true);
            ClearBuildReportFilePaths(type);
        }

        private bool? GetBoolProperty(string propertyName)
        {
            PropertyInfo property = GetProperty(propertyName);
            if (property == null || property.PropertyType != typeof(bool) || !property.CanRead)
            {
                return null;
            }

            return (bool)property.GetValue(null);
        }

        private void RestoreBoolProperty(string propertyName, bool? value)
        {
            if (value.HasValue)
            {
                SetBoolProperty(propertyName, value.Value);
            }
        }

        private void SetBoolProperty(string propertyName, bool value)
        {
            SetBoolProperty(_projectConfigDataType, propertyName, value);
        }

        private static void SetBoolProperty(Type type, string propertyName, bool value)
        {
            PropertyInfo property = type.GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (property == null || property.PropertyType != typeof(bool) || !property.CanWrite)
            {
                return;
            }

            property.SetValue(null, value);
        }

        private static void ClearBuildReportFilePaths(Type type)
        {
            MethodInfo clearMethod = type.GetMethod(
                "ClearBuildReportFilePaths",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            clearMethod?.Invoke(null, null);
        }

        private PropertyInfo GetProperty(string propertyName)
        {
            return _projectConfigDataType.GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        }
    }
}
