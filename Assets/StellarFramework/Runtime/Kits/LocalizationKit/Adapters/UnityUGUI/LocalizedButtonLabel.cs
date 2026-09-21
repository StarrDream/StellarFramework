using UnityEngine;
using UnityEngine.UI;

namespace StellarFramework.Localization.UnityUGUI
{
    /// <summary>
    /// Button 文本本地化的轻量组合组件。
    /// 具体查询/订阅逻辑委托给 <see cref="LocalizedTextView"/>。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class LocalizedButtonLabel : MonoBehaviour
    {
        [SerializeField] private LocalizedTextView _labelView;

        /// <summary>负责按钮文字绑定的 LocalizedTextView。</summary>
        public LocalizedTextView LabelView => _labelView;

        /// <summary>代码式绑定 Label View。</summary>
        public void Configure(LocalizedTextView labelView)
        {
            _labelView = labelView;
        }

        /// <summary>立即刷新按钮文字。</summary>
        public bool Refresh(out string error)
        {
            if (_labelView == null)
            {
                error = "LocalizedTextView label is not assigned.";
                return false;
            }
            return _labelView.Refresh(out error);
        }
    }
}
