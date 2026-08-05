using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;

namespace StellarFrameworkBootstrap
{
    internal enum BootstrapWorkflowStage
    {
        Idle = 0,
        ExtractPayload = 1,
        InstallDependencies = 2,
        WaitForReloadOrCompile = 3,
        EnsureAddressables = 4,
        ImportPayload = 5,
        EnsureDefines = 6,
        OpenToolsHub = 7,
        CloseBootstrapWindow = 8,
        CleanupBootstrapAssets = 9,
        Complete = 10,
        Failed = 11
    }

    [InitializeOnLoad]
    internal static class StellarFrameworkBootstrapInstaller
    {
        private sealed class PackageSpec
        {
            public string PackageId;
            public string Version;
            public string GitUrl;
        }

        private const char EntrySeparator = '\u001f';
        private const string StageSessionKey = "StellarFrameworkBootstrap.Workflow.Stage";
        private const string PayloadPathSessionKey = "StellarFrameworkBootstrap.Workflow.PayloadPath";
        private const string MessagesSessionKey = "StellarFrameworkBootstrap.Workflow.Messages";
        private const string ErrorsSessionKey = "StellarFrameworkBootstrap.Workflow.Errors";
        private const string CurrentPackageSourceSessionKey = "StellarFrameworkBootstrap.Workflow.CurrentPackageSource";

        private static readonly PackageSpec[] DependencyOrder =
        {
            new PackageSpec
            {
                PackageId = "com.cysharp.unitask",
                GitUrl = "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask"
            },
            new PackageSpec
            {
                PackageId = "com.unity.nuget.newtonsoft-json",
                Version = "3.2.2"
            },
            new PackageSpec
            {
                PackageId = "com.unity.addressables",
                Version = "1.22.3"
            },
            new PackageSpec
            {
                PackageId = "com.code-philosophy.hybridclr",
                GitUrl = "https://github.com/focus-creative-games/hybridclr_unity.git"
            }
        };

        private static AddRequest _currentRequest;
        private static bool _isInitialized;

        static StellarFrameworkBootstrapInstaller()
        {
            EnsureInitialized();
        }

        public static bool IsBusy
        {
            get
            {
                BootstrapWorkflowStage stage = CurrentStage;
                return stage != BootstrapWorkflowStage.Idle &&
                       stage != BootstrapWorkflowStage.Complete &&
                       stage != BootstrapWorkflowStage.Failed;
            }
        }

        public static BootstrapWorkflowStage CurrentStage =>
            ParseStage(SessionState.GetString(StageSessionKey, BootstrapWorkflowStage.Idle.ToString()));

        public static IReadOnlyList<string> Messages => ReadEntries(MessagesSessionKey);
        public static IReadOnlyList<string> Errors => ReadEntries(ErrorsSessionKey);

        public static void StartSinglePackageInstall()
        {
            EnsureInitialized();
            ResetPersistentState();
            SetStage(BootstrapWorkflowStage.ExtractPayload);
            AppendMessage("已启动单包安装流程。");
            Tick();
        }

        public static void Tick()
        {
            EnsureInitialized();

            switch (CurrentStage)
            {
                case BootstrapWorkflowStage.Idle:
                case BootstrapWorkflowStage.Complete:
                case BootstrapWorkflowStage.Failed:
                    // 终态：退订 update，避免安装结束后 Tick 每帧空转。
                    UnsubscribeTick();
                    return;

                case BootstrapWorkflowStage.ExtractPayload:
                    HandleExtractPayload();
                    return;

                case BootstrapWorkflowStage.InstallDependencies:
                    HandleInstallDependencies();
                    return;

                case BootstrapWorkflowStage.WaitForReloadOrCompile:
                    HandleWaitForReloadOrCompile();
                    return;

                case BootstrapWorkflowStage.EnsureAddressables:
                    HandleEnsureAddressables();
                    return;

                case BootstrapWorkflowStage.ImportPayload:
                    HandleImportPayload();
                    return;

                case BootstrapWorkflowStage.EnsureDefines:
                    HandleEnsureDefines();
                    return;

                case BootstrapWorkflowStage.OpenToolsHub:
                    HandleOpenToolsHub();
                    return;

                case BootstrapWorkflowStage.CloseBootstrapWindow:
                    HandleCloseBootstrapWindow();
                    return;

                case BootstrapWorkflowStage.CleanupBootstrapAssets:
                    HandleCleanupBootstrapAssets();
                    return;
            }
        }

        public static void MarkCleanupCompleted()
        {
            ClearPayloadPath();
            ClearCurrentPackageSource();
            if (CurrentStage != BootstrapWorkflowStage.Failed)
            {
                SetStage(BootstrapWorkflowStage.Complete);
            }
        }

        private static void EnsureInitialized()
        {
            if (_isInitialized)
            {
                return;
            }

            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            _isInitialized = true;
        }

