using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace StellarFramework.Editor
{
    /// <summary>
    /// 工具模块基类。
    /// 默认仍可通过 OnGUI 走旧的 IMGUI 路径；迁移后的模块可直接返回 UI Toolkit 视图。
    /// </summary>
    public abstract class ToolModule
    {
        public StellarFrameworkTools Window { get; private set; }

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

        public virtual VisualElement CreateView()
        {
            return null;
        }

        public virtual void OnEnable()
        {
        }

        public virtual void OnDisable()
        {
        }

        public virtual void OnSelectionChange()
        {
        }

        protected void Section(string title)
        {
            GUILayout.Space(10);
            GUILayout.Label(title, Window.SectionHeaderStyle);
            GUILayout.Space(2);
        }

        protected bool PrimaryButton(string label, params GUILayoutOption[] options)
        {
            return PrimaryButton(new GUIContent(label), options);
        }

        protected bool PrimaryButton(GUIContent content, params GUILayoutOption[] options)
        {
            Color old = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.22f, 0.52f, 0.88f);
            bool clicked = GUILayout.Button(content, Window.PrimaryButtonStyle, options);
            GUI.backgroundColor = old;
            return clicked;
        }

        protected bool DangerButton(string label, params GUILayoutOption[] options)
        {
            Color old = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.90f, 0.25f, 0.25f);
            bool clicked = GUILayout.Button(label, Window.DangerButtonStyle, options);
            GUI.backgroundColor = old;
            return clicked;
        }
    }
}
