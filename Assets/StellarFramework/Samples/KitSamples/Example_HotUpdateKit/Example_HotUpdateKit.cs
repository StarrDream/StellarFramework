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
}
