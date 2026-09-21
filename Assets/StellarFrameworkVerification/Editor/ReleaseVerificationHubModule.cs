#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using StellarFramework.Editor;
using StellarFramework.Editor.Modules;
using StellarFramework.HybridCLR;
using UnityEditor;
using UnityEngine;

namespace StellarFrameworkVerification.Editor
{
    [StellarTool("发布前自检", "Verification", 50)]
    internal sealed class ReleaseVerificationHubModule : ToolModule
    {
        private string _lastSummary = "等待执行。";
        private Vector2 _scroll;
        private readonly List<string> _details = new List<string>();

        public override string Icon => "d_Profiler.FirstFrame";
        public override string Description =>
            "框架开发者使用的发布前自检：Addressables 本地加载配置与 HybridCLR 代码热更产物分别验证。";

        public override void OnGUI()
        {
            Section("发布前自检");
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.HelpBox(
                    "Addressables 只检查本地 ResKit 后端配置；HybridCLR 单独检查 Manifest、DLL SHA256 与 AOT metadata。内容版本、下载与缓存属于 YooAsset 启动层。",
                    MessageType.None);

                using (new GUILayout.HorizontalScope())
                {
                    if (PrimaryButton("检查 Addressables", GUILayout.Height(30)))
                    {
                        ValidateAddressables();
                    }

                    if (PrimaryButton("检查 HybridCLR", GUILayout.Height(30)))
                    {
                        ValidateHybridClr();
                    }
                }
            }

            Section("最近结果");
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.HelpBox(_lastSummary, MessageType.None);
                _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MinHeight(180f));
                for (int i = 0; i < _details.Count; i++)
                {
                    EditorGUILayout.LabelField(_details[i], EditorStyles.wordWrappedMiniLabel);
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private void ValidateAddressables()
        {
            ClearDetails();
            AddressablesLocalConfigurationReport report =
                AddressablesBuildToolLogic.InspectLocalConfiguration();

            foreach (string message in report.Messages) _details.Add("[信息] " + message);
            foreach (string warning in report.Warnings) _details.Add("[警告] " + warning);
            foreach (string error in report.Errors) _details.Add("[错误] " + error);

            if (report.Success)
            {
                SetSuccess("Addressables 本地加载配置检查通过。");
            }
            else
            {
                SetFailure("Addressables 本地加载配置检查失败。");
            }
        }

        private void ValidateHybridClr()
        {
            ClearDetails();

            HotUpdateSettings settings = HotUpdateSettings.LoadOrCreateDefault();
            HotUpdateSettingsValidationReport settingsReport = settings.Validate(strictProduction: true);
            foreach (string warning in settingsReport.Warnings) _details.Add("[警告] " + warning);
            foreach (string error in settingsReport.Errors) _details.Add("[错误] " + error);
            if (!settingsReport.IsValid)
            {
                SetFailure("HybridCLR Settings 未通过校验。");
                return;
            }

            string manifestPath = ToAbsoluteProjectPath(settings.HotUpdateManifestKey);
            if (!File.Exists(manifestPath))
            {
                SetFailure("HybridCLR Manifest 不存在。", settings.HotUpdateManifestKey);
                return;
            }

            HotUpdateManifest manifest = HotUpdateManifest.FromJson(File.ReadAllText(manifestPath, Encoding.UTF8));
            if (manifest == null)
            {
                SetFailure("HybridCLR Manifest JSON 无效。", settings.HotUpdateManifestKey);
                return;
            }

            HotUpdateManifestValidationReport manifestReport = manifest.Validate(strictAssemblyIntegrity: true);
            foreach (string warning in manifestReport.Warnings) _details.Add("[警告] " + warning);
            foreach (string error in manifestReport.Errors) _details.Add("[错误] " + error);
            if (!manifestReport.IsValid)
            {
                SetFailure("HybridCLR Manifest 未通过生产校验。");
                return;
            }

            string dllPath = ToAbsoluteProjectPath(manifest.hotUpdateAssemblyKey);
            if (!File.Exists(dllPath))
            {
                SetFailure("HybridCLR 热更 DLL 不存在。", manifest.hotUpdateAssemblyKey);
                return;
            }

            string actualSha = ComputeSha256(File.ReadAllBytes(dllPath));
            if (!string.Equals(
                    HotUpdateManifest.NormalizeSha256(manifest.hotUpdateAssemblySha256),
                    actualSha,
                    StringComparison.OrdinalIgnoreCase))
            {
                SetFailure(
                    "HybridCLR 热更 DLL SHA256 不匹配。",
                    $"Expected={manifest.hotUpdateAssemblySha256}, Actual={actualSha}");
                return;
            }

            for (int i = 0; i < manifest.aotMetadataKeys.Count; i++)
            {
                string metadataKey = manifest.aotMetadataKeys[i];
                if (!File.Exists(ToAbsoluteProjectPath(metadataKey)))
                {
                    SetFailure("HybridCLR AOT metadata 缺失。", metadataKey);
                    return;
                }
            }

            _details.Add("[通过] Manifest、DLL SHA256 与全部 AOT metadata 一致。");
            SetSuccess("HybridCLR 发布前自检通过。");
        }

        private void SetSuccess(string summary, string extra = null)
        {
            _lastSummary = summary;
            if (!string.IsNullOrWhiteSpace(extra)) _details.Add(extra);
            Window.ShowNotification(new GUIContent(summary));
        }

        private void SetFailure(string summary, string extra = null)
        {
            _lastSummary = summary;
            if (!string.IsNullOrWhiteSpace(extra)) _details.Add(extra);
            Window.ShowNotification(new GUIContent(summary));
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }

        private void ClearDetails()
        {
            _details.Clear();
        }

        private static string ToAbsoluteProjectPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(bytes);
                var builder = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++) builder.Append(hash[i].ToString("x2"));
                return builder.ToString();
            }
        }
    }
}
#endif
