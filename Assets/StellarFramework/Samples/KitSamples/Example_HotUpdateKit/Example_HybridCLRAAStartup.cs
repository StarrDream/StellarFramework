using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using StellarFramework.HotUpdate;
using UnityEngine;

namespace StellarFramework.Examples
{
    /// <summary>
    /// 启动期 HybridCLR + Addressables 热更示例。
    /// 真实 dll.bytes 与 AOT metadata 的生成仍然遵循 HybridCLR 官方流程。
    /// </summary>
    public sealed class Example_HybridCLRAAStartup : MonoBehaviour
    {
        [SerializeField] private HotUpdateSettings settingsOverride;
        [SerializeField] private bool runOnStart;

        private CancellationTokenSource _cancellationTokenSource;
        private float _progress;
        private string _status = "等待操作";

        private void Start()
        {
            if (runOnStart)
            {
                RunStartupHotUpdateAsync().Forget();
            }
        }

        private void OnDestroy()
        {
            CancelCurrentRun();
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(20, 300, 560, 250), GUI.skin.box);
            GUILayout.Label("HybridCLR AA 启动期热更示例");
            GUILayout.Label($"进度: {_progress:P0}");
            GUILayout.TextArea(_status, GUILayout.Height(110));

            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("运行 AA 热更", GUILayout.Height(32)))
                {
                    RunStartupHotUpdateAsync().Forget();
                }

                if (GUILayout.Button("取消", GUILayout.Height(32)))
                {
                    CancelCurrentRun();
                    _status = "用户已取消。";
                }
            }

            GUILayout.EndArea();
        }

        private async UniTaskVoid RunStartupHotUpdateAsync()
        {
            CancelCurrentRun();
            _cancellationTokenSource = new CancellationTokenSource();
            _progress = 0f;

            HotUpdateSettings settings =
                settingsOverride != null ? settingsOverride : HotUpdateSettings.LoadOrCreateDefault();
            HotUpdateSettingsValidationReport validation = settings.Validate();
            if (!validation.IsValid)
            {
                _status = BuildValidationText(validation);
                return;
            }

            try
            {
                _status = "正在检查 Addressables Catalog 并下载热更内容...";
                HybridCLRAAHotUpdateResult result = await HybridCLRAAHotUpdateRunner.RunAsync(
                    settings,
                    progress => _progress = progress,
                    _cancellationTokenSource.Token);

                _status = result.Success
                    ? $"Hot update entered.\nManifest={result.ManifestSource}\nAssembly={result.LoadedAssemblyFullName}\nKey={result.Manifest?.hotUpdateAssemblyKey}"
                    : $"热更失败。\nState={result.State}\nError={result.Error}";
            }
            catch (OperationCanceledException)
            {
                _status = "热更流程已取消。";
            }
        }

        private void CancelCurrentRun()
        {
            if (_cancellationTokenSource == null)
            {
                return;
            }

            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
        }

        private static string BuildValidationText(HotUpdateSettingsValidationReport validation)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("ResKitRuntimeSettings 校验失败。");
            for (int i = 0; i < validation.Errors.Count; i++)
            {
                builder.AppendLine("错误: " + validation.Errors[i]);
            }

            for (int i = 0; i < validation.Warnings.Count; i++)
            {
                builder.AppendLine("警告: " + validation.Warnings[i]);
            }

            return builder.ToString();
        }
    }
}
