#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using StellarFramework.UI;

namespace StellarFramework.Editor
{
    /// <summary>
    /// UIAutoBind 的智能检视面板
    /// 职责：自动推断目标组件，并向开发者展示即将生成的变量名（只读，跟随 GameObject 名称）。
    /// </summary>
    [CustomEditor(typeof(UIAutoBind))]
    public class UIAutoBindEditor : UnityEditor.Editor
    {
        private UIAutoBind _target;
        private string[] _componentNames;
        private Object[] _components;
        private int _selectedIndex = 0;

        private void OnEnable()
        {
            _target = (UIAutoBind)target;
            RefreshComponents();

            // 首次挂载时，自动推断目标
            if (_target.Target == null)
            {
                AutoSelectTarget();
                EditorUtility.SetDirty(_target);
            }
            else
            {
                // 恢复选中状态
                _selectedIndex = System.Array.IndexOf(_components, _target.Target);
                if (_selectedIndex == -1) _selectedIndex = 0;
            }
        }

        private void RefreshComponents()
        {
            var comps = _target.GetComponents<Component>().Where(c => c != _target && c != null).ToList();
            
            // 索引 0 永远是 GameObject 本身
            var objList = new List<Object> { _target.gameObject };
            objList.AddRange(comps);

            _components = objList.ToArray();
            _componentNames = new string[_components.Length];

            for (int i = 0; i < _components.Length; i++)
            {
                if (_components[i] is GameObject)
                {
                    _componentNames[i] = "GameObject";
                }
                else
                {
                    _componentNames[i] = _components[i].GetType().Name;
                }
            }
        }

        private void AutoSelectTarget()
        {
            if (_components.Length <= 1)
            {
                _target.Target = _components[0];
                _selectedIndex = 0;
                return;
            }

            int bestIndex = 0; // 默认指向 GameObject (索引 0)
            int highestPriority = -1;

            // 从索引 1 开始遍历组件，跳过 GameObject
            for (int i = 1; i < _components.Length; i++)
            {
                int priority = 0;
                var comp = _components[i];
                string typeName = comp.GetType().Name;

                // 核心推断权重梯队
                if (typeName == "Button") 
                {
                    priority = 1000; // T0: 交互核心，绝对首位
                }
                else if (typeName == "Text" || typeName == "TextMeshProUGUI" || typeName == "TMP_Text") 
                {
                    priority = 900;  // T1: 文本展示
                }
                else if (typeName == "Toggle" || typeName == "Slider" || typeName == "InputField" || typeName == "TMP_InputField" || typeName == "Dropdown" || typeName == "TMP_Dropdown" || typeName == "ScrollRect" || typeName == "Scrollbar") 
                {
                    priority = 800;  // T2: 复杂交互组件 (ScrollRect 等)
                }
                else if (typeName == "Image" || typeName == "RawImage") 
                {
                    priority = 700;  // T3: 图像展示 (仅高于兜底)
                }
                else if (comp is UIBehaviour) 
                {
                    priority = 600;  // T4: 兜底的其他 UI 组件 (如 CanvasGroup, Mask 等)
                }
                else if (comp is RectTransform) 
                {
                    priority = 100;  // T5: 布局控制
                }
                else if (comp is Transform) 
                {
                    priority = 50;   // T6: 基础变换
                }

                if (priority > highestPriority)
                {
                    highestPriority = priority;
                    bestIndex = i;
                }
            }

            _selectedIndex = bestIndex;
            _target.Target = _components[bestIndex];
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("代码生成配置", EditorStyles.boldLabel);

            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                // 动态读取 GameObject 名称，替换空格防止语法错误
                string generatedName = _target.gameObject.name.Replace(" ", "_");
                
                // 仅作只读展示，明确告知开发者变量名由节点名决定
                EditorGUILayout.LabelField("生成的变量名", generatedName);
                EditorGUILayout.Space(2);

                // 目标组件下拉框
                EditorGUI.BeginChangeCheck();
                _selectedIndex = EditorGUILayout.Popup("绑定目标 (Target)", _selectedIndex, _componentNames);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_target, "Change Bind Target");
                    _target.Target = _components[_selectedIndex];
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
