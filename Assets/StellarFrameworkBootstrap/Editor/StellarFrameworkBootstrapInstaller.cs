using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;

namespace StellarFrameworkBootstrap
{
    internal sealed class StellarFrameworkBootstrapInstaller
    {
        private sealed class PackageSpec
        {
            public string PackageId;
            public string Version;
            public string GitUrl;
        }

        private readonly Queue<PackageSpec> _pendingPackages = new Queue<PackageSpec>();
        private readonly List<string> _messages = new List<string>();
        private readonly List<string> _errors = new List<string>();
        private AddRequest _currentRequest;
        private string _pendingUnityPackagePath = string.Empty;

        public bool IsBusy => _currentRequest != null || _pendingPackages.Count > 0 || !string.IsNullOrWhiteSpace(_pendingUnityPackagePath);
        public IReadOnlyList<string> Messages => _messages;
        public IReadOnlyList<string> Errors => _errors;

        public void StartSinglePackageInstall()
        {
            Reset();

            _pendingUnityPackagePath = StellarFrameworkBootstrapPackageUtility.ExtractEmbeddedPayloadToTempPackage();
            if (string.IsNullOrWhiteSpace(_pendingUnityPackagePath))
            {
                _errors.Add("未找到内嵌 payload。请重新使用“导出单包安装版”生成分发包。");
                return;
            }

            _messages.Add("已找到内嵌 payload，开始检查并安装依赖。");
            EnqueueBaseDependencies();
            EnqueueFullDependencies();
            StartNextPackage();

            if (_currentRequest == null)
            {
                Tick();
            }
        }

        public void Tick()
        {
            if (_currentRequest != null)
            {
                if (!_currentRequest.IsCompleted)
                {
                    return;
                }

                if (_currentRequest.Status == StatusCode.Failure)
                {
                    string error = _currentRequest.Error != null ? _currentRequest.Error.message : "Unknown package error.";
                    _errors.Add("依赖安装失败：" + error);
                    _currentRequest = null;
                    _pendingPackages.Clear();
                    CleanupPendingPayload();
                    return;
                }

                _messages.Add("依赖安装完成，继续下一步。");
                _currentRequest = null;
            }

            if (_pendingPackages.Count > 0)
            {
                StartNextPackage();
                return;
            }

            if (!string.IsNullOrWhiteSpace(_pendingUnityPackagePath))
            {
                if (StellarFrameworkBootstrapPackageUtility.EnsureDefaultAddressablesSettings(out string addressablesMessage))
                {
                    _messages.Add(addressablesMessage);
                }
                else if (!string.IsNullOrWhiteSpace(addressablesMessage))
                {
                    _messages.Add(addressablesMessage);
                }

                string unityPackagePath = _pendingUnityPackagePath;
                _pendingUnityPackagePath = string.Empty;
                ImportUnityPackage(unityPackagePath);
                StellarFrameworkBootstrapPackageUtility.TryDeleteFile(unityPackagePath);
            }
        }

        private void Reset()
        {
            CleanupPendingPayload();
            _pendingPackages.Clear();
            _messages.Clear();
            _errors.Clear();
            _currentRequest = null;
            _pendingUnityPackagePath = string.Empty;
        }

        private void EnqueueBaseDependencies()
        {
            EnqueuePackage("com.cysharp.unitask", string.Empty,
                "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask");
            EnqueuePackage("com.unity.nuget.newtonsoft-json", "3.2.2", string.Empty);
        }

        private void EnqueueFullDependencies()
        {
            EnqueuePackage("com.unity.addressables", "1.22.3", string.Empty);
            EnqueuePackage("com.code-philosophy.hybridclr", string.Empty,
                "https://github.com/focus-creative-games/hybridclr_unity.git");
        }

        private void EnqueuePackage(string packageId, string version, string gitUrl)
        {
            if (IsPackageListedInManifest(packageId))
            {
                _messages.Add(packageId + " 已安装，跳过。");
                return;
            }

            _pendingPackages.Enqueue(new PackageSpec
            {
                PackageId = packageId,
                Version = version,
                GitUrl = gitUrl
            });
        }

        private bool IsPackageListedInManifest(string packageId)
        {
            string manifestPath = StellarFrameworkBootstrapPackageUtility.GetManifestPath();
            if (!File.Exists(manifestPath))
            {
                return false;
            }

            string manifest = File.ReadAllText(manifestPath);
            return StellarFrameworkBootstrapPackageUtility.ManifestContainsPackage(manifest, packageId);
        }

        private void StartNextPackage()
        {
            if (_pendingPackages.Count == 0)
            {
                return;
            }

            PackageSpec spec = _pendingPackages.Dequeue();
            string source = StellarFrameworkBootstrapPackageUtility.BuildPackageSource(
                spec.PackageId,
                spec.Version,
                spec.GitUrl);

            _messages.Add("正在安装依赖：" + source);
            _currentRequest = Client.Add(source);
        }

        private void ImportUnityPackage(string unityPackagePath)
        {
            if (string.IsNullOrWhiteSpace(unityPackagePath) || !File.Exists(unityPackagePath))
            {
                _errors.Add("内嵌 payload 临时包无效：" + unityPackagePath);
                return;
            }

            AssetDatabase.ImportPackage(unityPackagePath, false);
            AssetDatabase.Refresh();
            StellarFrameworkBootstrapPackageUtility.RequestOpenToolsHub();
            _messages.Add("完整框架导入完成：" + Path.GetFileName(unityPackagePath));
            _messages.Add("安装成功后将自动打开 Tools Hub。");
        }

        private void CleanupPendingPayload()
        {
            if (string.IsNullOrWhiteSpace(_pendingUnityPackagePath))
            {
                return;
            }

            StellarFrameworkBootstrapPackageUtility.TryDeleteFile(_pendingUnityPackagePath);
            _pendingUnityPackagePath = string.Empty;
        }
    }
}
