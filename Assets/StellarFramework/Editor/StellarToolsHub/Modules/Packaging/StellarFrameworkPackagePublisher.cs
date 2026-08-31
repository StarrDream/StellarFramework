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
        private const int CurrentDistributionCatalogSchemaVersion = 2;
        private const string ArchitectureStandaloneOutputFileName = "StellarArchitecture.cs";
        private const string ExtensionsStandaloneOutputFileName = "StellarExtensions.cs";
        private const string ArchitectureSourcePath = "Assets/StellarFramework/Runtime/Core/Architecture/StellarFramework.cs";
        private const string KitBootstrapSourcePath =
            "Assets/StellarFramework/Editor/KitPackageBootstrap/StellarFrameworkKitPackageBootstrapInstaller.cs";
        private const string KitBootstrapRoot = "Assets/StellarFramework/Editor/KitPackageBootstrap";
        private const string KitBootstrapRequestPrefix = "__StellarFramework-KitBootstrap-";
        private const string KitBootstrapPayloadPrefix = "__StellarFramework-KitPayload-";

        private static readonly string[] OptionalSampleProfileIds =
        {
            "samples.actionkit", "samples.audiokit", "samples.bindablekit", "samples.configkit",
            "samples.eventkit", "samples.fsmkit", "samples.httpkit", "samples.logkit", "samples.poolkit",
            "samples.reskit", "samples.settingskit", "samples.singletonkit", "samples.uikit",
            "samples.architecture", "samples.hotupdate.hybridclr"
        };

        private static readonly IReadOnlyDictionary<string, string> UpmPackageSources =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                {
                    "com.cysharp.unitask",
                    "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask"
                },
                { "com.unity.nuget.newtonsoft-json", "com.unity.nuget.newtonsoft-json@3.2.2" },
                { "com.unity.addressables", "com.unity.addressables@1.22.3" },
                { "com.unity.ugui", "com.unity.ugui@1.0.0" },
                {
                    "com.code-philosophy.hybridclr",
                    "https://github.com/focus-creative-games/hybridclr_unity.git#4feac30cb2e105992986c737f7f54992b8300e1a"
                }
            };

        private static readonly HashSet<string> ArchitectureTiers = new HashSet<string>(StringComparer.Ordinal)
        {
            "foundation", "extension", "adapter"
        };

        private static readonly HashSet<string> ArchitectureCategories = new HashSet<string>(StringComparer.Ordinal)
        {
            "diagnostics", "infrastructure", "flow", "data", "network", "resource", "simulation",
            "presentation", "world", "gameplay", "runtime-delivery"
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

        public static void ExportStandaloneArchitecture()
        {
            string outputPath = ExportStandaloneArchitectureInternal();
            EditorUtility.RevealInFinder(outputPath);
        }

        public static void ExportStandaloneExtensions()
        {
            string outputPath = ExportStandaloneExtensionsInternal();
            EditorUtility.RevealInFinder(outputPath);
        }

        public static void ExportAllStandaloneSources()
        {
            ExportStandaloneArchitectureInternal();
            string extensionsOutputPath = ExportStandaloneExtensionsInternal();
            EditorUtility.RevealInFinder(extensionsOutputPath);
        }

        public static void ExportEventKitPackage()
        {
            ExportKitPackageAndReveal("eventkit");
        }

        public static void ExportActionKitPackage()
        {
            ExportKitPackageAndReveal("actionkit");
        }

        public static void ExportAudioKitCorePackage()
        {
            ExportKitPackageAndReveal("audiokit.core");
        }

        public static void ExportAudioKitResKitAdapterPackage()
        {
            ExportKitPackageAndReveal("audiokit.reskit");
        }

        public static void ExportBindableKitPackage()
        {
            ExportKitPackageAndReveal("bindablekit");
        }

        public static void ExportConfigKitCorePackage()
        {
            ExportKitPackageAndReveal("configkit.core");
        }

        public static void ExportConfigKitJsonPackage()
        {
            ExportKitPackageAndReveal("configkit.json");
        }

        public static void ExportSettingsKitCorePackage()
        {
            ExportKitPackageAndReveal("settingskit.core");
        }

        public static void ExportSettingsKitUnityAdaptersPackage()
        {
            ExportKitPackageAndReveal("settingskit.unity");
        }

        public static void ExportSettingsKitAudioKitAdapterPackage()
        {
            ExportKitPackageAndReveal("settingskit.audiokit");
        }

        public static void ExportFsmKitPackage()
        {
            ExportKitPackageAndReveal("fsmkit");
        }

        public static void ExportPoolKitPackage()
        {
            ExportKitPackageAndReveal("poolkit");
        }

        public static void ExportSingletonKitPackage()
        {
            ExportKitPackageAndReveal("singletonkit");
        }

        public static void ExportToolsHubCorePackage()
        {
            ExportKitPackageAndReveal("toolshub.core");
        }

        public static void ExportHttpKitPackage()
        {
            ExportKitPackageAndReveal("httpkit");
        }

        public static void ExportLogKitPackage()
        {
            ExportKitPackageAndReveal("logkit");
        }

        public static void ExportResKitCorePackage()
        {
            ExportKitPackageAndReveal("reskit.core");
        }

        public static void ExportResKitAssetBundlePackage()
        {
            ExportKitPackageAndReveal("reskit.assetbundle");
        }

        public static void ExportResKitAddressablesPackage()
        {
            ExportKitPackageAndReveal("reskit.addressables");
        }

        public static void ExportUIKitCorePackage()
        {
            ExportKitPackageAndReveal("uikit.core");
        }

        public static void ExportUIKitResKitAdapterPackage()
        {
            ExportKitPackageAndReveal("uikit.reskit");
        }

        public static void ExportHotUpdateCorePackage()
        {
            ExportKitPackageAndReveal("hotupdate.core");
        }

        public static void ExportHotUpdateAddressablesPackage()
        {
            ExportKitPackageAndReveal("hotupdate.addressables");
        }

        public static void ExportHotUpdateHybridClrPackage()
        {
            ExportKitPackageAndReveal("hotupdate.hybridclr");
        }

        public static void ExportActionKitSamplePackage() => ExportKitPackageAndReveal("samples.actionkit");

        public static void ExportAudioKitSamplePackage() => ExportKitPackageAndReveal("samples.audiokit");

        public static void ExportBindableKitSamplePackage() => ExportKitPackageAndReveal("samples.bindablekit");

        public static void ExportConfigKitSamplePackage() => ExportKitPackageAndReveal("samples.configkit");

        public static void ExportEventKitSamplePackage() => ExportKitPackageAndReveal("samples.eventkit");

        public static void ExportFsmKitSamplePackage() => ExportKitPackageAndReveal("samples.fsmkit");

        public static void ExportHttpKitSamplePackage() => ExportKitPackageAndReveal("samples.httpkit");

        public static void ExportLogKitSamplePackage() => ExportKitPackageAndReveal("samples.logkit");

        public static void ExportPoolKitSamplePackage() => ExportKitPackageAndReveal("samples.poolkit");

        public static void ExportResKitSamplePackage() => ExportKitPackageAndReveal("samples.reskit");

        public static void ExportSettingsKitSamplePackage() => ExportKitPackageAndReveal("samples.settingskit");

        public static void ExportSingletonKitSamplePackage() => ExportKitPackageAndReveal("samples.singletonkit");

        public static void ExportUIKitSamplePackage() => ExportKitPackageAndReveal("samples.uikit");

        public static void ExportArchitectureSamplePackage() => ExportKitPackageAndReveal("samples.architecture");

        public static void ExportHotUpdateHybridClrSamplePackage() => ExportKitPackageAndReveal("samples.hotupdate.hybridclr");

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

            string[] payloadAssetPaths = GetAssetsForProfiles(closure);
            if (payloadAssetPaths.Length == 0)
            {
                throw new InvalidOperationException($"Kit distribution profile has no exportable assets: {profile.displayName}");
            }

            string exportDirectory = ToProjectPath(KitExportRoot);
            Directory.CreateDirectory(exportDirectory);
            string outputPath = Path.Combine(exportDirectory, profile.output);
            string[] bootstrapAssetPaths = null;
            string temporaryPayloadPath = CreateTemporaryPayloadPath();
            try
            {
                AssetDatabase.ExportPackage(payloadAssetPaths, temporaryPayloadPath, ExportPackageOptions.Recurse);
                bootstrapAssetPaths = CreateKitBootstrapAssets(closure, profile.output, profile.displayName,
                    payloadAssetPaths, temporaryPayloadPath);
                AssetDatabase.ExportPackage(bootstrapAssetPaths, outputPath, ExportPackageOptions.Recurse);
                WriteKitDependencyGuide(exportDirectory, profile, closure);
            }
            finally
            {
                DeleteKitBootstrapAssets(bootstrapAssetPaths);
                DeleteTemporaryPayload(temporaryPayloadPath);
                AssetDatabase.Refresh();
            }

            return outputPath;
        }

        /// <summary>
        /// Exports multiple selected Kit profiles into one unitypackage. Dependencies are deduplicated by profile id.
        /// This API is intentionally only compiled in the framework source project's Packaging directory.
        /// </summary>
        internal static string ExportKitPackageGroupInternal(IEnumerable<string> profileIds, string outputFileName)
        {
            string[] selectedIds = (profileIds ?? Enumerable.Empty<string>())
                .Where(profileId => !string.IsNullOrWhiteSpace(profileId))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (selectedIds.Length == 0)
            {
                throw new InvalidOperationException("Select at least one Kit profile before exporting.");
            }

            DistributionCatalog catalog = LoadDistributionCatalog();
            var profilesById = catalog.profiles.ToDictionary(profile => profile.id, StringComparer.Ordinal);
            var selectedProfiles = new List<DistributionProfile>(selectedIds.Length);
            var closureById = new Dictionary<string, DistributionProfile>(StringComparer.Ordinal);

            foreach (string profileId in selectedIds)
            {
                if (!profilesById.TryGetValue(profileId, out DistributionProfile profile))
                {
                    throw new InvalidOperationException($"Unknown Kit distribution profile: {profileId}");
                }

                selectedProfiles.Add(profile);
                foreach (DistributionProfile dependency in ResolveProfileClosure(catalog, profile))
                {
                    closureById[dependency.id] = dependency;
                }
            }

            List<DistributionProfile> closure = closureById.Values
                .OrderBy(profile => profile.displayName, StringComparer.Ordinal)
                .ToList();
            if (closure.Any(profile => profile.availability != "available"))
            {
                string unavailable = string.Join(", ", closure
                    .Where(profile => profile.availability != "available")
                    .Select(profile => profile.displayName));
                throw new InvalidOperationException($"Selected Kit profiles have unavailable dependencies: {unavailable}");
            }

            if (closure.Any(profile => profile.sourcePaths == null || profile.sourcePaths.Length == 0))
            {
                throw new InvalidOperationException("Selected Kit profiles contain an incomplete export profile.");
            }

            string[] payloadAssetPaths = GetAssetsForProfiles(closure);
            if (payloadAssetPaths.Length == 0)
            {
                throw new InvalidOperationException("Selected Kit profiles have no exportable assets.");
            }

            string exportDirectory = ToProjectPath(KitExportRoot);
            Directory.CreateDirectory(exportDirectory);
            string fileName = NormalizePackageFileName(outputFileName);
            string outputPath = Path.Combine(exportDirectory, fileName);
            string[] bootstrapAssetPaths = null;
            string temporaryPayloadPath = CreateTemporaryPayloadPath();
            try
            {
                string displayName = string.Join(" + ", selectedProfiles
                    .Select(profile => profile.displayName)
                    .OrderBy(name => name, StringComparer.Ordinal));
                AssetDatabase.ExportPackage(payloadAssetPaths, temporaryPayloadPath, ExportPackageOptions.Recurse);
                bootstrapAssetPaths = CreateKitBootstrapAssets(closure, fileName, displayName,
                    payloadAssetPaths, temporaryPayloadPath);
                AssetDatabase.ExportPackage(bootstrapAssetPaths, outputPath, ExportPackageOptions.Recurse);
                WriteCombinedKitDependencyGuide(exportDirectory, fileName, selectedProfiles, closure);
            }
            finally
            {
                DeleteKitBootstrapAssets(bootstrapAssetPaths);
                DeleteTemporaryPayload(temporaryPayloadPath);
                AssetDatabase.Refresh();
            }

            return outputPath;
        }

        internal static DistributionProfile[] GetSourceProjectExportProfiles()
        {
            return LoadDistributionCatalog().profiles
                .Where(profile => profile.availability == "available")
                .Where(profile => profile.kind != "single-file" && profile.kind != "sample")
                // Generated support is selected transitively with its owning Kit. It is not a useful standalone choice.
                .Where(profile => profile.kind != "generated-support")
                .OrderBy(profile => profile.displayName, StringComparer.Ordinal)
                .ToArray();
        }

        internal static DistributionProfile[] GetSourceProjectSampleProfiles()
        {
            return LoadDistributionCatalog().profiles
                .Where(profile => profile.availability == "available")
                .Where(profile => profile.kind == "sample")
                .OrderBy(profile => profile.displayName, StringComparer.Ordinal)
                .ToArray();
        }

        internal static bool IsFrameworkSourceProject()
        {
            return File.Exists(ToProjectPath(DistributionCatalogPath)) &&
                   File.Exists(ToProjectPath(
                       "Assets/StellarFramework/Editor/StellarToolsHub/Modules/Packaging/StellarFrameworkPackagePublisher.cs"));
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

        private static string NormalizePackageFileName(string outputFileName)
        {
            string fileName = string.IsNullOrWhiteSpace(outputFileName)
                ? "StellarFramework-CombinedKits.unitypackage"
                : outputFileName.Trim();
            foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(invalidCharacter, '-');
            }

            return fileName.EndsWith(".unitypackage", StringComparison.OrdinalIgnoreCase)
                ? fileName
                : fileName + ".unitypackage";
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

            ValidateDistributionCatalog(catalog);

            return catalog;
        }

        private static void ValidateDistributionCatalog(DistributionCatalog catalog)
        {
            if (catalog.schemaVersion != CurrentDistributionCatalogSchemaVersion)
            {
                throw new InvalidOperationException(
                    $"Unsupported Kit distribution catalog schema: {catalog.schemaVersion}. " +
                    $"Expected {CurrentDistributionCatalogSchemaVersion}.");
            }

            var profilesById = new Dictionary<string, DistributionProfile>(StringComparer.Ordinal);
            foreach (DistributionProfile profile in catalog.profiles)
            {
                if (profile == null || string.IsNullOrWhiteSpace(profile.id) || profilesById.ContainsKey(profile.id))
                {
                    throw new InvalidOperationException("Kit distribution catalog contains an invalid or duplicate profile id.");
                }

                profilesById.Add(profile.id, profile);

                bool isRuntimeKit = profile.kind == "kit" || profile.kind == "kit-with-dependencies";
                bool hasTier = !string.IsNullOrWhiteSpace(profile.tier);
                bool hasCategory = !string.IsNullOrWhiteSpace(profile.category);
                if (isRuntimeKit && (!hasTier || !hasCategory || !ArchitectureTiers.Contains(profile.tier) ||
                    !ArchitectureCategories.Contains(profile.category)))
                {
                    throw new InvalidOperationException(
                        $"Runtime Kit profile '{profile.id}' must declare a valid tier and category.");
                }

                if (!isRuntimeKit && (hasTier || hasCategory))
                {
                    throw new InvalidOperationException(
                        $"Non-runtime Kit profile '{profile.id}' must not declare tier or category metadata.");
                }
            }

            foreach (DistributionProfile profile in catalog.profiles.Where(profile => profile.tier == "foundation"))
            {
                foreach (string dependencyId in profile.requiredProfileIds ?? Array.Empty<string>())
                {
                    if (!profilesById.TryGetValue(dependencyId, out DistributionProfile dependency))
                    {
                        throw new InvalidOperationException(
                            $"Kit distribution profile '{profile.id}' references unknown dependency '{dependencyId}'.");
                    }

                    if (dependency.tier == "extension")
                    {
                        throw new InvalidOperationException(
                            $"Foundation Kit profile '{profile.id}' must not depend on Extension profile '{dependency.id}'.");
                    }
                }
            }
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

        private static string[] CreateKitBootstrapAssets(IEnumerable<DistributionProfile> profiles,
            string packageFileName, string displayName, IEnumerable<string> payloadAssetPaths, string payloadSourcePath)
        {
            if (!File.Exists(ToProjectPath(KitBootstrapSourcePath)))
            {
                throw new FileNotFoundException("Kit bootstrap source was not found.", ToProjectPath(KitBootstrapSourcePath));
            }

            string requestId = Path.GetFileNameWithoutExtension(packageFileName);
            string safeRequestId = Regex.Replace(requestId ?? "Kit", "[^A-Za-z0-9_.-]", "-");
            string requestAssetPath = KitBootstrapRoot + "/" + KitBootstrapRequestPrefix + safeRequestId + ".json";
            string payloadAssetPath = KitBootstrapRoot + "/" + KitBootstrapPayloadPrefix + safeRequestId + ".unitypackage.bytes";
            Directory.CreateDirectory(ToProjectPath(KitBootstrapRoot));
            AssetDatabase.DeleteAsset(requestAssetPath);
            AssetDatabase.DeleteAsset(payloadAssetPath);

            File.Copy(payloadSourcePath, ToProjectPath(payloadAssetPath), true);
            var request = new KitBootstrapRequest
            {
                requestId = safeRequestId,
                displayName = displayName,
                dependencies = CreatePackageDependencies(GetRequiredUpm(profiles)),
                payloadAssetPath = payloadAssetPath,
                expectedAssetPaths = GetPayloadVerificationPaths(payloadAssetPaths),
                flattenRuntimeSources = profiles.Any(profile =>
                    string.Equals(profile.id, "runtime.core", StringComparison.Ordinal)),
                createAddressablesSettings = GetRequiredUpm(profiles)
                    .Contains("com.unity.addressables", StringComparer.Ordinal)
            };
            File.WriteAllText(ToProjectPath(requestAssetPath), JsonUtility.ToJson(request, true), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(payloadAssetPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(requestAssetPath, ImportAssetOptions.ForceSynchronousImport);

            return new[] { KitBootstrapSourcePath, requestAssetPath, payloadAssetPath };
        }

        private static KitBootstrapPackageDependency[] CreatePackageDependencies(IEnumerable<string> requiredUpm)
        {
            return requiredUpm.Select(packageId =>
            {
                if (!UpmPackageSources.TryGetValue(packageId, out string source))
                {
                    throw new InvalidOperationException($"No package source is configured for {packageId}.");
                }

                return new KitBootstrapPackageDependency
                {
                    packageId = packageId,
                    source = source
                };
            }).ToArray();
        }

        private static string[] GetPayloadVerificationPaths(IEnumerable<string> payloadAssetPaths)
        {
            string[] allPaths = payloadAssetPaths.Where(path => !path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            string[] asmdefPaths = allPaths.Where(path => path.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase))
                .Take(3)
                .ToArray();
            return asmdefPaths.Length > 0 ? asmdefPaths : allPaths.Take(1).ToArray();
        }

        private static string CreateTemporaryPayloadPath()
        {
            return Path.Combine(Path.GetTempPath(), "StellarFramework-Kit-" + Guid.NewGuid().ToString("N") + ".unitypackage");
        }

        private static void DeleteTemporaryPayload(string payloadPath)
        {
            if (!string.IsNullOrWhiteSpace(payloadPath) && File.Exists(payloadPath))
            {
                File.Delete(payloadPath);
            }
        }

        private static void DeleteKitBootstrapAssets(IEnumerable<string> bootstrapAssetPaths)
        {
            if (bootstrapAssetPaths == null)
            {
                return;
            }

            foreach (string assetPath in bootstrapAssetPaths.Where(path => path != KitBootstrapSourcePath))
            {
                AssetDatabase.DeleteAsset(assetPath);
            }
        }

        private static string[] GetRequiredUpm(IEnumerable<DistributionProfile> profiles)
        {
            return profiles
                .SelectMany(profile => profile.requiredUpm ?? Array.Empty<string>())
                .Where(packageId => !string.IsNullOrWhiteSpace(packageId))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(packageId => packageId, StringComparer.Ordinal)
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
            string[] requiredUpm = GetRequiredUpm(closure);
            builder.AppendLine(requiredUpm.Length > 0
                ? "导入 Bootstrap 后会先自动调用 Unity Package Manager 安装下列 UPM 包，再导入 Kit payload。"
                : "本包没有额外 UPM 依赖；Bootstrap 会直接导入 Kit payload。");
            builder.AppendLine();
            AppendGuideList(builder, "自动安装的 UPM 包", requiredUpm, "无");
            AppendGuideList(builder, "不会引入的能力", profile.excludedCapabilities, "无");
            File.WriteAllText(guidePath, builder.ToString(), new UTF8Encoding(false));
        }

        private static void WriteCombinedKitDependencyGuide(string outputDirectory, string outputFileName,
            IEnumerable<DistributionProfile> selectedProfiles, IEnumerable<DistributionProfile> closure)
        {
            string guidePath = Path.Combine(outputDirectory,
                Path.GetFileNameWithoutExtension(outputFileName) + "-Dependencies.md");
            var builder = new StringBuilder();
            builder.AppendLine("# StellarFramework 组合 Kit 导入说明");
            builder.AppendLine();
            builder.AppendLine($"导入 `{outputFileName}` 即可获得所选 Kit 及其去重后的依赖闭包。");
            builder.AppendLine();
            AppendGuideList(builder, "本次选择的 Kit", selectedProfiles
                .Select(profile => profile.displayName)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name)
                .ToArray(), "无");
            AppendGuideList(builder, "本包已包含的 Kit", closure
                .Select(profile => profile.displayName)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name)
                .ToArray(), "无");
            string[] requiredUpm = GetRequiredUpm(closure);
            builder.AppendLine(requiredUpm.Length > 0
                ? "导入 Bootstrap 后会先自动调用 Unity Package Manager 安装下列 UPM 包，再导入 Kit payload。"
                : "本包没有额外 UPM 依赖；Bootstrap 会直接导入 Kit payload。");
            builder.AppendLine();
            AppendGuideList(builder, "自动安装的 UPM 包", requiredUpm, "无");
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
                "导入后打开 `StellarFramework/安装/单包安装器`，点击“一键安装 StellarFramework”，安装器会继续完成依赖安装和完整框架导入。\r\n\r\n" +
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
        internal sealed class DistributionCatalog
        {
            public int schemaVersion;
            public DistributionProfile[] profiles;
        }

        [Serializable]
        internal sealed class DistributionProfile
        {
            public string id;
            public string displayName;
            public string kind;
            public string tier;
            public string category;
            public string availability;
            public string output;
            public string[] sourcePaths;
            public string[] excludedSourcePaths;
            public string[] requiredProfileIds;
            public string[] requiredKits;
            public string[] requiredUpm;
            public string[] excludedCapabilities;
        }

        [Serializable]
        private sealed class KitBootstrapRequest
        {
            public string requestId;
            public string displayName;
            public KitBootstrapPackageDependency[] dependencies;
            public string payloadAssetPath;
            public string[] expectedAssetPaths;
            public bool flattenRuntimeSources;
            public bool createAddressablesSettings;
        }

        [Serializable]
        private sealed class KitBootstrapPackageDependency
        {
            public string packageId;
            public string source;
        }

        private static readonly string[] FullPayloadExcludedPrefixes =
        {
            "Assets/StellarFramework/Editor/StellarToolsHub/Modules/Packaging",
            "Assets/StellarFramework/Editor/KitPackageBootstrap",
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
