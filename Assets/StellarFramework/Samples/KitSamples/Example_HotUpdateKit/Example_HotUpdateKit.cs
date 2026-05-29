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
    /// HotUpdateKit 最小使用示例。
    ///
    /// 场景: Scenes/HotUpdateKit_Playable.unity
    /// 前置条件: 完整代码热更需要按 HybridCLR 官方流程生成 hot update dll 与 AOT metadata。
    /// 操作: 点击 OnGUI 按钮打印配置；可手动拖入 TextAsset 验证本地 dll.bytes 装载链路。
    /// 通过标准: 未开启 HYBRIDCLR_ENABLE 时返回明确不可用信息；开启后能阻断缺失、校验失败路径。
    /// </summary>
    public class Example_HotUpdateKit : MonoBehaviour
    {
        [Header("可选调试资源")]
        public TextAsset hotUpdateDllAsset;

        public TextAsset[] aotMetadataAssets;

        private string _status = "等待操作";

        private void Start()
        {
#if HYBRIDCLR_ENABLE
            _status = "HYBRIDCLR_ENABLE 已开启，可以通过按钮验证接入链路。";
#else
            _status = "未定义 HYBRIDCLR_ENABLE，当前 Scene 仅用于查看配置与挂载入口。";
#endif
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(20, 20, 520, 260), GUI.skin.box);
            GUILayout.Label("HotUpdateKit 示例场景");
            GUILayout.Space(8);
            GUILayout.Label($"状态: {HybridCLRHook.State}");
            GUILayout.Label($"HotUpdateAssemblyName: {HybridCLRHook.HotUpdateAssemblyName}");
            GUILayout.Label($"入口: {HybridCLRHook.HotUpdateEntryClass}.{HybridCLRHook.HotUpdateEntryMethod}");
            GUILayout.Label($"最近错误: {HybridCLRHook.LastError ?? "<无>"}");
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
                _status = "未提供 hotUpdateDllAsset，当前只验证 Scene 挂载与配置展示。";
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
                _status = $"AOT metadata 加载失败: {HybridCLRHook.LastError}";
                return;
            }

            bool started = HybridCLRHook.LoadAndStartHotUpdateAssembly(hotUpdateDllAsset.bytes);
            _status = started
                ? "热更入口已调用，请检查 Console 中 HotUpdate 入口逻辑。"
                : $"热更装载失败: {HybridCLRHook.LastError}";
        }
    }

    /// <summary>
    /// 启动期 HybridCLR + Addressables 热更示例。
    /// 真实 dll.bytes 与 AOT metadata 的生成仍然遵循 HybridCLR 官方流程。
    /// </summary>
    public sealed class Example_HybridCLRAAStartup : MonoBehaviour
    {
        [SerializeField] private ResKitRuntimeSettings settingsOverride;
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
                _status = "正在检查 Addressables Catalog 并下载热更内容...";
                HybridCLRAAHotUpdateResult result = await HybridCLRAAHotUpdateRunner.RunAsync(
                    settings,
                    progress => _progress = progress,
                    _cancellationTokenSource.Token);

                _status = result.Success
                    ? $"已进入热更入口。\nAssembly={result.LoadedAssemblyFullName}"
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

        private static string BuildValidationText(ResKitRuntimeSettingsValidationReport validation)
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
