using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace StellarFramework.Editor.Modules
{
    internal static class StellarFrameworkPackagePublisher
    {
        private const string ExportRoot = "BuildArtifacts/StellarFramework";
        private const string LegacyAssetExportRoot = "Assets/StellarFramework/Editor/StellarToolsHub/Exports";
        private const string PublicPackageName = "StellarFramework.unitypackage";
        private const string FullPayloadPackageName = "StellarFramework-FullHotUpdate-Payload.unitypackage";
        private const string DependencyGuideName = "StellarFramework-Package-Dependencies.md";
        private const string EmbeddedPayloadDirectory = "Assets/StellarFrameworkBootstrap/Payloads";
        private const string EmbeddedPayloadAssetName = "StellarFramework-FullHotUpdate-Payload.unitypackage.bytes";
        private const string PayloadReadmeName = "README.md";
        private const string StandaloneExportRoot = "BuildArtifacts/StellarFramework/Standalone";
        private const string KitExportRoot = "BuildArtifacts/StellarFramework/Kits";
        private const string DistributionCatalogPath = "Assets/StellarFramework/KitCatalog/KitDistributionCatalog.json";
        private const string ArchitectureStandaloneOutputFileName = "StellarArchitecture.cs";
        private const string ExtensionsStandaloneOutputFileName = "StellarExtensions.cs";
        private const string ArchitectureSourcePath = "Assets/StellarFramework/Runtime/Core/Architecture/StellarFramework.cs";

        private static readonly string[] OptionalSampleProfileIds =
        {
            "samples.actionkit", "samples.audiokit", "samples.bindablekit", "samples.configkit",
            "samples.eventkit", "samples.fsmkit", "samples.httpkit", "samples.logkit", "samples.poolkit",
            "samples.reskit", "samples.settingskit", "samples.singletonkit", "samples.uikit",
            "samples.architecture", "samples.hotupdate.hybridclr"
        };

        private static readonly string[] ExtensionSourcePaths =
        {
            "Assets/StellarFramework/Runtime/Extensions/CollectionExtensions.cs",
            "Assets/StellarFramework/Runtime/Extensions/ColorExtensions.cs",
            "Assets/StellarFramework/Runtime/Extensions/CoroutineExtensions.cs",
            "Assets/StellarFramework/Runtime/Extensions/GameObjectExtensions.cs",
            "Assets/StellarFramework/Runtime/Extensions/LayerExtensions.cs",
            "Assets/StellarFramework/Runtime/Extensions/RectTransformExtensions.cs",
            "Assets/StellarFramework/Runtime/Extensions/RenderPipelineCompatibility.cs",
            "Assets/StellarFramework/Runtime/Extensions/StringExtensions.cs",
            "Assets/StellarFramework/Runtime/Extensions/TransformExtensions.cs",
            "Assets/StellarFramework/Runtime/Extensions/VectorExtensions.cs"
        };

        private static readonly string[] StandaloneExtensionUsingDirectives =
        {
            "using System;",
            "using System.Collections;",
            "using System.Collections.Generic;",
            "using System.Text;",
            "using UnityEngine;",
            "using UnityEngine.Rendering;",
            "using Object = UnityEngine.Object;"
        };

        [MenuItem("StellarFramework/Packages/导出单包安装版")]
        public static void ExportSinglePackageInstaller()
        {
            string exportDirectory = ToProjectPath(ExportRoot);
            Directory.CreateDirectory(exportDirectory);
            DeleteLegacyAssetExportArtifacts();
            DeleteLegacySplitPackageArtifacts(exportDirectory);

            string payloadOutputPath = Path.Combine(exportDirectory, FullPayloadPackageName);
            string publicPackageOutputPath = Path.Combine(exportDirectory, PublicPackageName);

            AssetDatabase.ExportPackage(GetFullFrameworkAssetPaths(), payloadOutputPath, ExportPackageOptions.Recurse);
            EmbedPayloadIntoBootstrap(payloadOutputPath);
            AssetDatabase.ExportPackage(new[] { "Assets/StellarFrameworkBootstrap" }, publicPackageOutputPath,
                ExportPackageOptions.Recurse);

            WriteDependencyGuide(exportDirectory);
            AssetDatabase.Refresh();
            EditorUtility.RevealInFinder(publicPackageOutputPath);
        }

        [MenuItem("StellarFramework/Packages/导出独立文件/Architecture.cs")]
        public static void ExportStandaloneArchitecture()
        {
            string outputPath = ExportStandaloneArchitectureInternal();
            EditorUtility.RevealInFinder(outputPath);
        }

        [MenuItem("StellarFramework/Packages/导出独立文件/Extensions.cs")]
        public static void ExportStandaloneExtensions()
        {
            string outputPath = ExportStandaloneExtensionsInternal();
            EditorUtility.RevealInFinder(outputPath);
        }

        [MenuItem("StellarFramework/Packages/导出独立文件/全部")]
        public static void ExportAllStandaloneSources()
        {
            ExportStandaloneArchitectureInternal();
            string extensionsOutputPath = ExportStandaloneExtensionsInternal();
            EditorUtility.RevealInFinder(extensionsOutputPath);
        }

        [MenuItem("StellarFramework/Packages/导出 Kit/EventKit")]
        public static void ExportEventKitPackage()
        {
            ExportKitPackageAndReveal("eventkit");
        }

        [MenuItem("StellarFramework/Packages/导出 Kit/ActionKit")]
        public static void ExportActionKitPackage()
        {
            ExportKitPackageAndReveal("actionkit");
        }

        [MenuItem("StellarFramework/Packages/导出组合 Kit/AudioKit.Core")]
        public static void ExportAudioKitCorePackage()
        {
            ExportKitPackageAndReveal("audiokit.core");
        }

        [MenuItem("StellarFramework/Packages/导出组合 Kit/AudioKit.ResKitAdapter")]
        public static void ExportAudioKitResKitAdapterPackage()
        {
            ExportKitPackageAndReveal("audiokit.reskit");
        }

        [MenuItem("StellarFramework/Packages/导出 Kit/BindableKit")]
        public static void ExportBindableKitPackage()
        {
            ExportKitPackageAndReveal("bindablekit");
        }

        [MenuItem("StellarFramework/Packages/导出组合 Kit/ConfigKit.Core")]
        public static void ExportConfigKitCorePackage()
        {
            ExportKitPackageAndReveal("configkit.core");
        }

        [MenuItem("StellarFramework/Packages/导出组合 Kit/ConfigKit.NewtonsoftJson")]
        public static void ExportConfigKitJsonPackage()
        {
            ExportKitPackageAndReveal("configkit.json");
        }

        [MenuItem("StellarFramework/Packages/导出组合 Kit/SettingsKit.Core")]
        public static void ExportSettingsKitCorePackage()
        {
            ExportKitPackageAndReveal("settingskit.core");
        }

        [MenuItem("StellarFramework/Packages/导出组合 Kit/SettingsKit.UnityAdapters")]
        public static void ExportSettingsKitUnityAdaptersPackage()
        {
            ExportKitPackageAndReveal("settingskit.unity");
        }

        [MenuItem("StellarFramework/Packages/导出组合 Kit/SettingsKit.AudioKitAdapter")]
        public static void ExportSettingsKitAudioKitAdapterPackage()
        {
            ExportKitPackageAndReveal("settingskit.audiokit");
        }

        [MenuItem("StellarFramework/Packages/导出 Kit/FSMKit")]
        public static void ExportFsmKitPackage()
        {
            ExportKitPackageAndReveal("fsmkit");
        }

        [MenuItem("StellarFramework/Packages/导出 Kit/PoolKit")]
        public static void ExportPoolKitPackage()
        {
            ExportKitPackageAndReveal("poolkit");
        }

        [MenuItem("StellarFramework/Packages/导出 Kit/SingletonKit")]
        public static void ExportSingletonKitPackage()
        {
            ExportKitPackageAndReveal("singletonkit");
        }

        [MenuItem("StellarFramework/Packages/导出 Kit/ToolsHub.Core")]
        public static void ExportToolsHubCorePackage()
        {
            ExportKitPackageAndReveal("toolshub.core");
        }

        [MenuItem("StellarFramework/Packages/导出 Kit/HttpKit")]
        public static void ExportHttpKitPackage()
        {
            ExportKitPackageAndReveal("httpkit");
        }

        [MenuItem("StellarFramework/Packages/导出 Kit/LogKit")]
        public static void ExportLogKitPackage()
        {
            ExportKitPackageAndReveal("logkit");
        }

        [MenuItem("StellarFramework/Packages/导出组合 Kit/ResKit.Core")]
        public static void ExportResKitCorePackage()
        {
            ExportKitPackageAndReveal("reskit.core");
        }

        [MenuItem("StellarFramework/Packages/导出组合 Kit/ResKit.AssetBundle")]
        public static void ExportResKitAssetBundlePackage()
        {
            ExportKitPackageAndReveal("reskit.assetbundle");
        }

        [MenuItem("StellarFramework/Packages/导出组合 Kit/ResKit.Addressables")]
        public static void ExportResKitAddressablesPackage()
        {
            ExportKitPackageAndReveal("reskit.addressables");
        }

        [MenuItem("StellarFramework/Packages/导出组合 Kit/UIKit.Core")]
        public static void ExportUIKitCorePackage()
        {
            ExportKitPackageAndReveal("uikit.core");
        }

        [MenuItem("StellarFramework/Packages/导出组合 Kit/UIKit.ResKitAdapter")]
        public static void ExportUIKitResKitAdapterPackage()
        {
            ExportKitPackageAndReveal("uikit.reskit");
        }

        [MenuItem("StellarFramework/Packages/导出组合 Kit/HotUpdate.Core")]
        public static void ExportHotUpdateCorePackage()
        {
            ExportKitPackageAndReveal("hotupdate.core");
        }

        [MenuItem("StellarFramework/Packages/导出组合 Kit/HotUpdate.Addressables")]
        public static void ExportHotUpdateAddressablesPackage()
        {
            ExportKitPackageAndReveal("hotupdate.addressables");
        }

        [MenuItem("StellarFramework/Packages/导出组合 Kit/HotUpdate.HybridCLR")]
        public static void ExportHotUpdateHybridClrPackage()
        {
            ExportKitPackageAndReveal("hotupdate.hybridclr");
        }

        [MenuItem("StellarFramework/Packages/导出样例/ActionKit")]
        public static void ExportActionKitSamplePackage() => ExportKitPackageAndReveal("samples.actionkit");

        [MenuItem("StellarFramework/Packages/导出样例/AudioKit")]
        public static void ExportAudioKitSamplePackage() => ExportKitPackageAndReveal("samples.audiokit");

        [MenuItem("StellarFramework/Packages/导出样例/BindableKit")]
        public static void ExportBindableKitSamplePackage() => ExportKitPackageAndReveal("samples.bindablekit");

        [MenuItem("StellarFramework/Packages/导出样例/ConfigKit")]
        public static void ExportConfigKitSamplePackage() => ExportKitPackageAndReveal("samples.configkit");

        [MenuItem("StellarFramework/Packages/导出样例/EventKit")]
        public static void ExportEventKitSamplePackage() => ExportKitPackageAndReveal("samples.eventkit");

        [MenuItem("StellarFramework/Packages/导出样例/FSMKit")]
        public static void ExportFsmKitSamplePackage() => ExportKitPackageAndReveal("samples.fsmkit");

        [MenuItem("StellarFramework/Packages/导出样例/HttpKit")]
        public static void ExportHttpKitSamplePackage() => ExportKitPackageAndReveal("samples.httpkit");

        [MenuItem("StellarFramework/Packages/导出样例/LogKit")]
        public static void ExportLogKitSamplePackage() => ExportKitPackageAndReveal("samples.logkit");

        [MenuItem("StellarFramework/Packages/导出样例/PoolKit")]
        public static void ExportPoolKitSamplePackage() => ExportKitPackageAndReveal("samples.poolkit");

        [MenuItem("StellarFramework/Packages/导出样例/ResKit")]
        public static void ExportResKitSamplePackage() => ExportKitPackageAndReveal("samples.reskit");

        [MenuItem("StellarFramework/Packages/导出样例/SettingsKit")]
        public static void ExportSettingsKitSamplePackage() => ExportKitPackageAndReveal("samples.settingskit");

        [MenuItem("StellarFramework/Packages/导出样例/SingletonKit")]
        public static void ExportSingletonKitSamplePackage() => ExportKitPackageAndReveal("samples.singletonkit");

        [MenuItem("StellarFramework/Packages/导出样例/UIKit")]
        public static void ExportUIKitSamplePackage() => ExportKitPackageAndReveal("samples.uikit");

        [MenuItem("StellarFramework/Packages/导出样例/Architecture")]
        public static void ExportArchitectureSamplePackage() => ExportKitPackageAndReveal("samples.architecture");

        [MenuItem("StellarFramework/Packages/导出样例/HotUpdate.HybridCLR")]
        public static void ExportHotUpdateHybridClrSamplePackage() => ExportKitPackageAndReveal("samples.hotupdate.hybridclr");

        [MenuItem("StellarFramework/Packages/导出样例/全部可选样例")]
        public static void ExportAllOptionalSamplePackages()
        {
            foreach (string profileId in OptionalSampleProfileIds)
            {
                ExportKitPackageInternal(profileId);
            }

            AssetDatabase.Refresh();
            EditorUtility.RevealInFinder(ToProjectPath(KitExportRoot));
        }

        internal static string ExportStandaloneArchitectureInternal()
        {
            string source = ReadProjectAssetText(ArchitectureSourcePath);
            string generated = BuildStandaloneHeader("Architecture") + ReplaceLogKitCalls(source);
            return WriteStandaloneSource(ArchitectureStandaloneOutputFileName, generated);
        }

        internal static string ExportStandaloneExtensionsInternal()
        {
            var builder = new StringBuilder(32 * 1024);
            builder.Append(BuildStandaloneHeader("Extensions"));
            foreach (string usingDirective in StandaloneExtensionUsingDirectives)
            {
                builder.AppendLine(usingDirective);
            }

            builder.AppendLine();
            foreach (string sourcePath in ExtensionSourcePaths)
            {
                string source = ReadProjectAssetText(sourcePath);
                builder.AppendLine($"// Source: {sourcePath}");
                builder.AppendLine(ReplaceLogKitCalls(RemoveTopLevelUsingDirectives(source)).Trim());
                builder.AppendLine();
            }

            return WriteStandaloneSource(ExtensionsStandaloneOutputFileName, builder.ToString());
        }

        internal static string ExportKitPackageInternal(string profileId)
        {
            DistributionCatalog catalog = LoadDistributionCatalog();
            DistributionProfile profile = catalog.profiles
                .FirstOrDefault(candidate => candidate.id == profileId);
            if (profile == null)
            {
                throw new InvalidOperationException($"Unknown Kit distribution profile: {profileId}");
            }

            List<DistributionProfile> closure = ResolveProfileClosure(catalog, profile);
            if (closure.Any(candidate => candidate.availability != "available"))
            {
                string unavailable = string.Join(", ", closure
                    .Where(candidate => candidate.availability != "available")
                    .Select(candidate => candidate.displayName));
                throw new InvalidOperationException($"Kit distribution profile has unavailable dependencies: {unavailable}");
            }

            if (string.IsNullOrWhiteSpace(profile.output) || closure.Any(candidate =>
                    candidate.sourcePaths == null || candidate.sourcePaths.Length == 0))
            {
                throw new InvalidOperationException($"Kit distribution profile is incomplete: {profile.displayName}");
            }

            string[] assetPaths = GetAssetsForProfiles(closure);
            if (assetPaths.Length == 0)
            {
                throw new InvalidOperationException($"Kit distribution profile has no exportable assets: {profile.displayName}");
            }

            string exportDirectory = ToProjectPath(KitExportRoot);
            Directory.CreateDirectory(exportDirectory);
            string outputPath = Path.Combine(exportDirectory, profile.output);
            AssetDatabase.ExportPackage(assetPaths, outputPath, ExportPackageOptions.Recurse);
            WriteKitDependencyGuide(exportDirectory, profile, closure);
            AssetDatabase.Refresh();
            return outputPath;
        }

        internal static string[] GetBaseFrameworkAssetPaths()
        {
            return AssetDatabase.GetAllAssetPaths()
                .Where(path => path.StartsWith("Assets/StellarFramework"))
                .Where(path => !AssetDatabase.IsValidFolder(path))
                .Where(IsIncludedInBasePackage)
                .OrderBy(path => path)
                .ToArray();
        }

        internal static string[] GetFullFrameworkAssetPaths()
        {
            return AssetDatabase.GetAllAssetPaths()
                .Where(path => !AssetDatabase.IsValidFolder(path))
                .Where(IsIncludedInFullPayload)
                .OrderBy(path => path)
                .ToArray();
        }

        internal static bool IsIncludedInBasePackage(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return false;
            }

            string normalized = NormalizePath(assetPath);
            return normalized.StartsWith("Assets/StellarFramework")
                   && !BasePackageExcludedPrefixes.Any(prefix => normalized.StartsWith(prefix))
                   && !BasePackageExcludedExactPaths.Contains(normalized)
                   && !GeneratedArtifactPrefixes.Any(prefix => normalized.StartsWith(prefix));
        }

        private static bool IsIncludedInFullPayload(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return false;
            }

            string normalized = NormalizePath(assetPath);
            bool isFrameworkSource = normalized.StartsWith("Assets/StellarFramework")
                                     || normalized.StartsWith("Assets/GameHotUpdate");
            if (!isFrameworkSource)
            {
                return false;
            }

            return !GeneratedArtifactPrefixes.Any(prefix => normalized.StartsWith(prefix))
                   && !FullPayloadExcludedPrefixes.Any(prefix => normalized.StartsWith(prefix))
                   && !FullPayloadExcludedExactPaths.Contains(normalized);
        }

        private static void EmbedPayloadIntoBootstrap(string payloadOutputPath)
        {
            string payloadDirectory = ToProjectPath(EmbeddedPayloadDirectory);
            Directory.CreateDirectory(payloadDirectory);

            string payloadAssetPath = Path.Combine(payloadDirectory, EmbeddedPayloadAssetName);
            File.Copy(payloadOutputPath, payloadAssetPath, true);

            string payloadReadmePath = Path.Combine(payloadDirectory, PayloadReadmeName);
            File.WriteAllText(payloadReadmePath,
                "# StellarFramework 内嵌 Payload\r\n\r\n" +
                "这个目录由分发导出器维护。\r\n\r\n" +
                "- `*.unitypackage.bytes` 是单包安装器自动导入的完整框架 payload。\r\n" +
                "- 正常分发时，外部用户不需要手动操作这里的文件。\r\n");

            AssetDatabase.Refresh();
        }

        private static void DeleteLegacySplitPackageArtifacts(string exportDirectory)
        {
            foreach (string legacyFileName in LegacySplitPackageArtifacts)
            {
                string legacyFilePath = Path.Combine(exportDirectory, legacyFileName);
                if (File.Exists(legacyFilePath))
                {
                    File.Delete(legacyFilePath);
                }

                string legacyMetaPath = legacyFilePath + ".meta";
                if (File.Exists(legacyMetaPath))
                {
                    File.Delete(legacyMetaPath);
                }
            }
        }

        private static void DeleteLegacyAssetExportArtifacts()
        {
            string legacyDirectory = ToProjectPath(LegacyAssetExportRoot);
            if (!Directory.Exists(legacyDirectory))
            {
                return;
            }

            foreach (string legacyFileName in LegacyGeneratedArtifactFiles)
            {
                string legacyFilePath = Path.Combine(legacyDirectory, legacyFileName);
                if (File.Exists(legacyFilePath))
                {
                    File.Delete(legacyFilePath);
                }

                string legacyMetaPath = legacyFilePath + ".meta";
                if (File.Exists(legacyMetaPath))
                {
                    File.Delete(legacyMetaPath);
                }
            }

            AssetDatabase.Refresh();
        }

        private static string NormalizePath(string assetPath)
        {
            return assetPath.Replace('\\', '/');
        }

        private static string ReadProjectAssetText(string assetPath)
        {
            string fullPath = ToProjectPath(assetPath);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"Standalone export source was not found: {assetPath}", fullPath);
            }

            return File.ReadAllText(fullPath);
        }

        private static string WriteStandaloneSource(string fileName, string content)
        {
            string exportDirectory = ToProjectPath(StandaloneExportRoot);
            Directory.CreateDirectory(exportDirectory);

            string outputPath = Path.Combine(exportDirectory, fileName);
            File.WriteAllText(outputPath, content, new UTF8Encoding(false));
            return outputPath;
        }

        private static void ExportKitPackageAndReveal(string profileId)
        {
            string outputPath = ExportKitPackageInternal(profileId);
            EditorUtility.RevealInFinder(outputPath);
        }

        private static DistributionCatalog LoadDistributionCatalog()
        {
            string json = ReadProjectAssetText(DistributionCatalogPath);
            DistributionCatalog catalog = JsonUtility.FromJson<DistributionCatalog>(json);
            if (catalog == null || catalog.profiles == null)
            {
                throw new InvalidOperationException("Kit distribution catalog could not be read.");
            }

            return catalog;
        }

        internal static string[] ResolveKitProfileClosureIds(string profileId)
        {
            DistributionCatalog catalog = LoadDistributionCatalog();
            DistributionProfile profile = catalog.profiles.FirstOrDefault(candidate => candidate.id == profileId);
            if (profile == null)
            {
                throw new InvalidOperationException($"Unknown Kit distribution profile: {profileId}");
            }

            return ResolveProfileClosure(catalog, profile).Select(candidate => candidate.id).ToArray();
        }

        private static List<DistributionProfile> ResolveProfileClosure(DistributionCatalog catalog,
            DistributionProfile rootProfile)
        {
            var profilesById = catalog.profiles.ToDictionary(profile => profile.id, StringComparer.Ordinal);
            var resolved = new List<DistributionProfile>();
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var visiting = new HashSet<string>(StringComparer.Ordinal);

            void Visit(DistributionProfile profile)
            {
                if (!visiting.Add(profile.id))
                {
                    throw new InvalidOperationException($"Circular Kit distribution dependency: {profile.id}");
                }

                if (profile.requiredProfileIds != null)
                {
                    foreach (string dependencyId in profile.requiredProfileIds)
                    {
                        if (!profilesById.TryGetValue(dependencyId, out DistributionProfile dependency))
                        {
                            throw new InvalidOperationException(
                                $"Kit distribution profile '{profile.id}' references unknown dependency '{dependencyId}'.");
                        }

                        if (!visited.Contains(dependencyId))
                        {
                            Visit(dependency);
                        }
                    }
                }

                visiting.Remove(profile.id);
                if (visited.Add(profile.id))
                {
                    resolved.Add(profile);
                }
            }

            Visit(rootProfile);
            return resolved;
        }

        private static string[] GetAssetsForProfiles(IEnumerable<DistributionProfile> profiles)
        {
            return AssetDatabase.GetAllAssetPaths()
                .Where(path => !AssetDatabase.IsValidFolder(path))
                .Where(path => profiles.Any(profile => IsIncludedInProfile(path, profile)))
                .OrderBy(path => path)
                .ToArray();
        }

        private static bool IsIncludedInProfile(string assetPath, DistributionProfile profile)
        {
            bool insideSource = profile.sourcePaths.Any(sourcePath => IsPathInside(assetPath, sourcePath));
            bool excluded = profile.excludedSourcePaths != null &&
                            profile.excludedSourcePaths.Any(excludedPath => IsPathInside(assetPath, excludedPath));
            return insideSource && !excluded;
        }

        private static bool IsPathInside(string assetPath, string sourcePath)
        {
            string normalizedAssetPath = NormalizePath(assetPath);
            string normalizedSourcePath = NormalizePath(sourcePath).TrimEnd('/');
            return normalizedAssetPath == normalizedSourcePath || normalizedAssetPath.StartsWith(normalizedSourcePath + "/");
        }

        private static void WriteKitDependencyGuide(string outputDirectory, DistributionProfile profile,
            IEnumerable<DistributionProfile> closure)
        {
            string guidePath = Path.Combine(outputDirectory,
                Path.GetFileNameWithoutExtension(profile.output) + "-Dependencies.md");
            var builder = new StringBuilder();
            builder.AppendLine($"# {profile.displayName} 导入说明");
            builder.AppendLine();
            builder.AppendLine($"导入 `{profile.output}` 即可获得此 Kit 的源代码。");
            builder.AppendLine();
            AppendGuideList(builder, "本包已包含的 Kit", closure.Select(candidate => candidate.displayName).ToArray(), "无");
            AppendGuideList(builder, "需要安装的 UPM 包", closure
                .SelectMany(candidate => candidate.requiredUpm ?? Array.Empty<string>())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(packageName => packageName)
                .ToArray(), "无");
            AppendGuideList(builder, "不会引入的能力", profile.excludedCapabilities, "无");
            File.WriteAllText(guidePath, builder.ToString(), new UTF8Encoding(false));
        }

        private static void AppendGuideList(StringBuilder builder, string title, string[] values, string emptyValue)
        {
            builder.AppendLine($"## {title}");
            builder.AppendLine();
            if (values == null || values.Length == 0)
            {
                builder.AppendLine(emptyValue);
            }
            else
            {
                foreach (string value in values)
                {
                    builder.AppendLine($"- {value}");
                }
            }

            builder.AppendLine();
        }

        private static string BuildStandaloneHeader(string profileName)
        {
            return
                "// <auto-generated>\r\n" +
                $"// StellarFramework standalone export: {profileName}\r\n" +
                "// Generated from the framework source project. Edit the source project, not this file.\r\n" +
                "// This export intentionally replaces LogKit calls with UnityEngine.Debug so it has no StellarFramework Kit dependency.\r\n" +
                "// </auto-generated>\r\n\r\n";
        }

        private static string RemoveTopLevelUsingDirectives(string source)
        {
            return Regex.Replace(source, @"^using\s+[^\r\n]+;\s*\r?\n", string.Empty, RegexOptions.Multiline);
        }

        private static string ReplaceLogKitCalls(string source)
        {
            return source
                .Replace("LogKit.LogError(", "Debug.LogError(")
                .Replace("LogKit.LogWarning(", "Debug.LogWarning(")
                .Replace("LogKit.Log(", "Debug.Log(");
        }

        private static string ToProjectPath(string assetRelativePath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.Combine(projectRoot, assetRelativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static void WriteDependencyGuide(string outputDirectory)
        {
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                return;
            }

            string content =
                "# StellarFramework 单包安装说明\r\n\r\n" +
                "## 使用方式\r\n\r\n" +
                "只需要导入 `StellarFramework.unitypackage` 这一个包。\r\n\r\n" +
                "导入后打开 `StellarFramework/单包安装器`，点击“一键安装 StellarFramework”，安装器会继续完成依赖安装和完整框架导入。\r\n\r\n" +
                "## 自动安装的依赖\r\n\r\n" +
                "- UniTask (`com.cysharp.unitask`)\r\n" +
                "- Newtonsoft.Json (`com.unity.nuget.newtonsoft-json`)\r\n" +
                "- Addressables (`com.unity.addressables`)\r\n" +
                "- HybridCLR (`com.code-philosophy.hybridclr`)\r\n\r\n" +
                "## 兼容性\r\n\r\n" +
                "- Unity 版本：面向 Unity 2022.3 LTS 和 Unity 6000.x。\r\n" +
                "- 渲染管线：Runtime 和 Samples 通过 `RenderPipelineCompatibility` 自动选择 Built-in、URP 或 HDRP 的可用 Lit Shader。\r\n" +
                "- 样例：导出包保留 SampleTemplates，安装后可在 Tools Hub 里重新点击 `构建样例` 生成场景和资源。\r\n\r\n" +
                "## 说明\r\n\r\n" +
                "- 单包里已经内嵌完整框架 payload，用户不需要再手动选择基础包或热更包。\r\n" +
                "- 安装器会先补齐 UPM 依赖，再自动导入完整框架内容。\r\n";

            File.WriteAllText(Path.Combine(outputDirectory, DependencyGuideName), content);
        }

        private static readonly string[] GeneratedArtifactPrefixes =
        {
            ExportRoot,
            LegacyAssetExportRoot
        };

        [Serializable]
        private sealed class DistributionCatalog
        {
            public DistributionProfile[] profiles;
        }

        [Serializable]
        private sealed class DistributionProfile
        {
            public string id;
            public string displayName;
            public string availability;
            public string output;
            public string[] sourcePaths;
            public string[] excludedSourcePaths;
            public string[] requiredProfileIds;
            public string[] requiredKits;
            public string[] requiredUpm;
            public string[] excludedCapabilities;
        }

        private static readonly string[] FullPayloadExcludedPrefixes =
        {
            "Assets/StellarFramework/Editor/StellarToolsHub/Modules/Packaging",
            "Assets/StellarFramework/Samples/KitSamples/Scenes",
            "Assets/StellarFramework/Samples/KitSamples/Generated",
            "Assets/StellarFramework/Samples/KitSamples/Example_ResKit/Addressables",
            "Assets/StellarFramework/Samples/KitSamples/Example_ResKit/Art",
            "Assets/StellarFramework/Samples/KitSamples/Example_ResKit/Resources",
            "Assets/StellarFramework/Samples/ArchitectureDemo/Scene",
            "Assets/StellarFramework/Samples/ArchitectureDemo/Resources",
            "Assets/StellarFramework/Resources/Audio",
            "Assets/StellarFramework/Tests",
            "Assets/StellarFrameworkBootstrap",
            "Assets/StellarFrameworkVerification"
        };

        private static readonly HashSet<string> FullPayloadExcludedExactPaths = new HashSet<string>
        {
            "Assets/StellarFramework/Resources/UIPanel/UIRoot.prefab",
            "Assets/StellarFramework/Resources/UIPanel/ExamplePanel.prefab"
        };

        private static readonly string[] LegacySplitPackageArtifacts =
        {
            "StellarFramework-Bootstrap.unitypackage",
            "StellarFramework-Base.unitypackage",
            "StellarFramework-FullHotUpdate.unitypackage"
        };

        private static readonly string[] LegacyGeneratedArtifactFiles =
        {
            PublicPackageName,
            FullPayloadPackageName,
            DependencyGuideName
        };

        private static readonly string[] BasePackageExcludedPrefixes =
        {
            "Assets/StellarFramework/Runtime/Kits/HotUpdateKit",
            "Assets/StellarFramework/Runtime/Kits/Reskit/Loaders/AddressableLoader",
            "Assets/StellarFramework/Editor/StellarToolsHub/Modules/Addressables",
            "Assets/StellarFramework/Samples/KitSamples/Scenes",
            "Assets/StellarFramework/Samples/KitSamples/Generated",
            "Assets/StellarFramework/Samples/KitSamples/Example_ResKit/Addressables",
            "Assets/StellarFramework/Samples/KitSamples/Example_ResKit/Art",
            "Assets/StellarFramework/Samples/KitSamples/Example_ResKit/Resources",
            "Assets/StellarFramework/Samples/ArchitectureDemo/Scene",
            "Assets/StellarFramework/Samples/ArchitectureDemo/Resources",
            "Assets/StellarFramework/Resources/Audio",
            "Assets/StellarFramework/Tests"
        };

        private static readonly HashSet<string> BasePackageExcludedExactPaths = new HashSet<string>
        {
            "Assets/StellarFramework/Resources/UIPanel/UIRoot.prefab",
            "Assets/StellarFramework/Resources/UIPanel/ExamplePanel.prefab"
        };
    }
}
