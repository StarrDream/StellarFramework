using System.IO;
using UnityEditor;

namespace StellarFrameworkInstaller
{
    internal static class StellarFrameworkCoreImporter
    {
        public static bool IsCoreAlreadyImported()
        {
            return Directory.Exists(StellarFrameworkInstallerPathUtility.ToFullPath(StellarFrameworkInstallerConstants.FrameworkRoot));
        }

        public static bool ImportUnityPackageIfExists(string packagePath, StellarFrameworkInstallerReport report)
        {
            if (string.IsNullOrWhiteSpace(packagePath))
            {
                report?.AddError("UnityPackage 路径为空。");
                return false;
            }

            string fullPath = StellarFrameworkInstallerPathUtility.ToFullPath(packagePath);
            if (!File.Exists(fullPath))
            {
                report?.AddError("找不到 UnityPackage: " + packagePath);
                return false;
            }

            AssetDatabase.ImportPackage(fullPath, false);
            report?.AddMessage("已导入 UnityPackage: " + Path.GetFileName(fullPath));
            return true;
        }

        public static bool ImportCorePayloadOrSkipIfAlreadyPresent(StellarFrameworkInstallerReport report)
        {
            if (IsCoreAlreadyImported())
            {
                report?.AddMessage("已检测到 Assets/StellarFramework，跳过 Core payload 导入。");
                return true;
            }

            return ImportUnityPackageIfExists(StellarFrameworkInstallerConstants.CorePayloadPath, report);
        }

        public static bool ImportHotUpdatePayloadOrSkipIfAlreadyPresent(StellarFrameworkInstallerReport report)
        {
            if (Directory.Exists(StellarFrameworkInstallerPathUtility.ToFullPath(StellarFrameworkInstallerConstants.GameHotUpdateRoot)))
            {
                report?.AddMessage("已检测到 Assets/GameHotUpdate，跳过 HotUpdate addon payload 导入。");
                return true;
            }

            if (!File.Exists(StellarFrameworkInstallerPathUtility.ToFullPath(StellarFrameworkInstallerConstants.HotUpdatePayloadPath)))
            {
                report?.AddWarning("未找到热更新 addon payload，将只创建默认目录和配置。");
                return true;
            }

            return ImportUnityPackageIfExists(StellarFrameworkInstallerConstants.HotUpdatePayloadPath, report);
        }
    }
}
