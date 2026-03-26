using UnityEngine;

namespace StellarFramework.UI
{
    /// <summary>
    /// UI 自动绑定标记组件
    /// 职责：在 Editor 阶段标记需要导出到代码的 UI 控件。
    /// 规范：强制使用挂载的 GameObject 名称作为生成的变量名，拒绝手动配置，保证视图与代码命名严格统一。
    /// </summary>
    [DisallowMultipleComponent]
    public class UIAutoBind : MonoBehaviour
    {
        [Tooltip("实际绑定的目标组件 (如 Button, Text, 或 GameObject 本身)")]
        public Object Target;
    }
}