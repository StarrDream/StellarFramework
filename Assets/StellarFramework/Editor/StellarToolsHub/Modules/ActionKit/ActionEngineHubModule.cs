#if UNITY_EDITOR
using UnityEngine.UIElements;

namespace StellarFramework.Editor.Modules
{
    [StellarTool("动画编组", "框架核心", 9,
        RequiredAssemblyNames = new[] { "StellarFramework.ActionKit" })]
    public sealed class ActionEngineHubModule : ToolModule
    {
        private readonly ActionEngineEditorWindow _panel = new ActionEngineEditorWindow();

        public override string Icon => "d_AnimationClip Icon";
        public override string Description => "在 ToolsHub 内编辑 ActionEngine 资产。";

        public override void OnGUI() => _panel.DrawLegacyContent(Window);
        public override VisualElement CreateView() => _panel.CreateView(Window);
        public override void OnEnable() => _panel.Activate(Window);
        public override void OnDisable() => _panel.Deactivate();
        public override void OnSelectionChange() => _panel.HandleSelectionChange();
    }
}
#endif