        private static void UnsubscribeTick()
        {
            if (!_isInitialized)
            {
                return;
            }

            EditorApplication.update -= Tick;
            _isInitialized = false;
        }

        private static void HandleExtractPayload()
        {
            if (HasPayloadPath())
            {
                SetStage(BootstrapWorkflowStage.InstallDependencies);
                return;
            }

            string payloadPath = StellarFrameworkBootstrapPackageUtility.ExtractEmbeddedPayloadToTempPackage();
            if (string.IsNullOrWhiteSpace(payloadPath))
            {
                Fail("未找到内嵌 payload。请重新使用“导出单包安装版”生成分发包。");
                return;
            }

            SetPayloadPath(payloadPath);
            AppendMessage("已找到内嵌 payload，开始检查并安装依赖。");
            SetStage(BootstrapWorkflowStage.InstallDependencies);
        }

        private static void HandleInstallDependencies()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            if (_currentRequest != null)
            {
                if (!_currentRequest.IsCompleted)
                {
                    return;
                }

                if (_currentRequest.Status == StatusCode.Failure)
                {
                    string error = _currentRequest.Error != null
                        ? _currentRequest.Error.message
                        : "Unknown package error.";
                    _currentRequest = null;
                    Fail("依赖安装失败：" + error);
                    return;
                }

                string source = SessionState.GetString(CurrentPackageSourceSessionKey, string.Empty);
                if (!string.IsNullOrWhiteSpace(source))
                {
                    AppendMessage("依赖安装完成：" + source);
                }

                _currentRequest = null;
                ClearCurrentPackageSource();
                SetStage(BootstrapWorkflowStage.WaitForReloadOrCompile);
                return;
            }

            PackageSpec nextDependency = FindNextMissingDependency();
            if (nextDependency == null)
            {
                SetStage(BootstrapWorkflowStage.EnsureAddressables);
                return;
            }

            string sourceToAdd = StellarFrameworkBootstrapPackageUtility.BuildPackageSource(
                nextDependency.PackageId,
                nextDependency.Version,
                nextDependency.GitUrl);

            SessionState.SetString(CurrentPackageSourceSessionKey, sourceToAdd);
            AppendMessage("正在安装依赖：" + sourceToAdd);
            _currentRequest = Client.Add(sourceToAdd);
        }

