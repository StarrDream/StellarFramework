using UnityEngine;
using UnityEditor;

namespace StellarFramework.Editor
{
    /// <summary>
    /// 工具模块基类
    /// 所有扩展工具都应继承此类
    /// </summary>
    public abstract class ToolModule
    {
        public StellarFrameworkTools Window { get; private set; }

        // 从 Attribute 中获取的元数据
        public string Title { get; set; }
        public string Group { get; set; }
        public int Order { get; set; }

        public virtual string Description => "";
        public virtual string Icon => "d_ScriptableObject Icon";

        public void Initialize(StellarFrameworkTools window)
        {
            Window = window;
        }

        public abstract void OnGUI();

        public virtual void OnEnable()
        {
        }

        public virtual void OnDisable()
        {
        }

        public virtual void OnSelectionChange()
        {
        }

        // --- 样式辅助方法 (封装对 Window 样式的访问) ---

        protected void Section(string title)
        {
            GUILayout.Space(10);
            // 访问 Window 公开的样式
            GUILayout.Label(title, Window.SectionHeaderStyle);
            GUILayout.Space(2);
        }

        protected bool PrimaryButton(string label, params GUILayoutOption[] options)
        {
            var old = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.22f, 0.52f, 0.88f); // AccentDark
            bool clicked = GUILayout.Button(label, Window.PrimaryButtonStyle, options);
            GUI.backgroundColor = old;
            return clicked;
        }

        protected bool DangerButton(string label, params GUILayoutOption[] options)
        {
            var old = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.90f, 0.25f, 0.25f); // Danger
            bool clicked = GUILayout.Button(label, Window.DangerButtonStyle, options);
            GUI.backgroundColor = old;
            return clicked;
        }
    }
}