using UnityEngine.UIElements;

namespace StellarFramework.Editor.Modules.FlowKit
{
    [StellarTool(
        "FlowKit 流程编辑器",
        "框架核心",
        20,
        RequiredAssemblyNames = new[]
        {
            "StellarFramework.FlowKit.Core",
            "StellarFramework.FlowKit.Unity"
        })]
    public sealed class FlowKitHubModule : ToolModule
    {
        private readonly FlowKitEditorPanel _panel = new FlowKitEditorPanel();

        public override string Icon => "d_PlayButton";
        public override string Description => "创建、编辑、校验 FlowKit 工作流；运行逻辑与 Editor 布局数据分离。";

        public override void OnGUI() => _panel.DrawLegacyContent(Window);
        public override VisualElement CreateView() => _panel.CreateView(Window);
        public override void OnEnable() => _panel.Activate(Window);
        public override void OnDisable() => _panel.Deactivate();
        public override void OnSelectionChange() => _panel.HandleSelectionChange();
    }
}
