using System.IO;
using UnityEditor;

namespace StellarFrameworkInstaller
{
    internal static class StellarFrameworkInstallerPackageBuilder
    {
        private const string ExportRoot = "Assets/StellarFrameworkInstaller/Exports";

        [MenuItem("StellarFramework/Installer/Export Installer Package")]
        public static void ExportInstallerPackage()
        {
            ExportPackage(
                "StellarFrameworkInstaller.unitypackage",
                new[]
                {
                    "Assets/StellarFrameworkInstaller/Editor",
                    "Assets/StellarFrameworkInstaller/Docs",
                    "Assets/StellarFrameworkInstaller/Payloads",
                    "Assets/StellarFrameworkInstaller/OfflinePackages"
                });
        }

        [MenuItem("StellarFramework/Installer/Build Payloads And Installer Package")]
        public static void BuildPayloadsAndInstallerPackage()
        {
            ExportPackageToPath(
                StellarFrameworkInstallerConstants.CorePayloadPath,
                new[] { "Assets/StellarFramework" });

            if (Directory.Exists(StellarFrameworkInstallerPathUtility.ToFullPath(StellarFrameworkInstallerConstants.GameHotUpdateRoot)))
            {
                ExportPackageToPath(
                    StellarFrameworkInstallerConstants.HotUpdatePayloadPath,
                    new[] { "Assets/GameHotUpdate" });
            }

            ExportInstallerPackage();
        }

        [MenuItem("StellarFramework/Installer/Export Core Payload")]
        public static void ExportCorePayload()
        {
            ExportPackage(
                "StellarFrameworkCore.unitypackage",
                new[] { "Assets/StellarFramework" });
        }

        [MenuItem("StellarFramework/Installer/Export HotUpdate Addon Payload")]
        public static void ExportHotUpdateAddonPayload()
        {
            ExportPackage(
                "StellarFrameworkHotUpdateAddon.unitypackage",
                new[] { "Assets/GameHotUpdate" });
        }

        private static void ExportPackage(string fileName, string[] assetPaths)
        {
            string outputPath = StellarFrameworkInstallerPathUtility.ToFullPath(ExportRoot + "/" + fileName);
            ExportPackageToPath(outputPath, assetPaths);
            EditorUtility.RevealInFinder(outputPath);
        }

        private static void ExportPackageToPath(string outputPath, string[] assetPaths)
        {
            string fullOutputPath = StellarFrameworkInstallerPathUtility.ToFullPath(outputPath);
            string directory = Path.GetDirectoryName(fullOutputPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            AssetDatabase.ExportPackage(assetPaths, fullOutputPath, ExportPackageOptions.Recurse);
            AssetDatabase.Refresh();
        }
    }
}
