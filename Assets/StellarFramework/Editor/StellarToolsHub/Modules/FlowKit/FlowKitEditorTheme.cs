using UnityEngine;
using UnityEngine.UIElements;
using StellarFramework.FlowKit;

namespace StellarFramework.Editor.Modules.FlowKit
{
    internal static class FlowKitEditorTheme
    {
        internal static readonly Color Panel = new Color(0.14f, 0.16f, 0.19f);
        internal static readonly Color Card = new Color(0.18f, 0.20f, 0.24f);
        internal static readonly Color CardSoft = new Color(0.16f, 0.18f, 0.21f);
        internal static readonly Color Border = new Color(0.27f, 0.31f, 0.37f);
        internal static readonly Color Accent = new Color(0.22f, 0.52f, 0.88f);
        internal static readonly Color AccentSoft = new Color(0.16f, 0.32f, 0.50f);
        internal static readonly Color TextPrimary = new Color(0.96f, 0.97f, 0.99f);
        internal static readonly Color TextSecondary = new Color(0.73f, 0.77f, 0.83f);
        internal static readonly Color Success = new Color(0.35f, 0.78f, 0.50f);
        internal static readonly Color Warning = new Color(0.95f, 0.70f, 0.28f);
        internal static readonly Color Error = new Color(0.90f, 0.35f, 0.35f);

        internal static Color PortColor(FlowPortSemantic semantic)
        {
            switch (semantic)
            {
                case FlowPortSemantic.Success:
                case FlowPortSemantic.ConditionTrue: return Success;
                case FlowPortSemantic.Failure:
                case FlowPortSemantic.ConditionFalse: return Error;
                case FlowPortSemantic.Cancelled:
                case FlowPortSemantic.Timeout: return Warning;
                default: return Accent;
            }
        }

        internal static void ApplyRoot(VisualElement root)
        {
            root.style.backgroundColor = Panel;
            root.style.color = TextPrimary;
        }

        internal static void ApplyCard(VisualElement element)
        {
            element.style.backgroundColor = CardSoft;
            element.style.borderLeftWidth = 1f;
            element.style.borderRightWidth = 1f;
            element.style.borderTopWidth = 1f;
            element.style.borderBottomWidth = 1f;
            element.style.borderLeftColor = Border;
            element.style.borderRightColor = Border;
            element.style.borderTopColor = Border;
            element.style.borderBottomColor = Border;
            element.style.borderTopLeftRadius = 7f;
            element.style.borderTopRightRadius = 7f;
            element.style.borderBottomLeftRadius = 7f;
            element.style.borderBottomRightRadius = 7f;
        }

        internal static void ApplyToolbar(VisualElement toolbar)
        {
            toolbar.style.backgroundColor = Card;
            toolbar.style.borderBottomWidth = 1f;
            toolbar.style.borderBottomColor = Border;
        }

        internal static void ApplyButton(Button button, bool primary = false)
        {
            button.style.height = 24f;
            button.style.marginRight = 4f;
            button.style.color = TextPrimary;
            button.style.backgroundColor = primary ? AccentSoft : CardSoft;
            button.style.borderLeftColor = primary ? Accent : Border;
            button.style.borderRightColor = primary ? Accent : Border;
            button.style.borderTopColor = primary ? Accent : Border;
            button.style.borderBottomColor = primary ? Accent : Border;
            button.style.borderLeftWidth = 1f;
            button.style.borderRightWidth = 1f;
            button.style.borderTopWidth = 1f;
            button.style.borderBottomWidth = 1f;
            button.style.borderTopLeftRadius = 5f;
            button.style.borderTopRightRadius = 5f;
            button.style.borderBottomLeftRadius = 5f;
            button.style.borderBottomRightRadius = 5f;
        }

        internal static Label Header(string text)
        {
            var label = new Label(text);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.fontSize = 12f;
            label.style.color = TextPrimary;
            label.style.marginTop = 6f;
            label.style.marginBottom = 4f;
            return label;
        }

        internal static void ApplyPanelDivider(VisualElement element)
        {
            element.style.borderLeftColor = Border;
            element.style.borderRightColor = Border;
            element.style.borderTopColor = Border;
            element.style.borderBottomColor = Border;
        }
    }
}
