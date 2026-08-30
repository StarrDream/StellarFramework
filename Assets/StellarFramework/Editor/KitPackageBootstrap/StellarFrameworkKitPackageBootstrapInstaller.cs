using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace StellarFramework.Editor.KitBootstrap
{
    /// <summary>
    /// This bootstrap is the only code imported before a Kit payload. It has no StellarFramework or
    /// third-party package dependency, so it can install the requested UPM packages before importing the Kit.
    /// </summary>
    [InitializeOnLoad]
    internal static class StellarFrameworkKitPackageBootstrapInstaller
    {
        private const string BootstrapRoot = "Assets/StellarFramework/Editor/KitPackageBootstrap";
        private const string RequestSearchPattern = "__StellarFramework-KitBootstrap-*.json";
        private const string ManifestPath = "Packages/manifest.json";
        private const string SourceProjectMarker =
            "Assets/StellarFramework/Editor/StellarToolsHub/Modules/Packaging/StellarFrameworkPackagePublisher.cs";
        private const string FailureSessionKeyPrefix = "StellarFramework.KitBootstrap.Failed.";
        private const string PendingRequestSessionKey = "StellarFramework.KitBootstrap.PendingRequest";
        private const string PendingPayloadSessionKey = "StellarFramework.KitBootstrap.PendingPayload";
        private const string ProcessedSessionKey = "StellarFramework.KitBootstrap.Processed";

        private static AddRequest _currentRequest;
        private static string _currentRequestAssetPath;
        private static string _currentPackageId;

        static StellarFrameworkKitPackageBootstrapInstaller()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (IsFrameworkSourceProject() || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            if (TryCompletePendingPayloadImport())
            {
                return;
            }

            if (_currentRequest != null)
            {
                HandleCurrentRequest();
                return;
            }

            ProcessNextRequest();
        }

        private static void HandleCurrentRequest()
        {
            if (!_currentRequest.IsCompleted)
            {
                return;
            }

            if (_currentRequest.Status == StatusCode.Failure)
            {
                string error = _currentRequest.Error == null ? "Unknown package error." : _currentRequest.Error.message;
                SessionState.SetBool(FailureSessionKeyPrefix + _currentRequestAssetPath + _currentPackageId, true);
                Debug.LogError($"[StellarFramework] 安装 Kit 依赖失败 {_currentPackageId}: {error}");
            }
            else
            {
                SessionState.EraseBool(FailureSessionKeyPrefix + _currentRequestAssetPath + _currentPackageId);
                Debug.Log($"[StellarFramework] Kit 依赖已安装：{_currentPackageId}");
            }

            _currentRequest = null;
            _currentRequestAssetPath = null;
            _currentPackageId = null;
        }

        private static void ProcessNextRequest()
        {
            foreach (string requestAssetPath in GetRequestAssetPaths())
            {
                BootstrapRequest request = ReadRequest(requestAssetPath);
                if (request == null || request.dependencies == null || string.IsNullOrWhiteSpace(request.payloadAssetPath))
                {
                    LogFailureOnce(requestAssetPath, "invalid", "[StellarFramework] Kit 安装请求无效：" + requestAssetPath);
                    continue;
                }

                PackageDependency missing = request.dependencies.FirstOrDefault(dependency =>
                    dependency != null &&
                    !string.IsNullOrWhiteSpace(dependency.packageId) &&
                    !IsPackageInManifest(dependency.packageId));
                if (missing != null)
                {
                    InstallDependency(requestAssetPath, request.displayName, missing);
                    return;
                }

                if (request.createAddressablesSettings)
                {
                    EnsureDefaultAddressablesSettings();
                }

                ImportPayload(requestAssetPath, request);
                return;
            }

            TryCleanupBootstrap();
        }

        private static void InstallDependency(string requestAssetPath, string displayName, PackageDependency dependency)
        {
            string failureKey = FailureSessionKeyPrefix + requestAssetPath + dependency.packageId;
            if (SessionState.GetBool(failureKey, false))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(dependency.source))
            {
                LogFailureOnce(requestAssetPath, dependency.packageId,
                    $"[StellarFramework] 未定义 Kit 依赖安装源：{dependency.packageId}");
                return;
            }

            try
            {
                _currentRequestAssetPath = requestAssetPath;
                _currentPackageId = dependency.packageId;
                Debug.Log($"[StellarFramework] 正在安装 {displayName} 的依赖：{dependency.packageId}");
                _currentRequest = Client.Add(dependency.source);
            }
            catch (Exception exception)
            {
                LogFailureOnce(requestAssetPath, dependency.packageId,
                    $"[StellarFramework] 无法开始安装 {dependency.packageId}: {exception.Message}");
            }
        }

        private static void ImportPayload(string requestAssetPath, BootstrapRequest request)
        {
            string payloadPath = ToAbsoluteProjectPath(request.payloadAssetPath);
            if (!File.Exists(payloadPath))
            {
                LogFailureOnce(requestAssetPath, "payload", "[StellarFramework] 未找到 Kit payload：" + request.payloadAssetPath);
                return;
            }

            string tempPackagePath = Path.Combine(Path.GetTempPath(),
                "StellarFramework-Kit-" + Guid.NewGuid().ToString("N") + ".unitypackage");
            try
            {
                File.Copy(payloadPath, tempPackagePath, true);
                SessionState.SetString(PendingRequestSessionKey, requestAssetPath);
                SessionState.SetString(PendingPayloadSessionKey, tempPackagePath);
                SessionState.SetBool(ProcessedSessionKey, true);

                // Set pending state before ImportPackage because it can trigger a domain reload.
                AssetDatabase.ImportPackage(tempPackagePath, false);
                AssetDatabase.Refresh();
                Debug.Log($"[StellarFramework] 已导入 {request.displayName} 的 Kit payload。");
            }
            catch (Exception exception)
            {
                TryDeleteFile(tempPackagePath);
                SessionState.EraseString(PendingRequestSessionKey);
                SessionState.EraseString(PendingPayloadSessionKey);
                LogFailureOnce(requestAssetPath, "payload", "[StellarFramework] 导入 Kit payload 失败：" + exception.Message);
            }
        }

        private static bool TryCompletePendingPayloadImport()
        {
            string requestAssetPath = SessionState.GetString(PendingRequestSessionKey, string.Empty);
            if (string.IsNullOrWhiteSpace(requestAssetPath))
            {
                return false;
            }

            BootstrapRequest request = ReadRequest(requestAssetPath);
            if (request == null)
            {
                ClearPendingPayload();
                return false;
            }

            if (!PayloadWasImported(request))
            {
                LogFailureOnce(requestAssetPath, "verification",
                    "[StellarFramework] Kit payload 导入后校验失败，请重新导入该 Kit 安装包。");
                ClearPendingPayload();
                return true;
            }

            string tempPackagePath = SessionState.GetString(PendingPayloadSessionKey, string.Empty);
            AssetDatabase.DeleteAsset(requestAssetPath);
            AssetDatabase.DeleteAsset(request.payloadAssetPath);
            AssetDatabase.Refresh();
            TryDeleteFile(tempPackagePath);
            ClearPendingPayload();
            Debug.Log($"[StellarFramework] {request.displayName} 已完成安装。");
            return true;
        }

        private static bool PayloadWasImported(BootstrapRequest request)
        {
            return request.expectedAssetPaths != null && request.expectedAssetPaths.Length > 0 &&
                   request.expectedAssetPaths.All(path => File.Exists(ToAbsoluteProjectPath(path)));
        }

        private static void EnsureDefaultAddressablesSettings()
        {
            Type settingsType = FindType("UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject");
            if (settingsType == null)
            {
                Debug.LogWarning("[StellarFramework] Addressables Editor 尚未加载，跳过默认 Settings 创建。");
                return;
            }

            object settings = settingsType.GetProperty("Settings", BindingFlags.Public | BindingFlags.Static)
                ?.GetValue(null, null);
            if (settings != null)
            {
                return;
            }

            MethodInfo getSettings = settingsType.GetMethod("GetSettings", BindingFlags.Public | BindingFlags.Static,
                null, new[] { typeof(bool) }, null);
            if (getSettings?.Invoke(null, new object[] { true }) == null)
            {
                Debug.LogWarning("[StellarFramework] 未能自动创建 Addressables Settings。");
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static IEnumerable<string> GetRequestAssetPaths()
        {
            string absoluteRoot = ToAbsoluteProjectPath(BootstrapRoot);
            if (!Directory.Exists(absoluteRoot))
            {
                return Array.Empty<string>();
            }

            return Directory.GetFiles(absoluteRoot, RequestSearchPattern, SearchOption.TopDirectoryOnly)
                .Select(path => BootstrapRoot + "/" + Path.GetFileName(path))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
        }

        private static BootstrapRequest ReadRequest(string assetPath)
        {
            string absolutePath = ToAbsoluteProjectPath(assetPath);
            if (!File.Exists(absolutePath))
            {
                return null;
            }

            try
            {
                return JsonUtility.FromJson<BootstrapRequest>(File.ReadAllText(absolutePath));
            }
            catch (Exception exception)
            {
                Debug.LogError($"[StellarFramework] 无法读取 Kit 安装请求 {assetPath}: {exception.Message}");
                return null;
            }
        }

        private static bool IsPackageInManifest(string packageId)
        {
            string manifestPath = ToAbsoluteProjectPath(ManifestPath);
            if (!File.Exists(manifestPath))
            {
                return false;
            }

            return Regex.IsMatch(File.ReadAllText(manifestPath), "\\\"" + Regex.Escape(packageId) + "\\\"\\s*:");
        }

        private static bool IsFrameworkSourceProject()
        {
            return File.Exists(ToAbsoluteProjectPath(SourceProjectMarker));
        }

        private static void TryCleanupBootstrap()
        {
            if (!SessionState.GetBool(ProcessedSessionKey, false) || GetRequestAssetPaths().Any())
            {
                return;
            }

            SessionState.EraseBool(ProcessedSessionKey);
            EditorApplication.delayCall += () =>
            {
                if (!IsFrameworkSourceProject() && AssetDatabase.IsValidFolder(BootstrapRoot))
                {
                    AssetDatabase.DeleteAsset(BootstrapRoot);
                    AssetDatabase.Refresh();
                }
            };
        }

        private static void ClearPendingPayload()
        {
            SessionState.EraseString(PendingRequestSessionKey);
            SessionState.EraseString(PendingPayloadSessionKey);
        }

        private static void LogFailureOnce(string requestPath, string key, string message)
        {
            string sessionKey = FailureSessionKeyPrefix + requestPath + key;
            if (SessionState.GetBool(sessionKey, false))
            {
                return;
            }

            SessionState.SetBool(sessionKey, true);
            Debug.LogError(message);
        }

        private static Type FindType(string fullName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(type => type != null);
        }

        private static void TryDeleteFile(string path)
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private static string ToAbsoluteProjectPath(string projectRelativePath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.Combine(projectRoot, projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        [Serializable]
        private sealed class BootstrapRequest
        {
            public string requestId;
            public string displayName;
            public PackageDependency[] dependencies;
            public string payloadAssetPath;
            public string[] expectedAssetPaths;
            public bool createAddressablesSettings;
        }

        [Serializable]
        private sealed class PackageDependency
        {
            public string packageId;
            public string source;
        }
    }
}
