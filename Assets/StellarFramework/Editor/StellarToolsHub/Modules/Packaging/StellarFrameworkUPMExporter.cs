using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace StellarFramework.Editor.Modules
{
    /// <summary>
    /// 导出标准 UPM 包（com.stellar.framework）。
    /// 从 Assets/StellarFramework 镜像 Runtime/Editor 到临时目录，组装 package.json，
    /// 供发布到 GitHub upm 分支 / tag 后由开发者通过 Package Manager 安装与更新。
    /// </summary>
    internal static class StellarFrameworkUPMExporter
    {
        public const string PackageName = "com.stellar.framework";
        public const string PackageVersion = "1.0.0";

        private const string SourceRoot = "Assets/StellarFramework";
        private const string DefaultExportRoot = "BuildArtifacts/UpmPackage";

        // 只镜像 Runtime + Editor（样例可后装）。
        private static readonly string[] MirroredFolders = { "Runtime", "Editor" };

        // 镜像时需要排除的框架内子目录（避免把工具/生成产物带进包）。
        private static readonly string[] ExcludedSourceSuffixes =
        {
            "/Samples",
            "/Tests"
        };

        [MenuItem("StellarFramework/Packages/导出 UPM 包 (com.stellar.framework)")]
        public static void ExportUPM()
        {
            string exportRoot = ToProjectPath(DefaultExportRoot);
            Directory.CreateDirectory(exportRoot);

            string outputDir = Path.Combine(exportRoot, PackageName);
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, true);
            }

            Directory.CreateDirectory(outputDir);

            foreach (string folder in MirroredFolders)
            {
                string source = Path.Combine(ToProjectPath(SourceRoot), folder);
                string dest = Path.Combine(outputDir, folder);
                if (Directory.Exists(source))
                {
                    CopyDirectory(source, dest);
                }
            }

            WritePackageJson(outputDir);

            AssetDatabase.Refresh();
            Debug.Log($"[UPM] 已导出 UPM 包: {outputDir}");
            EditorUtility.RevealInFinder(outputDir);
        }

        private static void WritePackageJson(string packageDir)
        {
            var dependencies = new Dictionary<string, string>
            {
                { "com.cysharp.unitask", "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask" },
                { "com.unity.nuget.newtonsoft-json", "3.2.2" },
                { "com.unity.addressables", "1.22.3" },
                { "com.code-philosophy.hybridclr", "https://github.com/focus-creative-games/hybridclr_unity.git#4feac30cb2e105992986c737f7f54992b8300e1a" },
                { "com.unity.ugui", "1.0.0" },
                { "com.unity.textmeshpro", "3.0.7" }
            };

            string depsJson = string.Join(",\n",
                dependencies.Select(kv => $"    \"{kv.Key}\": \"{kv.Value}\""));

            string json =
                "{\n" +
                "  \"name\": \"" + PackageName + "\",\n" +
                "  \"version\": \"" + PackageVersion + "\",\n" +
                "  \"displayName\": \"StellarFramework\",\n" +
                "  \"description\": \"StellarFramework - MSV 架构 + 14 Kits 的 Unity 游戏开发框架（热更新/UI/资源/事件/音频等）。\",\n" +
                "  \"unity\": \"2022.3\",\n" +
                "  \"keywords\": [\"stellar\", \"framework\", \"msv\", \"hotupdate\", \"ui\"],\n" +
                "  \"author\": { \"name\": \"StarrDream\" },\n" +
                "  \"dependencies\": {\n" +
                depsJson +
                "\n  }\n" +
                "}\n";

            File.WriteAllText(Path.Combine(packageDir, "package.json"), json);
        }

        private static void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);

            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string fileName = Path.GetFileName(file);
                if (fileName == null || fileName.StartsWith("."))
                {
                    continue;
                }

                // 跳过 .meta（UPM 包由 git 管理，不需要 meta 文件）
                if (fileName.EndsWith(".meta", System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string relPath = GetRelativePath(sourceDir, file);
                if (IsExcluded(relPath))
                {
                    continue;
                }

                File.Copy(file, Path.Combine(destDir, relPath), true);
            }

            foreach (string dir in Directory.GetDirectories(sourceDir))
            {
                string dirName = Path.GetFileName(dir);
                string relPath = GetRelativePath(sourceDir, dir);
                if (string.IsNullOrEmpty(dirName) || IsExcluded(relPath))
                {
                    continue;
                }

                CopyDirectory(dir, Path.Combine(destDir, relPath));
            }
        }

        private static bool IsExcluded(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
            {
                return false;
            }

            string normalized = relativePath.Replace('\\', '/');
            return ExcludedSourceSuffixes.Any(suffix => normalized.StartsWith(suffix, System.StringComparison.OrdinalIgnoreCase));
        }

        private static string GetRelativePath(string root, string fullPath)
        {
            string rootNorm = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string fullNorm = Path.GetFullPath(fullPath);
            return fullNorm.StartsWith(rootNorm, System.StringComparison.OrdinalIgnoreCase)
                ? fullNorm.Substring(rootNorm.Length)
                : Path.GetFileName(fullPath);
        }

        private static string ToProjectPath(string assetRelativePath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.Combine(projectRoot, assetRelativePath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
