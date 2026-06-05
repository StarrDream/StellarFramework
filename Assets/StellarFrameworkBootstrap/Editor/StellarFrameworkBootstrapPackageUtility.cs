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
        internal const string BootstrapAssetRoot = "Assets/StellarFrameworkBootstrap";

        private const string DevelopmentProjectPackagingMarkerAssetPath =
            "Assets/StellarFramework/Editor/StellarToolsHub/Modules/Packaging/StellarFrameworkPackagePublisher.cs";
        private const string EnableLogDefineSymbol = "ENABLE_LOG";
        private const string EmbeddedPayloadRelativePath =
            "Assets/StellarFrameworkBootstrap/Payloads/StellarFramework-FullHotUpdate-Payload.unitypackage.bytes";
        private const string ToolsHubMenuPath = "StellarFramework/Tools Hub";
        private const string PendingOpenToolsHubSessionKey = "StellarFrameworkBootstrap.PendingOpenToolsHub";
        private const string PendingOpenToolsHubAttemptKey = "StellarFrameworkBootstrap.PendingOpenToolsHubAttempt";
        private const string PendingCleanupBootstrapSessionKey = "StellarFrameworkBootstrap.PendingCleanupBootstrap";
        private const string PendingCleanupBootstrapAttemptKey = "StellarFrameworkBootstrap.PendingCleanupBootstrapAttempt";
        private const string PendingWindowCloseSessionKey = "StellarFrameworkBootstrap.PendingCloseBootstrapWindows";
        private const int MaxOpenToolsHubAttempts = 300;
        private const int MaxCleanupBootstrapAttempts = 30;

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
            return Directory.GetParent(Application.dataPath)?.FullName
                   ?? Application.dataPath;
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

            string tempPackagePath = Path.Combine(
                Path.GetTempPath(),
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

        public static void RequestCleanupBootstrapArtifacts()
        {
            SessionState.SetBool(PendingCleanupBootstrapSessionKey, true);
            SessionState.SetInt(PendingCleanupBootstrapAttemptKey, 0);
            SessionState.SetBool(PendingWindowCloseSessionKey, true);
        }

        public static bool EnsureLogKitDefine(out string message)
        {
            return TryAddDefineForSelectedBuildTarget(EnableLogDefineSymbol, out message);
        }

        public static void TryDeleteFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return;
            }

            File.Delete(path);
        }

        public static void CloseAllOpenWindows()
        {
            StellarFrameworkBootstrapWindow[] windows =
                Resources.FindObjectsOfTypeAll<StellarFrameworkBootstrapWindow>();
            for (int i = 0; i < windows.Length; i++)
            {
                StellarFrameworkBootstrapWindow window = windows[i];
                if (window != null)
                {
                    window.Close();
                }
            }
        }

        public static bool HasPendingPostInstallRequests()
        {
            return SessionState.GetBool(PendingOpenToolsHubSessionKey, false) ||
                   SessionState.GetBool(PendingCleanupBootstrapSessionKey, false) ||
                   SessionState.GetBool(PendingWindowCloseSessionKey, false);
        }

        public static void ClearPendingPostInstallRequests()
        {
            ClearPendingToolsHubRequest();
            ClearPendingBootstrapCleanupRequest();
            SessionState.EraseBool(PendingWindowCloseSessionKey);
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

        internal static bool IsFrameworkDevelopmentProject()
        {
            return File.Exists(Path.Combine(
                GetProjectRootPath(),
                DevelopmentProjectPackagingMarkerAssetPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static void TryOpenToolsHubFromSession()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            if (!TryOpenToolsHub())
            {
                return;
            }

            TryCleanupBootstrapArtifacts();
        }

        private static bool TryOpenToolsHub()
        {
            if (!SessionState.GetBool(PendingOpenToolsHubSessionKey, false))
            {
                return true;
            }

            if (EditorApplication.ExecuteMenuItem(ToolsHubMenuPath))
            {
                ClearPendingToolsHubRequest();
                return true;
            }

            int attempts = SessionState.GetInt(PendingOpenToolsHubAttemptKey, 0) + 1;
            SessionState.SetInt(PendingOpenToolsHubAttemptKey, attempts);
            if (attempts < MaxOpenToolsHubAttempts)
            {
                return false;
            }

            ClearPendingToolsHubRequest();
            Debug.LogWarning("StellarFrameworkBootstrap: Tools Hub 菜单尚不可用，已停止自动打开尝试。");
            return true;
        }

        private static void ClearPendingToolsHubRequest()
        {
            SessionState.EraseBool(PendingOpenToolsHubSessionKey);
            SessionState.EraseInt(PendingOpenToolsHubAttemptKey);
        }

        private static void TryCleanupBootstrapArtifacts()
        {
            if (!SessionState.GetBool(PendingCleanupBootstrapSessionKey, false))
            {
                return;
            }

            if (IsFrameworkDevelopmentProject())
            {
                ClearPendingBootstrapCleanupRequest();
                BootstrapInstallerSafeComplete();
                return;
            }

            if (!AssetDatabase.IsValidFolder(BootstrapAssetRoot))
            {
                ClearPendingBootstrapCleanupRequest();
                BootstrapInstallerSafeComplete();
                return;
            }

            if (SessionState.GetBool(PendingWindowCloseSessionKey, false))
            {
                CloseAllOpenWindows();
                SessionState.SetBool(PendingWindowCloseSessionKey, false);
                return;
            }

            if (AssetDatabase.DeleteAsset(BootstrapAssetRoot))
            {
                ClearPendingBootstrapCleanupRequest();
                AssetDatabase.Refresh();
                BootstrapInstallerSafeComplete();
                return;
            }

            int attempts = SessionState.GetInt(PendingCleanupBootstrapAttemptKey, 0) + 1;
            SessionState.SetInt(PendingCleanupBootstrapAttemptKey, attempts);
            if (attempts < MaxCleanupBootstrapAttempts)
            {
                return;
            }

            ClearPendingBootstrapCleanupRequest();
            Debug.LogWarning("StellarFrameworkBootstrap: 安装后未能自动删除 Assets/StellarFrameworkBootstrap，请手动清理该目录。");
            BootstrapInstallerSafeComplete();
        }

        private static void ClearPendingBootstrapCleanupRequest()
        {
            SessionState.EraseBool(PendingCleanupBootstrapSessionKey);
            SessionState.EraseInt(PendingCleanupBootstrapAttemptKey);
            SessionState.EraseBool(PendingWindowCloseSessionKey);
        }

        private static bool TryAddDefineForSelectedBuildTarget(string define, out string message)
        {
            message = string.Empty;
            if (string.IsNullOrWhiteSpace(define))
            {
                return false;
            }

            BuildTargetGroup group = EditorUserBuildSettings.selectedBuildTargetGroup;
            if (group == BuildTargetGroup.Unknown)
            {
                message = "当前 BuildTargetGroup 未知，无法自动写入 " + define + "。";
                return false;
            }

#if UNITY_2021_2_OR_NEWER
            UnityEditor.Build.NamedBuildTarget namedBuildTarget =
                UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(group);
            string current = PlayerSettings.GetScriptingDefineSymbols(namedBuildTarget);
            string merged = MergeDefineSymbols(current, define);
            if (string.Equals(current, merged, StringComparison.Ordinal))
            {
                message = define + " 已存在于当前 BuildTarget。";
                return true;
            }

            PlayerSettings.SetScriptingDefineSymbols(namedBuildTarget, merged);
#else
            string current = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
            string merged = MergeDefineSymbols(current, define);
            if (string.Equals(current, merged, StringComparison.Ordinal))
            {
                message = define + " 已存在于当前 BuildTarget。";
                return true;
            }

            PlayerSettings.SetScriptingDefineSymbolsForGroup(group, merged);
#endif
            message = "已为当前 BuildTarget 写入 " + define + "。";
            return true;
        }

        private static string MergeDefineSymbols(string currentSymbols, params string[] requiredSymbols)
        {
            var symbols = string.IsNullOrWhiteSpace(currentSymbols)
                ? new System.Collections.Generic.List<string>()
                : currentSymbols
                    .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(item => item.Trim())
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Distinct(StringComparer.Ordinal)
                    .ToList();

            if (requiredSymbols != null)
            {
                for (int i = 0; i < requiredSymbols.Length; i++)
                {
                    string symbol = requiredSymbols[i];
                    if (string.IsNullOrWhiteSpace(symbol))
                    {
                        continue;
                    }

                    string trimmed = symbol.Trim();
                    if (!symbols.Contains(trimmed))
                    {
                        symbols.Add(trimmed);
                    }
                }
            }

            return string.Join(";", symbols);
        }

        private static void BootstrapInstallerSafeComplete()
        {
            StellarFrameworkBootstrapInstaller.MarkCleanupCompleted();
        }
    }
}
