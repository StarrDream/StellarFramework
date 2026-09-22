using StellarFramework.Localization.UnityUGUI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace StellarFramework.Localization.Editor
{
    [CustomEditor(typeof(LocalizedTextView))]
    public sealed class LocalizedTextViewEditor : UnityEditor.Editor
    {
        private bool _showAdvanced;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty context = serializedObject.FindProperty("_context");
            SerializedProperty target = serializedObject.FindProperty("_target");
            SerializedProperty key = serializedObject.FindProperty("_key");
            SerializedProperty bindingId = serializedObject.FindProperty("_bindingId");
            SerializedProperty autoResolve = serializedObject.FindProperty("_autoResolveContext");

            EditorGUILayout.LabelField("Localized Text", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(target);
            EditorGUILayout.PropertyField(autoResolve, new GUIContent("Auto Resolve Context"));
            using (new EditorGUI.DisabledScope(autoResolve.boolValue))
            {
                EditorGUILayout.PropertyField(context);
            }

            bool scannerManaged = !string.IsNullOrWhiteSpace(bindingId.stringValue);
            EditorGUILayout.Space(4f);
            using (new EditorGUI.DisabledScope(scannerManaged))
            {
                EditorGUILayout.PropertyField(
                    key,
                    new GUIContent(scannerManaged ? "Localization Key (Scanner Managed)" : "Localization Key"));
            }

            Text textTarget = target.objectReferenceValue as Text;
            EditorGUILayout.LabelField(
                "Source",
                textTarget == null ? "(Target 未绑定)" : textTarget.text,
                EditorStyles.wordWrappedLabel);

            bool synced =
                textTarget != null &&
                !string.IsNullOrWhiteSpace(key.stringValue) &&
                (!scannerManaged || bindingId.stringValue.Length >= 8);
            EditorGUILayout.HelpBox(
                synced ? "✓ Binding 配置完整" : "Binding 未完成，请使用 Localization Scanner 或手动补齐。",
                synced ? MessageType.Info : MessageType.Warning);

            _showAdvanced = EditorGUILayout.Foldout(_showAdvanced, "Advanced", true);
            if (_showAdvanced)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.TextField(
                        "Binding ID",
                        string.IsNullOrWhiteSpace(bindingId.stringValue) ? "(未生成)" : bindingId.stringValue);
                }
                EditorGUILayout.HelpBox(
                    "BindingId 是机器稳定身份。Rename / Reparent / Reorder 不应修改它；复制冲突由 Scanner 重新 Fork。",
                    MessageType.None);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
