using UnityEditor;
using UnityEngine;
using StellarFramework.FlowKit.Unity;

namespace StellarFramework.Editor.Modules.FlowKit
{
    [CustomPropertyDrawer(typeof(FlowAuthoringContractEntry))]
    internal sealed class FlowAuthoringContractEntryDrawer : PropertyDrawer
    {
        private const float Gap = 2f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            Rect line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            SerializedProperty id = property.FindPropertyRelative("id");
            string title = string.IsNullOrEmpty(id.stringValue) ? label.text : id.stringValue;
            property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, title, true);

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                float y = line.yMax + Gap;
                Draw(ref y, position, id, new GUIContent("ID"));
                Draw(ref y, position, property.FindPropertyRelative("displayName"), new GUIContent("Display Name"));
                Draw(ref y, position, property.FindPropertyRelative("description"), new GUIContent("Description"));

                FlowContractEditorCategory category = ResolveCategory(property.propertyPath);
                switch (category)
                {
                    case FlowContractEditorCategory.Operation:
                        Draw(ref y, position, property.FindPropertyRelative("externalCallKind"), new GUIContent("Call Kind"));
                        Draw(ref y, position, property.FindPropertyRelative("arguments"), new GUIContent("Arguments"), true);
                        Draw(ref y, position, property.FindPropertyRelative("resultKind"), new GUIContent("Result Kind"));
                        Draw(ref y, position, property.FindPropertyRelative("requiredCapability"), new GUIContent("Required Capability"));
                        Draw(ref y, position, property.FindPropertyRelative("allowAdditionalArguments"), new GUIContent("Allow Additional Arguments"));
                        break;
                    case FlowContractEditorCategory.Signal:
                    case FlowContractEditorCategory.State:
                    case FlowContractEditorCategory.Blackboard:
                        Draw(ref y, position, property.FindPropertyRelative("valueKind"), new GUIContent("Value Kind"));
                        break;
                    case FlowContractEditorCategory.Binding:
                        Draw(ref y, position, property.FindPropertyRelative("expectedBindingType"), new GUIContent("Expected Binding Type"));
                        break;
                    default:
                        Draw(ref y, position, property.FindPropertyRelative("valueKind"), new GUIContent("Value Kind"));
                        break;
                }
                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded) return height;

            height += Gap + Height(property.FindPropertyRelative("id"));
            height += Gap + Height(property.FindPropertyRelative("displayName"));
            height += Gap + Height(property.FindPropertyRelative("description"));

            switch (ResolveCategory(property.propertyPath))
            {
                case FlowContractEditorCategory.Operation:
                    height += Gap + Height(property.FindPropertyRelative("externalCallKind"));
                    height += Gap + Height(property.FindPropertyRelative("arguments"), true);
                    height += Gap + Height(property.FindPropertyRelative("resultKind"));
                    height += Gap + Height(property.FindPropertyRelative("requiredCapability"));
                    height += Gap + Height(property.FindPropertyRelative("allowAdditionalArguments"));
                    break;
                case FlowContractEditorCategory.Signal:
                case FlowContractEditorCategory.State:
                case FlowContractEditorCategory.Blackboard:
                    height += Gap + Height(property.FindPropertyRelative("valueKind"));
                    break;
                case FlowContractEditorCategory.Binding:
                    height += Gap + Height(property.FindPropertyRelative("expectedBindingType"));
                    break;
                default:
                    height += Gap + Height(property.FindPropertyRelative("valueKind"));
                    break;
            }

            return height + Gap;
        }

        private static void Draw(
            ref float y,
            Rect outer,
            SerializedProperty property,
            GUIContent label,
            bool includeChildren = false)
        {
            float height = Height(property, includeChildren);
            var rect = new Rect(outer.x, y, outer.width, height);
            EditorGUI.PropertyField(rect, property, label, includeChildren);
            y += height + Gap;
        }

        private static float Height(SerializedProperty property, bool includeChildren = false) =>
            property == null ? 0f : EditorGUI.GetPropertyHeight(property, includeChildren);

        private static FlowContractEditorCategory ResolveCategory(string path)
        {
            if (path.Contains("operations.Array.")) return FlowContractEditorCategory.Operation;
            if (path.Contains("signals.Array.")) return FlowContractEditorCategory.Signal;
            if (path.Contains("states.Array.")) return FlowContractEditorCategory.State;
            if (path.Contains("blackboardKeys.Array.")) return FlowContractEditorCategory.Blackboard;
            if (path.Contains("bindings.Array.")) return FlowContractEditorCategory.Binding;
            return FlowContractEditorCategory.Unknown;
        }

        private enum FlowContractEditorCategory
        {
            Unknown,
            Operation,
            Signal,
            State,
            Blackboard,
            Binding
        }
    }
}
