using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace StellarFrameworkBootstrap
{
    [InitializeOnLoad]
    internal static class StellarFrameworkBootstrapPackageUtility
    {
        private const string EmbeddedPayloadRelativePath =
            "Assets/StellarFrameworkBootstrap/Payloads/StellarFramework-FullHotUpdate-Payload.unitypackage.bytes";
        private const string ToolsHubMenuPath = "StellarFramework/Tools Hub";
        private const string PendingOpenToolsHubSessionKey = "StellarFrameworkBootstrap.PendingOpenToolsHub";
        private const string PendingOpenToolsHubAttemptKey = "StellarFrameworkBootstrap.PendingOpenToolsHubAttempt";
        private const int MaxOpenToolsHubAttempts = 300;

        static StellarFrameworkBootstrapPackageUtility()
        {
            EditorApplication.update -= TryOpenToolsHubFromSession;
            EditorApplication.update += TryOpenToolsHubFromSession;
        }

        public static bool ManifestContainsPackage(string manifestJson, string packageId)
        {
            if (string.IsNullOrWhiteSpace(manifestJson) || string.IsNullOrWhiteSpace(packageId))
            {
                return false;
            }

            string escapedPackageId = Regex.Escape(packageId.Trim());
            return Regex.IsMatch(manifestJson, "\"" + escapedPackageId + "\"\\s*:", RegexOptions.CultureInvariant);
        }

        public static string BuildPackageSource(string packageId, string version, string gitUrl)
        {
            if (!string.IsNullOrWhiteSpace(gitUrl))
            {
                return gitUrl.Trim();
            }

            if (string.IsNullOrWhiteSpace(packageId))
            {
                throw new ArgumentException("Package id is empty.", nameof(packageId));
            }

            return string.IsNullOrWhiteSpace(version)
                ? packageId.Trim()
                : packageId.Trim() + "@" + version.Trim();
        }

        public static string GetProjectRootPath()
        {
            return Directory.GetParent(UnityEngine.Application.dataPath)?.FullName
                   ?? UnityEngine.Application.dataPath;
        }

        public static string GetManifestPath()
        {
            return Path.Combine(GetProjectRootPath(), "Packages", "manifest.json");
        }

        public static string GetEmbeddedPayloadPath()
        {
            return Path.Combine(GetProjectRootPath(), EmbeddedPayloadRelativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        public static string ExtractEmbeddedPayloadToTempPackage()
        {
            string payloadPath = GetEmbeddedPayloadPath();
            if (!File.Exists(payloadPath))
            {
                return string.Empty;
            }

            string tempPackagePath = Path.Combine(Path.GetTempPath(),
                "StellarFramework-" + Guid.NewGuid().ToString("N") + ".unitypackage");
            File.Copy(payloadPath, tempPackagePath, true);
            return tempPackagePath;
        }

        public static bool EnsureDefaultAddressablesSettings(out string message)
        {
            Type defaultObjectType = FindType("UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject");
            if (defaultObjectType == null)
            {
                message = "Addressables Editor 尚未加载，跳过默认 Addressables Settings 创建。";
                return false;
            }

            object settings = defaultObjectType.GetProperty("Settings", BindingFlags.Public | BindingFlags.Static)
                ?.GetValue(null, null);
            if (settings != null)
            {
                message = "已检测到 Addressables Settings。";
                return true;
            }

            MethodInfo getSettingsMethod = defaultObjectType
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(method =>
                {
                    if (!string.Equals(method.Name, "GetSettings", StringComparison.Ordinal))
                    {
                        return false;
                    }

                    ParameterInfo[] parameters = method.GetParameters();
                    return parameters.Length == 1 && parameters[0].ParameterType == typeof(bool);
                });

            if (getSettingsMethod != null)
            {
                settings = getSettingsMethod.Invoke(null, new object[] { true });
            }

            if (settings == null)
            {
                message = "未能自动创建 Addressables Settings，请检查 Addressables 包是否安装完成。";
                return false;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            message = "已自动创建 Addressables Settings。";
            return true;
        }

        public static void RequestOpenToolsHub()
        {
            SessionState.SetBool(PendingOpenToolsHubSessionKey, true);
            SessionState.SetInt(PendingOpenToolsHubAttemptKey, 0);
        }

        public static void TryDeleteFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return;
            }

            File.Delete(path);
        }

        internal static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static void TryOpenToolsHubFromSession()
        {
            if (!SessionState.GetBool(PendingOpenToolsHubSessionKey, false))
            {
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            if (EditorApplication.ExecuteMenuItem(ToolsHubMenuPath))
            {
                ClearPendingToolsHubRequest();
                return;
            }

            int attempts = SessionState.GetInt(PendingOpenToolsHubAttemptKey, 0) + 1;
            SessionState.SetInt(PendingOpenToolsHubAttemptKey, attempts);
            if (attempts < MaxOpenToolsHubAttempts)
            {
                return;
            }

            ClearPendingToolsHubRequest();
            Debug.LogWarning("StellarFrameworkBootstrap: Tools Hub 菜单尚不可用，已停止自动打开尝试。");
        }

        private static void ClearPendingToolsHubRequest()
        {
            SessionState.EraseBool(PendingOpenToolsHubSessionKey);
            SessionState.EraseInt(PendingOpenToolsHubAttemptKey);
        }
    }
}