        private static void HandleWaitForReloadOrCompile()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            SetStage(BootstrapWorkflowStage.InstallDependencies);
        }

        private static void HandleEnsureAddressables()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            if (StellarFrameworkBootstrapPackageUtility.EnsureDefaultAddressablesSettings(out string message))
            {
                if (!string.IsNullOrWhiteSpace(message))
                {
                    AppendMessage(message);
                }
            }
            else if (!string.IsNullOrWhiteSpace(message))
            {
                AppendMessage(message);
            }

            SetStage(BootstrapWorkflowStage.ImportPayload);
        }

        private static void HandleImportPayload()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            string unityPackagePath = SessionState.GetString(PayloadPathSessionKey, string.Empty);
            if (string.IsNullOrWhiteSpace(unityPackagePath) || !File.Exists(unityPackagePath))
            {
                Fail("内嵌 payload 临时包无效：" + unityPackagePath);
                return;
            }

            // Advance state before import because ImportPackage may trigger a domain reload.
            ClearPayloadPath();
            SetStage(BootstrapWorkflowStage.EnsureDefines);

            AssetDatabase.ImportPackage(unityPackagePath, false);
            AssetDatabase.Refresh();
            StellarFrameworkBootstrapPackageUtility.TryDeleteFile(unityPackagePath);

            AppendMessage("完整框架 payload 已导入。");
        }

        private static void HandleEnsureDefines()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            // 校验 payload 是否真正导入成功：
            // AssetDatabase.ImportPackage 是 void，无法直接得知成败，
            // 因此检查框架核心标记资产是否存在；缺失即判定导入失败。
            if (!AssetDatabase.IsValidFolder("Assets/StellarFramework") ||
                AssetDatabase.LoadAssetAtPath<UnityEngine.TextAsset>(
                    "Assets/StellarFramework/StellarFramework.asmdef") == null)
            {
                Fail("完整框架 payload 未成功导入（缺少 Assets/StellarFramework/StellarFramework.asmdef）。请重新安装。");
                return;
            }

            if (StellarFrameworkBootstrapPackageUtility.EnsureLogKitDefine(out string message))
            {
                if (!string.IsNullOrWhiteSpace(message))
                {
                    AppendMessage(message);
                }
            }
            else if (!string.IsNullOrWhiteSpace(message))
            {
                AppendError(message);
            }

            SetStage(BootstrapWorkflowStage.OpenToolsHub);
        }

        private static void HandleOpenToolsHub()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            StellarFrameworkBootstrapPackageUtility.RequestOpenToolsHub();
            AppendMessage("已请求自动打开 Tools Hub。");
            SetStage(BootstrapWorkflowStage.CloseBootstrapWindow);
        }

        private static void HandleCloseBootstrapWindow()
        {
            StellarFrameworkBootstrapPackageUtility.CloseAllOpenWindows();
            StellarFrameworkBootstrapPackageUtility.RequestCleanupBootstrapArtifacts();
            AppendMessage("安装器窗口将自动关闭，随后清理安装器资源。");
            SetStage(BootstrapWorkflowStage.CleanupBootstrapAssets);
        }

        private static void HandleCleanupBootstrapAssets()
        {
            if (StellarFrameworkBootstrapPackageUtility.IsFrameworkDevelopmentProject())
            {
                MarkCleanupCompleted();
                return;
            }

            if (!StellarFrameworkBootstrapPackageUtility.HasPendingPostInstallRequests() &&
                !AssetDatabase.IsValidFolder(StellarFrameworkBootstrapPackageUtility.BootstrapAssetRoot))
            {
                MarkCleanupCompleted();
            }
        }

        private static void ResetPersistentState()
        {
            _currentRequest = null;

            string existingPayloadPath = SessionState.GetString(PayloadPathSessionKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(existingPayloadPath))
            {
                StellarFrameworkBootstrapPackageUtility.TryDeleteFile(existingPayloadPath);
            }

            SessionState.SetString(PayloadPathSessionKey, string.Empty);
            SessionState.SetString(CurrentPackageSourceSessionKey, string.Empty);
            SessionState.SetString(MessagesSessionKey, string.Empty);
            SessionState.SetString(ErrorsSessionKey, string.Empty);
            SessionState.SetString(StageSessionKey, BootstrapWorkflowStage.Idle.ToString());
            StellarFrameworkBootstrapPackageUtility.ClearPendingPostInstallRequests();
        }

        private static void Fail(string error)
        {
            if (!string.IsNullOrWhiteSpace(error))
            {
                AppendError(error);
            }

            string payloadPath = SessionState.GetString(PayloadPathSessionKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(payloadPath))
            {
                StellarFrameworkBootstrapPackageUtility.TryDeleteFile(payloadPath);
            }

            ClearPayloadPath();
            ClearCurrentPackageSource();
            _currentRequest = null;
            StellarFrameworkBootstrapPackageUtility.ClearPendingPostInstallRequests();
            SetStage(BootstrapWorkflowStage.Failed);
        }

        private static PackageSpec FindNextMissingDependency()
        {
            for (int i = 0; i < DependencyOrder.Length; i++)
            {
                PackageSpec dependency = DependencyOrder[i];
                if (!IsPackageListedInManifest(dependency.PackageId))
                {
                    return dependency;
                }
            }

            return null;
        }

        private static bool IsPackageListedInManifest(string packageId)
        {
            string manifestPath = StellarFrameworkBootstrapPackageUtility.GetManifestPath();
            if (!File.Exists(manifestPath))
            {
                return false;
            }

            string manifest = File.ReadAllText(manifestPath);
            return StellarFrameworkBootstrapPackageUtility.ManifestContainsPackage(manifest, packageId);
        }

        private static bool HasPayloadPath()
        {
            string payloadPath = SessionState.GetString(PayloadPathSessionKey, string.Empty);
            return !string.IsNullOrWhiteSpace(payloadPath) && File.Exists(payloadPath);
        }

        private static void SetPayloadPath(string payloadPath)
        {
            SessionState.SetString(PayloadPathSessionKey, payloadPath ?? string.Empty);
        }

        private static void ClearPayloadPath()
        {
            SessionState.SetString(PayloadPathSessionKey, string.Empty);
        }

        private static void ClearCurrentPackageSource()
        {
            SessionState.SetString(CurrentPackageSourceSessionKey, string.Empty);
        }

        private static void SetStage(BootstrapWorkflowStage stage)
        {
            SessionState.SetString(StageSessionKey, stage.ToString());
        }

        private static BootstrapWorkflowStage ParseStage(string value)
        {
            if (Enum.TryParse(value, out BootstrapWorkflowStage stage))
            {
                return stage;
            }

            return BootstrapWorkflowStage.Idle;
        }

        private static IReadOnlyList<string> ReadEntries(string sessionKey)
        {
            string raw = SessionState.GetString(sessionKey, string.Empty);
            if (string.IsNullOrEmpty(raw))
            {
                return Array.Empty<string>();
            }

            return raw.Split(new[] { EntrySeparator }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static void AppendMessage(string message)
        {
            AppendEntry(MessagesSessionKey, message);
        }

        private static void AppendError(string error)
        {
            AppendEntry(ErrorsSessionKey, error);
        }

        private static void AppendEntry(string sessionKey, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            List<string> values = ReadEntries(sessionKey).ToList();
            if (values.Count > 0 && string.Equals(values[values.Count - 1], value, StringComparison.Ordinal))
            {
                return;
            }

            values.Add(value);
            SessionState.SetString(sessionKey, string.Join(EntrySeparator.ToString(), values));
        }
    }
}
