using System.Collections.Generic;
using System.IO;
using System.Linq;
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
