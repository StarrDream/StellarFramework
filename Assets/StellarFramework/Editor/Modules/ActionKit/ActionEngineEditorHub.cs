using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using StellarFramework.ActionEngine;

namespace StellarFramework.Editor
{
    /// <summary>
    /// 动作编排中心 - 现代化工程版
    /// 功能：资产绑定、路径自动拾取、节点缺失预警、运行时同步
    /// </summary>
    [StellarTool("动作编排中心", "常用工具", 6)]
    public class ActionEngineEditorHub : ToolModule
    {
        private ActionEngineAsset _activeAsset;
        private GameObject _rootTarget;
        private Vector2 _mainScroll;
        
        private Type[] _strategyTypes;
        private string[] _strategyNames;
        private GUIStyle _headerStyle;
        private GUIStyle _errorBoxStyle;

        #region 初始化

        public override void OnEnable()
        {
            base.OnEnable();
            InitStyles();
            ScanStrategies();
        }

        private void InitStyles()
        {
            _headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
            _errorBoxStyle = new GUIStyle(EditorStyles.helpBox);
            _errorBoxStyle.normal.textColor = Color.red;
        }

        private void ScanStrategies()
        {
            _strategyTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .Where(p => typeof(IActionStrategy).IsAssignableFrom(p) && !p.IsInterface && !p.IsAbstract)
                .ToArray();
            
            _strategyNames = _strategyTypes.Select(t => t.Name.Replace("Strategy", "")).ToArray();
        }

        #endregion

        public override void OnGUI()
        {
            DrawToolbar();

            if (_activeAsset == null)
            {
                DrawEmptyState();
                return;
            }

            _mainScroll = EditorGUILayout.BeginScrollView(_mainScroll);
            DrawBindingPanel();
            GUILayout.Space(10);
            DrawAssetEditor();
            EditorGUILayout.EndScrollView();

            DrawFooter();
        }

        private void DrawToolbar()
        {
            using (new GUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.Height(25)))
            {
                GUILayout.Label("当前资产:", EditorStyles.miniLabel);
                var lastAsset = _activeAsset;
                _activeAsset = (ActionEngineAsset)EditorGUILayout.ObjectField(_activeAsset, typeof(ActionEngineAsset), false, GUILayout.Width(200));

                if (lastAsset != _activeAsset && _activeAsset != null) AutoBindTarget();

                if (GUILayout.Button("新建资源", EditorStyles.toolbarButton, GUILayout.Width(70))) CreateNewAsset();

                GUILayout.FlexibleSpace();

                if (Application.isPlaying)
                {
                    GUI.color = Color.cyan;
                    if (GUILayout.Button("同步运行时修改到资源", EditorStyles.toolbarButton)) SaveAssetToDisk();
                    GUI.color = Color.white;
                }
            }
        }

        private void DrawBindingPanel()
        {
            using (new GUILayout.VerticalScope("helpbox"))
            {
                EditorGUILayout.LabelField("1. 资产与实例绑定", _headerStyle);
                
                EditorGUI.BeginChangeCheck();
                var prefab = (GameObject)EditorGUILayout.ObjectField("关联预制体 (Prefab)", _activeAsset.TargetPrefab, typeof(GameObject), false);
                if (EditorGUI.EndChangeCheck())
                {
                    _activeAsset.TargetPrefab = prefab;
                    EditorUtility.SetDirty(_activeAsset);
                }

                using (new GUILayout.HorizontalScope())
                {
                    _rootTarget = (GameObject)EditorGUILayout.ObjectField("场景实例 (Root)", _rootTarget, typeof(GameObject), true);
                    if (_activeAsset.TargetPrefab != null && _rootTarget == null)
                    {
                        if (GUILayout.Button("查找/生成实例", GUILayout.Width(100))) AutoBindTarget();
                    }
                }
            }
        }

        private void DrawAssetEditor()
        {
            SerializedObject so = new SerializedObject(_activeAsset);
            so.Update();

            EditorGUILayout.LabelField("2. 动作序列编排", _headerStyle);
            DrawGroup(so.FindProperty("RootGroup"));

            if (so.hasModifiedProperties)
            {
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(_activeAsset);
            }
        }

        private void DrawGroup(SerializedProperty groupProp)
        {
            if (groupProp == null) return;

            using (new GUILayout.VerticalScope("window"))
            {
                EditorGUILayout.PropertyField(groupProp.FindPropertyRelative("GroupName"), new GUIContent("组名"));
                EditorGUILayout.PropertyField(groupProp.FindPropertyRelative("Mode"), new GUIContent("播放模式"));

                GUILayout.Space(5);
                DrawStepsList(groupProp.FindPropertyRelative("Steps"));
                
                GUILayout.Space(5);
                DrawSubGroupsList(groupProp.FindPropertyRelative("SubGroups"));
            }
        }

        private void DrawStepsList(SerializedProperty stepsProp)
        {
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"动作步骤清单 ({stepsProp.arraySize})", EditorStyles.miniBoldLabel);

