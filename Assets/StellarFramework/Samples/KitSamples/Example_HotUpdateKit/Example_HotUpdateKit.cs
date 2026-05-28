using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using StellarFramework.HotUpdate;
using StellarFramework.Res;
using UnityEngine;

namespace StellarFramework.Examples
{
    /// <summary>
    /// HotUpdateKit 最小示例
    /// 职责：提供一个可挂载的场景入口，用于观察当前 HybridCLRHook 状态与接入参数。
    /// 说明：真正的热更装载仍依赖外部 DLL 字节流提供方，本示例不伪造完整热更资源环境。
    ///
    /// 场景: Scenes/HotUpdateKit_Playable.unity
    /// 操作: 点击 OnGUI 按钮打印配置；可手动拖入 TextAsset 验证本地 dll.bytes 装载链路。
    /// 前置: 完整代码热更需要 HybridCLR 官方流程生成 hot update dll 与 AOT metadata。
    /// 通过标准: 未开启 HYBRIDCLR_ENABLE 时返回明确不可用信息；开启后能阻断缺失/校验失败路径。
    /// </summary>
    public class Example_HotUpdateKit : MonoBehaviour
    {
        [Header("可选调试资源")] public TextAsset hotUpdateDllAsset;

        public TextAsset[] aotMetadataAssets;

        private string _status = "等待操作";

        private void Start()
        {
#if HYBRIDCLR_ENABLE
            _status = "HYBRIDCLR_ENABLE 已开启，可通过按钮验证接入链路。";
#else
            _status = "未定义 HYBRIDCLR_ENABLE，当前 Scene 仅用于查看配置与挂载入口。";
#endif
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(20, 20, 520, 260), GUI.skin.box);
            GUILayout.Label("HotUpdateKit Example Scene");
            GUILayout.Space(8);
            GUILayout.Label($"State: {HybridCLRHook.State}");
            GUILayout.Label($"HotUpdateAssemblyName: {HybridCLRHook.HotUpdateAssemblyName}");
            GUILayout.Label($"Entry: {HybridCLRHook.HotUpdateEntryClass}.{HybridCLRHook.HotUpdateEntryMethod}");
            GUILayout.Label($"LastError: {HybridCLRHook.LastError ?? "<none>"}");
            GUILayout.Space(8);
            GUILayout.TextArea(_status, GUILayout.Height(80));
            GUILayout.Space(8);

            if (GUILayout.Button("打印当前配置", GUILayout.Height(28)))
            {
                LogCurrentConfig();
            }

            if (GUILayout.Button("尝试用 TextAsset 验证装载链路", GUILayout.Height(32)))
            {
                ValidateLoadFlowAsync().Forget();
            }

            GUILayout.EndArea();
        }

        private void LogCurrentConfig()
        {
            LogKit.Log(
                $"[Example_HotUpdateKit] State={HybridCLRHook.State}, Assembly={HybridCLRHook.HotUpdateAssemblyName}, Entry={HybridCLRHook.HotUpdateEntryClass}.{HybridCLRHook.HotUpdateEntryMethod}");
        }

        private async UniTaskVoid ValidateLoadFlowAsync()
        {
            if (hotUpdateDllAsset == null)
            {
                _status = "未提供 hotUpdateDllAsset，当前只验证了 Scene 挂载与配置展示。";
                return;
            }

            bool metadataLoaded = await HybridCLRHook.LoadMetadataForAOTAssembliesAsync(name =>
            {
                if (aotMetadataAssets == null)
                {
                    return UniTask.FromResult<byte[]>(null);
                }

                for (int i = 0; i < aotMetadataAssets.Length; i++)
                {
                    TextAsset asset = aotMetadataAssets[i];
                    if (asset != null && asset.name == name.Replace(".dll", string.Empty))
                    {
                        return UniTask.FromResult(asset.bytes);
                    }
                }

                return UniTask.FromResult<byte[]>(null);
            });

            if (!metadataLoaded)
            {
                _status = $"AOT 元数据加载失败: {HybridCLRHook.LastError}";
                return;
            }

            bool started = HybridCLRHook.LoadAndStartHotUpdateAssembly(hotUpdateDllAsset.bytes);
            _status = started
                ? "热更入口调用已执行，请检查 Console 与 HotUpdate 入口逻辑。"
                : $"热更装载失败: {HybridCLRHook.LastError}";
        }
    }

    /// <summary>
    /// Startup-only HybridCLR + Addressables hot update example.
    /// Real dll.bytes and AOT metadata generation still follows the official HybridCLR workflow.
    /// </summary>
    public sealed class Example_HybridCLRAAStartup : MonoBehaviour
    {
        [SerializeField] private ResKitRuntimeSettings settingsOverride;
        [SerializeField] private bool runOnStart;

        private CancellationTokenSource _cancellationTokenSource;
        private float _progress;
        private string _status = "Waiting";

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
            GUILayout.Label("HybridCLR AA Startup Example");
            GUILayout.Label($"Progress: {_progress:P0}");
            GUILayout.TextArea(_status, GUILayout.Height(110));

            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Run AA Hot Update", GUILayout.Height(32)))
                {
                    RunStartupHotUpdateAsync().Forget();
                }

                if (GUILayout.Button("Cancel", GUILayout.Height(32)))
                {
                    CancelCurrentRun();
                    _status = "Cancelled by user.";
                }
            }

            GUILayout.EndArea();
        }

        private async UniTaskVoid RunStartupHotUpdateAsync()
        {
            CancelCurrentRun();
            _cancellationTokenSource = new CancellationTokenSource();
            _progress = 0f;

            ResKitRuntimeSettings settings =
                settingsOverride != null ? settingsOverride : ResKitRuntimeSettings.LoadOrCreateDefault();
            ResKitRuntimeSettingsValidationReport validation = settings.Validate(true);
            if (!validation.IsValid)
            {
                _status = BuildValidationText(validation);
                return;
            }

            try
            {
                _status = "Checking Addressables catalogs and downloading hot update content...";
                HybridCLRAAHotUpdateResult result = await HybridCLRAAHotUpdateRunner.RunAsync(
                    settings,
                    progress => _progress = progress,
                    _cancellationTokenSource.Token);

                _status = result.Success
                    ? $"Hot update entered.\nAssembly={result.LoadedAssemblyFullName}"
                    : $"Hot update failed.\nState={result.State}\nError={result.Error}";
            }
            catch (OperationCanceledException)
            {
                _status = "Hot update cancelled.";
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

        private static string BuildValidationText(ResKitRuntimeSettingsValidationReport validation)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("ResKitRuntimeSettings validation failed.");
            for (int i = 0; i < validation.Errors.Count; i++)
            {
                builder.AppendLine("ERROR: " + validation.Errors[i]);
            }

            for (int i = 0; i < validation.Warnings.Count; i++)
            {
                builder.AppendLine("WARNING: " + validation.Warnings[i]);
            }

            return builder.ToString();
        }
    }
}
