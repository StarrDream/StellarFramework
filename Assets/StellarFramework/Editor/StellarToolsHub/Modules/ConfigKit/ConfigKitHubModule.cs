#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace StellarFramework.Editor.Modules
{
    /// <summary>
    /// ConfigKit 的 Hub 入口模块
    /// 职责: 将 ConfigKit 注册到 StellarFramework Tools Hub，提供统一的 Dashboard 入口
    /// </summary>
    [StellarTool("ConfigKit 配置中心", "框架核心", 4,
        RequiredAssemblyNames = new[] { "StellarFramework.ConfigKit.Json" })]
    public class ConfigKitHubModule : ToolModule
    {
        private readonly ConfigKitWindow _panel = new ConfigKitWindow();

        public override string Icon => "d_SettingsIcon";
        public override string Description => "统一的配置管理入口。支持普通配置与网络配置的横向扩展、可视化编辑与环境切换。";

        public override void OnGUI()
        {
            _panel.DrawLegacyContent(Window);
        }

        public override VisualElement CreateView() => _panel.CreateView(Window);
        public override void OnEnable() => _panel.Activate(Window);
        public override void OnDisable() => _panel.Deactivate();
        public override void OnSelectionChange() => _panel.HandleSelectionChange();
    }
}
#endif
