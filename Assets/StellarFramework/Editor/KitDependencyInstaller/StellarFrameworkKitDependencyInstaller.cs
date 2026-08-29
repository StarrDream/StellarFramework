using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace StellarFramework.Editor.KitDependencies
{
    /// <summary>
    /// Imported with Kit unitypackages. It reads the small request file generated for that package,
    /// installs only the required UPM dependencies, and removes the request file after completion.
    /// </summary>
    [InitializeOnLoad]
    internal static class StellarFrameworkKitDependencyInstaller
    {
        private const string RequestRoot = "Assets/StellarFramework/Editor/KitDependencyInstaller";
        private const string RequestSearchPattern = "__StellarFramework-KitDependencies-*.json";
        private const string ManifestPath = "Packages/manifest.json";
        private const string FailureSessionKeyPrefix = "StellarFramework.KitDependencyInstaller.Failed.";

        private static AddRequest _currentRequest;
        private static string _currentRequestAssetPath;
        private static string _currentPackageId;

        static StellarFrameworkKitDependencyInstaller()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
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
                string error = _currentRequest.Error == null
                    ? "Unknown package error."
                    : _currentRequest.Error.message;
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
                KitDependencyRequest request = ReadRequest(requestAssetPath);
                if (request == null || request.dependencies == null || request.dependencies.Length == 0)
                {
                    DeleteRequest(requestAssetPath);
                    continue;
                }

                PackageDependency missingPackage = request.dependencies.FirstOrDefault(dependency =>
                    dependency != null &&
                    !string.IsNullOrWhiteSpace(dependency.packageId) &&
                    !IsPackageInManifest(dependency.packageId));
                if (missingPackage == null)
                {
                    Debug.Log($"[StellarFramework] {request.displayName} 的 Kit 依赖已就绪。");
                    DeleteRequest(requestAssetPath);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(missingPackage.source))
                {
                    LogFailureOnce(requestAssetPath, missingPackage.packageId,
                        $"[StellarFramework] 未定义 Kit 依赖安装源：{missingPackage.packageId}");
                    continue;
                }

                if (SessionState.GetBool(FailureSessionKeyPrefix + requestAssetPath + missingPackage.packageId, false))
                {
                    continue;
                }

                _currentRequestAssetPath = requestAssetPath;
                _currentPackageId = missingPackage.packageId;
                Debug.Log($"[StellarFramework] 正在安装 {request.displayName} 的依赖：{missingPackage.packageId}");
                _currentRequest = Client.Add(missingPackage.source);
                return;
            }
        }

        private static IEnumerable<string> GetRequestAssetPaths()
        {
            string absoluteRoot = ToAbsoluteProjectPath(RequestRoot);
            if (!Directory.Exists(absoluteRoot))
            {
                return Array.Empty<string>();
            }

            return Directory.GetFiles(absoluteRoot, RequestSearchPattern, SearchOption.TopDirectoryOnly)
                .Select(path => RequestRoot + "/" + Path.GetFileName(path))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
        }

        private static KitDependencyRequest ReadRequest(string assetPath)
        {
            string absolutePath = ToAbsoluteProjectPath(assetPath);
            if (!File.Exists(absolutePath))
            {
                return null;
            }

            try
            {
                return JsonUtility.FromJson<KitDependencyRequest>(File.ReadAllText(absolutePath));
            }
            catch (Exception exception)
            {
                Debug.LogError($"[StellarFramework] 无法读取 Kit 依赖清单 {assetPath}: {exception.Message}");
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

            string manifest = File.ReadAllText(manifestPath);
            return Regex.IsMatch(manifest, "\\\"" + Regex.Escape(packageId) + "\\\"\\s*:");
        }

        private static void DeleteRequest(string assetPath)
        {
            AssetDatabase.DeleteAsset(assetPath);
            AssetDatabase.Refresh();
        }

        private static void LogFailureOnce(string requestPath, string packageId, string message)
        {
            string sessionKey = FailureSessionKeyPrefix + requestPath + packageId;
            if (SessionState.GetBool(sessionKey, false))
            {
                return;
            }

            SessionState.SetBool(sessionKey, true);
            Debug.LogError(message);
        }

        private static string ToAbsoluteProjectPath(string projectRelativePath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.Combine(projectRoot, projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        [Serializable]
        private sealed class KitDependencyRequest
        {
            public string requestId;
            public string displayName;
            public PackageDependency[] dependencies;
        }

        [Serializable]
        private sealed class PackageDependency
        {
            public string packageId;
            public string source;
        }
    }
}