                for (int i = 0; i < stepsProp.arraySize; i++)
                {
                    SerializedProperty step = stepsProp.GetArrayElementAtIndex(i);
                    SerializedProperty pathProp = step.FindPropertyRelative("TargetPath");
                    
                    // --- 核心：节点存在性校验 ---
                    bool isPathValid = ValidatePath(pathProp.stringValue, out Transform foundTarget);

                    using (new GUILayout.VerticalScope("box"))
                    {
                        if (!isPathValid)
                        {
                            EditorGUILayout.HelpBox($"错误：在当前 Root 下找不到路径 '{pathProp.stringValue}'，请检查预制体节点！", MessageType.Error);
                            GUI.backgroundColor = Color.red;
                        }

                        EditorGUILayout.BeginHorizontal();
                        
                        // 路径编辑与拾取
                        EditorGUILayout.PropertyField(pathProp, new GUIContent("相对路径"));
                        
                        var pickedObj = (GameObject)EditorGUILayout.ObjectField(GUIContent.none, null, typeof(GameObject), true, GUILayout.Width(60));
                        if (pickedObj != null && _rootTarget != null)
                        {
                            string newPath = GetRelativePath(_rootTarget.transform, pickedObj.transform);
                            if (newPath != null) pathProp.stringValue = newPath;
                        }

                        if (GUILayout.Button("✕", GUILayout.Width(20))) { stepsProp.DeleteArrayElementAtIndex(i); return; }
                        EditorGUILayout.EndHorizontal();
                        GUI.backgroundColor = Color.white;

                        DrawStrategySelector(step);
                        
                        using (new GUILayout.HorizontalScope())
                        {
                            EditorGUILayout.PropertyField(step.FindPropertyRelative("TargetVector"), new GUIContent("目标值"));
                            EditorGUILayout.PropertyField(step.FindPropertyRelative("Duration"), GUIContent.none, GUILayout.Width(50));
                            EditorGUILayout.PropertyField(step.FindPropertyRelative("Ease"), GUIContent.none, GUILayout.Width(80));
                        }
                    }
                }

                if (GUILayout.Button("+ 添加步骤 (拖拽子物体到右侧小框可自动填路径)"))
                {
                    stepsProp.InsertArrayElementAtIndex(stepsProp.arraySize);
                }
            }
        }

        private void DrawSubGroupsList(SerializedProperty subGroupsProp)
        {
            for (int i = 0; i < subGroupsProp.arraySize; i++)
            {
                DrawGroup(subGroupsProp.GetArrayElementAtIndex(i));
                if (GUILayout.Button("移除此子组", EditorStyles.miniButton)) { subGroupsProp.DeleteArrayElementAtIndex(i); return; }
            }
            if (GUILayout.Button("+ 添加嵌套子组")) { subGroupsProp.InsertArrayElementAtIndex(subGroupsProp.arraySize); }
        }

        #region 逻辑辅助

        private bool ValidatePath(string path, out Transform target)
        {
            target = null;
            if (_rootTarget == null) return true; // 未绑定实例时不报错
            if (string.IsNullOrEmpty(path)) { target = _rootTarget.transform; return true; }
            
            target = _rootTarget.transform.Find(path);
            return target != null;
        }

        private string GetRelativePath(Transform root, Transform target)
        {
            if (root == target) return "";
            List<string> parts = new List<string>();
            Transform curr = target;
            while (curr != null && curr != root) { parts.Add(curr.name); curr = curr.parent; }
            if (curr == null) { Debug.LogError("拾取的物体不在 Root 层级下！"); return null; }
            parts.Reverse();
            return string.Join("/", parts);
        }

        private void DrawStrategySelector(SerializedProperty stepProp)
        {
            SerializedProperty strategyProp = stepProp.FindPropertyRelative("Strategy");
            string typeName = strategyProp.managedReferenceFullTypename;
            int idx = -1;
            if (!string.IsNullOrEmpty(typeName))
            {
                for (int i = 0; i < _strategyTypes.Length; i++)
                    if (typeName.Contains(_strategyTypes[i].Name)) { idx = i; break; }
            }

            int next = EditorGUILayout.Popup("执行策略", idx, _strategyNames);
            if (next != idx && next >= 0) strategyProp.managedReferenceValue = Activator.CreateInstance(_strategyTypes[next]);
        }

        private void AutoBindTarget()
        {
            if (_activeAsset.TargetPrefab == null) return;
            var existing = GameObject.FindObjectsOfType<GameObject>()
                .FirstOrDefault(go => PrefabUtility.GetCorrespondingObjectFromSource(go) == _activeAsset.TargetPrefab);

            if (existing != null) _rootTarget = existing;
            else if (!Application.isPlaying && EditorUtility.DisplayDialog("自动绑定", "未找到实例，是否生成？", "生成", "取消"))
            {
                _rootTarget = (GameObject)PrefabUtility.InstantiatePrefab(_activeAsset.TargetPrefab);
                Undo.RegisterCreatedObjectUndo(_rootTarget, "Auto Instantiate");
            }
        }

        private void CreateNewAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject("创建资源", "NewActionAsset", "asset", "");
            if (string.IsNullOrEmpty(path)) return;
            var asset = ScriptableObject.CreateInstance<ActionEngineAsset>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            _activeAsset = asset;
        }

        private void SaveAssetToDisk()
        {
            if (_activeAsset == null) return;
            EditorUtility.SetDirty(_activeAsset);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ActionEngine] 运行时数据已持久化至: {AssetDatabase.GetAssetPath(_activeAsset)}");
        }

        private void DrawEmptyState()
        {
            GUILayout.FlexibleSpace();
            EditorGUILayout.HelpBox("请选择或创建一个 ActionEngineAsset。", MessageType.Info);
            if (GUILayout.Button("创建新资源", GUILayout.Height(30))) CreateNewAsset();
            GUILayout.FlexibleSpace();
        }

        private void DrawFooter()
        {
            bool canPlay = Application.isPlaying && _rootTarget != null && _activeAsset != null;
            using (new EditorGUI.DisabledGroupScope(!canPlay))
            {
                GUI.backgroundColor = canPlay ? Color.green : Color.white;
                if (GUILayout.Button(Application.isPlaying ? "▶ 执行编排" : "请在运行时预览", GUILayout.Height(40)))
                    ActionEngineRunner.Play(_rootTarget, _activeAsset).Forget();
                GUI.backgroundColor = Color.white;
            }
        }

        #endregion
    }
}
