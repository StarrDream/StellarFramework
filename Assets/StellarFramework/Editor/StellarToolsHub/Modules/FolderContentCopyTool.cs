using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace StellarFramework.Editor
{
    public class FolderContentCopyTool : ToolsHubEmbeddedPanel
    {
        private string _rootFolder = "";
        private Vector2 _scroll;
        private readonly List<string> _subFolders = new List<string>(128);
        private readonly HashSet<string> _selectedFolders = new HashSet<string>();

        private TextField _rootFolderField;
        private ScrollView _folderListView;
        private Label _folderCountLabel;
        private Button _copyButton;

        // 文件类型过滤
        private bool _includeCs = true;
        private bool _includeShader = true;
        private bool _includeTxt = false;
        private bool _includeJson = false;
        private bool _includeAsmdef = false;
        private bool _includeMeta = false;

        // 优化选项
        private bool _optimizeForContext = true; // 基础压缩（去空行）
        private bool _removeComments = false; // 移除注释（大幅减少）
        private bool _removeIndentation = false; // 移除缩进（代码变平，大幅减少）

        protected override VisualElement BuildView()
        {
            VisualElement root = new VisualElement
            {
                style =
                {
                    flexGrow = 1f
                }
            };

            _rootFolderField = new TextField("根目录")
            {
                value = _rootFolder,
                isReadOnly = true
            };
            root.Add(_rootFolderField);

            VisualElement rootActions = CreateRow();
            rootActions.Add(CreateButton("选择目录", () =>
            {
                string path = EditorUtility.OpenFolderPanel("选择根目录", _rootFolder, "");
                if (!string.IsNullOrEmpty(path))
                {
                    _rootFolder = path;
                    _rootFolderField.value = path;
                    RefreshSubFolders();
                    RefreshFolderListView();
                }
            }));
            rootActions.Add(CreateButton("刷新", () =>
            {
                RefreshSubFolders();
                RefreshFolderListView();
            }));
            root.Add(rootActions);

            root.Add(new Label("文件类型")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginTop = 8
                }
            });

            VisualElement fileTypeRow = CreateRow();
            fileTypeRow.Add(CreateToggle(".cs", _includeCs, value => _includeCs = value));
            fileTypeRow.Add(CreateToggle("Shader", _includeShader, value => _includeShader = value));
            fileTypeRow.Add(CreateToggle(".json", _includeJson, value => _includeJson = value));
            fileTypeRow.Add(CreateToggle(".asmdef", _includeAsmdef, value => _includeAsmdef = value));
            fileTypeRow.Add(CreateToggle("Txt/Md", _includeTxt, value => _includeTxt = value));
            fileTypeRow.Add(CreateToggle(".meta", _includeMeta, value => _includeMeta = value));
            root.Add(fileTypeRow);

            root.Add(new Label("压缩策略")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginTop = 8
                }
            });

            root.Add(CreateToggle("基础压缩 (合并空行 + Markdown 格式)", _optimizeForContext, value => _optimizeForContext = value));

            VisualElement optimizeRow = CreateRow();
            optimizeRow.Add(CreateToggle("移除注释", _removeComments, value => _removeComments = value));
            optimizeRow.Add(CreateToggle("移除缩进", _removeIndentation, value => _removeIndentation = value));
            root.Add(optimizeRow);

            root.Add(new HelpBox("复制前请确认 IDE 已保存代码文件。会跳过大于 500KB 的文件，并按当前过滤与压缩策略输出。", HelpBoxMessageType.Info));

            _folderCountLabel = new Label();
            _folderCountLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _folderCountLabel.style.marginTop = 8;
            root.Add(_folderCountLabel);

            _folderListView = new ScrollView
            {
                style =
                {
                    flexGrow = 1f,
                    minHeight = 260
                }
            };
            root.Add(_folderListView);

            VisualElement actionRow = CreateRow();
            actionRow.Add(CreateButton("全选", () =>
            {
                _selectedFolders.UnionWith(_subFolders);
                RefreshFolderListView();
            }));
            actionRow.Add(CreateButton("清空", () =>
            {
                _selectedFolders.Clear();
                RefreshFolderListView();
            }));

            _copyButton = CreateButton("复制到剪贴板", CopySelectedFoldersToClipboard);
            actionRow.Add(_copyButton);
            root.Add(actionRow);

            RefreshFolderListView();
            return root;
        }

        protected override void DrawIMGUI()
        {
            DrawHeader();
            DrawFilters();
            DrawFolderList();
            DrawActionButtons();
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("根目录设置", EditorStyles.boldLabel);

            using (new GUILayout.HorizontalScope())
            {
                string displayPath = string.IsNullOrEmpty(_rootFolder) ? "未选择..." : _rootFolder;
                EditorGUILayout.TextField(displayPath, EditorStyles.textField);

                if (GUILayout.Button("选择目录", GUILayout.Width(80)))
                {
                    var path = EditorUtility.OpenFolderPanel("选择根目录", _rootFolder, "");
                    if (!string.IsNullOrEmpty(path))
                    {
                        _rootFolder = path;
                        RefreshSubFolders();
                    }
                }

                if (GUILayout.Button("刷新", GUILayout.Width(60)))
                {
                    RefreshSubFolders();
                }
            }
        }

        private void DrawFilters()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("输出配置", EditorStyles.boldLabel);

            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("文件类型:", EditorStyles.miniLabel);
                using (new GUILayout.HorizontalScope())
                {
                    _includeCs = EditorGUILayout.ToggleLeft(".cs", _includeCs, GUILayout.Width(60));
                    _includeShader = EditorGUILayout.ToggleLeft("Shader", _includeShader, GUILayout.Width(70));
                    _includeJson = EditorGUILayout.ToggleLeft(".json", _includeJson, GUILayout.Width(60));
                    _includeAsmdef = EditorGUILayout.ToggleLeft(".asmdef", _includeAsmdef, GUILayout.Width(80));
                    _includeTxt = EditorGUILayout.ToggleLeft("Txt/Md", _includeTxt, GUILayout.Width(70));
                    _includeMeta = EditorGUILayout.ToggleLeft(".meta", _includeMeta, GUILayout.Width(60));
                }

                GUILayout.Space(5);
                GUILayout.Label("压缩策略 (上下文优化):", EditorStyles.boldLabel);

                _optimizeForContext = EditorGUILayout.ToggleLeft("基础压缩 (合并空行 + Markdown 格式)", _optimizeForContext);

                if (_optimizeForContext)
                {
                    using (new GUILayout.HorizontalScope())
                    {
                        GUILayout.Space(20);
                        _removeComments = EditorGUILayout.ToggleLeft("移除注释 (//...)", _removeComments, GUILayout.Width(140));
                        _removeIndentation = EditorGUILayout.ToggleLeft("移除缩进 (扁平化)", _removeIndentation, GUILayout.Width(140));
                    }

                    string tips = "当前策略预估效果：\n";
                    if (!_removeComments && !_removeIndentation) tips += "• 保留原始格式，仅去除多余空行。";
                    if (_removeComments) tips += "• 移除所有注释，节省约 20% Token。\n";
                    if (_removeIndentation) tips += "• 移除行首空格，节省约 15% Token，同时保留基础可读性。";

                    EditorGUILayout.HelpBox(tips, MessageType.Info);
                }
            }
        }

        private void DrawFolderList()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField($"子目录列表 ({_selectedFolders.Count}/{_subFolders.Count})", EditorStyles.boldLabel);

            _scroll = EditorGUILayout.BeginScrollView(_scroll, EditorStyles.helpBox);

            if (_subFolders.Count == 0)
            {
                EditorGUILayout.LabelField("无子目录或未选择根目录", EditorStyles.centeredGreyMiniLabel);
            }

            for (int i = 0; i < _subFolders.Count; i++)
            {
                string fullPath = _subFolders[i];
                string folderName = Path.GetFileName(fullPath);
                bool selected = _selectedFolders.Contains(fullPath);

                using (new GUILayout.HorizontalScope())
                {
                    bool newSelected = EditorGUILayout.ToggleLeft(new GUIContent(folderName, fullPath), selected);
                    if (newSelected != selected)
                    {
                        if (newSelected) _selectedFolders.Add(fullPath);
                        else _selectedFolders.Remove(fullPath);
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawActionButtons()
        {
            EditorGUILayout.Space(8);
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("全选", GUILayout.Height(30)))
                {
                    _selectedFolders.UnionWith(_subFolders);
                }

                if (GUILayout.Button("清空", GUILayout.Height(30)))
                {
                    _selectedFolders.Clear();
                }

                GUI.enabled = _selectedFolders.Count > 0 && Directory.Exists(_rootFolder);

                string btnLabel = "复制到剪贴板";
                if (_optimizeForContext) btnLabel += " (已压缩)";

                if (GUILayout.Button(btnLabel, GUILayout.Height(30)))
                {
                    CopySelectedFoldersToClipboard();
                }

                GUI.enabled = true;
            }
        }

        private void RefreshSubFolders()
        {
            _subFolders.Clear();
            _selectedFolders.Clear();

            if (!Directory.Exists(_rootFolder)) return;

            var dirs = Directory.GetDirectories(_rootFolder);
            _subFolders.AddRange(dirs);
            _subFolders.Sort();
        }

        private void CopySelectedFoldersToClipboard()
        {
            if (!EditorUtility.DisplayDialog("准备复制", "请确认所有代码文件已在 IDE (VS/Rider) 中保存。\n未保存的修改无法被读取。", "已保存，继续", "取消"))
            {
                return;
            }

            AssetDatabase.SaveAssets();
            var exts = BuildExtensions();

            StringBuilder sb = new StringBuilder(1024 * 1024 * 2);
            int fileCount = 0;

            try
            {
                int folderIndex = 0;
                foreach (var folder in _selectedFolders)
                {
                    folderIndex++;
                    EditorUtility.DisplayProgressBar("处理中", $"扫描目录: {Path.GetFileName(folder)}", (float)folderIndex / _selectedFolders.Count);

                    var files = Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories);

                    foreach (var filePath in files)
                    {
                        string ext = Path.GetExtension(filePath).ToLowerInvariant();
                        if (!exts.Contains(ext)) continue;

                        FileInfo fi = new FileInfo(filePath);
                        if (fi.Length > 500 * 1024)
                        {
                            Debug.LogWarning($"[CopyTool] 跳过大文件 (>500KB): {filePath}");
                            continue;
                        }

                        string content = File.ReadAllText(filePath, Encoding.UTF8);

                        string relativePath = filePath.Replace("\\", "/");
                        int assetIdx = relativePath.IndexOf("Assets/");
                        if (assetIdx >= 0) relativePath = relativePath.Substring(assetIdx);

                        AppendFileContent(sb, relativePath, content);

                        fileCount++;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CopyTool] 错误: {e.Message}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (sb.Length > 0)
            {
                EditorGUIUtility.systemCopyBuffer = sb.ToString();

                int totalLength = sb.Length;
                int estimatedTokens = totalLength / 4;

                Debug.Log($"--------------------------------------------------");
                Debug.Log($"[FolderContentCopyTool] 复制成功！");
                Debug.Log($"📄 文件数量: {fileCount}");
                Debug.Log($"📏 字符总长: {totalLength:N0}");
                Debug.Log($"🤖 预估Tokens: ~{estimatedTokens:N0}");
                if (_optimizeForContext)
                {
                    string details = "";
                    if (_removeComments) details += "[无注释] ";
                    if (_removeIndentation) details += "[无缩进] ";
                    Debug.Log($"⚡ 压缩模式: {details}");
                }

                Debug.Log($"--------------------------------------------------");

                Notify($"复制成功: {totalLength} 字符");
            }
            else
            {
                Notify("未找到文件");
            }
        }

        private void AppendFileContent(StringBuilder sb, string path, string content)
        {
            if (_optimizeForContext)
            {
                // 1. 移除注释 (如果开启)
                if (_removeComments)
                {
                    // 移除块注释 /* ... */
                    content = Regex.Replace(content, @"/\*[\s\S]*?\*/", "");
                    // 移除行注释 // ...
                    content = Regex.Replace(content, @"//.*", "");
                }

                // 2. 移除缩进 (如果开启)
                if (_removeIndentation)
                {
                    // 移除每一行开头的空白字符
                    content = Regex.Replace(content, @"(?m)^\s+", "");
                }

                // 3. 基础压缩：合并多余空行
                // 将所有连续的换行符替换为单个换行符，并移除空行
                content = Regex.Replace(content, @"(\r\n|\n){2,}", "\n");

                // 4. 移除行首尾空白
                if (_removeIndentation)
                {
                    // 如果已经去除了缩进，这里的 Trim 会更激进
                    content = content.Trim();
                }

                sb.AppendLine($"\n`{path}`:");
                sb.AppendLine("```csharp");
                sb.AppendLine(content);
                sb.AppendLine("```");
            }
            else
            {
                sb.AppendLine("=================================================");
                sb.AppendLine($"FILE: {path}");
                sb.AppendLine("-------------------------------------------------");
                sb.AppendLine(content);
                sb.AppendLine();
            }
        }

        private HashSet<string> BuildExtensions()
        {
            var set = new HashSet<string>();
            if (_includeCs) set.Add(".cs");
            if (_includeAsmdef) set.Add(".asmdef");
            if (_includeJson) set.Add(".json");
            if (_includeMeta) set.Add(".meta");
            if (_includeTxt)
            {
                set.Add(".txt");
                set.Add(".md");
                set.Add(".xml");
            }

            if (_includeShader)
            {
                set.Add(".shader");
                set.Add(".cginc");
                set.Add(".hlsl");
                set.Add(".glsl");
            }

            return set;
        }

        private void RefreshFolderListView()
        {
            if (_folderCountLabel != null)
            {
                _folderCountLabel.text = $"子目录列表 ({_selectedFolders.Count}/{_subFolders.Count})";
            }

            if (_copyButton != null)
            {
                string buttonText = "复制到剪贴板";
                if (_optimizeForContext) buttonText += " (已压缩)";
                _copyButton.text = buttonText;
                _copyButton.SetEnabled(_selectedFolders.Count > 0 && Directory.Exists(_rootFolder));
            }

            if (_folderListView == null)
            {
                return;
            }

            _folderListView.Clear();

            if (_subFolders.Count == 0)
            {
                _folderListView.Add(new Label("无子目录或未选择根目录"));
                return;
            }

            foreach (string fullPath in _subFolders)
            {
                Toggle toggle = new Toggle(Path.GetFileName(fullPath))
                {
                    value = _selectedFolders.Contains(fullPath)
                };
                toggle.tooltip = fullPath;
                toggle.RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue)
                    {
                        _selectedFolders.Add(fullPath);
                    }
                    else
                    {
                        _selectedFolders.Remove(fullPath);
                    }

                    RefreshFolderListView();
                });
                _folderListView.Add(toggle);
            }
        }

        private static VisualElement CreateRow()
        {
            return new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginTop = 4
                }
            };
        }

        private static Toggle CreateToggle(string label, bool value, System.Action<bool> onChanged)
        {
            Toggle toggle = new Toggle(label)
            {
                value = value
            };
            toggle.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
            return toggle;
        }

        private static Button CreateButton(string text, System.Action onClick)
        {
            Button button = new Button(onClick)
            {
                text = text
            };
            button.style.marginRight = 4;
            return button;
        }
    }
}
