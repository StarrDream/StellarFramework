using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;

namespace StellarFrameworkInstaller
{
    internal sealed class StellarFrameworkDependencyInstaller
    {
        private AddRequest _currentRequest;
        private string _currentPackageName = string.Empty;

        public bool IsInstalling => _currentRequest != null && !_currentRequest.IsCompleted;

        public bool IsPackageListedInManifest(string packageId)
        {
            string manifestPath = StellarFrameworkInstallerPathUtility.ToFullPath("Packages/manifest.json");
            if (!File.Exists(manifestPath))
            {
                return false;
            }

            string manifest = File.ReadAllText(manifestPath);
            return StellarFrameworkPackageManifestUtility.ManifestContainsPackage(manifest, packageId);
        }

        public void InstallPackage(
            string packageId,
            string version,
            string gitUrl,
            StellarFrameworkInstallerReport report)
        {
            if (IsInstalling)
            {
                report?.AddWarning("Package Manager 正在安装 " + _currentPackageName + "，请等待当前请求完成。");
                return;
            }

            if (IsPackageListedInManifest(packageId))
            {
                report?.AddMessage(packageId + " 已存在，跳过安装。");
                return;
            }

            string source = StellarFrameworkPackageManifestUtility.BuildPackageSource(packageId, version, gitUrl);
            StartAddRequest(packageId, source, report);
        }

        public void InstallLocalPackage(string packageId, string localPath, StellarFrameworkInstallerReport report)
        {
            if (IsPackageListedInManifest(packageId))
            {
                report?.AddMessage(packageId + " 已存在，跳过本地包安装。");
                return;
            }

            if (string.IsNullOrWhiteSpace(localPath))
            {
                report?.AddWarning("本地包路径为空。");
                return;
            }

            string fullPath = StellarFrameworkInstallerPathUtility.ToFullPath(localPath);
            if (File.Exists(fullPath) && fullPath.EndsWith(".unitypackage", System.StringComparison.OrdinalIgnoreCase))
            {
                AssetDatabase.ImportPackage(fullPath, false);
                report?.AddMessage("已导入 UnityPackage: " + Path.GetFileName(fullPath));
                return;
            }

            if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
            {
                report?.AddError("找不到本地包: " + localPath);
                return;
            }

            StartAddRequest(packageId, StellarFrameworkPackageManifestUtility.BuildFilePackageSource(fullPath), report);
        }

        public bool Poll(StellarFrameworkInstallerReport report)
        {
            if (_currentRequest == null)
            {
                return true;
            }

            if (!_currentRequest.IsCompleted)
            {
                return false;
            }

            if (_currentRequest.Status == StatusCode.Failure)
            {
                string error = _currentRequest.Error != null ? _currentRequest.Error.message : "Unknown package error.";
                report?.AddError("Package Manager 安装失败: " + _currentPackageName + " / " + error);
            }
            else
            {
                report?.AddMessage("Package Manager 安装完成: " + _currentPackageName);
            }

            _currentRequest = null;
            _currentPackageName = string.Empty;
            return true;
        }

        private void StartAddRequest(string packageName, string source, StellarFrameworkInstallerReport report)
        {
            _currentPackageName = packageName;
            _currentRequest = Client.Add(source);
            report?.AddMessage("开始安装包: " + source);
        }
    }
}
