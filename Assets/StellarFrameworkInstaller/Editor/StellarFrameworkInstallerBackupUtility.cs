using System;
using System.IO;

namespace StellarFrameworkInstaller
{
    internal static class StellarFrameworkInstallerBackupUtility
    {
        public static string BackupAssetPath(
            string sourceAssetPath,
            string backupRootAssetPath,
            string timestamp,
            StellarFrameworkInstallerReport report)
        {
            if (string.IsNullOrWhiteSpace(sourceAssetPath))
            {
                report?.AddError("备份源路径为空。");
                return string.Empty;
            }

            string sourceFullPath = StellarFrameworkInstallerPathUtility.ToFullPath(sourceAssetPath);
            if (!File.Exists(sourceFullPath) && !Directory.Exists(sourceFullPath))
            {
                report?.AddError("找不到需要备份的路径: " + sourceAssetPath);
                return string.Empty;
            }

            string safeTimestamp = string.IsNullOrWhiteSpace(timestamp)
                ? DateTime.Now.ToString("yyyyMMdd_HHmmss")
                : timestamp.Trim();
            string backupRoot = StellarFrameworkInstallerPathUtility.NormalizeAssetPath(backupRootAssetPath);
            string backupFolder = backupRoot + "/" + Path.GetFileName(sourceAssetPath.TrimEnd('/', '\\')) + "_" + safeTimestamp;
            string backupFullPath = StellarFrameworkInstallerPathUtility.ToFullPath(backupFolder);
            Directory.CreateDirectory(backupFullPath);

            if (File.Exists(sourceFullPath))
            {
                File.Copy(sourceFullPath, Path.Combine(backupFullPath, Path.GetFileName(sourceFullPath)), false);
            }
            else
            {
                CopyDirectory(sourceFullPath, backupFullPath);
            }

            report?.AddMessage("已备份: " + sourceAssetPath + " -> " + backupFolder);
            return backupFolder;
        }

        public static string BackupDefaultHotUpdateTargets(StellarFrameworkInstallerReport report)
        {
            string backupRoot = "Assets/StellarFrameworkInstaller/Backups";
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string latestBackup = string.Empty;

            if (Directory.Exists(StellarFrameworkInstallerPathUtility.ToFullPath(StellarFrameworkInstallerConstants.GameHotUpdateRoot)))
            {
                latestBackup = BackupAssetPath(StellarFrameworkInstallerConstants.GameHotUpdateRoot, backupRoot, timestamp, report);
            }

            if (File.Exists(StellarFrameworkInstallerPathUtility.ToFullPath(StellarFrameworkInstallerConstants.AAWorkflowConfigAssetPath)))
            {
                latestBackup = BackupAssetPath(StellarFrameworkInstallerConstants.AAWorkflowConfigAssetPath, backupRoot, timestamp, report);
            }

            if (File.Exists(StellarFrameworkInstallerPathUtility.ToFullPath(StellarFrameworkInstallerConstants.ResKitRuntimeSettingsAssetPath)))
            {
                latestBackup = BackupAssetPath(StellarFrameworkInstallerConstants.ResKitRuntimeSettingsAssetPath, backupRoot, timestamp, report);
            }

            return latestBackup;
        }

        private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
        {
            Directory.CreateDirectory(destinationDirectory);
            foreach (string file in Directory.GetFiles(sourceDirectory))
            {
                File.Copy(file, Path.Combine(destinationDirectory, Path.GetFileName(file)), false);
            }

            foreach (string directory in Directory.GetDirectories(sourceDirectory))
            {
                CopyDirectory(directory, Path.Combine(destinationDirectory, Path.GetFileName(directory)));
            }
        }
    }
}
