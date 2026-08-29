#if UNITY_EDITOR
using UnityEngine.UIElements;

namespace StellarFramework.Editor.Modules
{
    [StellarTool("管线材质转换", "框架核心", 20,
        RequiredAssemblyNames = new[] { "StellarFramework.Runtime" })]
    public sealed class URPConverterHubModule : ToolModule
    {
        private readonly URPMaterialConverterWindow _panel = new URPMaterialConverterWindow();

        public override string Icon => "d_Material Icon";
        public override string Description => "在 ToolsHub 内执行渲染管线材质转换与材质槽修复。";

        public override void OnGUI() => _panel.DrawLegacyContent(Window);
        public override VisualElement CreateView() => _panel.CreateView(Window);
        public override void OnEnable() => _panel.Activate(Window);
        public override void OnDisable() => _panel.Deactivate();
        public override void OnSelectionChange() => _panel.HandleSelectionChange();
    }
}
#endif
