using System.IO;
using UnityEditor;

namespace StellarFrameworkInstaller
{
    internal static class StellarFrameworkHotUpdateLayoutInitializer
    {
        public static void CreateDefaultLayout(StellarFrameworkInstallerReport report)
        {
            CreateGameHotUpdateLayout(StellarFrameworkInstallerConstants.GameHotUpdateRoot, report);
        }

        public static void CreateGameHotUpdateLayout(string rootAssetPath, StellarFrameworkInstallerReport report)
        {
            string root = StellarFrameworkInstallerPathUtility.NormalizeAssetPath(rootAssetPath);
            if (string.IsNullOrWhiteSpace(root))
            {
                report?.AddError("GameHotUpdate 根目录为空。");
                return;
            }

            EnsureDirectory(root, report);
            EnsureDirectory(root + "/Code", report);
            EnsureDirectory(root + "/Metadata", report);
            EnsureDirectory(root + "/Manifest", report);
            EnsureDirectory(root + "/Source", report);
            EnsureDefaultManifest(root + "/Manifest/HotUpdateManifest.json", report);
            AssetDatabase.Refresh();
        }

        public static string BuildDefaultManifestJson()
        {
            return "{\n"
                   + "  \"version\": \"1.0.0\",\n"
                   + "  \"buildTarget\": \"StandaloneWindows64\",\n"
                   + "  \"catalogPathOrUrl\": \"\",\n"
                   + "  \"catalogHashPathOrUrl\": \"\",\n"
                   + "  \"hotUpdateAssemblyKey\": \"HotUpdate.dll.bytes\",\n"
                   + "  \"hotUpdateAssemblySha256\": \"\",\n"
                   + "  \"hotUpdateEntryClass\": \"HotUpdate.HotUpdateMain\",\n"
                   + "  \"hotUpdateEntryMethod\": \"Main\",\n"
                   + "  \"aotMetadataKeys\": [\n"
                   + "    \"mscorlib.dll.bytes\",\n"
                   + "    \"System.dll.bytes\",\n"
                   + "    \"System.Core.dll.bytes\"\n"
                   + "  ]\n"
                   + "}\n";
        }

        private static void EnsureDirectory(string assetPath, StellarFrameworkInstallerReport report)
        {
            string fullPath = StellarFrameworkInstallerPathUtility.ToFullPath(assetPath);
            if (Directory.Exists(fullPath))
            {
                return;
            }

            Directory.CreateDirectory(fullPath);
            report?.AddMessage("已创建目录: " + assetPath);
        }

        private static void EnsureDefaultManifest(string manifestAssetPath, StellarFrameworkInstallerReport report)
        {
            string fullPath = StellarFrameworkInstallerPathUtility.ToFullPath(manifestAssetPath);
            if (File.Exists(fullPath))
            {
                report?.AddMessage("已存在 HotUpdateManifest.json，保留现有文件。");
                return;
            }

            File.WriteAllText(fullPath, BuildDefaultManifestJson());
            report?.AddMessage("已创建默认 HotUpdateManifest.json。");
        }
    }
}
