using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace StellarFramework.Editor.Modules
{
    /// <summary>
    /// 文档中心模块
    /// 自动扫描框架目录下的所有 Markdown 文件，提供集成的阅读体验
    /// </summary>
    [StellarTool("文档中心 (Docs)", "框架核心", -999)]
    public class DocumentationHubModule : ToolModule
    {
        public override string Icon => "d_TextAsset Icon";
        public override string Description => "统一管理与查阅框架内所有 Markdown 文档。";

        private List<string> _docPaths = new List<string>();
        private string _selectedDocPath = "";
        private string _docContent = "";
        private Vector2 _leftScroll;
        private Vector2 _rightScroll;

        public override void OnEnable()
        {
            RefreshDocs();
        }

        private void RefreshDocs()
        {
            _docPaths.Clear();
            string rootPath = Application.dataPath + "/StellarFramework";
            if (Directory.Exists(rootPath))
            {
                string[] files = Directory.GetFiles(rootPath, "*.md", SearchOption.AllDirectories);
                // 统一路径分隔符并排序
                _docPaths.AddRange(files.Select(f => f.Replace("\\", "/")).OrderBy(f => Path.GetFileName(f)));
            }
        }

        public override void OnGUI()
        {
            using (new GUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("刷新文档列表", EditorStyles.toolbarButton, GUILayout.Width(100)))
                {
                    RefreshDocs();
                }

                GUILayout.FlexibleSpace();
            }

            EditorGUILayout.BeginHorizontal();
            DrawLeftPanel();
            DrawRightPanel();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawLeftPanel()
        {
            using (new GUILayout.VerticalScope("box", GUILayout.Width(260), GUILayout.ExpandHeight(true)))
            {
                GUILayout.Label($"文档列表 ({_docPaths.Count})", EditorStyles.boldLabel);
                GUILayout.Space(5);

                _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);
                foreach (var path in _docPaths)
                {
                    string fileName = Path.GetFileNameWithoutExtension(path);
                    bool isSelected = _selectedDocPath == path;

                    var oldColor = GUI.backgroundColor;
                    if (isSelected) GUI.backgroundColor = new Color(0.22f, 0.52f, 0.88f);

                    if (GUILayout.Button(fileName, Window.SidebarButtonStyle))
                    {
                        if (_selectedDocPath != path)
                        {
                            _selectedDocPath = path;
                            _docContent = File.ReadAllText(path);
                            _rightScroll = Vector2.zero;
                            GUI.FocusControl(null);
                        }
                    }

                    GUI.backgroundColor = oldColor;
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawRightPanel()
        {
            using (new GUILayout.VerticalScope("box", GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true)))
            {
                if (string.IsNullOrEmpty(_selectedDocPath))
                {
                    GUILayout.FlexibleSpace();
                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    GUILayout.Label("请在左侧选择要查阅的文档", EditorStyles.largeLabel);
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    GUILayout.FlexibleSpace();
                    return;
                }

                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Label(Path.GetFileName(_selectedDocPath), EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("在外部编辑器打开", GUILayout.Width(120), GUILayout.Height(24)))
                    {
                        EditorUtility.OpenWithDefaultApp(_selectedDocPath);
                    }
                }

                GUILayout.Space(5);

                _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);

                // 使用 TextArea 渲染 Markdown 文本，支持选中复制
                GUIStyle mdStyle = new GUIStyle(EditorStyles.textArea)
                {
                    wordWrap = true,
                    richText = true,
                    fontSize = 13,
                    padding = new RectOffset(10, 10, 10, 10)
                };

                EditorGUILayout.TextArea(_docContent, mdStyle, GUILayout.ExpandHeight(true));

                EditorGUILayout.EndScrollView();
            }
        }
    }
}